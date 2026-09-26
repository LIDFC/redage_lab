using NeptuneEvo.Core;
using NeptuneEvo.EternalDev.MarketPlace.Classes;
using NeptuneEvo.EternalDev.MarketPlace.DTOs;
using NeptuneEvo.EternalDev.MarketPlace.Enums;
using NeptuneEvo.Handles;
using NeptuneEvo.Houses;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Extensions
{
    public static class Players
    {
        public static bool GetMarketPlaceSession(this ExtPlayer player, out AppSession appSession)
        {
            return Manager.GetPlayerAppSession(player, out appSession);
        }

        public static bool GetMarketPlaceProfile(this ExtPlayer player, out UserProfile userProfile)
        {
            return Manager.GetUserProfile(player.GetUUID(), out userProfile);
        }

        public static Dictionary<LotType, List<string>> GetMarketPlaceStorage(this ExtPlayer player)
        {
            if (!player.GetMarketPlaceProfile(out var userProfile))
                return new Dictionary<LotType, List<string>>();

            var result = new Dictionary<LotType, List<string>>();

            var locationName = $"marketStorage_{player.GetUUID()}";
            if (Chars.Repository.ItemsData.ContainsKey(locationName) && Chars.Repository.ItemsData[locationName].TryGetValue("marketStorage", out var items)) {
                foreach (var itemData in items.Values)
                {
                    var lotType = Chars.Repository.ItemsInfo[itemData.ItemId].functionType == Chars.Models.newItemType.Clothes && itemData.ItemId != Chars.Models.ItemId.BodyArmor ? LotType.Clothes : LotType.Item;

                    if (!result.ContainsKey(lotType)) 
                        result.TryAdd(lotType, new List<string>());

                    result[lotType].Add(itemData.SqlId.ToString());
                }
            }

            userProfile.Storage.ForEach(x =>
            {
                if (!result.ContainsKey(x.Type))
                    result.TryAdd(x.Type, new List<string>());

                result[x.Type].Add($"{x.Id}__{x.Data}");
            });

            return result;
        }

        public static Dictionary<LotType, List<string>> GetPropertyOnEstate(this ExtPlayer player)
        {
            var result = new Dictionary<LotType, List<string>>();

            var playerVehicles = VehicleManager.GetVehiclesCarNumberToPlayer(player.GetName());
            if (playerVehicles != null && playerVehicles.Count > 0)
            {
                result.Add(LotType.Vehicle, new List<string>());

                foreach (string vehicleNumber in playerVehicles)
                {
                    var vehicleData = VehicleManager.GetVehicleToNumber(vehicleNumber);
                    if (vehicleData is null) continue;

                    result[LotType.Vehicle].Add(vehicleData.SqlId.ToString());
                }
            }

            var house = HouseManager.GetHouse(player, true);
            if (house != null)
                result.Add(LotType.House, new List<string> { house.ID.ToString() });

            var business = BusinessManager.BizList.Values.ToList().Find(x => x.Owner == player.GetName());
            if (business != null)
                result.Add(LotType.Business, new List<string> { business.ID.ToString() });

            return result;
        }

        public static List<FavouriteDataDTO> GetMarketplaceFavouriteItems(this ExtPlayer player)
        {
            var favourites = new List<FavouriteDataDTO>();
            var marketItems = Manager.MarketItems.Values.Where(x => x.Favourites.Contains(player.GetUUID())).ToList();
            foreach (var item in marketItems)
            {
                var isGroupped = item.Type == LotType.Clothes || item.Type == LotType.Item;
                var favouriteItem = new FavouriteDataDTO()
                {
                    Id = isGroupped ? Convert.ToInt32(item.Data.Split("@@")[0]) : item.Id,
                    SubData = isGroupped ? item.Data.Split("@@")[2] : "",
                    Type = isGroupped ? item.Type.ToString().ToLower() : "market"
                };

                if (isGroupped && favourites.Find(x => x.SubData == favouriteItem.SubData && x.Type == favouriteItem.Type) != null)
                    continue;

                favourites.Add(favouriteItem);
            }

            var auctionItems = Auction.AuctionManager.AuctionItems.Values.Where(x => x.Favourites.Contains(player.GetUUID())).ToList();
            foreach (var item in auctionItems)
            {
                var favouriteItem = new FavouriteDataDTO()
                {
                    Id = item.Id,
                    SubData = "",
                    Type = "auction"
                };

                favourites.Add(favouriteItem);
            }

            return favourites;
        }

        public static void UpdateMarketplaceFavourites(this ExtPlayer player)
        {
            var favourites = player.GetMarketplaceFavouriteItems();
            Trigger.ClientEvent(player, "client.marketPlace.setItems", "favourites", JsonConvert.SerializeObject(favourites));
        }
    }
}
