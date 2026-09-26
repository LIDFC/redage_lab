using GTANetworkAPI;
using NeptuneEvo.Core;
using NeptuneEvo.EternalDev.MarketPlace.Auction;
using NeptuneEvo.Handles;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Extensions
{
    public static class Vehicles
    {
        public static bool IsOnMarketplace(this NeptuneEvo.VehicleData.Models.VehicleData vehicleData, ExtPlayer player)
        {
            bool Reject(string message)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, message, 3000);
                return true;
            }

            var marketItem = Manager.MarketItems.Values.ToList().Find(x => x.Type == Enums.LotType.Vehicle && x.Data == vehicleData.SqlId.ToString());
            if (marketItem != null)
                return Reject($"Этот транспорт выставлен на маркетплейсе, взаимодействие с ним невозможно");

            var auctionItem = AuctionManager.AuctionItems.Values.ToList().Find(x => x.Type == Enums.LotType.Vehicle && x.Data == vehicleData.SqlId.ToString());
            if (auctionItem != null)
                return Reject($"Этот транспорт выставлен на аукционе, взаимодействие с ним невозможно");

            return false;
        }

        public static bool IsOnMarketplace(this Vehicle entity, ExtPlayer player)
        {
            var vehicle = entity as ExtVehicle;
            if (vehicle.VehicleLocalData.Access != VehicleData.LocalData.Models.VehicleAccess.Personal)
                return false;

            var vehicleData = VehicleManager.GetVehicleToNumber(vehicle.NumberPlate);
            return vehicleData.IsOnMarketplace(player);
        }
    }
}
