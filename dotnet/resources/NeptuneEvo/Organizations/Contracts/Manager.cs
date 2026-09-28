using System;
using System.Collections.Generic;
using System.Linq;
using NeptuneEvo.Core;
using NeptuneEvo.Handles;
using NeptuneEvo.Organizations.Contracts.Config;
using NeptuneEvo.Organizations.Contracts.Generators;
using NeptuneEvo.Organizations.Contracts.Logs;
using NeptuneEvo.Organizations.Contracts.Methods;
using NeptuneEvo.Organizations.Contracts.Models;
using NeptuneEvo.Organizations.Models;
using NeptuneEvo.Organizations.Player;
using NeptuneEvo.Table.Models;
using Redage.SDK;

namespace NeptuneEvo.Organizations.Contracts
{
    /// <summary>
    /// Строительные подряды: глобальный набор контрактов, их жизненный цикл, награды, неустойки и репутация.
    /// Все изменения — под ContractsCore.Sync и только из главного потока (события/таймер с isnapitask).
    /// </summary>
    public static class ContractsManager
    {
        public static bool Ready { get; private set; }

        /// <summary>Доступные и активные контракты (завершённые/проваленные выгружаются из памяти, остаются в БД).</summary>
        private static readonly Dictionary<int, Contract> Contracts = new Dictionary<int, Contract>();
        private static int _lastId = 0;

        /// <summary>Ключ текущего набора (его контракты показываются, даже если взяты другими).</summary>
        public static string CurrentSlot { get; private set; }
        /// <summary>Последнее обработанное время генерации по расписанию (ручная генерация его не трогает).</summary>
        private static string _lastScheduledSlot;

        public static void Init()
        {
            try
            {
                ContractsConfig.Load();
                ContractTemplates.Load();
                RegisterCargoTypes();
                ContractsRepository.Init();

                foreach (var (orgId, reputation) in ContractsRepository.LoadReputations())
                {
                    var organizationData = Manager.GetOrganizationData(orgId);
                    if (organizationData == null)
                        continue;
                    organizationData.Reputation = reputation;
                    ContractsCore.ApplyTypeAccess(organizationData);
                }

                lock (ContractsCore.Sync)
                {
                    _lastId = ContractsRepository.LoadLastId();
                    foreach (var contract in ContractsRepository.LoadContracts())
                        Contracts[contract.Id] = contract;
                    CurrentSlot = ContractsRepository.LoadLastSlot(false);
                    _lastScheduledSlot = ContractsRepository.LoadLastSlot(true);
                }

                Cargo.CargoManager.Load();
                MaterialShop.Seed();
                Cargo.CargoInteraction.LoadValidator = ValidateLoad;
                Cargo.CargoInteraction.OnLoaded = (player, unit, number) =>
                    ContractAudit.OrgLog(unit.OwnerId, player.GetUUID(), player.Name, OrganizationLogsType.ContractLoad,
                        $"Загрузил: {Cargo.CargoManager.TypeName(unit.CargoType)} ×{unit.Quantity}{(unit.ContractId > 0 ? $" (подряд #{unit.ContractId})" : "")} в машину {number}");

                Ready = true;
                ContractsCore.Log.Write($"Loaded {Contracts.Count} contracts, slot {CurrentSlot ?? "-"}", nLog.Type.Success);

                // Сразу: если сервер был выключен во время генерации — создать набор, просроченные — провалить
                Tick();
                Timers.Start("orgcontracts.tick", 5000, Tick, true);
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"Init Exception: {e}");
            }
        }

        /// <summary>Грузить можно только то, что нужно активному подряду организации (свой контракт или свободный груз под недостающий материал).</summary>
        private static string ValidateLoad(Cargo.CargoUnit unit)
        {
            if (unit.OwnerType != Cargo.CargoOwner.Organization)
                return null;
            var active = GetActive(unit.OwnerId);
            if (unit.ContractId > 0 && active.Any(c => c.Id == unit.ContractId))
                return null;
            if (active.Any(c => c.GetMaterial(unit.CargoType) is ContractMaterial m && m.Delivered < m.Required))
                return null;
            return "Этот груз не нужен ни одному активному подряду организации";
        }

        /// <summary>Материалы подрядов — типы груза универсального модуля Cargo.</summary>
        public static void RegisterCargoTypes()
        {
            foreach (var material in ContractsConfig.Current.Materials)
            {
                Cargo.CargoManager.RegisterType(new Cargo.CargoType
                {
                    Id = material.Id,
                    Name = material.Name,
                    Icon = material.Icon,
                    KgPerUnit = material.KgPerUnit,
                    UnitsPerPallet = Math.Max(1, material.UnitsPerPallet),
                    Prop = material.Prop,
                });
            }
        }

        private static void Tick()
        {
            if (!Ready)
                return;
            try
            {
                CheckGeneration();
                CheckDeadlines();
                lock (ContractsCore.Sync)
                    Cargo.CargoVehicle.Sync();
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"Tick Exception: {e}");
            }
        }

