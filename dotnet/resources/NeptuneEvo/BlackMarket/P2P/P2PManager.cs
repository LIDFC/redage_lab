using NeptuneEvo.BlackMarket.Audit;
using NeptuneEvo.BlackMarket.Config;
using NeptuneEvo.BlackMarket.Crypto;
using NeptuneEvo.BlackMarket.History;
using NeptuneEvo.BlackMarket.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Handles;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace NeptuneEvo.BlackMarket.P2P
{
    /// <summary>
    /// P2P: игрок продаёт BTC за наличные $. При создании заявки BTC блокируются в кошельке (reserved) —
    /// их нельзя потратить, перевести или выставить второй раз. Покупатель берёт любую часть по цене за 1 BTC,
    /// комиссия (p2pFeePercent) удерживается в BTC с покупателя и уходит на системный кошелёк.
    /// </summary>
    public static class P2PManager
    {
        private static readonly Dictionary<int, P2POffer> All = new Dictionary<int, P2POffer>();
        private static int _lastId = 0;
        private const decimal MaxPricePerBtc = 1_000_000m;

        public static void Load()
        {
            lock (BlackMarketCore.Sync)
            {
                All.Clear();
                using (var data = BlackMarketRepository.Read("SELECT * FROM `crypto_p2p`"))
                {
                    if (data != null)
                        foreach (DataRow row in data.Rows)
                        {
                            var offer = new P2POffer
                            {
                                Id = Convert.ToInt32(row["id"]),
                                OwnerUuid = Convert.ToInt32(row["owner_uuid"]),
                                AmountLeft = Convert.ToInt64(row["amount_left"]),
                                PricePerBtc = Convert.ToDecimal(row["price_per_btc"]),
                                Created = Convert.ToDateTime(row["created"]),
                                Ends = Convert.ToDateTime(row["ends"]),
                            };
                            _lastId = Math.Max(_lastId, offer.Id);
                            All[offer.Id] = offer;
                        }
                }

                // Резерв в кошельках должен совпадать с суммой открытых заявок
                foreach (var group in All.Values.GroupBy(o => o.OwnerUuid))
                {
                    var wallet = CryptoWallets.Get(group.Key);
                    var expected = group.Sum(o => o.AmountLeft);
                    if (wallet.Reserved != expected)
                    {
                        BlackMarketCore.Log.Write($"P2P: резерв {group.Key} = {wallet.Reserved}, заявок на {expected} — выравниваю");
                        if (wallet.Reserved > expected) CryptoWallets.Release(group.Key, wallet.Reserved - expected);
                        else CryptoWallets.Reserve(group.Key, expected - wallet.Reserved);
                    }
                }

                foreach (var offer in All.Values.Where(o => o.Ends <= DateTime.Now || o.AmountLeft <= 0).ToList())
                    Close(offer, "p2p_expire", 0);
            }
        }

        public static List<P2POffer> Snapshot()
        {
            lock (BlackMarketCore.Sync)
                return All.Values.Select(o => new P2POffer
                {
                    Id = o.Id, OwnerUuid = o.OwnerUuid, AmountLeft = o.AmountLeft,
                    PricePerBtc = o.PricePerBtc, Created = o.Created, Ends = o.Ends,
                }).ToList();
        }

        public static OpResult Create(ExtPlayer player, long amount, decimal pricePerBtc, int hours)
        {
            var config = BlackMarketConfig.Current;
            if (amount < 1)
                return OpResult.Fail("Укажите количество BTC");
            if (pricePerBtc <= 0 || pricePerBtc > MaxPricePerBtc)
                return OpResult.Fail("Некорректная цена за 1 BTC");
            if (hours < config.LotMinHours || hours > config.LotMaxHours)
                return OpResult.Fail($"Срок заявки: от {config.LotMinHours} до {config.LotMaxHours} ч");
            if (Cost(amount, pricePerBtc) > int.MaxValue)
                return OpResult.Fail("Слишком большая сумма заявки");

            var uuid = player.GetUUID();
            lock (BlackMarketCore.Sync)
            {
                if (!CryptoWallets.Reserve(uuid, amount))
                    return OpResult.Fail("Не хватает свободных BTC");

                var offer = new P2POffer
                {
                    Id = ++_lastId,
                    OwnerUuid = uuid,
                    AmountLeft = amount,
                    PricePerBtc = decimal.Round(pricePerBtc, 4),
                    Created = DateTime.Now,
                    Ends = DateTime.Now.AddHours(hours),
                };
                All[offer.Id] = offer;
                BlackMarketRepository.Enqueue(
                    "INSERT INTO `crypto_p2p` (`id`, `owner_uuid`, `amount_left`, `price_per_btc`, `created`, `ends`) VALUES (@id, @owner, @amount, @price, @created, @ends)",
                    ("@id", offer.Id), ("@owner", uuid), ("@amount", amount), ("@price", offer.PricePerBtc),
                    ("@created", offer.Created), ("@ends", offer.Ends));
                AuditLog.Write("p2p_create", uuid, amount: amount, source: Source.Personal, details: new { offer = offer.Id, price = offer.PricePerBtc, hours });
                return OpResult.Success($"Заявка создана: {BlackMarketCore.Btc(amount)} по {offer.PricePerBtc:0.####}$ за 1 BTC");
            }
        }

        public static OpResult Cancel(ExtPlayer player, int offerId)
        {
            lock (BlackMarketCore.Sync)
            {
                if (!All.TryGetValue(offerId, out var offer) || offer.OwnerUuid != player.GetUUID())
                    return OpResult.Fail("Заявка не найдена");
                Close(offer, "p2p_cancel", offer.OwnerUuid);
                return OpResult.Success("Заявка снята, BTC разблокированы");
            }
        }

        public static bool AdminDelete(int offerId, int adminUuid)
        {
            lock (BlackMarketCore.Sync)
            {
                if (!All.TryGetValue(offerId, out var offer))
                    return false;
                Close(offer, "p2p_admin_delete", adminUuid);
                return true;
            }
        }

        public static OpResult Buy(ExtPlayer player, int offerId, long amount)
        {
            var config = BlackMarketConfig.Current;
            var characterData = player.GetCharacterData();
            if (characterData == null)
                return OpResult.Fail("Ошибка персонажа");
            var buyerUuid = characterData.UUID;

            lock (BlackMarketCore.Sync)
            {
                if (!All.TryGetValue(offerId, out var offer))
                    return OpResult.Fail("Заявка уже закрыта");
                if (offer.OwnerUuid == buyerUuid)
                    return OpResult.Fail("Нельзя купить свою заявку");
                if (amount < 1 || amount > offer.AmountLeft)
                    return OpResult.Fail($"Доступно {BlackMarketCore.Btc(offer.AmountLeft)}");

                var cost = Cost(amount, offer.PricePerBtc);
                var fee = (long)Math.Ceiling(amount * config.P2PFeePercent / 100m);
                var received = amount - fee;
                if (received <= 0)
                    return OpResult.Fail("Слишком маленькая сумма: всё уйдёт в комиссию");
                if (cost > int.MaxValue || characterData.Money < cost)
                    return OpResult.Fail($"Нужно {MoneySystem.Wallet.Format(cost)}$ наличными");

                if (!MoneySystem.Wallet.Change(player, -(int)cost))
                    return OpResult.Fail($"Нужно {MoneySystem.Wallet.Format(cost)}$ наличными");
                if (!CryptoWallets.ConsumeReserved(offer.OwnerUuid, amount))
                {
                    MoneySystem.Wallet.Change(player, (int)cost);
                    BlackMarketCore.Log.Write($"P2P {offer.Id}: резерв продавца меньше {amount}, покупка отменена");
                    return OpResult.Fail("Заявка недоступна");
                }
                CryptoWallets.ChangePersonal(buyerUuid, received);
                CryptoWallets.ChangeSystem(fee);
                EternalDev.MarketPlace.Manager.AddMoney(offer.OwnerUuid, (int)cost,
                    $"Чёрный рынок: P2P-заявка исполнена, +{MoneySystem.Wallet.Format(cost)}$");

                offer.AmountLeft -= amount;
                if (offer.AmountLeft <= 0)
                {
                    All.Remove(offer.Id);
                    BlackMarketRepository.Enqueue("DELETE FROM `crypto_p2p` WHERE `id`=@id", ("@id", offer.Id));
                }
                else
                    BlackMarketRepository.Enqueue("UPDATE `crypto_p2p` SET `amount_left`=@amount WHERE `id`=@id",
                        ("@amount", offer.AmountLeft), ("@id", offer.Id));

                GameLog.Money($"player({buyerUuid})", $"player({offer.OwnerUuid})", cost, "blackmarketP2P");
                HistoryLog.Add(buyerUuid, "p2p", "P2P: покупка BTC", received);
                HistoryLog.Add(buyerUuid, "p2p", "P2P", -cost, HistoryLog.Usd);
                HistoryLog.Add(offer.OwnerUuid, "p2p", "P2P: продажа BTC", -amount);
                HistoryLog.Add(offer.OwnerUuid, "p2p", "P2P", cost, HistoryLog.Usd);
                AuditLog.Write("p2p_buy", buyerUuid, offer.OwnerUuid, amount: amount, source: Source.Cash, fee: fee,
                    details: new { offer = offer.Id, usd = cost, price = offer.PricePerBtc });

                return OpResult.Success($"Получено {BlackMarketCore.Btc(received)} (комиссия {BlackMarketCore.Btc(fee)}), списано {MoneySystem.Wallet.Format(cost)}$");
            }
        }

        public static void Tick()
        {
            lock (BlackMarketCore.Sync)
            {
                var now = DateTime.Now;
                foreach (var offer in All.Values.Where(o => o.Ends <= now).ToList())
                    Close(offer, "p2p_expire", 0);
            }
        }

        private static void Close(P2POffer offer, string action, int actorUuid)
        {
            All.Remove(offer.Id);
            CryptoWallets.Release(offer.OwnerUuid, offer.AmountLeft);
            BlackMarketRepository.Enqueue("DELETE FROM `crypto_p2p` WHERE `id`=@id", ("@id", offer.Id));
            AuditLog.Write(action, actorUuid, offer.OwnerUuid, amount: offer.AmountLeft, details: new { offer = offer.Id });
        }

        /// <summary>Стоимость в $ с округлением вверх.</summary>
        public static long Cost(long amount, decimal pricePerBtc) => (long)Math.Ceiling(amount * pricePerBtc);
    }
}
