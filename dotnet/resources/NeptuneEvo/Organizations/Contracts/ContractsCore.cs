using System;
using System.Collections.Concurrent;
using NeptuneEvo.Handles;
using NeptuneEvo.Organizations.Models;
using NeptuneEvo.Organizations.Player;
using Redage.SDK;

namespace NeptuneEvo.Organizations.Contracts
{
    /// <summary>
    /// Общие вещи модуля подрядов: единая блокировка, антиспам и проверка «легальная организация».
    /// Все переходы состояний контрактов, списания/начисления денег организации и репутации
    /// выполняются внутри <c>lock (Sync)</c>: проверка → изменение → запись идут одним куском,
    /// поэтому две организации не могут одновременно взять один контракт, а выплата не пройдёт дважды.
    /// </summary>
    public static class ContractsCore
    {
        public static readonly object Sync = new object();
        public static readonly nLog Log = new nLog("OrgContracts");

        private static readonly ConcurrentDictionary<int, DateTime> LastAction = new ConcurrentDictionary<int, DateTime>();

        /// <summary>Антиспам: не чаще одного действия в <paramref name="ms"/> мс на игрока.</summary>
        public static bool AntiSpam(ExtPlayer player, int ms = 500)
        {
            var uuid = player.GetUUID();
            var now = DateTime.Now;
            if (LastAction.TryGetValue(uuid, out var last) && (now - last).TotalMilliseconds < ms)
                return false;
            LastAction[uuid] = now;
            return true;
        }

        /// <summary>
        /// Легальная организация — «Сообщество» (CrimeOptions = false). Отдельной системы типов нет:
        /// тот же флаг включает крим-права (OrgCrime, InCar, Cuff…) в Organizations.Manager.
        /// </summary>
        public static bool IsLegal(OrganizationData organizationData) =>
            organizationData != null && organizationData.Status && !organizationData.CrimeOptions;

        /// <summary>
        /// Право «Строительные подряды» есть только у легальных организаций: добавляет/убирает его
        /// из DefaultAccess (владелец получает все права из DefaultAccess, остальным выдаются рангами).
        /// Вызывается при загрузке, создании организации и смене её типа.
        /// </summary>
        public static void ApplyTypeAccess(OrganizationData organizationData)
        {
            if (organizationData == null)
                return;
            organizationData.DefaultAccess.Remove(NeptuneEvo.Table.Models.RankToAccess.OrganizationContracts);
            if (!organizationData.CrimeOptions)
                organizationData.DefaultAccess.Add(NeptuneEvo.Table.Models.RankToAccess.OrganizationContracts);
        }

        public static string Money(long amount) => $"${MoneySystem.Wallet.Format(amount)}";
    }
}
