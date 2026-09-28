using System;
using Localization;
using NeptuneEvo.Accounts;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Houses;
using NeptuneEvo.Organizations.Contracts;
using NeptuneEvo.Organizations.Contracts.Methods;
using NeptuneEvo.Organizations.Models;
using NeptuneEvo.Organizations.Player;
using NeptuneEvo.Players;
using NeptuneEvo.Players.Phone.Messages.Models;
using Redage.SDK;

namespace NeptuneEvo.MoneySystem
{
    /// <summary>
    /// Банковские операции, общие для банкомата и приложения Fleeca в телефоне:
    /// лимиты налоговых счетов, оплата налогов с карты, перевод (подготовка + подтверждение), пополнение счёта организации.
    /// </summary>
    public static class BankOps
    {
        /// <summary>Максимум одной операции пополнения счёта организации.</summary>
        public const int OrgDepositMax = 10_000_000;

        /// <summary>На сколько дней вперёд можно оплатить налог (как в банкомате): 7/14/21/28 по VIP.</summary>
        public static int TaxDays(int vipLvl) => vipLvl switch
        {
            2 => 14,
            3 => 21,
            4 => 28,
            5 => 28,
            _ => 7,
        };

        public static int HouseTaxMax(House house, int vipLvl) =>
            Convert.ToInt32(house.Price / 100 * 0.026 * 24 * TaxDays(vipLvl));

        public static int BizTaxMax(Business biz, int vipLvl) =>
            Convert.ToInt32(biz.SellPrice / 100 * biz.Tax * 24 * TaxDays(vipLvl));

        // ------------------------------------------------------------------ налоги с карты (телефон)

        /// <summary>Оплатить налоговый счёт (дом/бизнес) с карты игрока. Возвращает текст ошибки или null.</summary>
        public static string PayTaxFromCard(ExtPlayer player, int targetBankId, int maxMoney, int amount, string log, out int paid)
        {
            paid = 0;
            var characterData = player.GetCharacterData();
            if (characterData == null)
                return "Недоступно";
            if (Admin.IsServerStoping)
                return LangFunc.GetText(LangType.Ru, DataName.ServerCant);
            if (amount <= 0)
                return "Укажите сумму";
            var balance = Bank.GetBalance(targetBankId);
            if (balance + amount > maxMoney)
                amount = maxMoney - (int)balance;
            if (amount <= 0)
                return "Налог уже оплачен на максимальный срок";
            if (Bank.GetBalance(characterData.Bank) < amount)
                return "Недостаточно денег на карте";
            if (!Bank.Transfer(characterData.Bank, targetBankId, amount))
                return "Операция отклонена банком";
            paid = amount;
            GameLog.Money($"bank({characterData.Bank})", $"bank({targetBankId})", amount, log);
            return null;
        }

        // ------------------------------------------------------------------ перевод

        /// <summary>
        /// Проверки перевода (как в банкомате) и запрос подтверждения стандартным диалогом AcceptBankTransfer
        /// (после «Да» перевод выполняет ATM.AcceptTransfer). Возвращает текст ошибки или null.
        /// </summary>
        public static string PrepareTransfer(ExtPlayer player, int toAccount, int amount)
        {
            var sessionData = player.GetSessionData();
            var characterData = player.GetCharacterData();
            if (sessionData == null || characterData == null)
                return "Недоступно";
            if (Admin.IsServerStoping)
                return LangFunc.GetText(LangType.Ru, DataName.ServerCant);
            if (!FunctionsAccess.IsWorking("atmtransfer"))
                return LangFunc.GetText(LangType.Ru, DataName.FunctionOffByAdmins);
            if (characterData.LVL < 1)
                return LangFunc.GetText(LangType.Ru, DataName.flvltotransact);
            if (DateTime.Now < sessionData.TimingsData.NextBankTransfer)
                return LangFunc.GetText(LangType.Ru, DataName.NextTransactionSoon);
            if (toAccount <= 0 || !Bank.Accounts.ContainsKey(toAccount) || (Bank.Accounts[toAccount].Type != 1 && characterData.AdminLVL == 0))
                return LangFunc.GetText(LangType.Ru, DataName.CantFindBankAccount);
            if (toAccount == characterData.Bank)
                return "Нельзя перевести на свой же счёт";
            amount = Math.Abs(amount);
            if (amount <= 0)
                return "Укажите сумму";
            if (Bank.GetBalance(characterData.Bank) < amount)
                return "Недостаточно денег на карте";

            sessionData.CurrentBankTransferSumAccBankInfo = characterData.Bank;
            sessionData.CurrentBankTransferBankInfo = toAccount;
            sessionData.CurrentBankTransferSum = amount;
            if (characterData.AdminLVL == 0)
                sessionData.TimingsData.NextBankTransfer = DateTime.Now.AddSeconds(10);

            var holder = Bank.Accounts[toAccount].Holder;
            Trigger.ClientEvent(player, "openDialog", "AcceptBankTransfer", LangFunc.GetText(LangType.Ru, DataName.TransactionConfirm1, amount) +
                (holder.Length > 3 ? LangFunc.GetText(LangType.Ru, DataName.TransactionConfirm2, holder) : "на неизвестный счет?"));
            return null;
        }

        // ------------------------------------------------------------------ счёт организации

        /// <summary>
        /// Пополнить счёт своей организации с карты (fromCard) или наличными. Может любой участник (как на складе).
        /// Баланс организации пишется в БД сразу, операция — в логи организации и GameLog.
        /// </summary>
        public static string OrgDeposit(ExtPlayer player, int amount, bool fromCard)
        {
            var characterData = player.GetCharacterData();
            var organizationData = player.GetOrganizationData();
            if (characterData == null)
                return "Недоступно";
            if (Admin.IsServerStoping)
                return LangFunc.GetText(LangType.Ru, DataName.ServerCant);
            if (organizationData == null || !organizationData.Status)
                return "Вы не состоите в организации";
            amount = Math.Abs(amount);
            if (amount <= 0)
                return "Укажите сумму";
            if (amount > OrgDepositMax)
                return $"За один раз — не больше ${Wallet.Format(OrgDepositMax)}";

            lock (ContractsCore.Sync)
            {
                if (fromCard)
                {
                    if (Bank.GetBalance(characterData.Bank) < amount || !Bank.Change(characterData.Bank, -amount))
                        return "Недостаточно денег на карте";
                }
                else if (!Wallet.Change(player, -amount))
                    return "Недостаточно наличных";

                Finance.ChangeMoney(organizationData, amount);
                ContractsRepository.Enqueue(Finance.SaveCommand(organizationData));
            }

            var source = fromCard ? $"bank({characterData.Bank})" : $"player({characterData.UUID})";
            GameLog.Money(source, $"org({organizationData.Id})", amount, fromCard ? "bankOrgDeposit" : "atmOrgDeposit");
            Organizations.Table.Logs.Repository.AddLogs(player, OrganizationLogsType.TakeMoney,
                $"Пополнил счёт организации {(fromCard ? "с банковской карты" : "наличными через банкомат")} (${Wallet.Format(amount)})");
            Players.Phone.Messages.Repository.AddSystemMessage(player, (int)DefaultNumber.Bank,
                $"Счёт организации «{organizationData.Name}» пополнен на ${Wallet.Format(amount)}. Баланс организации: ${Wallet.Format(organizationData.Money)}", DateTime.Now);
            return null;
        }
    }
}
