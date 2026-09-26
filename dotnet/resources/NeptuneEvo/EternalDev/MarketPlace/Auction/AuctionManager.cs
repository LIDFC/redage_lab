// EternalCore заменён на nLog проекта
using GTANetworkAPI;
using MySqlConnector;
using NeptuneEvo.Core;
using NeptuneEvo.EternalDev.MarketPlace.Auction.Classes;
using NeptuneEvo.EternalDev.MarketPlace.Auction.DTOs;
using NeptuneEvo.EternalDev.MarketPlace.Classes;
using NeptuneEvo.EternalDev.MarketPlace.Enums;
using NeptuneEvo.Handles;
using NeptuneEvo.Houses;
using Newtonsoft.Json;
using Redage.SDK;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Auction
{
    public class AuctionManager
    {
        public static ConcurrentDictionary<int, AuctionItem> AuctionItems = new ConcurrentDictionary<int, AuctionItem>();
        private static int _lastId = 0;

        public static void OnStart()
        {
            DataTable data = MySQL.QueryRead("SELECT * FROM `e-dev_marketplace-auction`");
            if (data != null && data.Rows.Count > 0)
            {
                foreach (DataRow row in data.Rows)
                {
                    var auctionItem = new AuctionItem()
                    {
                        Id = Convert.ToInt32(row["id"]),
                        Data = row["data"].ToString(),
                        EndDate = (DateTime)row["endDate"],
                        Bets = JsonConvert.DeserializeObject<List<BetData>>(row["bets"].ToString()),
                        Favourites = JsonConvert.DeserializeObject<List<int>>(row["favourites"].ToString()),
                        Views = JsonConvert.DeserializeObject<List<int>>(row["views"].ToString()),
                        Type = (LotType)Convert.ToInt32(row["type"]),
                        BetStep = Convert.ToInt32(row["betStep"]),
                        Cost = Convert.ToInt32(row["cost"]),
                    };

                    if (auctionItem.Id > _lastId)
                        _lastId = auctionItem.Id;

                    AuctionItems.TryAdd(auctionItem.Id, auctionItem);
                }
            }

            MarketLog.Write($"Loaded {AuctionItems.Count} lots in auction");
        }

        public static AuctionItem GetAuctionItem(int id)
        {
            AuctionItems.TryGetValue(id, out AuctionItem item);
            return item;
        }

        public static List<AuctionItemDTO> GetAuctionItemDTOs(List<AuctionItem> auctionItems)
        {
            return auctionItems.Select(x => new AuctionItemDTO(x.Id, x.Type)).ToList();
        }

        private static int GenerateId()
        {
            _lastId++;
            return _lastId;
        }

        public static void SetPropertyToAuction(House house)
        {
            var auctionItem = new AuctionItem()
            {
                Type = LotType.House,
                Data = house.ID.ToString(),
                EndDate = DateTime.Now.AddHours(Manager.Config.App.AuctionTime),
                Cost = house.Price,
                BetStep = Manager.Config.App.BetStep,
            };

            CreateAuction(auctionItem);
        }

        public static void SetPropertyToAuction(Business business)
        {
            var auctionItem = new AuctionItem()
            {
                Type = LotType.Business,
                Data = business.ID.ToString(),
                EndDate = DateTime.Now.AddHours(Manager.Config.App.AuctionTime),
                Cost = business.SellPrice,
                BetStep = Manager.Config.App.BetStep,
            };

            CreateAuction(auctionItem);
        }

        public static AuctionItem CreateAuction(AuctionItem auctionItem)
        {
            auctionItem.Id = GenerateId();
            AuctionItems.TryAdd(auctionItem.Id, auctionItem);

            var sqlCommand = new MySqlCommand("INSERT INTO `e-dev_marketplace-auction` (`id`, `type`, `data`, `bets`, `endDate`, `views`, `favourites`, `cost`, `betStep`)" +
                "VALUES(@id, @type, @data, @bets, @endDate, @views, @favourites, @cost, @betStep)");

            sqlCommand.Parameters.AddWithValue("@id", auctionItem.Id);
            sqlCommand.Parameters.AddWithValue("@type", (int)auctionItem.Type);
            sqlCommand.Parameters.AddWithValue("@data", auctionItem.Data.ToString());
            sqlCommand.Parameters.AddWithValue("@bets", JsonConvert.SerializeObject(auctionItem.Bets));
            sqlCommand.Parameters.AddWithValue("@endDate", auctionItem.EndDate.ToString("s"));
            sqlCommand.Parameters.AddWithValue("@views", JsonConvert.SerializeObject(auctionItem.Views));
            sqlCommand.Parameters.AddWithValue("@favourites", JsonConvert.SerializeObject(auctionItem.Favourites));
            sqlCommand.Parameters.AddWithValue("@cost", auctionItem.Cost);
            sqlCommand.Parameters.AddWithValue("@betStep", auctionItem.BetStep);

            MySQL.Query(sqlCommand);
            Manager.UpdatePage("auction");
            return auctionItem;
        }

        public static bool DeleteAuctionItem(AuctionItem auctionItem)
        {
            if (!AuctionItems.TryRemove(auctionItem.Id, out _))
                return false;

            MySQL.Query($"DELETE FROM `e-dev_marketplace-auction` WHERE `id`={auctionItem.Id}");
            Manager.UpdatePage("auction");

            return true;
        }

        public static void SetItems(ExtPlayer player)
        {
            var items = GetAuctionItemDTOs(AuctionItems.Values.ToList());
            Trigger.ClientEvent(player, "client.marketPlace.setItems", "auction", JsonConvert.SerializeObject(items));
        }

        public static void OnEverySecond()
        {
            foreach (var auctionItem in AuctionItems.Values)
            {
                auctionItem.TryFinish();
            }
        }

        public static void Save()
        {
            foreach (var auctionItem in AuctionItems.Values)
            {
                if (!auctionItem.IsSave)
                    continue;

                MySQL.Query($"UPDATE `e-dev_marketplace-auction` SET `bets`='{JsonConvert.SerializeObject(auctionItem.Bets)}', `endDate`='{auctionItem.EndDate.ToString("s")}', " +
                    $"views='{JsonConvert.SerializeObject(auctionItem.Views)}', `favourites`='{JsonConvert.SerializeObject(auctionItem.Favourites)}' WHERE `id`={auctionItem.Id}");

                auctionItem.IsSave = false;
            }
        }
    }
}
