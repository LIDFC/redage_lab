using NeptuneEvo.EternalDev.MarketPlace.Auction;
using NeptuneEvo.Handles;
using NeptuneEvo.Houses;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Extensions
{
    public static class Houses
    {
        public static bool IsOnMarketplace(this House house, ExtPlayer player)
        {
            bool Reject(string message)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, message, 3000);
                return true;
            }

            var marketItem = Manager.MarketItems.Values.ToList().Find(x => x.Type == Enums.LotType.House && x.Data == house.ID.ToString());
            if (marketItem != null)
                return Reject($"Этот дом выставлен на маркетплейсе, взаимодействие с ним невозможно");

            var auctionItem = AuctionManager.AuctionItems.Values.ToList().Find(x => x.Type == Enums.LotType.House && x.Data == house.ID.ToString());
            if (auctionItem != null)
                return Reject($"Этот дом выставлен на аукционе, взаимодействие с ним невозможно");

            return false;
        }
    }
}
