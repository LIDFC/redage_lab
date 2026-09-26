using GTANetworkAPI;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.EternalDev.MarketPlace.Classes;
using NeptuneEvo.EternalDev.MarketPlace.Extensions;
using NeptuneEvo.Handles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Methods
{
    public class Favourite : Script
    {
        [RemoteEvent("server.marketPlace.set_favourite")]
        public static void On(ExtPlayer player, string type, int id, string subData)
        {
            if (!player.GetMarketPlaceSession(out var appSession))
                return;

            int uuid = player.GetUUID();

            switch (type)
            {
                case "auction":
                    var auctionItem = Auction.AuctionManager.GetAuctionItem(id);
                    if (auctionItem is null)
                        return;

                    if (auctionItem.Favourites.Contains(uuid))
                        auctionItem.Favourites.Remove(uuid);
                    else
                        auctionItem.Favourites.Add(uuid);

                    auctionItem.Save();
                    break;
                case "market":
                    var marketItem = Manager.GetMarketItem(id);
                    if (marketItem is null) 
                        return;
                    
                    if (marketItem.Favourites.Contains(uuid))
                        marketItem.Favourites.Remove(uuid);
                    else
                        marketItem.Favourites.Add(uuid);

                    marketItem.Save();
                    break;

                case "clothes":
                case "item":
                    var marketItems = Manager.MarketItems.Values.Where(x =>
                    {
                        if (x.Type != Enums.LotType.Item && x.Type != Enums.LotType.Clothes)
                            return false;

                        var split = x.Data.Split("@@");

                        var itemId = Convert.ToInt32(split[0]);
                        var itemData = split[2].ToString();

                        return itemId == id && itemData == subData;
                    }).ToList();

                    marketItems.ForEach(marketItem =>
                    {
                        if (marketItem.Favourites.Contains(uuid))
                            marketItem.Favourites.Remove(uuid);
                        else
                            marketItem.Favourites.Add(uuid);

                        marketItem.Save();
                        Manager.UpdateItem("inventory", marketItem.Type, marketItem.Id);
                    });
                    break;
            }

            player.UpdateMarketplaceFavourites();
            if (appSession.Page == "favourites")
                Manager.SetPage(player, "favourites");
        }
    }
}
