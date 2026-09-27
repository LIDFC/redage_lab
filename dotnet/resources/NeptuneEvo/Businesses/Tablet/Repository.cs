using System;
using System.Collections.Generic;
using System.Linq;
using Database;
using GTANetworkAPI;
using LinqToDB;
using Localization;
using NeptuneEvo.Character;
using NeptuneEvo.Accounts;
using NeptuneEvo.Players;
using NeptuneEvo.Core;
using NeptuneEvo.Handles;
using NeptuneEvo.MoneySystem;
using Newtonsoft.Json;
using Redage.SDK;
using PhoneBusiness = NeptuneEvo.Players.Phone.Property.Businesses.Repository;

namespace NeptuneEvo.Businesses.Tablet
{
    /// <summary>
    /// Управление бизнесом с планшета (CEF hudevo/tablet/apps/business). Телефон бизнесом больше не управляет.
    /// Из CEF приходят только id и введённые числа — владелец, цены, лимиты и деньги проверяются здесь.
    /// </summary>
    public static class Repository
    {
        private static readonly nLog Log = new nLog("Businesses.Tablet");

        public const int MaxTransactions = 200;

        /// <summary>Выбранный бизнес игрока, если он действительно его владелец.</summary>
        public static Business GetOwned(ExtPlayer player, int bizId = -1)
        {
            var sessionData = player.GetSessionData();
            var characterData = player.GetCharacterData();
            if (sessionData == null || characterData == null)
                return null;
            if (bizId == -1)
                bizId = sessionData.SelectData.SelectedBiz;
            if (!characterData.BizIDs.Contains(bizId) || !BusinessManager.BizList.TryGetValue(bizId, out var biz))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.YouHaveNoBusiness), 3000);
                return null;
            }
            sessionData.SelectData.SelectedBiz = bizId;
            return biz;
        }

        public static int TaxPerHour(Business biz) => Convert.ToInt32(biz.SellPrice / 100 * biz.Tax);

        /// <summary>Максимум на налоговом счёте — как в банкомате: неделя налогов, больше для VIP.</summary>
        public static int TaxMax(ExtPlayer player, Business biz)
        {
            var days = 7;
            switch (player.GetAccountData()?.VipLvl ?? 0)
            {
                case 2: days = 14; break;
                case 3: days = 21; break;
                case 4:
                case 5: days = 28; break;
            }
            return Convert.ToInt32(biz.SellPrice / 100 * biz.Tax * 24 * days);
        }

        public static string Title(Business biz) =>
            $"{(biz.Type >= 0 && biz.Type < BusinessManager.BusinessTypeNames.Length ? BusinessManager.BusinessTypeNames[biz.Type] : "Бизнес")} №{biz.ID}";

        public static void SendData(ExtPlayer player, Business biz)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null)
                    return;

                var products = new List<object>();
                int whCount = 0, whMax = 0, orderAllPrice = 0;
                foreach (var product in biz.Products)
                {
                    if (!BusinessManager.BusProductsData.TryGetValue(product.Name, out var data))
                        continue;
                    var unitPrice = data.OtherPrice > 0 ? data.OtherPrice : data.Price;
                    var order = biz.Orders.FirstOrDefault(o => o.Name == product.Name);
                    products.Add(new
                    {
                        name = product.Name,
                        count = product.Lefts,
                        max = data.MaxCount,
                        price = product.Price,
                        defaultPrice = data.Price,
                        unitPrice,
                        minPrice = PhoneBusiness.GetPriceMin(product.Name, biz.Type, data.Price),
                        maxPrice = PhoneBusiness.GetPriceMax(product.Name, biz.Type, data.Price),
                        itemId = data.ItemId,
                        productType = data.Type,
                        ordered = product.Ordered,
                        orderUid = order?.UID ?? 0,
                        orderAmount = order?.Amount ?? 0,
                        minOrder = PhoneBusiness.CountMinOrder(biz.Type),
                        fixedPrice = product.Name == "Лотерейный билет" || product.Name == "Расходники",
                    });
                    whCount += product.Lefts;
                    whMax += data.MaxCount;
                    if (!product.Ordered && product.Lefts < data.MaxCount)
                        orderAllPrice += (biz.Type >= 2 && biz.Type <= 5 ? 3 : data.MaxCount - product.Lefts) * unitPrice;
                }

                var taxBalance = Bank.GetBalance(biz.BankID);
                var taxHour = TaxPerHour(biz);
                var paidUntil = taxHour > 0 && taxBalance > 0
                    ? DateTimeOffset.Now.AddHours((double)taxBalance / taxHour).ToUnixTimeMilliseconds()
                    : 0;

                var data2 = new
                {
                    id = biz.ID,
                    title = Title(biz),
                    type = biz.Type,
                    cash = biz.Cash,
                    taxBalance,
                    taxHour,
                    taxMax = TaxMax(player, biz),
                    paidUntil,
                    bank = Bank.GetBalance(characterData.Bank),
                    sellPrice = Wallet.GetPriceToVip(player, biz.SellPrice),
                    whCount,
                    whMax,
                    orderAllPrice,
                    products,
                    businesses = characterData.BizIDs
                        .Where(id => BusinessManager.BizList.ContainsKey(id))
                        .Select(id => new { id, title = Title(BusinessManager.BizList[id]) }),
                };
                Trigger.ClientEvent(player, "client.tablet.business.data", JsonConvert.SerializeObject(data2));
            }
            catch (Exception e)
            {
                Log.Write($"SendData Exception: {e}");
            }
        }

        /// <summary>Статистика и транзакции за период (1/7/30 дней) — в отдельном потоке.</summary>
        public static async void SendHistory(ExtPlayer player, int bizId, int days)
        {
            try
            {
                days = Math.Clamp(days, 1, 30);
                var from = days == 1 ? DateTime.Today : DateTime.Now.AddDays(-days);

                await using var db = new ServerBD("MainDB");
                var rows = await db.Businesshistory
                    .Where(bh => bh.Bizid == bizId && bh.Date >= from)
                    .OrderByDescending(bh => bh.Autoid)
                    .Select(bh => new { bh.Item, bh.Uuid, bh.Date, bh.Price, bh.Cost })
                    .ToListAsync();

                string Name(int uuid) => Main.PlayerNames.TryGetValue(uuid, out var name) ? name : $"#{uuid}";

                var result = new
                {
                    days,
                    revenue = rows.Sum(r => (long)r.Price),
                    profit = rows.Sum(r => (long)(r.Price - r.Cost)),
                    sales = rows.Count,
                    average = rows.Count == 0 ? 0 : rows.Sum(r => (long)r.Price) / rows.Count,
                    clients = rows.GroupBy(r => r.Uuid)
                        .Select(g => new { name = Name(g.Key), count = g.Count(), sum = g.Sum(r => (long)r.Price) })
                        .OrderByDescending(c => c.sum).Take(10),
                    products = rows.GroupBy(r => r.Item)
                        .Select(g => new { name = g.Key, count = g.Count(), sum = g.Sum(r => (long)r.Price) })
                        .OrderByDescending(p => p.sum).Take(10),
                    transactions = rows.Take(MaxTransactions)
                        .Select(r => new
                        {
                            item = r.Item,
                            buyer = Name(r.Uuid),
                            date = new DateTimeOffset(r.Date).ToUnixTimeMilliseconds(),
                            cost = r.Cost,
                            price = r.Price,
                        }),
                };
                var json = JsonConvert.SerializeObject(result);
                NAPI.Task.Run(() =>
                {
                    if (player.IsCharacterData())
                        Trigger.ClientEvent(player, "client.tablet.business.history", json);
                });
            }
            catch (Exception e)
            {
                Log.Write($"SendHistory Exception: {e}");
            }
        }

        public static void Withdraw(ExtPlayer player, Business biz)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null)
                return;
            if (biz.Cash <= 0)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Касса пуста", 3000);
                return;
            }
            if (characterData.Bank == 0 || !Bank.Accounts.ContainsKey(characterData.Bank))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.NoBanks), 3000);
                return;
            }
            var amount = biz.Cash;
            biz.Cash = 0;
            biz.IsSave = true;
            Bank.Change(characterData.Bank, amount, false);
            GameLog.Money($"biz({biz.ID})", $"bank({characterData.Bank})", amount, "bizCashWithdraw");
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Из кассы выведено ${Wallet.Format(amount)} на банковский счёт", 4000);
        }

        public static void PayTax(ExtPlayer player, Business biz, int amount)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null)
                return;
            amount = Math.Abs(amount);
            var room = TaxMax(player, biz) - (int)Bank.GetBalance(biz.BankID);
            if (room <= 0)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Налоговый счёт уже пополнен до максимума", 3000);
                return;
            }
            amount = Math.Min(amount, room);
            if (amount <= 0)
                return;
            if (!Bank.Change(characterData.Bank, -amount))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.NoBankMoney), 3000);
                return;
            }
            Bank.Change(biz.BankID, amount, false);
            GameLog.Money($"bank({characterData.Bank})", $"bank({biz.BankID})", amount, "bizTaxPay");
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Налоговый счёт пополнен на ${Wallet.Format(amount)}", 3000);
        }
    }
}
