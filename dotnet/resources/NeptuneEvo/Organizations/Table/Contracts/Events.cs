using System;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Handles;
using NeptuneEvo.Organizations.Contracts;
using NeptuneEvo.Organizations.Contracts.Cargo;
using NeptuneEvo.Organizations.Contracts.Config;
using NeptuneEvo.Organizations.Contracts.Generators;
using NeptuneEvo.Organizations.Contracts.Models;
using NeptuneEvo.Organizations.Player;
using NeptuneEvo.Table.Models;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Organizations.Table.Contracts
{
    /// <summary>
    /// Раздел «Подряды» в меню организации (планшет): список общих подрядов, свои активные, принять/отменить,
    /// маршрут до точки сдачи и до склада. Все решения — на сервере (ContractsManager), CEF только показывает.
    /// </summary>
    public class Events : Script
    {
        [RemoteEvent("server.org.main.contracts.load")]
        public static void OnLoad(ExtPlayer player)
        {
            try
            {
                if (!player.IsCharacterData() || !ContractsManager.Ready || !ContractsCore.AntiSpam(player, 300))
                    return;
                Send(player, null, true);
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"contracts.load Exception: {e}");
            }
        }

        [RemoteEvent("server.org.main.contracts.action")]
        public static void OnAction(ExtPlayer player, string action, int contractId)
        {
            try
            {
                if (!player.IsCharacterData() || !ContractsManager.Ready || !ContractsCore.AntiSpam(player, 700))
                    return;
                var organizationData = player.GetOrganizationData();
                if (organizationData == null)
                    return;

                switch (action)
                {
                    case "accept":
                        {
                            var ok = ContractsManager.Accept(player, contractId);
                            Send(player, ok ? $"Подряд #{contractId} принят" : null, ok);
                        }
                        return;
                    case "cancel":
                        {
                            var ok = ContractsManager.Cancel(player, contractId);
                            Send(player, ok ? $"Подряд #{contractId} отменён" : null, ok);
                        }
                        return;
                    case "route":
                        {
                            var contract = ContractsManager.Get(contractId);
                            if (contract == null || contract.OrganizationId != organizationData.Id)
                                return;
                            Trigger.ClientEvent(player, "createWaypoint", contract.DeliveryPosition.X, contract.DeliveryPosition.Y);
                            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"Метка: {contract.PointName}", 3000);
                        }
                        return;
                    case "shop":
                        {
                            var contract = ContractsManager.Get(contractId);
                            if (contract == null || contract.OrganizationId != organizationData.Id)
                                return;
                            var missing = contract.Materials.Where(m => m.Purchased < m.Required).Select(m => m.Material).ToList();
                            if (missing.Count == 0)
                                missing = contract.Materials.Select(m => m.Material).ToList();
                            var shop = ContractsConfig.Current.Shops
                                .Where(s => s.BusinessId > 0 && BusinessManager.BizList.ContainsKey(s.BusinessId) && missing.Any(s.Sells))
                                .OrderByDescending(s => missing.Count(s.Sells))
                                .ThenBy(s => BusinessManager.BizList[s.BusinessId].EnterPoint.DistanceTo(player.Position))
                                .FirstOrDefault();
                            if (shop == null)
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Склады стройматериалов не найдены", 3000);
                                return;
                            }
                            var point = BusinessManager.BizList[shop.BusinessId].EnterPoint;
                            Trigger.ClientEvent(player, "createWaypoint", point.X, point.Y);
                            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"Метка: {shop.Name}", 3000);
                        }
                        return;
                }
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"contracts.action Exception: {e}");
            }
        }

        /// <summary>Отправить состояние раздела (message — результат действия для строки статуса).</summary>
        public static void Send(ExtPlayer player, string message, bool ok)
        {
            var memberData = player.GetOrganizationMemberData();
            var organizationData = player.GetOrganizationData();
            if (memberData == null || organizationData == null)
                return;

            var config = ContractsConfig.Current;
            var uuid = player.GetUUID();
            var now = DateTime.Now;
            var active = ContractsManager.GetActive(organizationData.Id).Count;

            var contracts = ContractsManager.GetVisible(organizationData.Id).Select(c =>
            {
                var mine = c.IsActive && c.OrganizationId == organizationData.Id;
                var takenBy = c.IsActive && !mine ? Manager.GetOrganizationData(c.OrganizationId)?.Name ?? "другая организация" : "";
                return new
                {
                    id = c.Id,
                    type = c.Type,
                    title = c.Title,
                    description = c.Description,
                    status = c.Status == ContractStatus.Available ? "available" : mine ? "mine" : "taken",
                    takenBy,
                    acceptedBy = mine ? c.AcceptedByName : "",
                    isAcceptor = mine && c.AcceptedByUuid == uuid,
                    reward = c.Reward,
                    penalty = c.Penalty,
                    requiredRep = c.RequiredReputation,
                    repReward = c.ReputationReward,
                    repPenalty = c.ReputationPenalty,
                    deadlineMinutes = c.DeadlineMinutes,
                    deadlineLeft = mine && c.DeadlineAt.HasValue ? (long)Math.Max(0, (c.DeadlineAt.Value - now).TotalSeconds) : 0,
                    point = c.PointName,
                    progress = c.ProgressPercent,
                    totalKg = Math.Round(ContractGenerator.TotalKg(c)),
                    cost = ContractGenerator.MaterialsCost(c),
                    materials = c.Materials.Select(m =>
                    {
                        var def = config.GetMaterial(m.Material);
                        return new
                        {
                            id = m.Material,
                            name = def?.Name ?? m.Material,
                            icon = def?.Icon ?? m.Material,
                            required = m.Required,
                            delivered = m.Delivered,
                            purchased = m.Purchased,
                            kg = def?.KgPerUnit ?? 0,
                            shops = config.Shops.Where(s => s.BusinessId > 0 && s.Sells(m.Material)).Select(s => s.Name),
                        };
                    }),
                };
            });

            var json = JsonConvert.SerializeObject(new
            {
                legal = ContractsCore.IsLegal(organizationData),
                canManage = player.IsOrganizationAccess(RankToAccess.OrganizationContracts, false),
                org = organizationData.Name,
                reputation = organizationData.Reputation,
                money = organizationData.Money,
                active,
                maxActive = config.MaxActivePerOrganization,
                nextGen = (long)Math.Max(0, ((ContractGenerator.NextSlot(now) ?? now) - now).TotalSeconds),
                vehicles = config.Vehicles.GroupBy(v => v.Name).Select(g => g.First()).Select(v => new { name = v.Name, slots = v.Slots, kg = v.MaxKg }),
                contracts,
                message,
                ok,
            });
            Trigger.ClientEvent(player, "client.org.main.contractsData", json);
        }
    }
}
