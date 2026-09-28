using System;
using System.Collections.Generic;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.Organizations.Contracts.Config;
using NeptuneEvo.Organizations.Contracts.Models;

namespace NeptuneEvo.Organizations.Contracts.Generators
{
    /// <summary>
    /// Создание контрактов из шаблонов. Количества — случайные в диапазоне шаблона (кратно половине паллеты),
    /// награда = стоимость материалов × rewardFactor (не меньше minRewardFactor), неустойка = % от награды.
    /// Никаких коэффициентов по времени: после создания значения контракта не меняются.
    /// </summary>
    public static class ContractGenerator
    {
        private static readonly Random Rnd = new Random();

        /// <summary>Ключ слота генерации: '2026-09-28 16:00'.</summary>
        public static string SlotKey(DateTime time) => time.ToString("yyyy-MM-dd HH:mm");

        /// <summary>Последнее наступившее время генерации (сегодня или вчера).</summary>
        public static DateTime? LatestSlot(DateTime now)
        {
            var minutes = ContractsConfig.Current.GetGenerationMinutes();
            DateTime? best = null;
            foreach (var day in new[] { now.Date, now.Date.AddDays(-1) })
            {
                foreach (var minute in minutes)
                {
                    var time = day.AddMinutes(minute);
                    if (time <= now && (best == null || time > best))
                        best = time;
                }
            }
            return best;
        }

        /// <summary>Следующее время генерации (для CEF: «новые подряды в 16:00»).</summary>
        public static DateTime? NextSlot(DateTime now)
        {
            var minutes = ContractsConfig.Current.GetGenerationMinutes();
            foreach (var day in new[] { now.Date, now.Date.AddDays(1) })
            {
                foreach (var minute in minutes)
                {
                    var time = day.AddMinutes(minute);
                    if (time > now)
                        return time;
                }
            }
            return null;
        }

        /// <summary>Выбор шаблонов для набора: сначала «стартовые» (репутация 0), затем взвешенно-случайные без повторов, пока хватает шаблонов.</summary>
        public static List<ContractTemplate> PickTemplates(int count)
        {
            var pool = ContractTemplates.All.Where(t => t.Enabled && IsValid(t)).ToList();
            var result = new List<ContractTemplate>();
            if (pool.Count == 0 || count <= 0)
                return result;

            var starters = pool.Where(t => t.RequiredReputation <= 0).ToList();
            for (var i = 0; i < ContractsConfig.Current.StarterContracts && starters.Count > 0 && result.Count < count; i++)
            {
                var template = Weighted(starters);
                result.Add(template);
                starters.Remove(template);
            }

            while (result.Count < count)
            {
                var candidates = pool.Where(t => !result.Contains(t)).ToList();
                if (candidates.Count == 0)
                    candidates = pool;
                result.Add(Weighted(candidates));
            }
            return result;
        }

        private static ContractTemplate Weighted(List<ContractTemplate> list)
        {
            var total = list.Sum(t => Math.Max(1, t.Weight));
            var roll = Rnd.Next(total);
            foreach (var template in list)
            {
                roll -= Math.Max(1, template.Weight);
                if (roll < 0)
                    return template;
            }
            return list[list.Count - 1];
        }

        public static bool IsValid(ContractTemplate template) =>
            template != null && template.Materials.Count > 0 &&
            template.Materials.All(m => ContractsConfig.Current.GetMaterial(m.Material) != null && m.Min > 0 && m.Max >= m.Min);

        /// <summary>Контракт из шаблона (без Id — его выдаёт менеджер).</summary>
        public static Contract Create(ContractTemplate template, string slot, ContractSource source)
        {
            var config = ContractsConfig.Current;
            var materials = new List<ContractMaterial>();
            long cost = 0;
            foreach (var item in template.Materials)
            {
                var definition = config.GetMaterial(item.Material);
                var step = Math.Max(1, definition.UnitsPerPallet / 2);
                var amount = item.Min + Rnd.Next(0, (item.Max - item.Min) / step + 1) * step;
                amount = Math.Min(Math.Max(amount, item.Min), item.Max);
                materials.Add(new ContractMaterial { Material = item.Material, Required = amount });
                cost += (long)amount * definition.Price;
            }

            var factor = Math.Max(template.RewardFactor, config.MinRewardFactor);
            var reward = RoundTo((long)Math.Ceiling(cost * factor), 500);
            var penalty = template.PenaltyPercent > 0 ? RoundTo(reward * template.PenaltyPercent / 100, 100) : 0;

            return new Contract
            {
                TemplateId = template.Id,
                Type = template.Type,
                Title = template.Title,
                Description = template.Description,
                Status = ContractStatus.Available,
                Reward = (int)Math.Min(int.MaxValue, reward),
                Penalty = (int)Math.Min(int.MaxValue, penalty),
                RequiredReputation = Math.Max(0, template.RequiredReputation),
                ReputationReward = Math.Max(0, template.ReputationReward),
                ReputationPenalty = Math.Max(0, template.ReputationPenalty),
                DeadlineMinutes = Math.Max(10, template.DeadlineMinutes),
                PointName = template.PointName,
                DeliveryPosition = new Vector3(template.PointX, template.PointY, template.PointZ),
                Materials = materials,
                Source = source,
                GenSlot = slot,
                CreatedAt = DateTime.Now,
            };
        }

        /// <summary>Стоимость всех материалов контракта по текущим ценам магазина.</summary>
        public static long MaterialsCost(Contract contract) =>
            contract.Materials.Sum(m => (long)m.Required * (ContractsConfig.Current.GetMaterial(m.Material)?.Price ?? 0));

        /// <summary>Общий вес груза контракта, кг.</summary>
        public static double TotalKg(Contract contract) =>
            contract.Materials.Sum(m => m.Required * (double)(ContractsConfig.Current.GetMaterial(m.Material)?.KgPerUnit ?? 0));

        private static long RoundTo(long value, long step) =>
            Math.Max(step, (value + step / 2) / step * step);
    }
}
