using GTANetworkAPI;
using NeptuneEvo.Handles;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Auction.Methods
{
    public class SetBet : Script
    {
        [RemoteEvent("server.marketPlace.auction.setBet")]
        public static void On(ExtPlayer player, int lotId, int multiplayer)
        {
            var auctionLot = AuctionManager.GetAuctionItem(lotId); 
            if (auctionLot is null)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Лот не найден", 3000);
                return;
            }

            auctionLot.SetBet(player, multiplayer);
        }
    }
}
