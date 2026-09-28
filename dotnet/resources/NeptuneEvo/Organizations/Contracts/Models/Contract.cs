using System;
using System.Collections.Generic;
using System.Linq;
using GTANetworkAPI;
using Newtonsoft.Json;

namespace NeptuneEvo.Organizations.Contracts.Models
{
    public enum ContractStatus : byte
    {
        Available = 0,
        Active = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4,
        /// <summary>Никто не взял до следующей генерации.</summary>
        Expired = 5,
        /// <summary>Удалён администратором.</summary>
        Deleted = 6,
    }

    public enum ContractSource : byte
    {
        Generator = 0,
        Admin = 1,
    }

    public class ContractMaterial
    {
        [JsonProperty("m")] public string Material { get; set; }
        [JsonProperty("r")] public int Required { get; set; }
        /// <summary>Сколько уже сдано на точке.</summary>
        [JsonProperty("d")] public int Delivered { get; set; }
        /// <summary>Сколько куплено организацией по этому контракту (ограничивает повторную закупку).</summary>
        [JsonProperty("b")] public int Purchased { get; set; }
    }

    /// <summary>
    /// Подряд. Живёт в памяти (ContractsManager) и меняется только под ContractsCore.Sync;
    /// каждое изменение сразу уходит в очередь записи ContractsRepository.
    /// </summary>
    public class Contract
    {
        public int Id { get; set; }
        public string TemplateId { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public ContractStatus Status { get; set; }

        public int OrganizationId { get; set; }
        public int AcceptedByUuid { get; set; }
        public string AcceptedByName { get; set; } = "";

        public int Reward { get; set; }
        public int Penalty { get; set; }
        public int RequiredReputation { get; set; }
        public int ReputationReward { get; set; }
        public int ReputationPenalty { get; set; }

        /// <summary>Срок на выполнение; отсчёт начинается с момента принятия.</summary>
        public int DeadlineMinutes { get; set; }
        public DateTime? DeadlineAt { get; set; }

        public string PointName { get; set; }
        public Vector3 DeliveryPosition { get; set; }

        public List<ContractMaterial> Materials { get; set; } = new List<ContractMaterial>();

        public ContractSource Source { get; set; }
        /// <summary>Ключ генерации ('2026-09-28 16:00'), к которой относится контракт.</summary>
        public string GenSlot { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime? FinishedAt { get; set; }

        public bool IsActive => Status == ContractStatus.Active;

        public bool IsAllDelivered => Materials.Count > 0 && Materials.All(m => m.Delivered >= m.Required);

        public int ProgressPercent
        {
            get
            {
                var required = Materials.Sum(m => m.Required);
                if (required <= 0)
                    return 0;
                return (int)Math.Floor(Materials.Sum(m => Math.Min(m.Delivered, m.Required)) * 100.0 / required);
            }
        }

        public ContractMaterial GetMaterial(string material) =>
            Materials.FirstOrDefault(m => m.Material == material);
    }
}