        /// <summary>Вызывается при сохранении/остановке сервера: дописать очередь БД.</summary>
        public static void Save()
        {
            try
            {
                ContractsRepository.Flush();
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"Save Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ чтение

        public static Contract Get(int id)
        {
            lock (ContractsCore.Sync)
                return Contracts.TryGetValue(id, out var contract) ? contract : null;
        }

        /// <summary>Что видит организация: контракты текущего набора (включая взятые другими), свои активные и админские доступные.</summary>
        public static List<Contract> GetVisible(int orgId)
        {
            lock (ContractsCore.Sync)
            {
                return Contracts.Values
                    .Where(c => c.Status == ContractStatus.Available
                        || (c.Status == ContractStatus.Active && (c.OrganizationId == orgId || c.GenSlot == CurrentSlot)))
                    .OrderBy(c => c.OrganizationId == orgId && c.IsActive ? 0 : 1)
                    .ThenBy(c => c.Id)
                    .ToList();
            }
        }

        public static List<Contract> GetActive(int orgId)
        {
            lock (ContractsCore.Sync)
                return Contracts.Values.Where(c => c.IsActive && c.OrganizationId == orgId).OrderBy(c => c.Id).ToList();
        }

        public static List<Contract> GetAll()
        {
            lock (ContractsCore.Sync)
                return Contracts.Values.OrderBy(c => c.Id).ToList();
        }

        private static int ActiveCount(int orgId) =>
            Contracts.Values.Count(c => c.IsActive && c.OrganizationId == orgId);

        // ------------------------------------------------------------------ генерация

        private static void CheckGeneration()
        {
            var latest = ContractGenerator.LatestSlot(DateTime.Now);
            if (latest == null)
                return;
            var slot = ContractGenerator.SlotKey(latest.Value);
            if (slot == _lastScheduledSlot)
                return;
            _lastScheduledSlot = slot;
            Generate(slot);
        }

        /// <summary>Новый общий набор: непринятые контракты прошлого набора истекают, активные продолжают работать.</summary>
        private static void Generate(string slot)
        {
            var created = new List<Contract>();
            lock (ContractsCore.Sync)
            {
                if (slot == CurrentSlot)
                    return;

                foreach (var contract in Contracts.Values.Where(c => c.Status == ContractStatus.Available).ToList())
                {
                    contract.Status = ContractStatus.Expired;
                    contract.FinishedAt = DateTime.Now;
                    ContractsRepository.Save(contract);
                    Contracts.Remove(contract.Id);
                }

                foreach (var template in ContractGenerator.PickTemplates(ContractsConfig.Current.ContractsPerGeneration))
                {
                    var contract = ContractGenerator.Create(template, slot, ContractSource.Generator);
                    Add(contract);
                    created.Add(contract);
                }

                CurrentSlot = slot;
                ContractsRepository.SaveSlot(slot);
            }

            ContractAudit.Write("generate", details: new { slot, ids = created.Select(c => c.Id) });
            ContractsCore.Log.Write($"Generated {created.Count} contracts for slot {slot}", nLog.Type.Success);
            if (created.Count > 0)
                NotifyLegalOrganizations($"Новые строительные подряды ({created.Count}) — планшет → Организация → Подряды");
        }

        private static void Add(Contract contract)
        {
            contract.Id = ++_lastId;
            Contracts[contract.Id] = contract;
            ContractsRepository.Save(contract);
        }

        /// <summary>Админ: создать контракты вручную (общие, в текущем наборе). templateId = null — случайные шаблоны.</summary>
        public static List<Contract> CreateByAdmin(string templateId, int count, int adminUuid)
        {
            var created = new List<Contract>();
            var templates = new List<ContractTemplate>();
            if (!string.IsNullOrEmpty(templateId))
            {
                var template = ContractTemplates.Get(templateId);
                if (template == null || !ContractGenerator.IsValid(template))
                    return created;
                for (var i = 0; i < count; i++)
                    templates.Add(template);
            }
            else
                templates = ContractGenerator.PickTemplates(count);

            lock (ContractsCore.Sync)
            {
                foreach (var template in templates)
                {
                    var contract = ContractGenerator.Create(template, CurrentSlot ?? "", ContractSource.Admin);
                    Add(contract);
                    created.Add(contract);
                }
            }
            ContractAudit.Write("admin_create", actorUuid: adminUuid, details: new { templateId, ids = created.Select(c => c.Id) });
            return created;
        }

        /// <summary>Админ: принудительно запустить новую генерацию.</summary>
        public static void ForceGenerate()
        {
            Generate("force " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        // ------------------------------------------------------------------ принятие

        /// <summary>Проверки и принятие контракта сотрудником. Атомарно: второй запрос (своей или чужой организации) получит отказ.</summary>
        public static bool Accept(ExtPlayer player, int contractId)
        {
            var memberData = player.GetOrganizationMemberData();
            var organizationData = player.GetOrganizationData();
            if (memberData == null || organizationData == null)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Вы не состоите в организации", 3000);
                return false;
            }
            if (!ContractsCore.IsLegal(organizationData))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Подряды доступны только законным организациям", 3000);
                return false;
            }
            if (!player.IsOrganizationAccess(RankToAccess.OrganizationContracts, false))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Нет права «Строительные подряды»", 3000);
                return false;
            }

            string error = null;
            Contract contract;
            lock (ContractsCore.Sync)
            {
                if (!Contracts.TryGetValue(contractId, out contract))
                    error = "Контракт не найден или уже закрыт";
                else if (contract.Status != ContractStatus.Available)
                    error = contract.OrganizationId == organizationData.Id ? "Контракт уже взят вашей организацией" : "Контракт уже взят другой организацией";
                else if (Finance.IsInDebt(organizationData))
                    error = "Бюджет организации в минусе — сначала погасите долг";
                else if (organizationData.Reputation < contract.RequiredReputation)
                    error = $"Недостаточно репутации: {organizationData.Reputation} из {contract.RequiredReputation}";
                else if (ActiveCount(organizationData.Id) >= ContractsConfig.Current.MaxActivePerOrganization)
                    error = $"У организации уже {ContractsConfig.Current.MaxActivePerOrganization} активных подряда";
                else
                {
                    contract.Status = ContractStatus.Active;
                    contract.OrganizationId = organizationData.Id;
                    contract.AcceptedByUuid = player.GetUUID();
                    contract.AcceptedByName = player.Name;
                    contract.AcceptedAt = DateTime.Now;
                    contract.DeadlineAt = DateTime.Now.AddMinutes(contract.DeadlineMinutes);
                    ContractsRepository.Save(contract);
                }
            }

            if (error != null)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, error, 3000);
                ContractAudit.Write("accept", contractId, organizationData.Id, player.GetUUID(), result: "denied", details: error);
                return false;
            }

