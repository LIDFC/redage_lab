using System;
using System.Collections.Generic;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.BlackMarket.Crypto;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Organizations.Player;
using NeptuneEvo.Players;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Table.Tasks.Models;
using NeptuneEvo.Table.Tasks.Player;
using NeptuneEvo.VehicleData.LocalData;
using NeptuneEvo.VehicleData.LocalData.Models;
using Redage.SDK;

namespace NeptuneEvo.Crime.CarTheft
{
    /// <summary>
    /// Миссия банды «Угон» (меню банды, пункт 0): машина стоит на случайной уличной точке (точки закладок Чёрного рынка),
    /// при посадке с шансом срабатывает сигнализация (звёзды розыска), машину гонят к Мавру и разбирают —
    /// игрок получает «Детали угнанного авто», которые сдаёт Мавру в «Скупке краденого».
    /// </summary>
    public class CarTheftManager : Script
    {
        private const int CooldownMinutes = 5;
        private const int AlarmChance = 30;
        private const int ChopSeconds = 10;
        private const float ChopRadius = 20f;

        private class Theft
        {
            public int Fraction;
            public string Crew;
            public int PlayerUuid;
            public bool Entered;
            public bool Chopping;
            public int Parts;
        }

        private static readonly Dictionary<ExtVehicle, Theft> Thefts = new Dictionary<ExtVehicle, Theft>();
        private static readonly Dictionary<string, DateTime> NextTheft = new Dictionary<string, DateTime>();

        /// <summary>«Команда» угонщика: банда/мафия/байкеры, иначе криминальная организация, иначе сам игрок.</summary>
        private static string CrewKey(ExtPlayer player)
        {
            var fracId = player.GetFractionId();
            if (fracId > 0)
                return $"f{fracId}";
            var orgId = player.GetOrganizationMemberData()?.Id ?? 0;
            return orgId > 0 ? $"o{orgId}" : $"p{player.GetUUID()}";
        }

        /// <summary>Минут до следующего заказа для команды игрока (0 — можно брать).</summary>
        public static int CooldownLeft(ExtPlayer player) =>
            NextTheft.TryGetValue(CrewKey(player), out var next) && next > DateTime.Now ? (int)Math.Ceiling((next - DateTime.Now).TotalMinutes) : 0;

        private static readonly (string model, int parts)[] Cars =
        {
            ("primo", 2), ("tailgater", 3), ("oracle", 3), ("felon", 3), ("jackal", 3), ("schafter2", 4),
            ("buffalo", 3), ("fugitive", 2), ("washington", 2), ("sultan", 3), ("zion", 3), ("exemplar", 4), ("f620", 5), ("cogcabrio", 5),
        };

        private static Vector3 ChopPoint => BlackMarket.Config.BlackMarketConfig.Current.ChopPoint ?? BlackMarket.Config.BlackMarketConfig.Current.CashoutPoint;

        /// <summary>Из Main после BlackMarketManager.Init (нужна точка Мавра из конфига).</summary>
        public static void Init()
        {
            try
            {
                CustomColShape.CreateCylinderColShape(ChopPoint - new Vector3(0, 0, 2), ChopRadius, 6, 0, ColShapeEnums.ChopShop);
                NAPI.TextLabel.CreateTextLabel(Main.StringToU16("~o~Разборка~w~\nугнанная машина — [E] за рулём"), ChopPoint + new Vector3(0, 0, 2.2), 15f, 0.35f, 4, new Color(255, 255, 255), false, 0);
            }
            catch (Exception e)
            {
                CrimeCore.Log.Write($"CarTheft Init Exception: {e}");
            }
        }

        public static bool IsTheft(ExtVehicle vehicle) => vehicle != null && Thefts.ContainsKey(vehicle);

