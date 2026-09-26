using GTANetworkAPI;
using NeptuneEvo.EternalDev.MarketPlace.Auction;
using NeptuneEvo.EternalDev.MarketPlace.Enums;
using NeptuneEvo.EternalDev.MarketPlace.Extensions;
using NeptuneEvo.Handles;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Methods
{
    public class SelectLot : Script
    {
        [RemoteEvent("server.marketPlace.selectItem")]
        public static void On(ExtPlayer player, string type, int id)
        {
            if (!player.GetMarketPlaceSession(out var appSession))
                return;

            var lotType = Methods.Formatter.GetLotType(type);
            if (lotType == LotType.None || lotType == LotType.Item)
            {
                appSession.SelectedLot = 0;
                return;
            }

            var uuid = player.GetUUID();
            if (appSession.Page == "auction")
            {
                var auctionItem = AuctionManager.GetAuctionItem(id);
                if (auctionItem is null)
                    return;

                if (!auctionItem.Views.Contains(uuid))
                {
                    auctionItem.Views.Add(uuid);
                    auctionItem.Save();

                    Manager.UpdateItem("auction", lotType, auctionItem.Id);
                }

                appSession.SelectedLot = auctionItem.Id;
            }
            else
            {
                var marketItem = Manager.GetMarketItem(id);
                if (marketItem is null || marketItem.Type != lotType)
                    return;

                if (!marketItem.Views.Contains(uuid))
                {
                    marketItem.Views.Add(uuid);
                    marketItem.Save();

                    Manager.UpdateItem("main", lotType, marketItem.Id);
                }

                appSession.SelectedLot = marketItem.Id;
            }
        }
    }
}
