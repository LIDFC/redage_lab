using NeptuneEvo.BlackMarket.Audit;
using NeptuneEvo.BlackMarket.Config;
using NeptuneEvo.BlackMarket.History;
using NeptuneEvo.BlackMarket.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Handles;
using Redage.SDK;
using System;

namespace NeptuneEvo.BlackMarket.Crypto
{
    /// <summary>
    /// Действия игрока с криптой: перевод по номеру телефона, пополнение и вывод кошелька банды, системный обменник $→BTC.
    /// Получателя, фракцию, ранг и балансы сервер определяет сам.
    /// </summary>
    public static class CryptoOps
    {
        private const long MaxAmount = 1_000_000_000_000;

        /// <summary>Перевод BTC игроку по номеру телефона (SIM). Получатель может быть офлайн.</summary>
        public static OpResult Transfer(ExtPlayer player, int phoneNumber, long amount)
        {
            if (amount < 1 || amount > MaxAmount)
                return OpResult.Fail("Укажите сумму");
            if (!Main.SimCards.TryGetValue(phoneNumber, out var targetUuid) || targetUuid <= 0)
                return OpResult.Fail("Кошелёк с таким номером не найден");

            var uuid = player.GetUUID();
            if (targetUuid == uuid)
                return OpResult.Fail("Нельзя перевести самому себе");

            lock (BlackMarketCore.Sync)
            {
                if (!CryptoWallets.Transfer(uuid, targetUuid, amount))
                    return OpResult.Fail("Не хватает свободных BTC");

                HistoryLog.Add(uuid, "transfer", "Перевод", -amount);
                HistoryLog.Add(targetUuid, "transfer", "Перевод", amount);
                AuditLog.Write("transfer", uuid, targetUuid, amount: amount, source: Source.Personal, details: new { phoneNumber });
            }

            var target = Main.GetPlayerByUUID(targetUuid);
            if (target != null && target.IsCharacterData())
                Notify.Send(target, NotifyType.Info, NotifyPosition.BottomCenter, $"Поступление: +{BlackMarketCore.Btc(amount)}", 4000);
            return OpResult.Success($"Переведено {BlackMarketCore.Btc(amount)}");
        }

        /// <summary>Внести свои BTC в кошелёк банды — может любой участник криминальной фракции.</summary>
        public static OpResult FractionDeposit(ExtPlayer player, long amount)
        {
            if (amount < 1 || amount > MaxAmount)
                return OpResult.Fail("Укажите сумму");
            var fractionId = player.GetFractionId();
            if (!BlackMarketCore.IsCriminalFraction(fractionId))
                return OpResult.Fail("У вашей фракции нет крипто-кошелька");

            var uuid = player.GetUUID();
            lock (BlackMarketCore.Sync)
            {
                if (!CryptoWallets.ChangePersonal(uuid, -amount))
                    return OpResult.Fail("Не хватает свободных BTC");
                CryptoWallets.ChangeFraction(fractionId, amount);

                HistoryLog.Add(uuid, "fraction", "Взнос в кошелёк банды", -amount);
                HistoryLog.Add(0, "fraction", "Взнос участника", amount, fractionId: fractionId);
                AuditLog.Write("fraction_deposit", uuid, amount: amount, source: Source.Personal, details: new { fractionId });
                return OpResult.Success($"В кошелёк банды внесено {BlackMarketCore.Btc(amount)}");
            }
        }

        /// <summary>Вывести BTC из кошелька банды себе — с ранга fractionPaymentRank.</summary>
        public static OpResult FractionWithdraw(ExtPlayer player, long amount)
        {
            if (amount < 1 || amount > MaxAmount)
                return OpResult.Fail("Укажите сумму");
            var fractionId = player.GetFractionId();
            if (!BlackMarketCore.IsCriminalFraction(fractionId))
                return OpResult.Fail("У вашей фракции нет крипто-кошелька");
            var rank = player.GetFractionMemberData()?.Rank ?? 0;
            if (rank < BlackMarketConfig.Current.FractionPaymentRank)
                return OpResult.Fail($"Выводить из кошелька банды можно с {BlackMarketConfig.Current.FractionPaymentRank} ранга");

            var uuid = player.GetUUID();
            lock (BlackMarketCore.Sync)
            {
                if (!CryptoWallets.ChangeFraction(fractionId, -amount))
                    return OpResult.Fail("В кошельке банды не хватает BTC");
                CryptoWallets.ChangePersonal(uuid, amount);

                HistoryLog.Add(uuid, "fraction", "Вывод из кошелька банды", amount);
                HistoryLog.Add(0, "fraction", "Вывод участником", -amount, fractionId: fractionId);
                AuditLog.Write("fraction_withdraw", uuid, amount: amount, source: Source.Fraction, details: new { fractionId, rank });
                return OpResult.Success($"Выведено {BlackMarketCore.Btc(amount)}");
            }
        }

        /// <summary>Системный обменник: купить BTC за наличные по курсу из конфига (exchangeUsdPerBtc, 0 — выключен).</summary>
        public static OpResult Exchange(ExtPlayer player, long btc)
        {
            var rate = BlackMarketConfig.Current.ExchangeUsdPerBtc;
            if (rate <= 0)
                return OpResult.Fail("Обменник сейчас закрыт");
            if (btc < 1 || btc > MaxAmount)
                return OpResult.Fail("Укажите количество BTC");

            var cost = (long)Math.Ceiling(btc * rate);
            var characterData = player.GetCharacterData();
            if (characterData == null || cost > int.MaxValue || characterData.Money < cost)
                return OpResult.Fail($"Нужно {MoneySystem.Wallet.Format(cost)}$ наличными");

            var uuid = characterData.UUID;
            lock (BlackMarketCore.Sync)
            {
                if (!MoneySystem.Wallet.Change(player, -(int)cost))
                    return OpResult.Fail($"Нужно {MoneySystem.Wallet.Format(cost)}$ наличными");
                CryptoWallets.ChangePersonal(uuid, btc);

                GameLog.Money($"player({uuid})", "server", cost, "blackmarketExchange");
                HistoryLog.Add(uuid, "exchange", "Обменник", btc);
                HistoryLog.Add(uuid, "exchange", "Обменник", -cost, HistoryLog.Usd);
                AuditLog.Write("exchange", uuid, amount: btc, source: Source.Cash, details: new { usd = cost, rate });
                return OpResult.Success($"Куплено {BlackMarketCore.Btc(btc)} за {MoneySystem.Wallet.Format(cost)}$");
            }
        }
    }
}
