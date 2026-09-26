using Database;
// Авторская библиотека EternalCore не нужна: лог — nLog проекта, конфиг — MarketPlaceConfig.Load()
using GTANetworkAPI;
using LinqToDB;
using MySqlConnector;
using NeptuneEvo.Character;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.EternalDev.MarketPlace.Auction;
using NeptuneEvo.EternalDev.MarketPlace.Auction.Classes;
using NeptuneEvo.EternalDev.MarketPlace.Classes;
using NeptuneEvo.EternalDev.MarketPlace.Configs;
using NeptuneEvo.EternalDev.MarketPlace.DTOs;
using NeptuneEvo.EternalDev.MarketPlace.DTOs.Params;
using NeptuneEvo.EternalDev.MarketPlace.Enums;
using NeptuneEvo.EternalDev.MarketPlace.Extensions;
using NeptuneEvo.Fractions.LSNews.LiveStream;
using NeptuneEvo.Handles;
using NeptuneEvo.MoneySystem;
using Newtonsoft.Json;
using Redage.SDK;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace
{
    public class Manager
    {
        public static MarketPlaceConfig Config = MarketPlaceConfig.Load();

        private static ConcurrentDictionary<ExtPlayer, AppSession> _subscribers = new ConcurrentDictionary<ExtPlayer, AppSession>();
        public static ConcurrentDictionary<int, MarketItem> MarketItems = new ConcurrentDictionary<int, MarketItem>();
        public static ConcurrentDictionary<int, UserProfile> UserProfiles = new ConcurrentDictionary<int, UserProfile>();

        private static int _lastId = 0;

        public static void Initialize()
        {
            DataTable data = MySQL.QueryRead("SELECT * FROM `e-dev_marketplace-items`");

            List<int> inactiveLots = new List<int>();
            if (data != null && data.Rows.Count != 0)
            {
                foreach(DataRow row in data.Rows)
                {
                    var marketItem = new MarketItem()
                    {
                        Id = Convert.ToInt32(row["id"]),
                        Owner = Convert.ToInt32(row["owner"]),
                        Type = (LotType)Convert.ToInt32(row["type"]),
                        Data = row["data"].ToString(),
                        Cost = Convert.ToInt32(row["cost"]),
                        CreateDate = (DateTime)row["createDate"],
                        EndDate = (DateTime)row["endDate"],
                        Views = JsonConvert.DeserializeObject<List<int>>(row["views"].ToString()),
                        Favourites = JsonConvert.DeserializeObject<List<int>>(row["favourites"].ToString()),
                        Comment = row["comment"].ToString(),
                        Photos = JsonConvert.DeserializeObject<List<string>>(row["photos"].ToString()),
                    };

                    if (marketItem.EndDate < DateTime.Now)
                        inactiveLots.Add(marketItem.Id);
                    else
                        MarketItems.TryAdd(marketItem.Id, marketItem);

                    if (_lastId < marketItem.Id)
                        _lastId = marketItem.Id;
                }
            }

            MarketLog.Write($"Loaded {MarketItems.Count} lots in marketPlace");

            data = MySQL.QueryRead("SELECT * FROM `e-dev_marketplace-profiles`");
            if (data != null && data.Rows.Count != 0)
            {
                foreach (DataRow row in data.Rows)
                {
                    var profile = new UserProfile()
                    {
                        UUID = Convert.ToInt32(row["UUID"]),
                        Storage = JsonConvert.DeserializeObject<List<StorageItem>>(row["storage"].ToString())
                    };

                    UserProfiles.TryAdd(profile.UUID, profile);
                }
            }

            MarketLog.Write($"Loaded {UserProfiles.Count} profiles in marketPlace");

            inactiveLots.ForEach(id 
                => MySQL.Query($"DELETE FROM `e-dev_marketplace-items` WHERE `id`={id}"));

            foreach (var interiorPosition in Config.InteriorPositions)
                interiorPosition.GTAElements();

            foreach (var auctionPosition in Config.AuctionPositions)
                auctionPosition.GTAElements();

            foreach (var storagePlace in Config.StoragePostions)
                storagePlace.GTAElements();

            AuctionManager.OnStart();

            Timers.Start(1000, () => OnEverySecond());
        }

        public static void OnEverySecond()
        {
            foreach (var marketItem in MarketItems.Values)
            {
                if (marketItem.EndDate > DateTime.Now)
                    continue;

                marketItem.Delete();
            }

            AuctionManager.OnEverySecond();
        }

        private static int GenerateId()
        {
            _lastId++;
            return _lastId;
        }

        public static MarketItem CreateLot(MarketItem marketItem)
        {
            marketItem.Id = GenerateId();
            MarketItems.TryAdd(marketItem.Id, marketItem);

            var sqlCommand = new MySqlCommand("INSERT INTO `e-dev_marketplace-items` (`id`, `owner`, `type`, `data`, `cost`, `createDate`, `endDate`, `views`, `favourites`, `comment`, `photos`)" +
                "VALUES(@id, @owner, @type, @data, @cost, @createDate, @endDate, @views, @favourites, @comment, @photos)");
            
            sqlCommand.Parameters.AddWithValue("@id", marketItem.Id);
            sqlCommand.Parameters.AddWithValue("@owner", marketItem.Owner);
            sqlCommand.Parameters.AddWithValue("@type", (int)marketItem.Type);
            sqlCommand.Parameters.AddWithValue("@data", marketItem.Data.ToString());
            sqlCommand.Parameters.AddWithValue("@cost", marketItem.Cost);
            sqlCommand.Parameters.AddWithValue("@createDate", marketItem.CreateDate.ToString("s"));
            sqlCommand.Parameters.AddWithValue("@endDate", marketItem.EndDate.ToString("s"));
            sqlCommand.Parameters.AddWithValue("@views", JsonConvert.SerializeObject(marketItem.Views));
            sqlCommand.Parameters.AddWithValue("@favourites", JsonConvert.SerializeObject(marketItem.Favourites));
            sqlCommand.Parameters.AddWithValue("@comment", marketItem.Comment);
            sqlCommand.Parameters.AddWithValue("@photos", JsonConvert.SerializeObject(marketItem.Photos));

            MySQL.Query(sqlCommand);
            return marketItem;
        }

        public static void Save()
        {
            AuctionManager.Save();

            foreach (var marketItem in MarketItems.Values)
            {
                if (!marketItem.IsSave)
                    continue;
                
                MySQL.Query($"UPDATE `e-dev_marketplace-items` SET `endDate`='{marketItem.EndDate.ToString("s")}', " +
                    $"`views`='{JsonConvert.SerializeObject(marketItem.Views)}', `favourites`='{JsonConvert.SerializeObject(marketItem.Favourites)}'," +
                    $"`comment`='{marketItem.Comment}', `photos`='{JsonConvert.SerializeObject(marketItem.Photos)}', `cost`={marketItem.Cost} WHERE `id`={marketItem.Id}");
                
                marketItem.IsSave = false;
            }
        }

        public static bool DeleteLot(MarketItem marketItem)
        {
            if (!MarketItems.TryRemove(marketItem.Id, out _))
                return false;

            MySQL.Query($"DELETE FROM `e-dev_marketplace-items` WHERE `id`={marketItem.Id}");
            UpdatePage(marketItem.Type.ToString().ToLower());

            return true;
        }

        public static MarketItem GetMarketItem(int id)
        {
            MarketItems.TryGetValue(id, out var marketItem);
            return marketItem;
        }

        public static bool GetUserProfile(int uuid, out UserProfile profile)
        {
            if (!UserProfiles.TryGetValue(uuid, out profile))
            {
                profile = new UserProfile()
                {
                    UUID = uuid,
                };

                MySQL.Query("INSERT INTO `e-dev_marketplace-profiles` (`uuid`, `storage`)" +
                    $"VALUES({uuid}, '{JsonConvert.SerializeObject(profile.Storage)}')");

                UserProfiles.TryAdd(uuid, profile);
            }

            return profile != null;
        }

        public static void Subscribe(ExtPlayer player)
        {
            if (!_subscribers.ContainsKey(player)) 
                _subscribers.TryAdd(player, new AppSession());

            Trigger.ClientEvent(player, "client.marketPlace.open");

            UpdateStorage(player);
            SetPage(player, "auction");
            player.UpdateMarketplaceFavourites();
        }

        public static void UnSubscribe(ExtPlayer player)
        {
            if (_subscribers.ContainsKey(player))
                _subscribers.TryRemove(player, out _);

            Trigger.ClientEvent(player, "client.marketPlace.close");
        }

        public static void UpdateStorage(ExtPlayer player) 
        {
            Trigger.ClientEvent(player, "client.marketPlace.setItems", "storage", JsonConvert.SerializeObject(
                Methods.Formatter.FormatStorage(
                    player,
                    player.GetMarketPlaceStorage(),
                    player.GetPropertyOnEstate()
                )
            ));
        }

        public static bool GetPlayerAppSession(ExtPlayer player, out AppSession appSession)
            => _subscribers.TryGetValue(player, out appSession);

        public static List<ExtPlayer> GetPlayersInPage(string pageName) 
            => _subscribers.Where(x => x.Value.Page == pageName).Select(x => x.Key).ToList();

        public static List<ExtPlayer> GetPlayersInLot(string pageName, int lotId)
            => _subscribers.Where(x => x.Value.Page == pageName && x.Value.SelectedLot == lotId).Select(x => x.Key).ToList();

        public static void SetPage(ExtPlayer player, string pageName)
        {
            if (!player.GetMarketPlaceSession(out var appSession))
                return;

            appSession.Page = pageName;
            switch(pageName)
            {
                case "auction":
                    AuctionManager.SetItems(player);
                    break;
                case "house":
                case "vehicle":
                case "business":
                case "service":
                    var items = MarketItems.Values.Where(x => x.Type == Methods.Formatter.GetLotType(pageName)).ToList();
                    Trigger.ClientEvent(player, "client.marketPlace.setItems", "main", JsonConvert.SerializeObject(Methods.Formatter.CreateMarketDTO(items)));
                    break;
                case "advertisements":
                    items = MarketItems.Values.Where(x => x.Owner == player.GetUUID()).ToList();
                    Trigger.ClientEvent(player, "client.marketPlace.setItems", "main", JsonConvert.SerializeObject(Methods.Formatter.CreateMarketDTO(items)));
                    break;
                case "storage":
                    UpdateStorage(player);
                    break;
                case "favourites":
                    var marketItems = MarketItems.Values.Where(x => (x.Type != LotType.Item && x.Type != LotType.Clothes) && x.Favourites.Contains(player.GetUUID())).ToList();
                    var auctionItems = Auction.AuctionManager.AuctionItems.Values.Where(x => x.Favourites.Contains(player.GetUUID())).ToList();
                    var inventoryItems = MarketItems.Values.Where(x => (x.Type == LotType.Item || x.Type == LotType.Clothes) && x.Favourites.Contains(player.GetUUID())).ToList();

                    Trigger.ClientEvent(player, "client.marketPlace.setItems", "main", JsonConvert.SerializeObject(Methods.Formatter.CreateMarketDTO(marketItems)));
                    Trigger.ClientEvent(player, "client.marketPlace.setItems", "auction", JsonConvert.SerializeObject(Auction.AuctionManager.GetAuctionItemDTOs(auctionItems)));
                    Trigger.ClientEvent(player, "client.marketPlace.setItems", "inventory", JsonConvert.SerializeObject(Methods.Formatter.CreateMarketGroupDTO(inventoryItems)));
                    break;
                case "clothes":
                    items = MarketItems.Values.Where(x => x.Type == LotType.Clothes).ToList();
                    Trigger.ClientEvent(player, "client.marketPlace.setItems", "inventory", JsonConvert.SerializeObject(Methods.Formatter.CreateMarketGroupDTO(items)));
                    break;
                case "item":
                    items = MarketItems.Values.Where(x => x.Type == LotType.Item).ToList();
                    Trigger.ClientEvent(player, "client.marketPlace.setItems", "inventory", JsonConvert.SerializeObject(Methods.Formatter.CreateMarketGroupDTO(items)));
                    break;
            }
        }

        public static void UpdatePage(string page)
        {
            var players = GetPlayersInPage(page);
            if (players == null || !players.Any())
                return;

            players.ForEach(player => SetPage(player, page));
        }

        public static void UpdateItem(string type, LotType lotType, int id)
        {
            object data = null;
            var players = new List<ExtPlayer>();
            switch(type)
            {
                case "auction":
                    data = new Auction.DTOs.AuctionItemDTO(id, lotType);
                    players = GetPlayersInPage("auction");
                    break;
                case "main":
                    data = new MarketLotDTO(id, lotType);
                    players = GetPlayersInPage(lotType.ToString().ToLower());
                    break;
            }

            players.ForEach(player => Trigger.ClientEvent(player, "client.marketPlace.updateItem", type, id, JsonConvert.SerializeObject(data)));
        }

        public static void AddMoney(int uuid, int amount, string message)
        {
            var player = Main.GetPlayerByUUID(uuid);
            var characterData = player.GetCharacterData();
            if (characterData != null)
            {
                var accountId = characterData.Bank;
                if (Bank.Accounts.ContainsKey(accountId))
                    Bank.Change(characterData.Bank, amount);
                else
                    Wallet.Change(player, amount);

                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, message, 3000);
            }
            else
            {
                Trigger.SetTask(async () =>
                {
                    try
                    {
                        await using var db = new ServerBD("MainDB");//В отдельном потоке

                        var character = await db.Characters
                            .Select(v => new { v.Uuid, v.Money, v.Bank })
                            .Where(v => v.Uuid == uuid)
                            .FirstOrDefaultAsync();

                        if (character != null)
                        {
                            var accountId = Convert.ToInt32(character.Bank);
                            if (Bank.Accounts.ContainsKey(accountId))
                            {
                                Trigger.SetMainTask(() => Bank.Change(accountId, amount));
                            }
                            else
                            {
                                await db.Characters
                                    .Where(v => v.Uuid == uuid)
                                    .Set(v => v.Money, (character.Money + amount))
                                    .UpdateAsync();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debugs.Repository.Exception(e);
                    }
                });
            }
        }
    }
}
