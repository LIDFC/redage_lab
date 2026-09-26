using MySqlX.XDevAPI.Common;
using MySqlX.XDevAPI.Relational;
using NeptuneEvo.Database.Models;
using NeptuneEvo.EternalDev.MarketPlace.Classes;
using NeptuneEvo.EternalDev.MarketPlace.DTOs;
using NeptuneEvo.EternalDev.MarketPlace.DTOs.Params;
using NeptuneEvo.EternalDev.MarketPlace.Enums;
using NeptuneEvo.Handles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Methods
{
    public class Formatter
    {
        /// <summary>Предмет из хранилища по SqlId (в оригинале — Chars.Repository.GetItemDataBySqlId, которого нет в этой сборке)</summary>
        private static NeptuneEvo.Chars.Models.InventoryItemData GetItemDataBySqlId(string locationName, string location, int sqlId)
        {
            if (!Chars.Repository.ItemsData.TryGetValue(locationName, out var locations) || !locations.TryGetValue(location, out var items))
                return null;
            return items.Values.FirstOrDefault(i => i.SqlId == sqlId);
        }

        public static LotType GetLotType(string type)
        {
            switch(type)
            {
                case "vehicle": return LotType.Vehicle;
                case "house": return LotType.House;
                case "business": return LotType.Business;
                case "service": return LotType.Service;
                case "item": return LotType.Item;
                case "clothes": return LotType.Clothes;
                default: return LotType.None;
            }
        }

        public static long DateTimeToMilliseconds(DateTime dateTime)
        {
            DateTimeOffset dateTimeOffset = new DateTimeOffset(dateTime);
            long millisecondsSinceEpoch = dateTimeOffset.ToUnixTimeMilliseconds();

            return millisecondsSinceEpoch;
        }

        public static List<StorageItemDTO> FormatStorage(ExtPlayer player, Dictionary<LotType, List<string>> storageData, Dictionary<LotType, List<string>> estate)
        {
            var result = new List<StorageItemDTO>();

            void ProcessData(Dictionary<LotType, List<string>> data, bool isEsate)
            {
                foreach (var pair in data)
                {
                    foreach (var id in pair.Value)
                    {
                        var dto = CreateStorageDto(player, pair.Key, id, isEsate);
                        if (dto is null)
                            continue;

                        if (dto.LotType == LotType.House || dto.LotType == LotType.Business || dto.LotType == LotType.Vehicle)
                        {
                            var marketItem = Manager.MarketItems.Values.ToList().Find(m => m.Type == dto.LotType && m.Data == id);
                            if (marketItem != null)
                                continue;
                        }

                        result.Add(dto);
                    }
                }
            }

            ProcessData(storageData, false);
            ProcessData(estate, true);

            return result;
        }

        public static StorageItemDTO CreateStorageDto(ExtPlayer player, LotType type, string id, bool isEastate)
        {
            var storageId = isEastate || !id.Contains("__") ? 0 : Convert.ToInt32(id.Split("__")[0]);
            var data = isEastate || !id.Contains("__") ? id : id.Split("__")[1];

            var result = new StorageItemDTO(0, type)
            {
                Id = storageId,
                OnEstate = isEastate,
                EndDate = DateTimeToMilliseconds(DateTime.Now)
            };

            switch (type)
            {
                case LotType.Vehicle:
                    {
                        var paramsData = new VehicleParams(data);
                        if (paramsData == null)
                            return null;

                        result.Params = paramsData;
                    }
                    break;
                case LotType.Clothes:
                case LotType.Item:
                    {
                        var itemData = GetItemDataBySqlId($"marketStorage_{player.GetUUID()}", "marketStorage", Convert.ToInt32(data));
                        if (itemData == null) 
                            return null;

                        var extraData = $"{(int)itemData.ItemId}@@{itemData.Count}@@{itemData.Data}";
                        ParamsBase paramsData = null;

                        if (type == LotType.Clothes)
                            paramsData = new ClothesParams(extraData);

                        if (type == LotType.Item)
                            paramsData = new ItemParams(extraData);

                        if (paramsData == null)
                            return null;

                        result.Params = paramsData;
                    }
                    break;
                case LotType.House:
                    {
                        var paramsData = new HouseParams(data);
                        if (paramsData == null)
                            return null;

                        result.Params = paramsData;
                    }
                    break;
                case LotType.Business:
                    {
                        var paramsData = new BusinessParams(data);
                        if (paramsData == null)
                            return null;

                        result.Params = paramsData;
                    }
                    break;
            }

            return result;
        }

        public static List<MarketLotDTO> CreateMarketDTO(List<MarketItem> marketItems)
        {
            return marketItems.Select(x => new MarketLotDTO(x.Id, x.Type)).ToList();
        }

        public static List<MarketItemGroupDTO> CreateMarketGroupDTO(List<MarketItem> marketItems)
        {
            var groupItems = marketItems
                .GroupBy(g => new string[] { g.Data.Split("@@")[0], g.Data.Split("@@")[2], g.Type.ToString().ToLower() })
                .Select(g =>
                {
                    var itemData = $"{g.Key[0]}@@1@@{g.Key[1]}";
                    var dto = new MarketItemGroupDTO
                    {
                        Id = g.First().Id,
                        Type = g.Key[2],
                        MinPrice = g.Min(x => x.Cost),
                        Count = g.Sum(x => Convert.ToInt32(x.Data.Split("@@")[1])),
                    };

                    if (g.Key[2] == "clothes")
                        dto.Params = new ClothesParams(itemData);

                    if (g.Key[2] == "item")
                        dto.Params = new ItemParams(itemData);

                    return dto;
                }).ToList();

            return groupItems;
        }

        public static List<object> CreateInventoryItemMarketDTO(List<MarketItem> marketItems)
        {
            return marketItems.Select(x =>
            {
                var sim = Main.SimCards.FirstOrDefault(u => u.Value == x.Owner).Key;
                var split = x.Data.Split("@@");
                return new
                {
                    id = x.Id,
                    author = new
                    {
                        name = Main.PlayerNames[x.Owner],
                        id = x.Owner,
                        phoneNumber = Main.SimCards.ContainsKey(sim) ? sim : -1
                    },
                    cost = x.Cost,
                    count = Convert.ToInt32(split[1]),
                    itemData = split[2],
                    endTime = DateTimeToMilliseconds(x.EndDate)
                };
            }).ToList<object>();
        }
    }
}