        /// <summary>Взять заказ на угон (вызывается из меню банды).</summary>
        public static void Start(ExtPlayer player, int fracId)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null)
                return;
            if (!CrimeCore.IsCriminal(player))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "— Я с тобой не работаю.", 3000);
                return;
            }
            var crew = CrewKey(player);
            if (NextTheft.TryGetValue(crew, out var next) && DateTime.Now < next)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Следующий заказ через {Math.Ceiling((next - DateTime.Now).TotalMinutes)} мин", 3000);
                return;
            }
            if (sessionData.DeliveryData.Vehicle != null)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Сначала закончите с предыдущей машиной", 3000);
                return;
            }
            var points = BlackMarket.Config.BlackMarketConfig.Current.DropPoints
                .Where(p => p.DistanceTo(ChopPoint) > 900)
                .ToList();
            if (points.Count == 0)
                points = BlackMarket.Config.BlackMarketConfig.Current.DropPoints.ToList();
            if (points.Count == 0)
                return;
            var point = points[CrimeCore.Rnd.Next(points.Count)];
            var car = Cars[CrimeCore.Rnd.Next(Cars.Length)];
            var plate = $"{(char)('A' + CrimeCore.Rnd.Next(26))}{CrimeCore.Rnd.Next(100, 999)}{(char)('A' + CrimeCore.Rnd.Next(26))}{(char)('A' + CrimeCore.Rnd.Next(26))}";
            var vehicle = (ExtVehicle)VehicleStreaming.CreateVehicle(NAPI.Util.GetHashKey(car.model), point + new Vector3(0, 0, 0.3), CrimeCore.Rnd.Next(0, 360),
                CrimeCore.Rnd.Next(0, 100), CrimeCore.Rnd.Next(0, 100), plate, acc: VehicleAccess.DeliveryGang, petrol: 60);
            var vehicleLocalData = vehicle.GetVehicleLocalData();
            if (vehicleLocalData != null)
            {
                vehicleLocalData.DeliveryData.End = 0;
                vehicleLocalData.DeliveryData.Fraction = fracId;
                vehicleLocalData.DeliveryData.JStage = false;
                vehicleLocalData.DeliveryData.WhosVeh = player;
            }
            Thefts[vehicle] = new Theft { Fraction = fracId, Crew = crew, PlayerUuid = player.GetUUID(), Parts = car.parts };
            sessionData.DeliveryData.Vehicle = vehicle;
            sessionData.DeliveryData.Point = -1;
            NextTheft[crew] = DateTime.Now.AddMinutes(CooldownMinutes);
            Trigger.ClientEvent(player, "createWaypoint", point.X, point.Y);
            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter,
                $"Заказ: {car.model.ToUpper()} ({plate}). Машина отмечена в GPS — угоните её и пригоните к Мавру на разборку", 6000);
            BlackMarket.Audit.AuditLog.Write("theft_start", player.GetUUID(), details: new { fraction = fracId, car = car.model, x = point.X, y = point.Y });
        }

        /// <summary>Посадка в машину угона (из CrimeMissions.Event_PlayerEnterVehicle). true — обработано здесь.</summary>
        public static bool OnEnter(ExtPlayer player, ExtVehicle vehicle)
        {
            if (!Thefts.TryGetValue(vehicle, out var theft))
                return false;
            if (CrimeCore.IsPolice(player))
            {
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Машина в угоне. Отвезите её в участок", 3000);
                return true;
            }
            if (CrewKey(player) != theft.Crew)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Это не ваш заказ", 3000);
                VehicleManager.WarpPlayerOutOfVehicle(player);
                return true;
            }
            Trigger.ClientEvent(player, "createWaypoint", ChopPoint.X, ChopPoint.Y);
            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Гоните машину к Мавру: встаньте рядом и нажмите E — разборка", 4000);
            if (!theft.Entered)
            {
                theft.Entered = true;
                if (CrimeCore.Roll(AlarmChance))
                {
                    Trigger.ClientEvent(player, "client.crime.alarm", vehicle);
                    CrimeCore.CallPolice(player, vehicle.Position, $"theft_{vehicle.Value}", "Сработала автосигнализация — угон автомобиля", 2, "Угон автомобиля");
                }
            }
            return true;
        }

        [Interaction(ColShapeEnums.ChopShop)]
        public static void OnChop(ExtPlayer player)
        {
            try
            {
                var characterData = player.GetCharacterData();
                var sessionData = player.GetSessionData();
                if (characterData == null || sessionData == null)
                    return;
                if (!player.IsInVehicle)
                {
                    // Зона разборки накрывает Мавра — пешком E работает как обычное меню Мавра
                    if (CashOut.AtPoint(player) && player.Position.DistanceTo(BlackMarket.Config.BlackMarketConfig.Current.CashoutPoint) <= 3f)
                        SafeMain.OnBlackMarket(player);
                    return;
                }
                var vehicle = (ExtVehicle)player.Vehicle;
                if (!Thefts.TryGetValue(vehicle, out var theft))
                {
                    var local = vehicle.GetVehicleLocalData();
                    if (local != null && local.Access == VehicleAccess.DeliveryGang)
                        Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "— Эту тачку заказчик ждёт целой. Вези по адресу.", 3500);
                    else
                        Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "— Разбираю только то, что заказал.", 3000);
                    return;
                }
                if (player.VehicleSeat != (int)VehicleSeat.Driver || CrewKey(player) != theft.Crew || theft.Chopping)
                    return;
                theft.Chopping = true;
                Trigger.ClientEvent(player, "blockMove", true);
                VehicleStreaming.SetEngineState(vehicle, false);
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"Ребята Мавра разбирают машину… ({ChopSeconds} с)", ChopSeconds * 1000);
                NAPI.Task.Run(() => FinishChop(player, vehicle, theft), ChopSeconds * 1000);
            }
            catch (Exception e)
            {
                CrimeCore.Log.Write($"CarTheft OnChop Exception: {e}");
            }
        }

        private static void FinishChop(ExtPlayer player, ExtVehicle vehicle, Theft theft)
        {
            try
            {
                theft.Chopping = false;
                if (player == null || !player.IsCharacterData())
                    return;
                Trigger.ClientEvent(player, "blockMove", false);
                if (!Thefts.ContainsKey(vehicle) || !player.IsInVehicle || player.Vehicle != vehicle || vehicle.Position.DistanceTo(ChopPoint) > ChopRadius + 3)
                    return;
                var characterData = player.GetCharacterData();
                var sessionData = player.GetSessionData();
                var health = Math.Max(0.3, Math.Min(1.0, vehicle.Health / 1000.0));
                var parts = Math.Max(1, (int)Math.Round(theft.Parts * health));
                if (Chars.Repository.isFreeSlots(player, ItemId.StolenCarParts, parts) != 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Освободите место в инвентаре под детали", 3000);
                    return;
                }
                Chars.Repository.AddNewItem(player, $"char_{characterData.UUID}", "inventory", ItemId.StolenCarParts, parts);
                Remove(vehicle);
                var owner = Main.GetPlayerByUUID(theft.PlayerUuid);
                var ownerSession = owner?.GetSessionData();
                if (ownerSession != null && ownerSession.DeliveryData.Vehicle == vehicle)
                    ownerSession.DeliveryData.Vehicle = null;
                if (sessionData.DeliveryData.Vehicle == vehicle)
                    sessionData.DeliveryData.Vehicle = null;
                player.AddTableScore(TableTaskId.Item27);
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter,
                    $"Машина разобрана: {parts} дет. Продайте их Мавру в «Скупке краденого»", 5000);
                BlackMarket.Audit.AuditLog.Write("theft_chop", characterData.UUID, itemId: (int)ItemId.StolenCarParts, count: parts, details: new { fraction = theft.Fraction });
            }
            catch (Exception e)
            {
                CrimeCore.Log.Write($"CarTheft FinishChop Exception: {e}");
            }
        }

        private static void Remove(ExtVehicle vehicle)
        {
            Thefts.Remove(vehicle);
            VehicleStreaming.DeleteVehicle(vehicle);
        }

        [ServerEvent(Event.VehicleDeath)]
        public void OnVehicleDeath(ExtVehicle vehicle)
        {
            if (vehicle != null && Thefts.ContainsKey(vehicle))
                NAPI.Task.Run(() =>
                {
                    if (Thefts.ContainsKey(vehicle))
                        Remove(vehicle);
                }, 10000);
        }

        /// <summary>Игрок ушёл — его заказ удаляет CrimeMissions.Event_PlayerDisconnected, здесь только чистим учёт.</summary>
        public static void Forget(ExtVehicle vehicle)
        {
            if (vehicle != null)
                Thefts.Remove(vehicle);
        }
    }
}
