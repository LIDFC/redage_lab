using GTANetworkAPI;
using NeptuneEvo.EternalDev.MarketPlace.Auction;
using NeptuneEvo.EternalDev.MarketPlace.Auction.DTOs;
using NeptuneEvo.Handles;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Methods
{
    public class Requests : Script
    {
        [RemoteEvent("server.marketPlace.request")]
        public static async void On(ExtPlayer player, string type, string data)
        {
            object result = null;

            switch(type)
            {
                case "items":
                    var isClothes = data.Contains("^");

                    var itemId = isClothes ? Convert.ToInt32(data.Split('^')[0]) : Convert.ToInt32(data);
                    var items = Manager.MarketItems.Values.Where(x =>
                    {
                        var itemData = x.Data.Split("@@");

                        if (isClothes)
                        {
                            return x.Type == Enums.LotType.Clothes
                                && Convert.ToInt32(itemData[0]) == itemId
                                && itemData[2] == data.Split('^')[1];
                        }
                        else
                            return x.Type == Enums.LotType.Item 
                                && Convert.ToInt32(itemData[0]) == itemId;

                    }).ToList();
                    result = JsonConvert.SerializeObject(Formatter.CreateInventoryItemMarketDTO(items));
                    break;
                case "auction":
                    var auctionItem = AuctionManager.GetAuctionItem(Convert.ToInt32(data));
                    if (auctionItem is null)
                        return;

                    result = JsonConvert.SerializeObject(auctionItem.Bets.Select(x => new BetDTO(x)));
                    break;
                case "businessStats":
                    result = JsonConvert.SerializeObject(await NeptuneEvo.Businesses.History.Repository.GetBusinessStats(Convert.ToInt32(data)));
                    break;
            }

            Trigger.ClientEvent(player, "client.marketPlace.resolve", type, result);
        }
    }
}
