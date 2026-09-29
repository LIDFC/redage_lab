using System;
using System.Collections.Generic;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.Chars;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Crime;
using NeptuneEvo.Fractions.Models;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using NeptuneEvo.VehicleData.LocalData;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Fractions.ArmyRP
{
    /// <summary>
    /// В. Конвой снабжения.
    ///  После погрузки материалов в порту водитель оформляет рейс: /convoy lspd|sheriff|ems|fib|city.
    ///  Военные на смене видят конвой на карте; разгрузка на складе получателя (обычное меню склада) — премия экипажу.
    ///  Уничтожение грузовика — тревога армии и полиции. Остановленный грузовик можно вскрыть отмычкой (/robconvoy):
    ///  взломщик забирает часть материалов, армия и полиция получают сигнал.
    /// </summary>
    class ArmyConvoy : Script
    {
        private static nLog Log => ArmyConfig.Log;
        private static ArmyConfig Cfg => ArmyConfig.Current;

        private static readonly Dictionary<string, (int fraction, string name)> Destinations = new Dictionary<string, (int, string)>
        {
            { "lspd", ((int) Models.Fractions.POLICE, "LSPD") },
            { "sheriff", ((int) Models.Fractions.SHERIFF, "Шериф") },
            { "ems", ((int) Models.Fractions.EMS, "EMS") },
            { "fib", ((int) Models.Fractions.FIB, "FIB") },
            { "city", ((int) Models.Fractions.CITY, "Мэрия") },
        };

        private class Convoy
        {
            public ExtVehicle Vehicle;
            public int Destination;
            public string DestinationName;
            public string Driver;
            public DateTime Started;
            public bool BeingRobbed;
        }

        private static readonly Dictionary<ExtVehicle, Convoy> Convoys = new Dictionary<ExtVehicle, Convoy>();

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart() => Timers.Start("army.convoy", 5000, Tick, true);

        private static int MatsIn(ExtVehicle vehicle) =>
            Chars.Repository.getCountItem(VehicleManager.GetVehicleToInventory(vehicle.NumberPlate), ItemId.Material);

        private static string Where(Vector3 p) => $"{p.X:0} {p.Y:0}";

        public static bool IsConvoy(ExtVehicle vehicle) => vehicle != null && Convoys.ContainsKey(vehicle);

        [Command("convoy")]
        public static void CMD_Convoy(ExtPlayer player, string destination = "")
        {
            try
            {
                if (!ArmyUtil.IsArmy(player) || !ArmyUtil.OnDuty(player))
                {
                    ArmyUtil.Say(player, "Рейс оформляет военный на смене", false);
                    return;
                }
                if (!player.IsInVehicle || player.VehicleSeat != (int) VehicleSeat.Driver)
                {
                    ArmyUtil.Say(player, "Сядьте за руль грузовика с материалами", false);
                    return;
                }
                var vehicle = (ExtVehicle) player.Vehicle;
                var local = vehicle.GetVehicleLocalData();
                if (local == null || local.Fraction != ArmyUtil.ArmyId)
                {
                    ArmyUtil.Say(player, "Нужен армейский грузовик", false);
                    return;
                }
                if (!Destinations.TryGetValue((destination ?? "").ToLower(), out var dest))
                {
                    ArmyUtil.Say(player, "/convoy lspd | sheriff | ems | fib | city", false);
                    return;
                }
                var mats = MatsIn(vehicle);
                if (mats <= 0)
                {
                    ArmyUtil.Say(player, "Кузов пуст — сначала загрузите материалы в порту", false);
                    return;
                }
                if (!Stocks.matsCoords.TryGetValue(dest.fraction, out var point) || point.DistanceTo(new Vector3()) < 1)
                {
                    ArmyUtil.Say(player, "У этой фракции нет точки разгрузки", false);
                    return;
                }
                Convoys[vehicle] = new Convoy
                {
                    Vehicle = vehicle,
                    Destination = dest.fraction,
                    DestinationName = dest.name,
                    Driver = player.Name,
                    Started = DateTime.Now,
                };
                Trigger.ClientEvent(player, "createWaypoint", point.X, point.Y);
                ArmyUtil.Radio($"Конвой вышел: {player.Name}, {vehicle.NumberPlate} → склад {dest.name}, груз {mats} мат. Сопровождению — к грузовику.");
                Manager.sendFractionMessage(dest.fraction, $"!{{#6B8E23}}[Армия] К вам вышел конвой с материалами ({mats} мат.), водитель {player.Name}", true);
                Fractions.Table.Logs.Repository.AddLogs(player, FractionLogsType.TakeMats, $"Конвой {vehicle.NumberPlate} → {dest.name}, {mats} мат.");
            }
            catch (Exception e)
            {
                Log.Write($"CMD_Convoy Exception: {e}");
            }
        }

        /// <summary>Вызывается из меню склада (Fractions/Manager.cs, unload_mats) после успешной разгрузки.</summary>
        public static void OnUnloaded(ExtPlayer player, ExtVehicle vehicle, int stockFraction, int amount)
        {
            try
            {
                if (vehicle == null || !Convoys.TryGetValue(vehicle, out var convoy) || convoy.Destination != stockFraction)
                    return;
                if (MatsIn(vehicle) > 0)
                {
                    ArmyUtil.Say(player, $"Разгружено {amount}. Остаток в кузове: {MatsIn(vehicle)}");
                    return;
                }
                Convoys.Remove(vehicle);
                var crew = ArmyUtil.ArmyOnDuty().Where(p => p.Position.DistanceTo(vehicle.Position) <= Cfg.ConvoyCrewRadius).ToList();
                foreach (var member in crew)
                {
                    if (Cfg.ConvoyReward > 0)
                    {
                        MoneySystem.Wallet.Change(member, Cfg.ConvoyReward);
                        GameLog.Money("server", $"player({member.GetUUID()})", Cfg.ConvoyReward, "armyConvoy");
                    }
                    ArmyUtil.Say(member, $"Конвой доставлен! Премия +{Cfg.ConvoyReward}$");
                }
                var minutes = (int) (DateTime.Now - convoy.Started).TotalMinutes;
                ArmyUtil.Radio($"Конвой {vehicle.NumberPlate} доставлен на склад {convoy.DestinationName} за {minutes} мин. Экипаж: {string.Join(", ", crew.Select(c => c.Name))}");
            }
            catch (Exception e)
            {
                Log.Write($"OnUnloaded Exception: {e}");
            }
        }

        private static void Tick()
        {
            try
            {
                if (Convoys.Count == 0)
                    return;
                foreach (var (vehicle, convoy) in Convoys.ToList())
                {
                    if (vehicle == null || !vehicle.Exists)
                    {
                        Convoys.Remove(vehicle);
                        ArmyUtil.Radio($"Связь с конвоем на склад {convoy.DestinationName} потеряна");
                    }
                }
                var list = Convoys.Values.Select(c => new
                {
                    id = c.Vehicle.Value,
                    x = c.Vehicle.Position.X,
                    y = c.Vehicle.Position.Y,
                    z = c.Vehicle.Position.Z,
                    name = $"Конвой → {c.DestinationName}",
                }).ToList();
                var json = JsonConvert.SerializeObject(list);
                foreach (var soldier in ArmyUtil.ArmyOnDuty())
                    Trigger.ClientEvent(soldier, "client.army.convoys", json);
            }
            catch (Exception e)
            {
                Log.Write($"Convoy Tick Exception: {e}");
            }
        }

        [ServerEvent(Event.VehicleDeath)]
        public void OnVehicleDeath(ExtVehicle vehicle)
        {
            try
            {
                if (vehicle == null || !Convoys.TryGetValue(vehicle, out var convoy))
                    return;
                Convoys.Remove(vehicle);
                var text = $"Конвой {vehicle.NumberPlate} на склад {convoy.DestinationName} уничтожен! Координаты: {Where(vehicle.Position)}";
                ArmyUtil.Radio(text);
                Manager.sendFractionMessage((int) Models.Fractions.POLICE, "!{#FF8C00}[Диспетчер] " + text, true);
                Manager.sendFractionMessage((int) Models.Fractions.SHERIFF, "!{#FF8C00}[Диспетчер] " + text, true);
            }
            catch (Exception e)
            {
                Log.Write($"OnVehicleDeath Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ ограбление

        private static bool IsLawOrArmy(ExtPlayer player)
        {
            var fraction = player.GetFractionId();
            return fraction == ArmyUtil.ArmyId || Configs.IsFractionPolic(fraction) || fraction == (int) Models.Fractions.CITY || fraction == (int) Models.Fractions.EMS;
        }

        [Command("robconvoy")]
        public static void CMD_RobConvoy(ExtPlayer player)
        {
            try
            {
                if (!player.IsCharacterData() || player.IsInVehicle)
                    return;
                if (IsLawOrArmy(player))
                {
                    ArmyUtil.Say(player, "Госслужащий не может грабить конвой", false);
                    return;
                }
                var convoy = Convoys.Values.FirstOrDefault(c => c.Vehicle.Exists && c.Vehicle.Position.DistanceTo(player.Position) < 4f);
                if (convoy == null)
                {
                    ArmyUtil.Say(player, "Рядом нет армейского конвоя", false);
                    return;
                }
                if (convoy.BeingRobbed)
                {
                    ArmyUtil.Say(player, "Кузов уже вскрывают", false);
                    return;
                }
                var vehicle = convoy.Vehicle;
                if (vehicle.Occupants.Count > 0)
                {
                    ArmyUtil.Say(player, "Грузовик должен стоять без водителя", false);
                    return;
                }
                convoy.BeingRobbed = true;
                var text = $"Вскрывают конвой {vehicle.NumberPlate}! Координаты: {Where(vehicle.Position)}";
                ArmyUtil.Radio(text);
                Manager.sendFractionMessage((int) Models.Fractions.POLICE, "!{#FF8C00}[Диспетчер] " + text, true);
                if (!LockBreak.Start(player, "convoy", vehicle.Value, 5, "Замок кузова",
                        p => { convoy.BeingRobbed = false; Loot(p, convoy); },
                        (p, reason) => convoy.BeingRobbed = false))
                    convoy.BeingRobbed = false;
            }
            catch (Exception e)
            {
                Log.Write($"CMD_RobConvoy Exception: {e}");
            }
        }

        private static void Loot(ExtPlayer player, Convoy convoy)
        {
            var vehicle = convoy.Vehicle;
            if (vehicle == null || !vehicle.Exists || vehicle.Position.DistanceTo(player.Position) > 5f)
                return;
            var amount = Math.Min(Cfg.ConvoyRobAmount, MatsIn(vehicle));
            if (amount <= 0)
            {
                ArmyUtil.Say(player, "Кузов пуст", false);
                return;
            }
            if (Chars.Repository.isFreeSlots(player, ItemId.Material, amount) != 0)
                return;
            Chars.Repository.Remove(null, VehicleManager.GetVehicleToInventory(vehicle.NumberPlate), "vehicle", ItemId.Material, amount);
            Chars.Repository.AddNewItem(player, $"char_{player.GetUUID()}", "inventory", ItemId.Material, amount);
            ArmyUtil.Say(player, $"Вы вытащили {amount} материалов из кузова");
            ArmyUtil.Radio($"Из конвоя {vehicle.NumberPlate} похищено {amount} мат.!");
            GameLog.Stock(ArmyUtil.ArmyId, player.GetUUID(), player.Name, "mats_robbed", amount, "out");
            if (MatsIn(vehicle) <= 0)
                Convoys.Remove(vehicle);
        }
    }
}
