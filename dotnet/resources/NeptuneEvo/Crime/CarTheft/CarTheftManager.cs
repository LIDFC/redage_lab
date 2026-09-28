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
            /// <summary>Дорогая машина — электронный замок, только программатор.</summary>
            public bool Expensive;
            public bool Unlocked;
            public bool Busy;
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

        /// <summary>Программатор у Мавра.</summary>
        public const int ProgrammerPrice = 3000;

        /// <summary>Дешёвые — обычный замок (отмычка), дорогие — электронный (программатор). Дорогие дают больше деталей.</summary>
        private static readonly (string model, int parts, bool expensive)[] Cars =
        {
            ("primo", 2, false), ("tailgater", 3, false), ("oracle", 3, false), ("felon", 3, false), ("jackal", 3, false),
            ("buffalo", 3, false), ("fugitive", 2, false), ("washington", 2, false), ("sultan", 3, false), ("zion", 3, false),
            ("schafter2", 5, true), ("exemplar", 5, true), ("f620", 6, true), ("cogcabrio", 6, true), ("cognoscenti", 5, true),
            ("sentinel", 5, true), ("comet2", 6, true), ("feltzer2", 6, true), ("carbonizzare", 6, true),
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
            // Примерно каждая третья машина — дорогая, с электронным замком
            var pool = Cars.Where(c => c.expensive == CrimeCore.Roll(35)).ToArray();
            var car = pool.Length > 0 ? pool[CrimeCore.Rnd.Next(pool.Length)] : Cars[CrimeCore.Rnd.Next(Cars.Length)];
            var plate = $"{(char)('A' + CrimeCore.Rnd.Next(26))}{CrimeCore.Rnd.Next(100, 999)}{(char)('A' + CrimeCore.Rnd.Next(26))}{(char)('A' + CrimeCore.Rnd.Next(26))}";
            var vehicle = (ExtVehicle)VehicleStreaming.CreateVehicle(NAPI.Util.GetHashKey(car.model), point + new Vector3(0, 0, 0.3), CrimeCore.Rnd.Next(0, 360),
                CrimeCore.Rnd.Next(0, 100), CrimeCore.Rnd.Next(0, 100), plate, locked: true, acc: VehicleAccess.DeliveryGang, petrol: 60);
            VehicleStreaming.SetLockStatus(vehicle, true);
            VehicleStreaming.SetEngineState(vehicle, false);
            var vehicleLocalData = vehicle.GetVehicleLocalData();
            if (vehicleLocalData != null)
            {
                vehicleLocalData.DeliveryData.End = 0;
                vehicleLocalData.DeliveryData.Fraction = fracId;
                vehicleLocalData.DeliveryData.JStage = false;
                vehicleLocalData.DeliveryData.WhosVeh = player;
            }
            Thefts[vehicle] = new Theft { Fraction = fracId, Crew = crew, PlayerUuid = player.GetUUID(), Parts = car.parts, Expensive = car.expensive };
            sessionData.DeliveryData.Vehicle = vehicle;
            sessionData.DeliveryData.Point = -1;
            NextTheft[crew] = DateTime.Now.AddMinutes(CooldownMinutes);
            Trigger.ClientEvent(player, "createWaypoint", point.X, point.Y);
            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter,
                $"Заказ: {car.model.ToUpper()} ({plate}). Машина отмечена в GPS — угоните её и пригоните к Мавру на разборку", 7000);
            Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, car.expensive
                ? "Дорогая машина с электронным замком: нужен ПРОГРАММАТОР (Мавр). У машины: инвентарь → «Программатор» → Использовать"
                : "Обычный замок: у машины инвентарь → «Отмычка» → Использовать", 9000);
            Trigger.SendChatMessage(player, car.expensive
                ? "!{#f5a524}[Угон]!{#ffffff} Дорогая машина — нужен программатор (продаёт Мавр). Подойдите к двери, откройте инвентарь и используйте программатор."
                : "!{#f5a524}[Угон]!{#ffffff} Замок обычный — подойдите к двери, откройте инвентарь и используйте отмычку.");
            BlackMarket.Audit.AuditLog.Write("theft_start", player.GetUUID(), details: new { fraction = fracId, car = car.model, car.expensive, x = point.X, y = point.Y });
        }

        /// <summary>
        /// «Использовать» отмычку или программатор из инвентаря (хук в Chars.Repository.ItemsUse).
        /// true — предмет обработан здесь (рядом машина заказа); false — отмычку можно использовать как обычно.
        /// </summary>
        public static bool OnUseTool(ExtPlayer player, ItemId itemId)
        {
            var programmer = itemId == ItemId.CarProgrammer;
            if (player.IsInVehicle)
            {
                if (programmer)
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Выйдите из машины", 3000);
                return programmer;
            }
            var crew = CrewKey(player);
            var found = Thefts
                .Where(t => t.Key != null && t.Key.Exists && t.Key.Dimension == player.Dimension && t.Key.Position.DistanceTo(player.Position) < 3.5f)
                .OrderBy(t => t.Key.Position.DistanceTo(player.Position))
                .FirstOrDefault();
            var vehicle = found.Key;
            var theft = found.Value;
            if (vehicle == null)
            {
                if (programmer)
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Подойдите вплотную к машине из заказа на угон", 3000);
                return programmer;
            }
            if (theft.Crew != crew)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Это чужой заказ", 3000);
                return true;
            }
            if (theft.Unlocked || !VehicleStreaming.GetLockState(vehicle))
            {
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Машина уже открыта — садитесь", 3000);
                return true;
            }
            if (theft.Busy || LockBreak.IsBusy(player) || CyberHack.IsBusy(player))
                return true;
            if (theft.Expensive && !programmer)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Электронный замок — отмычка не поможет. Нужен программатор (Мавр)", 4000);
                return true;
            }

            theft.Busy = true;
            if (programmer)
            {
                var size = vehicle.Model == NAPI.Util.GetHashKey("comet2") || vehicle.Model == NAPI.Util.GetHashKey("carbonizzare") ? 6 : 5;
                if (!CyberHack.Start(player, size, 35, "Электронный замок",
                    p => { theft.Busy = false; Unlock(p, vehicle, theft); },
                    (p, reason) =>
                    {
                        theft.Busy = false;
                        if (reason == "cancel")
                            return;
                        // Провал: программатор сгорает, срабатывает сигнализация
                        if (Chars.Repository.getCountItem($"char_{p.GetUUID()}", ItemId.CarProgrammer, false) > 0)
                            Chars.Repository.Remove(p, $"char_{p.GetUUID()}", "inventory", ItemId.CarProgrammer, 1);
                        Notify.Send(p, NotifyType.Error, NotifyPosition.BottomCenter, "Защита сработала — программатор сгорел, включилась сигнализация!", 5000);
                        if (vehicle.Exists)
                        {
                            Trigger.ClientEventInRange(vehicle.Position, 80f, "client.crime.alarm", vehicle);
                            CrimeCore.CallPolice(p, vehicle.Position, $"theft_{vehicle.Value}", "Сработала автосигнализация дорогого автомобиля — попытка угона", 2, "Угон автомобиля");
                        }
                    }))
                    theft.Busy = false;
                return true;
            }

            if (!LockBreak.Start(player, "car", vehicle.Value, 7, "Взлом замка машины",
                p => { theft.Busy = false; Unlock(p, vehicle, theft); },
                (p, reason) => theft.Busy = false))
                theft.Busy = false;
            return true;
        }

        private static void Unlock(ExtPlayer player, ExtVehicle vehicle, Theft theft)
        {
            if (vehicle == null || !vehicle.Exists || !Thefts.ContainsKey(vehicle))
                return;
            theft.Unlocked = true;
            VehicleStreaming.SetLockStatus(vehicle, false);
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, "Замок открыт! Садитесь за руль — двигатель заведёте, замкнув провода", 4000);
            Commands.RPChat("sme", player, "вскрывает дверь автомобиля");
        }

        /// <summary>Программатор у Мавра (пункт 505).</summary>
        public static void BuyProgrammer(ExtPlayer player)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null)
                return;
            if (!CrimeCore.IsCriminal(player))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "— Такие игрушки только для своих.", 3000);
                return;
            }
            if (characterData.Money < ProgrammerPrice)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Недостаточно денег", 3000);
                return;
            }
            if (Chars.Repository.isFreeSlots(player, ItemId.CarProgrammer) != 0)
                return;
            if (Chars.Repository.AddNewItem(player, $"char_{characterData.UUID}", "inventory", ItemId.CarProgrammer, 1) == -1)
                return;
            MoneySystem.Wallet.Change(player, -ProgrammerPrice);
            GameLog.Money($"player({characterData.UUID})", "server", ProgrammerPrice, "buyMavr(programmer)");
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, "Программатор куплен. Нужен для дорогих машин из заказов на угон; при провале взлома сгорает", 5000);
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
            if (!theft.Entered && player.VehicleSeat == (int)VehicleSeat.Driver)
            {
                theft.Entered = true;
                VehicleStreaming.SetEngineState(vehicle, true);
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, "Вы замкнули провода — двигатель заведён", 3000);
                // Программатор отключает сигнализацию; у дешёвых машин она может сработать
                if (!theft.Expensive && CrimeCore.Roll(AlarmChance))
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
