using NeptuneEvo.Core;
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
    public static class Businesses
    {
        public static bool IsOnMarketplace(this Business business, ExtPlayer player)
        {
            bool Reject(string message)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, message, 3000);
                return true;
            }

            var marketItem = Manager.MarketItems.Values.ToList().Find(x => x.Type == Enums.LotType.Business && x.Data == business.ID.ToString());
            if (marketItem != null)
                return Reject($"Этот бизнес выставлен на маркетплейсе, взаимодействие с ним невозможно");

            var auctionItem = AuctionManager.AuctionItems.Values.ToList().Find(x => x.Type == Enums.LotType.Business && x.Data == business.ID.ToString());
            if (auctionItem != null)
                return Reject($"Этот бизнес выставлен на аукционе, взаимодействие с ним невозможно");

            return false;
        }
    }
}
