using GTANetworkAPI;
using NeptuneEvo.Fractions.Models;
using NeptuneEvo.Handles;
using Redage.SDK;
using System;
using System.Collections.Concurrent;

namespace NeptuneEvo.BlackMarket
{
    /// <summary>
    /// Общие вещи модуля: единая блокировка для всех денежных/предметных операций,
    /// антиспам на события и принадлежность к криминальной фракции.
    /// Все мутации кошельков, лотов, закладок и P2P выполняются внутри <c>lock (Sync)</c>:
    /// проверка → списание → перенос → запись идут одним куском, поэтому две покупки последнего товара,
    /// снятие лота во время покупки или трата крипты банды двумя участниками не пересекаются.
    /// </summary>
    public static class BlackMarketCore
    {
        public static readonly object Sync = new object();
        public static readonly nLog Log = new nLog("BlackMarket");

        private static readonly ConcurrentDictionary<int, DateTime> LastAction = new ConcurrentDictionary<int, DateTime>();

        /// <summary>Антиспам: не чаще одного действия в <paramref name="ms"/> мс на игрока.</summary>
        public static bool AntiSpam(ExtPlayer player, int ms = 400)
        {
            var uuid = player.GetUUID();
            var now = DateTime.Now;
            if (LastAction.TryGetValue(uuid, out var last) && (now - last).TotalMilliseconds < ms)
                return false;
            LastAction[uuid] = now;
            return true;
        }

        /// <summary>Банды, мафии и байкеры — у них есть крипто-кошелёк фракции.</summary>
        public static bool IsCriminalFraction(int fractionId)
        {
            if (fractionId <= 0 || !Fractions.Manager.FractionTypes.TryGetValue(fractionId, out var type))
                return false;
            return type == FractionsType.Gangs || type == FractionsType.Mafia || type == FractionsType.Bikers;
        }

        public static string FractionName(int fractionId) =>
            Fractions.Manager.FractionNames.TryGetValue(fractionId, out var name) ? name : $"#{fractionId}";

        public static string Btc(long amount) => $"{MoneySystem.Wallet.Format(amount)} BTC";
    }
}
