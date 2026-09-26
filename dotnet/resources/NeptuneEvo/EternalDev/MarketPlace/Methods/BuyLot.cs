using Database;
using GTANetworkAPI;
using LinqToDB;
using Localization;
using NeptuneEvo.Accounts.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Character.Models;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Core;
using NeptuneEvo.EternalDev.MarketPlace.Classes;
using NeptuneEvo.EternalDev.MarketPlace.Enums;
using NeptuneEvo.EternalDev.MarketPlace.Extensions;
using NeptuneEvo.Handles;
using NeptuneEvo.Houses;
using NeptuneEvo.Players.Phone.Auction.Models;
using Newtonsoft.Json;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeptuneEvo.EternalDev.MarketPlace.Methods
{
    public class BuyLot : Script
    {
        [RemoteEvent("server.marketPlace.buy")]
        public static void On(ExtPlayer player, int id, int count, string paymentType)
        {
            if (!player.GetMarketPlaceProfile(out var profile))
                return;

            var marketItem = Manager.GetMarketItem(id);
            if (marketItem is null)
                return;

            if (marketItem.Owner == player.GetUUID() || !IsPossibleToGet(player, marketItem.Type))
                return;

            if (paymentType == "Wallet" ? !MoneySystem.Wallet.Change(player, -marketItem.Cost)
              : paymentType == "Card" ? !MoneySystem.Bank.Change(player.CharacterData.Bank, -marketItem.Cost) : true)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"У вас недостаточно средств!", 3000);
                return;
            }

            SetToEstate(player.GetUUID(), marketItem.Type, marketItem.Data, count,
                moveStorage: true);

            Manager.AddMoney(marketItem.Owner, marketItem.Cost, $"Лот #{marketItem.Id} был куплен и вы получили {MoneySystem.Wallet.Format(marketItem.Cost)}$");

            bool deleteLot = true;
            if (marketItem.Type == LotType.Clothes || marketItem.Type == LotType.Item)
            {
                var itemData = marketItem.Data.Split("@@");
                var itemCount = Convert.ToInt32(itemData[1]);
                if (itemCount > count)
                {
                    itemData[1] = (itemCount - count).ToString();
                    deleteLot = false;
                }

                marketItem.Data = string.Join("@@", itemData);
                marketItem.Save();
            }

            if (deleteLot)
                Manager.DeleteLot(marketItem);

            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы успешно купили лот #{marketItem.Id} за {MoneySystem.Wallet.Format(marketItem.Cost)}$", 3000);
            Trigger.ClientEvent(player, "client.marketPlace.setMarketPage", "storage");
        }                                                                                                

        public static bool IsPossibleToGet(ExtPlayer player, LotType lotType)
        {
            switch(lotType)
            {
                case LotType.Vehicle:
                    var vehiclesCount = VehicleManager.GetVehiclesCarCountToPlayer(player.Name);
                    if (vehiclesCount >= GarageManager.MaxGarageCars)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"У Вас максимальное кол-во машин", 3000);
                        return false;
                    }
                    break;
                case LotType.House:
                    if (HouseManager.Houses.Count(h => h.Owner == player.Name) >= 1)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Вы не можете купить больше одной недвижимости", 3000);
                        return false;
                    }
                    break;
                case LotType.Business:
                    if (player.CharacterData.BizIDs.Count >= Group.GroupMaxBusinesses[player.AccountData.VipLvl])
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.CantGetMoreThanBiz, Group.GroupMaxBusinesses[player.AccountData.VipLvl]), 3000);
                        return false;
                    }
                    break;
            }
            return true;
        }

        [RemoteEvent("server.marketPlace.unload")]
        public static void Unload(ExtPlayer player, int lotId)
        {
            if (!player.GetMarketPlaceProfile(out var userProfile))
                return;

            var storageItem = userProfile.Storage.Find(x => x.Id == lotId);
            if (storageItem is null)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Предмет не найден", 3000);
                return;
            }

            if (!IsPossibleToGet(player, storageItem.Type))
                return;

            SetToEstate(player.GetUUID(), storageItem.Type, storageItem.Data, 1, false);
            userProfile.RemoveItem(storageItem);

            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы успешно забрали предмет со склада!", 3000);
        }

        public static void SetToEstate(int uuid, LotType lotType, string data, int count, bool moveStorage = true)
        {
            if (!Manager.GetUserProfile(uuid, out var userProfile))
                return;

            string name = Main.PlayerNames[uuid];

            switch(lotType)
            {
                case LotType.House:
                    var house = HouseManager.Houses.Find(x => x.ID == Convert.ToInt32(data));
                    if (house is null) return;

                    house.ClearOwner();

                    if (!moveStorage)
                    {
                        house.SetOwner(name);
                    }
                    break;
                case LotType.Business:
                    if (!BusinessManager.BizList.TryGetValue(Convert.ToInt32(data), out var business))
                        return;

                    void UpdateBizIds(int targetUuid, bool isRemove = true)
                    {
                        var target = Main.GetPlayerByUUID(targetUuid);
                        if (target != null)
                        {
                            if (isRemove)
                            {
                                if (target.CharacterData.BizIDs.Contains(business.ID))
                                    target.CharacterData.BizIDs.Remove(business.ID);
                            }
                            else
                                target.CharacterData.BizIDs.Add(business.ID);

                            Character.Save.Repository.SaveBiz(target);
                        }
                        else
                        {
                            Trigger.SetTask(async () =>
                            {
                                await using var db = new ServerBD("MainDB");//В отдельном потоке

                                var character = await db.Characters
                                    .Select(v => new { v.Uuid, v.Biz })
                                    .Where(v => v.Uuid == targetUuid)
                                    .FirstOrDefaultAsync();

                                if (character != null)
                                {
                                    var bizIDs = JsonConvert.DeserializeObject<List<int>>(character.Biz);

                                    if (isRemove)
                                    {
                                        if (bizIDs.Contains(business.ID))
                                            bizIDs.Remove(business.ID);
                                    }
                                    else
                                        bizIDs.Add(business.ID);

                                    await db.Characters
                                        .Where(v => v.Uuid == uuid)
                                        .Set(v => v.Biz, JsonConvert.SerializeObject(bizIDs))
                                        .UpdateAsync();

                                }
                            });
                        }
                    }

                    if (Main.PlayerUUIDs.TryGetValue(business.Owner, out var businessOwnerUuuid))
                        UpdateBizIds(businessOwnerUuuid, true);

                    business.ClearOwner();
                    if (!moveStorage)
                    {
                        business.SetOwner(name);
                        UpdateBizIds(uuid, false);
                    }
                    break;
                case LotType.Vehicle:
                    var vehicleData = VehicleManager.GetVehicleToAutoId(Convert.ToInt32(data));
                    if (vehicleData is null) return;

                    vehicleData.Holder = string.Empty;
                    if (!moveStorage)
                        vehicleData.Holder = name;

                    VehicleManager.SaveHolder(vehicleData.Number);
                    break;
                case LotType.Item:
                case LotType.Clothes:
                    var itemData = data.Split("@@");
                    Chars.Repository.AddNewItem(null, $"marketStorage_{uuid}", "marketStorage", (ItemId)Convert.ToInt32(itemData[0]), count, itemData[2], MaxSlots: Manager.Config.MaxSlotsInStorage);
                    break;
            }

            if (moveStorage && lotType != LotType.Item && lotType != LotType.Clothes)
            {
                var storageItem = new StorageItem()
                {
                    Type = lotType,
                    Data = data,
                };

                userProfile.AddItem(storageItem);
            }
        }
    }
}
