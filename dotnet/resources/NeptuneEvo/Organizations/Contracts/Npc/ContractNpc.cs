using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.Character;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Organizations.Contracts.Config;
using NeptuneEvo.Organizations.Contracts.Logs;
using NeptuneEvo.Organizations.Contracts.Models;
using NeptuneEvo.Organizations.Models;
using NeptuneEvo.Organizations.Player;
using NeptuneEvo.Table.Models;
using Redage.SDK;

namespace NeptuneEvo.Organizations.Contracts.Npc
{
    /// <summary>
    /// NPC-бонус: прораб на стройке раз в сутки на ОРГАНИЗАЦИЮ (не на игрока) с шансом (по умолчанию 25%)
    /// передаёт один из существующих свободных подрядов подходящего ему типа. Своих заказов не создаёт.
    /// Попытка тратится только когда подходящий подряд есть и организация может его принять; хранится в org_contract_npc.
    /// </summary>
    public class ContractNpc : Script
    {
        private static readonly Random Rnd = new Random();
        /// <summary>ped.Value → NPC.</summary>
        private static readonly Dictionary<int, ContractNpcDefinition> Peds = new Dictionary<int, ContractNpcDefinition>();
        /// <summary>Организации, уже использовавшие попытку сегодня (день → orgId).</summary>
        private static readonly HashSet<int> UsedToday = new HashSet<int>();
        private static DateTime _day = DateTime.Today;
        /// <summary>С каким NPC игрок начал разговор (uuid → NPC), для подтверждения в диалоге.</summary>
        private static readonly Dictionary<int, ContractNpcDefinition> Talking = new Dictionary<int, ContractNpcDefinition>();

        public static void Init()
        {
            lock (ContractsCore.Sync)
            {
                using var data = ContractsRepository.Read("SELECT `org_id` FROM `org_contract_npc` WHERE `day` = @day", ("@day", DateTime.Today));
                if (data != null)
                    foreach (DataRow row in data.Rows)
                        UsedToday.Add(Convert.ToInt32(row["org_id"]));
            }

            foreach (var npc in ContractsConfig.Current.Npcs)
            {
                if (npc.Position == null || string.IsNullOrEmpty(npc.Model))
                    continue;
                var ped = PedSystem.Repository.CreateQuest(npc.Model, npc.Position, npc.Heading, 0, null, ColShapeEnums.ContractNpc,
                    $"~y~NPC~w~ {npc.Name}\nСтроительные подряды", false);
                Peds[ped.Value] = npc;
            }
        }

        private static void RollDay()
        {
            if (_day == DateTime.Today)
                return;
            _day = DateTime.Today;
            UsedToday.Clear();
        }

        [Interaction(ColShapeEnums.ContractNpc)]
        public static void OnTalk(ExtPlayer player, int index)
        {
            try
            {
                if (!player.IsCharacterData() || !ContractsManager.Ready || !ContractsCore.AntiSpam(player, 1000))
                    return;
                if (!Peds.TryGetValue(index, out var npc))
                    return;

                var error = Check(player, npc, out _);
                if (error != null)
                {
                    Say(player, npc, error);
                    return;
                }
                lock (ContractsCore.Sync)
                    Talking[player.GetUUID()] = npc;
                Trigger.ClientEvent(player, "openDialog", "ORG_CONTRACT_NPC",
                    $"{npc.Name}: «{npc.Phrase}» Попросить подряд? Попытка одна в сутки на всю организацию, шанс {ContractsConfig.Current.NpcChance}%.");
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"Npc OnTalk Exception: {e}");
            }
        }

        /// <summary>Ответ «Да» в диалоге (Main.dialogCallback).</summary>
        public static void OnConfirm(ExtPlayer player)
        {
            try
            {
                if (!player.IsCharacterData() || !ContractsManager.Ready)
                    return;
                var uuid = player.GetUUID();
                ContractNpcDefinition npc;
                string reply;
                Contract given = null;
                var orgId = player.GetOrganizationMemberData()?.Id ?? 0;

                lock (ContractsCore.Sync)
                {
                    if (!Talking.Remove(uuid, out npc))
                        return;
                    if (player.Position.DistanceTo(npc.Position) > 6f)
                        return;

                    reply = Check(player, npc, out var candidates);
                    if (reply == null)
                    {
                        // Попытка организации тратится здесь — атомарно под Sync, повторно не пройдёт
                        UsedToday.Add(orgId);
                        var success = Rnd.Next(100) < ContractsConfig.Current.NpcChance;
                        ContractsRepository.Enqueue("INSERT IGNORE INTO `org_contract_npc` (`org_id`, `day`, `uuid`, `result`) VALUES (@org, @day, @uuid, @result)",
                            ("@org", orgId), ("@day", DateTime.Today), ("@uuid", uuid), ("@result", success ? "success" : "fail"));

                        if (success)
                        {
                            var contract = candidates[Rnd.Next(candidates.Count)];
                            if (ContractsManager.Accept(player, contract.Id))
                            {
                                given = contract;
                                reply = $"Договорился! Подряд #{contract.Id} «{contract.Title}» теперь ваш. Детали — в планшете.";
                            }
                            else
                                reply = "Эх, пока я звонил, заказ уже увели. Приходите завтра.";
                        }
                        else
                            reply = "Нет, друг не отвечает… Сегодня ничего не выйдет, заходите завтра.";

                        ContractAudit.Write("npc", given?.Id ?? 0, orgId, uuid, result: given != null ? "ok" : "fail", details: new { npc = npc.Id });
                        ContractAudit.OrgLog(orgId, uuid, player.Name, OrganizationLogsType.ContractAccept,
                            given != null ? $"Получил подряд #{given.Id} через {npc.Name}" : $"Попросил подряд у {npc.Name} — не повезло (попытка на сегодня использована)");
                    }
                }
                Say(player, npc, reply);
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"Npc OnConfirm Exception: {e}");
            }
        }

        /// <summary>Все условия до траты попытки. candidates — подходящие свободные подряды.</summary>
        private static string Check(ExtPlayer player, ContractNpcDefinition npc, out List<Contract> candidates)
        {
            candidates = new List<Contract>();
            var organizationData = player.GetOrganizationData();
            if (organizationData == null)
                return "Ты кто такой? Я с частниками не работаю — только с организациями.";
            if (!ContractsCore.IsLegal(organizationData))
                return "С такими, как вы, я дел не веду.";
            if (!player.IsOrganizationAccess(RankToAccess.OrganizationContracts, false))
                return "Пусть придёт тот, кто у вас подрядами занимается.";

            lock (ContractsCore.Sync)
            {
                RollDay();
                if (UsedToday.Contains(organizationData.Id))
                    return "Я уже говорил с вашими сегодня. Приходите завтра.";
                if (Methods.Finance.IsInDebt(organizationData))
                    return "У вас долги, какие вам подряды?";
                if (ContractsManager.GetActive(organizationData.Id).Count >= ContractsConfig.Current.MaxActivePerOrganization)
                    return "У вас и так работы полно — сначала закройте текущие подряды.";

                candidates = ContractsManager.GetAll()
                    .Where(c => c.Status == ContractStatus.Available && npc.Types.Contains(c.Type) && c.RequiredReputation <= organizationData.Reputation)
                    .ToList();
                if (candidates.Count == 0)
                    return "Сейчас подходящих заказов по моей части нет. Загляните после следующего набора.";
            }
            return null;
        }

        private static void Say(ExtPlayer player, ContractNpcDefinition npc, string text) =>
            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"{npc.Name}: {text}", 6000);
    }
}
