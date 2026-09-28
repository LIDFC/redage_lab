using System;
using MySqlConnector;
using NeptuneEvo.Organizations.Models;

namespace NeptuneEvo.Organizations.Contracts.Methods
{
    /// <summary>
    /// Деньги и репутация организации для подрядов. Используется существующий баланс OrganizationData.Money
    /// (тот же, что на складе/в планшете), без второй экономики. Вызывать только под ContractsCore.Sync.
    /// Возвращает команду записи, чтобы вызывающий положил её в одну транзакцию со статусом контракта.
    /// </summary>
    public static class Finance
    {
        /// <summary>Изменить баланс. Баланс может уйти в минус (неустойка) — тогда подряды и закупки блокируются.</summary>
        public static void ChangeMoney(OrganizationData organizationData, long delta)
        {
            if (organizationData == null || delta == 0)
                return;

            var lastMulti = organizationData.MoneyMultiplier();
            var money = Math.Max(int.MinValue, Math.Min(int.MaxValue, organizationData.Money + delta));
            organizationData.Money = (int)money;
            var newMulti = organizationData.MoneyMultiplier();
            if (lastMulti != newMulti)
                Manager.UpdateForMembers(organizationData.Id, OrganizationOfficeTypeUpdate.Money, newMulti);
        }

        /// <summary>Изменить репутацию (не ниже 0).</summary>
        public static void ChangeReputation(OrganizationData organizationData, int delta)
        {
            if (organizationData == null || delta == 0)
                return;
            organizationData.Reputation = (int)Math.Max(0, Math.Min(int.MaxValue, (long)organizationData.Reputation + delta));
        }

        /// <summary>Снимок баланса и репутации для транзакции.</summary>
        public static MySqlCommand SaveCommand(OrganizationData organizationData) =>
            ContractsRepository.OrganizationCommand(organizationData.Id, organizationData.Money, organizationData.Reputation);

        /// <summary>Организация в долгах (после неустойки) — брать подряды и закупать материалы нельзя.</summary>
        public static bool IsInDebt(OrganizationData organizationData) =>
            organizationData != null && organizationData.Money < 0;
    }
}
