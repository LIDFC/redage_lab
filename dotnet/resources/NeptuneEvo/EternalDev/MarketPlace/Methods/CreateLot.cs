using GTANetworkAPI;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Core;
using NeptuneEvo.Database.Models;
using NeptuneEvo.EternalDev.MarketPlace.Classes;
using NeptuneEvo.EternalDev.MarketPlace.Configs;
using NeptuneEvo.EternalDev.MarketPlace.Enums;
using NeptuneEvo.EternalDev.MarketPlace.Extensions;
using NeptuneEvo.Handles;
using NeptuneEvo.Houses;
using Redage.SDK;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Methods
{
    public class CreateLot : Script
    {
        [RemoteEvent("server.marketPlace.create_lot")]
        public static void On(ExtPlayer player, string type, string key, string comment, string title, int price, int hours, int count, string paymentType)
        {
            var lotType = Formatter.GetLotType(type);
            if (lotType == LotType.None)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Ошибка создания!", 3000);
                return;
            }

            FormatKey(lotType, key, out var id, out var extraData);

            if (!Check(player, lotType, id, extraData, count))
                return;

            var commission = Commission.Get(lotType, price, hours, count);
            if (paymentType == "Wallet" ? !MoneySystem.Wallet.Change(player, -commission) 
                : paymentType == "Card" ? !MoneySystem.Bank.Change(player.CharacterData.Bank, -commission) : true)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"У вас недостаточно средств!", 3000);
                return;
            }

            if (lotType == LotType.Item || lotType == LotType.Clothes)
                Chars.Repository.Remove(null, $"marketStorage_{player.GetUUID()}", "marketStorage", (ItemId)id, count, extraData);

            var marketItem = new MarketItem()
            {
                Type = lotType,
                Data = CreateData(player, lotType, id, title, extraData, count),
                CreateDate = DateTime.Now,
                EndDate = DateTime.Now.AddHours(hours),
                Comment = comment,
                Cost = price,
                Favourites = new List<int>(),
                Views = new List<int>(),
                Owner = player.GetUUID(),
                Photos = new List<string>()
            };

            var lot = Manager.CreateLot(marketItem);
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы успешно выставили лот за {MoneySystem.Wallet.Format(price)}$", 3000);

            Manager.UpdatePage(lot.Type.ToString().ToLower());
            Manager.UpdateStorage(player);
        }

        private static void FormatKey(LotType type, string key, out int id, out string data)
        {
            if (type == LotType.Clothes)
            {
                var split = key.Split("^");

                if (split.Length == 2)
                {
                    id = Convert.ToInt32(split[0]);
                    data = split[1];
                    return;
                }
            }

            id = Convert.ToInt32(key);
            data = string.Empty;
        }

        private static bool Check(ExtPlayer player, LotType type, int id, string extraData, int count)
        {
            if (!player.GetMarketPlaceProfile(out var profile))
                return false;

            switch(type)
            {
                case LotType.Clothes:
                case LotType.Item:
                    {
                        if (Chars.Repository.getCountToLacationItem($"marketStorage_{player.GetUUID()}", "marketStorage", (ItemId)id, type == LotType.Clothes ? extraData : null) < count)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"У вас нет столько {Chars.Repository.ItemsInfo[(ItemId)id].Name} на складе маркетплейса", 3000);
                            return false;
                        }
                    }
                    break;
                case LotType.Business:
                    {
                        var business = BusinessManager.BizList.Values.ToList().Find(x => x.Owner == player.GetName());
                        if (business is null || business.ID != id)
                        {
                            var storageItem = profile.Storage.Find(x => x.Type == LotType.Business && Convert.ToInt32(x.Data) == id);
                            if (storageItem != null)
                                business = BusinessManager.BizList.Values.ToList().Find(x => x.ID == id);
                        }
                        
                        if (business is null)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"У вас нет этого бизнеса!", 3000);
                            return false;
                        }
                    }
                    break;
                case LotType.House:
                    {
                        var house = HouseManager.GetHouse(player, true);
                        if (house is null || house.ID != id)
                        {
                            var storageItem = profile.Storage.Find(x => x.Type == LotType.House && Convert.ToInt32(x.Data) == id);
                            if (storageItem != null)
                                house = HouseManager.Houses.Find(x => x.ID == id);
                        }
                        if (house is null)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"У вас нет этого дома!", 3000);
                            return false;
                        }
                    }
                    break;
                case LotType.Vehicle:
                    {
                        var vehicleData = VehicleManager.GetVehicleToAutoId(id);
                        if (vehicleData is null || vehicleData.Holder != player.GetName())
                        {
                            var storageItem = profile.Storage.Find(x => x.Type == LotType.Vehicle && Convert.ToInt32(x.Data) == id);
                            if (storageItem != null)
                                vehicleData = VehicleManager.GetVehicleToAutoId(id);
                        }

                        if (vehicleData is null)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"У вас нет этого авто!", 3000);
                            return false;
                        }
                    }
                    break;
            }

            if (type == LotType.House && type == LotType.Business && type == LotType.Vehicle)
            {
                var marketItem = Manager.MarketItems.Values.ToList().Find(m => m.Type == type && m.Data == id.ToString());
                if (marketItem != null)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"Имущество уже выставлено на маркетплейсе!", 3000);
                    return false;
                }
            }

            return true;
        }

        private static string CreateData(ExtPlayer player, LotType lotType, int id, string title, string extraData, int count)
        {
            if (lotType == LotType.Service)
                return title;

            if (lotType != LotType.Item && lotType != LotType.Clothes)
                return id.ToString();

            return $"{id}@@{count}@@{extraData}";
        }
    }
}
