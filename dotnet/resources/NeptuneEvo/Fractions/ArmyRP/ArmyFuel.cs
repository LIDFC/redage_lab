using System;
using System.Collections.Generic;
using GTANetworkAPI;
using Localization;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Fractions.Models;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using NeptuneEvo.Players.Phone.Messages.Models;
using NeptuneEvo.VehicleData.LocalData;
using NeptuneEvo.VehicleData.LocalData.Models;
using NeptuneEvo.VehicleModel;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Fractions.ArmyRP
{
    /// <summary>
    /// Армейская заправка в Форт Занкудо: служебный транспорт гос. фракций заправляется за счёт штата.
    /// Та же система, что кнопка «За счёт штата» на обычных АЗС (Businesses.fillCar): дневной лимит фракции FuelLeft
    /// (задаёт мэрия), оплата — из бюджета мэрии, чек — SMS из банка. Окно — PlayerGasStation в режиме govOnly.
    /// Точка — settings/army.json (fuelPoint), /armyset point fuel.
    /// </summary>
    class ArmyFuel : Script
    {
        private static readonly nLog Log = new nLog("Fractions.ArmyFuel");
        private static ArmyConfig Cfg => ArmyConfig.Current;

        private static ExtColShape _shape;
        private static Marker _marker;
        private static Blip _blip;
        private static readonly HashSet<ExtPlayer> AtPump = new HashSet<ExtPlayer>();

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            try
            {
                if (Cfg.FuelPoint == null)
                    ArmyConfig.Load();
                CreatePoint();
            }
            catch (Exception e)
            {
                Log.Write($"OnResourceStart Exception: {e}");
            }
        }

        public static void CreatePoint()
        {
            CustomColShape.DeleteColShape(_shape);
            if (_marker != null && _marker.Exists) _marker.Delete();
            if (_blip != null && _blip.Exists) _blip.Delete();
            if (Cfg.FuelPoint == null)
                return;
            _shape = CustomColShape.CreateCylinderColShape(Cfg.FuelPoint, 4f, 4, 0, ColShapeEnums.ArmyFuel, 0);
            _marker = NAPI.Marker.CreateMarker(1, Cfg.FuelPoint - new Vector3(0, 0, 1.0), new Vector3(), new Vector3(), 4f, new Color(107, 142, 35, 120));
            _blip = NAPI.Blip.CreateBlip(361, Cfg.FuelPoint, 0.7f, 52, "Армейская заправка", 255, 0, true, 0, 0);
        }

        private static bool CanGov(ExtPlayer player, out FractionData fractionData, out ExtVehicle vehicle)
        {
            fractionData = player.GetFractionData();
            vehicle = player.IsInVehicle ? (ExtVehicle) player.Vehicle : null;
            var local = vehicle?.GetVehicleLocalData();
            return fractionData != null && local != null &&
                   Manager.FractionTypes[fractionData.Id] == FractionsType.Gov &&
                   local.Access == VehicleAccess.Fraction && local.Fraction == fractionData.Id;
        }

        [Interaction(ColShapeEnums.ArmyFuel)]
        public static void OnPump(ExtPlayer player, int index)
        {
            try
            {
                if (!player.IsCharacterData())
                    return;
                if (!player.IsInVehicle || player.VehicleSeat != (int) VehicleSeat.Driver)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Подъедьте к колонке за рулём служебного транспорта", 3000);
                    return;
                }
                var vehicle = (ExtVehicle) player.Vehicle;
                var local = vehicle.GetVehicleLocalData();
                if (local == null)
                    return;
                var canGov = CanGov(player, out var fractionData, out _);
                AtPump.Add(player);
                Trigger.ClientEvent(player, "openPetrol", JsonConvert.SerializeObject(new
                {
                    id = 0,
                    title = "Армейская заправка",
                    govOnly = true,
                    price = Cfg.FuelPrice,
                    stock = 0,
                    fuel = Math.Max(0, local.Petrol),
                    tank = VehicleManager.VehicleTank.ContainsKey(vehicle.Class) ? VehicleManager.VehicleTank[vehicle.Class] : 0,
                    money = 0,
                    canGov,
                    govLeft = canGov ? fractionData.FuelLeft : 0,
                    noFuel = local.Petrol <= -1,
                }));
            }
            catch (Exception e)
            {
                Log.Write($"OnPump Exception: {e}");
            }
        }

        [Interaction(ColShapeEnums.ArmyFuel, Out: true)]
        public static void OnLeave(ExtPlayer player, int _) => AtPump.Remove(player);

        /// <summary>Businesses.fillCar перенаправляет сюда, если игрок у армейской колонки.</summary>
        public static bool TryFill(ExtPlayer player)
        {
            if (!AtPump.Contains(player))
                return false;
            try
            {
                if (!CanGov(player, out var fractionData, out var vehicle) || player.VehicleSeat != (int) VehicleSeat.Driver)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Здесь заправляют только служебный транспорт гос. фракций", 4000);
                    return true;
                }
                if (Cfg.FuelPoint == null || vehicle.Position.DistanceTo(Cfg.FuelPoint) > 8f)
                {
                    AtPump.Remove(player);
                    return false;
                }
                var local = vehicle.GetVehicleLocalData();
                if (local == null || local.Petrol <= -1)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.CantZapravit), 3000);
                    return true;
                }
                if (VehicleStreaming.GetEngineState(vehicle))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.Zaglushite), 3000);
                    return true;
                }
                var tank = VehicleManager.VehicleTank.ContainsKey(vehicle.Class) ? VehicleManager.VehicleTank[vehicle.Class] : 0;
                var fuel = Math.Max(0, local.Petrol);
                var liters = tank - fuel;
                if (liters <= 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.FullFuel), 3000);
                    return true;
                }
                // Лимит фракции — в долларах: льём сколько позволяет остаток
                var price = Math.Max(1, Cfg.FuelPrice);
                liters = Math.Min(liters, fractionData.FuelLeft / price);
                if (liters <= 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Дневной лимит топлива фракции исчерпан — обратитесь к мэрии", 5000);
                    return true;
                }
                var cost = liters * price;
                fractionData.FuelLeft -= cost;
                var city = Manager.GetFractionData((int) Models.Fractions.CITY);
                if (city != null)
                    city.Money -= cost;
                GameLog.Money($"frac({(int) Models.Fractions.CITY})", "armyFuel", cost, $"armyFuel frac({fractionData.Id})");

                local.Petrol = fuel + liters;
                vehicle.SetSharedData("PETROL", local.Petrol);
                Trigger.ClientEvent(player, "client.fuel.filled", liters);
                Players.Phone.Messages.Repository.AddSystemMessage(player, (int) DefaultNumber.Bank,
                    $"{LangFunc.GetText(LangType.Ru, DataName.ZapravkaGos)}: {liters} л на ${cost}. Остаток лимита фракции ${fractionData.FuelLeft}", DateTime.Now);
                Commands.RPChat("sme", player, "заправил служебный транспорт");
            }
            catch (Exception e)
            {
                Log.Write($"TryFill Exception: {e}");
            }
            return true;
        }

        [ServerEvent(Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(ExtPlayer player, DisconnectionType type, string reason) => AtPump.Remove(player);
    }
}
