using NeptuneEvo.BlackMarket.Audit;
using NeptuneEvo.BlackMarket.Config;
using NeptuneEvo.BlackMarket.Crypto;
using NeptuneEvo.BlackMarket.History;
using NeptuneEvo.BlackMarket.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Core;
using NeptuneEvo.Handles;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace NeptuneEvo.BlackMarket.Fence
{
    /// <summary>
    /// Скупщик краденого у Мавра: трава (Наркотики), краденая техника, украшения и детали угнанных машин.
    /// Цена = база × max(minFactor, 1 − насыщение). Каждая продажа поднимает насыщение (count / capacity),
    /// со временем рынок «забывает» (recoverPercentPerHour). Оплата наличными или в BTC с бонусом.
    /// Работает только лично у Мавра (точка cashoutPoint), VPN не нужен.
    /// </summary>
    public static class FenceManager
    {
        private class Demand
        {
            public double Saturation;
            public DateTime Updated;
        }

        private static readonly Dictionary<int, Demand> Demands = new Dictionary<int, Demand>();

        private static FenceConfig Cfg => BlackMarketConfig.Current.Fence;

        public static void Load()
        {
            var table = BlackMarketRepository.Read("SELECT * FROM `blackmarket_fence_demand`");
            if (table == null)
                return;
            foreach (DataRow row in table.Rows)
            {
                Demands[Convert.ToInt32(row["item"])] = new Demand
                {
                    Saturation = Convert.ToDouble(row["saturation"]),
                    Updated = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(row["updated"])).LocalDateTime,
                };
            }
        }

        /// <summary>Текущее насыщение с учётом восстановления спроса.</summary>
        private static double Saturation(int itemId)
        {
            if (!Demands.TryGetValue(itemId, out var demand))
                return 0;
            var hours = (DateTime.Now - demand.Updated).TotalHours;
            return Math.Max(0, demand.Saturation - hours * Cfg.RecoverPercentPerHour / 100.0);
        }

        /// <summary>Цена штуки. factor — коэффициент игрока (Crime.CrimeCore.PayoutFactor: банда 1.0, криминальная организация выше).</summary>
        private static int UnitPrice(FenceItem item, double saturation, double factor) =>
            (int)Math.Round(item.BasePrice * factor * Math.Max(Cfg.MinFactor, 1 - saturation));

        /// <summary>Сколько $ дадут за count штук (каждая следующая штука уже с новым насыщением).</summary>
        private static long Quote(FenceItem item, int count, double factor, out double saturationAfter)
        {
            var saturation = Saturation(item.ItemId);
            long total = 0;
            var step = 1.0 / Math.Max(1, item.Capacity);
            for (var i = 0; i < count; i++)
            {
                total += UnitPrice(item, saturation, factor);
                saturation = Math.Min(1, saturation + step);
            }
            saturationAfter = saturation;
            return total;
        }

        public static object View(ExtPlayer player)
        {
            var config = BlackMarketConfig.Current;
            var location = $"char_{player.GetUUID()}";
            var factor = Crime.CrimeCore.PayoutFactor(player);
            return new
            {
                payoutNote = Crime.CrimeCore.PayoutNote(player),
                btcBonus = Cfg.BtcBonusPercent,
                minFactor = Cfg.MinFactor,
                rate = config.ExchangeUsdPerBtc,
                items = Cfg.Items.Where(i => Chars.Repository.ItemsInfo.ContainsKey((ItemId)i.ItemId)).Select(i =>
                {
                    var saturation = Saturation(i.ItemId);
                    return new
                    {
                        itemId = i.ItemId,
                        name = Methods.Lots.ItemName((ItemId)i.ItemId),
                        have = Chars.Repository.getCountItem(location, (ItemId)i.ItemId, false),
                        price = UnitPrice(i, saturation, factor),
                        basePrice = i.BasePrice * factor,
                        demand = (int)Math.Round(Math.Max(Cfg.MinFactor, 1 - saturation) * 100),
                        step = 1.0 / Math.Max(1, i.Capacity),
                    };
                }).ToList(),
            };
        }

        public static OpResult Sell(ExtPlayer player, int itemId, int count, bool toBtc)
        {
            var config = BlackMarketConfig.Current;
            if (!CashOut.AtPoint(player))
                return OpResult.Fail("Краденое принимает только Мавр — приезжайте лично");
            var item = Cfg.Items.FirstOrDefault(i => i.ItemId == itemId);
            if (item == null || !Chars.Repository.ItemsInfo.ContainsKey((ItemId)itemId))
                return OpResult.Fail("Мавр такое не берёт");
            if (count < 1 || count > 1000)
                return OpResult.Fail("Укажите количество");
            if (toBtc && config.ExchangeUsdPerBtc <= 0)
                return OpResult.Fail("Крипту сейчас не выдают");

            var characterData = player.GetCharacterData();
            if (characterData == null)
                return OpResult.Fail("Ошибка персонажа");
            var uuid = characterData.UUID;
            var location = $"char_{uuid}";

            lock (BlackMarketCore.Sync)
            {
                if (Chars.Repository.getCountItem(location, (ItemId)itemId, false) < count)
                    return OpResult.Fail("У вас нет столько в инвентаре");

                var usd = Quote(item, count, Crime.CrimeCore.PayoutFactor(player), out var saturationAfter);
                if (usd <= 0)
                    return OpResult.Fail("Слишком мало");
                long btc = 0;
                if (toBtc)
                {
                    btc = (long)Math.Floor(usd * (100m + Cfg.BtcBonusPercent) / 100m / config.ExchangeUsdPerBtc);
                    if (btc <= 0)
                        return OpResult.Fail("Слишком мало для оплаты в BTC");
                }
                else if (characterData.Money + usd > int.MaxValue)
                    return OpResult.Fail("Слишком большая сумма за один раз");

                Chars.Repository.Remove(player, location, "inventory", (ItemId)itemId, count);
                if (toBtc)
                {
                    CryptoWallets.ChangePersonal(uuid, btc);
                    HistoryLog.Add(uuid, "fence", $"Скупка: {Methods.Lots.ItemName((ItemId)itemId)} ×{count}", btc);
                }
                else
                {
                    MoneySystem.Wallet.Change(player, (int)usd);
                    GameLog.Money("server", $"player({uuid})", usd, $"fence({itemId},{count})");
                    HistoryLog.Add(uuid, "fence", $"Скупка: {Methods.Lots.ItemName((ItemId)itemId)} ×{count}", usd, HistoryLog.Usd);
                }

                var now = DateTime.Now;
                Demands[itemId] = new Demand { Saturation = saturationAfter, Updated = now };
                BlackMarketRepository.Enqueue(
                    "INSERT INTO `blackmarket_fence_demand` (`item`,`saturation`,`updated`) VALUES (@item,@s,@u) ON DUPLICATE KEY UPDATE `saturation`=@s, `updated`=@u",
                    ("@item", itemId), ("@s", saturationAfter), ("@u", new DateTimeOffset(now).ToUnixTimeSeconds()));
                AuditLog.Write("fence_sell", uuid, itemId: itemId, count: count, amount: toBtc ? btc : usd,
                    source: toBtc ? Source.Personal : Source.Cash, details: new { usd, btc });

                return OpResult.Success(toBtc
                    ? $"Мавр забрал товар: +{BlackMarketCore.Btc(btc)}{Crime.CrimeCore.FundSuffix(player)}"
                    : $"Мавр забрал товар: +{MoneySystem.Wallet.Format(usd)}${Crime.CrimeCore.FundSuffix(player)}");
            }
        }
    }
}
