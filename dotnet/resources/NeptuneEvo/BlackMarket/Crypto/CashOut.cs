using NeptuneEvo.BlackMarket.Audit;
using NeptuneEvo.BlackMarket.Config;
using NeptuneEvo.BlackMarket.History;
using NeptuneEvo.BlackMarket.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Character.Models;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Core;
using NeptuneEvo.Fractions;
using NeptuneEvo.Handles;
using Redage.SDK;
using System;

namespace NeptuneEvo.BlackMarket.Crypto
{
    /// <summary>
    /// Обнал у NPC «Мавр» (точка cashoutPoint из конфига):
    ///  - грязные $ из сумки после ограбления (BagWithMoney, сумма в Data) → BTC по курсу за вычетом launderFeePercent;
    ///  - BTC → наличные по курсу за вычетом cashoutFeePercent, с шансом cashoutWantedChance получить звезду розыска.
    /// Комиссии в BTC уходят на системный кошелёк. Старый отмыв сумки за наличные у Мавра остаётся как был.
    /// </summary>
    public static class CashOut
    {
        private const int BagSlot = 8;
        private static readonly Random Random = new Random();

        public static bool AtPoint(ExtPlayer player)
        {
            var config = BlackMarketConfig.Current;
            return player.Dimension == 0 && player.Position.DistanceTo(config.CashoutPoint) <= config.CashoutRadius;
        }

        /// <summary>Сумма грязных денег в сумке на спине (0 — сумки нет).</summary>
        public static long BagAmount(ExtPlayer player)
        {
            var bag = Chars.Repository.GetItemData(player, "accessories", BagSlot);
            if (bag == null || bag.ItemId != ItemId.BagWithMoney)
                return 0;
            return long.TryParse(bag.Data, out var amount) && amount > 0 ? amount : 0;
        }

        /// <summary>Грязные $ → BTC.</summary>
        public static OpResult Launder(ExtPlayer player)
        {
            var config = BlackMarketConfig.Current;
            if (!AtPoint(player))
                return OpResult.Fail("Обнал работает только у Мавра");
            if (config.ExchangeUsdPerBtc <= 0)
                return OpResult.Fail("Сейчас обнал закрыт");

            var uuid = player.GetUUID();
            lock (BlackMarketCore.Sync)
            {
                var dirty = BagAmount(player);
                if (dirty <= 0)
                    return OpResult.Fail("Нужна сумка с деньгами на спине");

                var gross = (long)Math.Floor(dirty / config.ExchangeUsdPerBtc);
                var fee = (long)Math.Ceiling(gross * config.LaunderFeePercent / 100m);
                var received = gross - fee;
                if (received <= 0)
                    return OpResult.Fail("Слишком маленькая сумма");

                Chars.Repository.RemoveIndex(player, "accessories", BagSlot);
                CryptoWallets.ChangePersonal(uuid, received);
                CryptoWallets.ChangeSystem(fee);

                HistoryLog.Add(uuid, "launder", "Обнал: грязные деньги", received);
                AuditLog.Write("launder", uuid, amount: received, source: Source.Cash, fee: fee,
                    details: new { dirtyUsd = dirty, rate = config.ExchangeUsdPerBtc });
                return OpResult.Success($"Сумка ушла в крипту: +{BlackMarketCore.Btc(received)} (комиссия {BlackMarketCore.Btc(fee)})");
            }
        }

        /// <summary>BTC → наличные, с риском розыска.</summary>
        public static OpResult Cashout(ExtPlayer player, long btc)
        {
            var config = BlackMarketConfig.Current;
            if (!AtPoint(player))
                return OpResult.Fail("Обналичить можно только у Мавра");
            if (config.ExchangeUsdPerBtc <= 0)
                return OpResult.Fail("Сейчас обнал закрыт");
            if (btc < 1)
                return OpResult.Fail("Укажите сумму BTC");

            var characterData = player.GetCharacterData();
            if (characterData == null)
                return OpResult.Fail("Ошибка персонажа");

            var fee = (long)Math.Ceiling(btc * config.CashoutFeePercent / 100m);
            var usd = (long)Math.Floor((btc - fee) * config.ExchangeUsdPerBtc);
            if (usd <= 0)
                return OpResult.Fail("Слишком маленькая сумма");
            if (usd > int.MaxValue || characterData.Money + usd > int.MaxValue)
                return OpResult.Fail("Слишком большая сумма за один раз");

            var uuid = characterData.UUID;
            lock (BlackMarketCore.Sync)
            {
                if (!CryptoWallets.ChangePersonal(uuid, -btc))
                    return OpResult.Fail("Не хватает свободных BTC");
                CryptoWallets.ChangeSystem(fee);
                MoneySystem.Wallet.Change(player, (int)usd);
                GameLog.Money("server", $"player({uuid})", usd, "blackmarketCashout");

                var wanted = Random.Next(100) < config.CashoutWantedChance;
                if (wanted)
                {
                    var level = Math.Min(6, (characterData.WantedLVL?.Level ?? 0) + 1);
                    Police.setPlayerWantedLevel(player, new WantedLevel(level, "Полиция", DateTime.Now, "Обналичивание"));
                }

                HistoryLog.Add(uuid, "cashout", "Обнал", -btc);
                HistoryLog.Add(uuid, "cashout", "Обнал", usd, HistoryLog.Usd);
                AuditLog.Write("cashout", uuid, amount: btc, source: Source.Personal, fee: fee,
                    result: wanted ? "wanted" : "ok", details: new { usd, rate = config.ExchangeUsdPerBtc });

                return OpResult.Success($"Обналичено {MoneySystem.Wallet.Format(usd)}$ (комиссия {BlackMarketCore.Btc(fee)})" +
                    (wanted ? ". Вас заметили — вы в розыске!" : ""));
            }
        }
    }
}
