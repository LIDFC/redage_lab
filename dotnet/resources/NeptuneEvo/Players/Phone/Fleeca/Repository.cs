using System;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.Accounts;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Handles;
using NeptuneEvo.MoneySystem;
using NeptuneEvo.Organizations.Player;
using NeptuneEvo.Players.Phone.Messages.Models;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Players.Phone.Fleeca
{
    /// <summary>
    /// Приложение Fleeca в телефоне: всё, что делает банкомат, кроме наличных — перевод на счёт,
    /// оплата налогов дома и бизнесов с карты, пополнение счёта организации, история операций (SMS банка).
    /// </summary>
    public static class Repository
    {
        public static string BuildJson(ExtPlayer player)
        {
            var characterData = player.GetCharacterData();
            var accountData = player.GetAccountData();
            if (characterData == null || accountData == null)
                return "null";

            object house = null;
            var houseData = Houses.HouseManager.GetHouse(player, true);
            if (houseData != null)
                house = new
                {
                    id = houseData.ID,
                    balance = MoneySystem.Bank.GetBalance(houseData.BankID),
                    max = BankOps.HouseTaxMax(houseData, accountData.VipLvl),
                };

            var businesses = characterData.BizIDs
                .Select((bizId, index) => (bizId, index))
                .Where(b => BusinessManager.BizList.ContainsKey(b.bizId))
                .Select(b =>
                {
                    var biz = BusinessManager.BizList[b.bizId];
                    return new
                    {
                        index = b.index,
                        id = biz.ID,
                        name = $"{BusinessManager.BusinessTypeNames[biz.Type]} №{biz.ID}",
                        balance = MoneySystem.Bank.GetBalance(biz.BankID),
                        max = BankOps.BizTaxMax(biz, accountData.VipLvl),
                    };
                });

            var organizationData = player.GetOrganizationData();
            return JsonConvert.SerializeObject(new
            {
                account = characterData.Bank,
                holder = player.Name.Replace('_', ' '),
                balance = MoneySystem.Bank.GetBalance(characterData.Bank),
                cash = characterData.Money,
                days = BankOps.TaxDays(accountData.VipLvl),
                house,
                businesses,
                org = organizationData != null && organizationData.Status ? new { name = organizationData.Name, money = organizationData.Money } : null,
                orgMax = BankOps.OrgDepositMax,
            });
        }

        public static void Load(ExtPlayer player)
        {
            if (!player.IsCharacterData())
                return;
            Trigger.ClientEvent(player, "client.phone.bank.data", BuildJson(player));
        }

        public static void Action(ExtPlayer player, string action, int arg1, int arg2)
        {
            var characterData = player.GetCharacterData();
            var accountData = player.GetAccountData();
            if (characterData == null || accountData == null)
                return;

            string error = null;
            string success = null;
            switch (action)
            {
                case "transfer":
                    error = BankOps.PrepareTransfer(player, arg1, arg2);
                    if (error == null)
                        success = "Подтвердите перевод";
                    break;
                case "house":
                    {
                        var house = Houses.HouseManager.GetHouse(player, true);
                        if (house == null)
                        {
                            error = "У вас нет дома";
                            break;
                        }
                        error = BankOps.PayTaxFromCard(player, house.BankID, BankOps.HouseTaxMax(house, accountData.VipLvl), arg1, "phoneHouse", out var paid);
                        if (error == null)
                        {
                            success = $"Налог за дом оплачен: ${MoneySystem.Wallet.Format(paid)}";
                            Messages.Repository.AddSystemMessage(player, (int)DefaultNumber.Bank,
                                $"Оплата налога за дом #{house.ID}: ${MoneySystem.Wallet.Format(paid)} с карты. На налоговом счёте ${MoneySystem.Wallet.Format(MoneySystem.Bank.GetBalance(house.BankID))}", DateTime.Now);
                        }
                    }
                    break;
                case "biz":
                    {
                        if (arg1 < 0 || arg1 >= characterData.BizIDs.Count || !BusinessManager.BizList.TryGetValue(characterData.BizIDs[arg1], out var biz))
                        {
                            error = "Бизнес не найден";
                            break;
                        }
                        error = BankOps.PayTaxFromCard(player, biz.BankID, BankOps.BizTaxMax(biz, accountData.VipLvl), arg2, "phoneBiz", out var paid);
                        if (error == null)
                        {
                            success = $"Налог за бизнес оплачен: ${MoneySystem.Wallet.Format(paid)}";
                            Messages.Repository.AddSystemMessage(player, (int)DefaultNumber.Bank,
                                $"Оплата налога за {BusinessManager.BusinessTypeNames[biz.Type]} №{biz.ID}: ${MoneySystem.Wallet.Format(paid)} с карты. На налоговом счёте ${MoneySystem.Wallet.Format(MoneySystem.Bank.GetBalance(biz.BankID))}", DateTime.Now);
                        }
                    }
                    break;
                case "org":
                    error = BankOps.OrgDeposit(player, arg1, true);
                    if (error == null)
                        success = $"Счёт организации пополнен на ${MoneySystem.Wallet.Format(Math.Abs(arg1))}";
                    break;
                default:
                    return;
            }

            Trigger.ClientEvent(player, "client.phone.bank.result", error == null, error ?? success, BuildJson(player));
        }

        /// <summary>История: последние SMS банка (4386) — асинхронно из БД сообщений.</summary>
        public static void History(ExtPlayer player)
        {
            Trigger.SetTask(async () =>
            {
                var messages = await Messages.Repository.getMessage(player, (int)DefaultNumber.Bank);
                var list = messages.AsEnumerable().Reverse().Take(40).Select(m => new { text = m[1], date = m[2] });
                var json = JsonConvert.SerializeObject(list);
                NAPI.Task.Run(() =>
                {
                    if (player.IsCharacterData())
                        Trigger.ClientEvent(player, "client.phone.bank.history", json);
                });
            });
        }
    }
}
