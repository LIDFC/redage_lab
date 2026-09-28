using System;
using System.Collections.Generic;
using System.Linq;
using GTANetworkAPI;
using MySqlConnector;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Handles;
using NeptuneEvo.Organizations.Contracts.Cargo;
using NeptuneEvo.Organizations.Contracts.Config;
using NeptuneEvo.Organizations.Contracts.Logs;
using NeptuneEvo.Organizations.Models;
using NeptuneEvo.Organizations.Player;
using NeptuneEvo.Table.Models;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Organizations.Contracts.Methods
{
    /// <summary>
    /// Государственный магазин «Строительные материалы» (бизнес типа 16, создаётся штатным /createbusiness).
    /// Покупают только сотрудники законных организаций с правом «Строительные подряды» и только под активный
    /// подряд своей организации — не больше, чем ещё не куплено. Платит бюджет организации, склад бесконечный.
    /// Купленное выкладывается паллетами у точки разгрузки бизнеса (UnloadPoint), иначе — у входа.
    /// </summary>
    public class MaterialShop : Script
    {
        public const int BusinessType = 16;
        private const float MaxDistance = 8f;

        /// <summary>Какой магазин открыт у игрока (uuid → id бизнеса).</summary>
        private static readonly Dictionary<int, int> Opened = new Dictionary<int, int>();

        public static void Open(ExtPlayer player, Business biz)
        {
            if (!ContractsManager.Ready || biz == null)
                return;
            if (!CanUse(player, out var organizationData, out var error))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, error, 4000);
                return;
            }
            lock (ContractsCore.Sync)
                Opened[player.GetUUID()] = biz.ID;
            Trigger.ClientEvent(player, "client.orgcontracts.shop.open", BuildJson(organizationData));
        }

        private static bool CanUse(ExtPlayer player, out OrganizationData organizationData, out string error)
        {
            organizationData = player.GetOrganizationData();
            error = null;
            if (organizationData == null)
                error = "Склад обслуживает только организации-подрядчиков";
            else if (!ContractsCore.IsLegal(organizationData))
                error = "Склад работает только с законными организациями";
            else if (!player.IsOrganizationAccess(RankToAccess.OrganizationContracts, false))
                error = "Нужно право организации «Строительные подряды»";
            return error == null;
        }

        private static string BuildJson(OrganizationData organizationData)
        {
            var config = ContractsConfig.Current;
            var contracts = ContractsManager.GetActive(organizationData.Id).Select(c => new
            {
                id = c.Id,
                title = c.Title,
                point = c.PointName,
                deadline = c.DeadlineAt.HasValue ? (long)Math.Max(0, (c.DeadlineAt.Value - DateTime.Now).TotalSeconds) : 0,
                materials = c.Materials.Select(m =>
                {
                    var def = config.GetMaterial(m.Material);
                    return new
                    {
                        id = m.Material,
                        name = def?.Name ?? m.Material,
                        icon = def?.Icon ?? "",
                        price = def?.Price ?? 0,
                        kg = def?.KgPerUnit ?? 0,
                        pallet = def?.UnitsPerPallet ?? 1,
                        required = m.Required,
                        purchased = m.Purchased,
                        delivered = m.Delivered,
                        left = Math.Max(0, m.Required - m.Purchased),
                    };
                }),
            });
            return JsonConvert.SerializeObject(new
            {
                org = organizationData.Name,
                money = organizationData.Money,
                contracts,
            });
        }

        [RemoteEvent("server.orgcontracts.shop.close")]
        public static void OnClose(ExtPlayer player)
        {
            lock (ContractsCore.Sync)
                Opened.Remove(player.GetUUID());
        }

        [RemoteEvent("server.orgcontracts.shop.buy")]
        public static void OnBuy(ExtPlayer player, int contractId, string materialId, int units)
        {
            try
            {
                if (!player.IsCharacterData() || !ContractsManager.Ready || !ContractsCore.AntiSpam(player, 700))
                    return;
                if (!CanUse(player, out var organizationData, out var error))
                {
                    Result(player, false, error, organizationData);
                    return;
                }

                var uuid = player.GetUUID();
                string message;
                long cost = 0;
                List<CargoUnit> created = null;
                ContractMaterialInfo info = default;

                lock (ContractsCore.Sync)
                {
                    message = Validate(player, uuid, organizationData, contractId, materialId, units, out var contract, out var material, out var definition, out var biz);
                    if (message == null)
                    {
                        cost = (long)units * definition.Price;
                        Finance.ChangeMoney(organizationData, -cost);
                        material.Purchased += units;

                        var point = biz.UnloadPoint != null && biz.UnloadPoint.DistanceTo(biz.EnterPoint) > 0.5f && biz.UnloadPoint.DistanceTo(biz.EnterPoint) < 80f
                            ? biz.UnloadPoint : biz.EnterPoint;
                        var commands = new List<MySqlCommand>
                        {
                            ContractsRepository.SaveCommand(contract),
                            Finance.SaveCommand(organizationData),
                        };
                        commands.AddRange(CargoManager.CreateStacks(CargoOwner.Organization, organizationData.Id, contract.Id, materialId, units,
                            point, 0, organizationData.Name, out created));
                        ContractsRepository.EnqueueTransaction(commands.ToArray());
                        info = new ContractMaterialInfo { Name = definition.Name, ContractId = contract.Id, Point = point, BizId = biz.ID };
                    }
                }

                if (message != null)
                {
                    Result(player, false, message, organizationData);
                    ContractAudit.Write("buy", contractId, organizationData.Id, uuid, (long)Math.Max(0, units), "denied", new { materialId, units, message });
                    return;
                }

                var text = $"Организация купила: {info.Name} ×{units} — {ContractsCore.Money(cost)}";
                ContractAudit.OrgLog(organizationData.Id, uuid, player.Name, OrganizationLogsType.ContractBuy, $"{text} (подряд #{info.ContractId})");
                ContractAudit.Write("buy", info.ContractId, organizationData.Id, uuid, cost,
                    details: new { materialId, units, pallets = created.Select(u => u.Id), biz = info.BizId, orgMoney = organizationData.Money });
                GameLog.Money($"org({organizationData.Id})", $"biz({info.BizId})", cost, $"orgContractBuy({info.ContractId},{materialId}x{units})");

                Trigger.ClientEvent(player, "createWaypoint", info.Point.X, info.Point.Y);
                Result(player, true, $"{text}. Паллет: {created.Count} — они у площадки погрузки", organizationData);
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"OnBuy Exception: {e}");
            }
        }

        private struct ContractMaterialInfo
        {
            public string Name;
            public int ContractId;
            public Vector3 Point;
            public int BizId;
        }

        /// <summary>Все проверки покупки (под Sync). null — можно покупать.</summary>
        private static string Validate(ExtPlayer player, int uuid, OrganizationData organizationData, int contractId, string materialId, int units,
            out Models.Contract contract, out Models.ContractMaterial material, out MaterialDefinition definition, out Business biz)
        {
            contract = null;
            material = null;
            definition = null;
            biz = null;

            if (!Opened.TryGetValue(uuid, out var bizId) || !BusinessManager.BizList.TryGetValue(bizId, out biz) || biz.Type != BusinessType)
                return "Откройте склад стройматериалов";
            if (player.Dimension != 0 || player.Position.DistanceTo(biz.EnterPoint) > MaxDistance)
                return "Вы отошли от склада";

            contract = ContractsManager.Get(contractId);
            if (contract == null || !contract.IsActive || contract.OrganizationId != organizationData.Id)
                return "Подряд не найден или уже закрыт";
            material = contract.GetMaterial(materialId);
            definition = ContractsConfig.Current.GetMaterial(materialId);
            if (material == null || definition == null)
                return "Этот материал не нужен по подряду";
            if (units <= 0)
                return "Укажите количество";
            var left = material.Required - material.Purchased;
            if (left <= 0)
                return $"{definition.Name}: уже куплено всё, что нужно по подряду";
            if (units > left)
                return $"{definition.Name}: по подряду осталось купить только {left}";
            if (Finance.IsInDebt(organizationData))
                return "Бюджет организации в минусе";
            var cost = (long)units * definition.Price;
            if (organizationData.Money < cost)
                return $"В бюджете организации {ContractsCore.Money(organizationData.Money)}, нужно {ContractsCore.Money(cost)}";
            return null;
        }

        private static void Result(ExtPlayer player, bool ok, string message, OrganizationData organizationData)
        {
            Notify.Send(player, ok ? NotifyType.Success : NotifyType.Error, NotifyPosition.BottomCenter, message, 4000);
            if (organizationData != null)
                Trigger.ClientEvent(player, "client.orgcontracts.shop.update", ok, message, BuildJson(organizationData));
        }
    }
}