            ContractAudit.OrgLog(organizationData.Id, player.GetUUID(), player.Name, OrganizationLogsType.ContractAccept,
                $"Принял подряд #{contract.Id} «{contract.Title}» (награда {ContractsCore.Money(contract.Reward)}, неустойка {ContractsCore.Money(contract.Penalty)})");
            ContractAudit.Write("accept", contract.Id, organizationData.Id, player.GetUUID(), contract.Reward);
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Подряд #{contract.Id} принят. Срок: {FormatDuration(contract.DeadlineMinutes)}", 4000);
            NotifyOrganization(organizationData.Id, $"{player.Name} принял подряд #{contract.Id}: {contract.Title}", player);
            return true;
        }

        // ------------------------------------------------------------------ закрытие

        /// <summary>Отмена сотрудником, который принял контракт: неустойка и потеря репутации, материалы не возвращаются.</summary>
        public static bool Cancel(ExtPlayer player, int contractId)
        {
            var organizationData = player.GetOrganizationData();
            Contract contract;
            string error = null;
            lock (ContractsCore.Sync)
            {
                if (!Contracts.TryGetValue(contractId, out contract) || !contract.IsActive)
                    error = "Контракт не найден или уже закрыт";
                else if (organizationData == null || contract.OrganizationId != organizationData.Id)
                    error = "Это не подряд вашей организации";
                else if (contract.AcceptedByUuid != player.GetUUID())
                    error = $"Отменить подряд может только {contract.AcceptedByName}, который его принял";
                else
                    Close(contract, ContractStatus.Cancelled, player.GetUUID(), player.Name);
            }

            if (error != null)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, error, 3000);
                return false;
            }
            Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, $"Подряд #{contract.Id} отменён. Неустойка {ContractsCore.Money(contract.Penalty)}", 4000);
            return true;
        }

        /// <summary>Все материалы сданы → выплата. Вызывается модулем доставки и админом. Повторный вызов ничего не делает.</summary>
        public static bool Complete(int contractId, int actorUuid, string actorName)
        {
            lock (ContractsCore.Sync)
            {
                if (!Contracts.TryGetValue(contractId, out var contract) || !contract.IsActive)
                    return false;
                Close(contract, ContractStatus.Completed, actorUuid, actorName);
                return true;
            }
        }

        /// <summary>Провал (дедлайн или админ): неустойка и потеря репутации.</summary>
        public static bool Fail(int contractId, int actorUuid, string actorName)
        {
            lock (ContractsCore.Sync)
            {
                if (!Contracts.TryGetValue(contractId, out var contract) || !contract.IsActive)
                    return false;
                Close(contract, ContractStatus.Failed, actorUuid, actorName);
                return true;
            }
        }

        /// <summary>Админ: удалить контракт без денег и репутации (доступный или активный).</summary>
        public static bool Delete(int contractId, int adminUuid)
        {
            Contract contract;
            lock (ContractsCore.Sync)
            {
                if (!Contracts.TryGetValue(contractId, out contract))
                    return false;
                contract.Status = ContractStatus.Deleted;
                contract.FinishedAt = DateTime.Now;
                ContractsRepository.Save(contract);
                Contracts.Remove(contractId);
            }
            ContractAudit.Write("admin_delete", contract.Id, contract.OrganizationId, adminUuid);
            if (contract.OrganizationId > 0)
            {
                ContractAudit.OrgLog(contract.OrganizationId, 0, "Администрация", OrganizationLogsType.ContractCancel, $"Подряд #{contract.Id} снят администрацией (без неустойки)");
                NotifyOrganization(contract.OrganizationId, $"Подряд #{contract.Id} снят администрацией без неустойки");
            }
            return true;
        }

        /// <summary>
        /// Закрыть активный контракт (только под Sync, статус проверен вызывающим). Контракт, деньги и репутация
        /// организации пишутся одной транзакцией; статус меняется до записи, поэтому двойная выплата невозможна.
        /// </summary>
        private static void Close(Contract contract, ContractStatus status, int actorUuid, string actorName)
        {
            var organizationData = Manager.GetOrganizationData(contract.OrganizationId);

            contract.Status = status;
            contract.FinishedAt = DateTime.Now;
            Contracts.Remove(contract.Id);

            // Купленные материалы остаются организации (без возврата денег): груз отвязывается от контракта
            // и может быть сдан в другой активный подряд организации с тем же материалом.
            foreach (var unit in Cargo.CargoManager.GetByContract(contract.Id))
                ContractsRepository.Enqueue(Cargo.CargoManager.Unbind(unit));

            long money = 0;
            var reputation = 0;
            if (organizationData != null)
            {
                if (status == ContractStatus.Completed)
                {
                    money = contract.Reward;
                    reputation = contract.ReputationReward;
                }
                else if (status == ContractStatus.Failed || status == ContractStatus.Cancelled)
                {
                    money = -contract.Penalty;
                    reputation = -contract.ReputationPenalty;
                }

                var reputationBefore = organizationData.Reputation;
                Finance.ChangeMoney(organizationData, money);
                Finance.ChangeReputation(organizationData, reputation);
                reputation = organizationData.Reputation - reputationBefore;

                ContractsRepository.EnqueueTransaction(ContractsRepository.SaveCommand(contract), Finance.SaveCommand(organizationData));
            }
            else
                ContractsRepository.Save(contract);

            var action = status == ContractStatus.Completed ? "complete" : status == ContractStatus.Failed ? "fail" : "cancel";
            ContractAudit.Write(action, contract.Id, contract.OrganizationId, actorUuid, money,
                details: new { reputation, orgMoney = organizationData?.Money, orgReputation = organizationData?.Reputation, by = actorName, progress = contract.ProgressPercent });

            if (money != 0)
                GameLog.Money(money > 0 ? "server" : $"org({contract.OrganizationId})", money > 0 ? $"org({contract.OrganizationId})" : "server",
                    Math.Abs(money), $"orgContract{action}({contract.Id})");

            if (organizationData == null)
                return;

            var repText = reputation == 0 ? "" : $", репутация {(reputation > 0 ? "+" : "")}{reputation}";
            string text;
            OrganizationLogsType type;
            switch (status)
            {
                case ContractStatus.Completed:
                    type = OrganizationLogsType.ContractComplete;
                    text = $"Подряд #{contract.Id} выполнен: организация получила {ContractsCore.Money(money)}{repText}";
                    break;
                case ContractStatus.Cancelled:
                    type = OrganizationLogsType.ContractCancel;
                    text = $"Подряд #{contract.Id} отменён: неустойка {ContractsCore.Money(-money)}{repText}";
                    break;
                default:
                    type = OrganizationLogsType.ContractFail;
                    text = $"Подряд #{contract.Id} провален{(actorUuid == 0 ? " (истёк срок)" : "")}: неустойка {ContractsCore.Money(-money)}{repText}";
                    break;
            }
            ContractAudit.OrgLog(contract.OrganizationId, actorUuid, actorName, type, text);
            NotifyOrganization(contract.OrganizationId, text);
        }

        private static void CheckDeadlines()
        {
            var now = DateTime.Now;
            lock (ContractsCore.Sync)
            {
                foreach (var contract in Contracts.Values.Where(c => c.IsActive).ToList())
                {
                    if (Manager.GetOrganizationData(contract.OrganizationId) == null)
                    {
                        // Организация удалена — подряд закрывается без денег
                        Close(contract, ContractStatus.Cancelled, 0, "Система");
                        continue;
                    }
                    if (contract.DeadlineAt != null && contract.DeadlineAt <= now)
                        Close(contract, ContractStatus.Failed, 0, "Система");
                }
            }
        }

        // ------------------------------------------------------------------ репутация (админ)

        public static bool SetReputation(int orgId, int value, int adminUuid, bool add)
        {
            OrganizationData organizationData;
            int before;
            lock (ContractsCore.Sync)
            {
                organizationData = Manager.GetOrganizationData(orgId);
                if (organizationData == null)
                    return false;
                before = organizationData.Reputation;
                Finance.ChangeReputation(organizationData, add ? value : value - before);
                ContractsRepository.Enqueue(Finance.SaveCommand(organizationData));
            }
            ContractAudit.Write("admin_rep", orgId: orgId, actorUuid: adminUuid, details: new { before, after = organizationData.Reputation });
            ContractAudit.OrgLog(orgId, 0, "Администрация", OrganizationLogsType.ContractComplete, $"Репутация изменена администрацией: {before} → {organizationData.Reputation}");
            return true;
        }

        // ------------------------------------------------------------------ уведомления

        /// <summary>Уведомить онлайн-участников организации (кроме except).</summary>
        public static void NotifyOrganization(int orgId, string text, ExtPlayer except = null)
        {
            foreach (var foreachPlayer in Character.Repository.GetPlayers())
            {
                if (foreachPlayer == except)
                    continue;
                var memberData = foreachPlayer.GetOrganizationMemberData();
                if (memberData == null || memberData.Id != orgId)
                    continue;
                Notify.Send(foreachPlayer, NotifyType.Info, NotifyPosition.BottomCenter, text, 5000);
            }
        }

        private static void NotifyLegalOrganizations(string text)
        {
            foreach (var foreachPlayer in Character.Repository.GetPlayers())
            {
                if (!ContractsCore.IsLegal(foreachPlayer.GetOrganizationData()))
                    continue;
                if (!foreachPlayer.IsOrganizationAccess(RankToAccess.OrganizationContracts, false))
                    continue;
                Notify.Send(foreachPlayer, NotifyType.Info, NotifyPosition.BottomCenter, text, 6000);
            }
        }

        public static string FormatDuration(int minutes) =>
            minutes >= 60 ? $"{minutes / 60} ч {minutes % 60:00} мин" : $"{minutes} мин";
    }
}
