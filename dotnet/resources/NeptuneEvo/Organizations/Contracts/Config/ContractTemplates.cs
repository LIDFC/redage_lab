using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace NeptuneEvo.Organizations.Contracts.Config
{
    public class TemplateMaterial
    {
        [JsonProperty("material")] public string Material { get; set; }
        [JsonProperty("min")] public int Min { get; set; }
        [JsonProperty("max")] public int Max { get; set; }
    }

    /// <summary>
    /// Шаблон подряда. «Косметические» типы (дороги, мосты, здания…) — это просто разные шаблоны:
    /// набор материалов, точка сдачи, награда, репутация, срок и неустойка.
    /// </summary>
    public class ContractTemplate
    {
        [JsonProperty("id")] public string Id { get; set; }
        /// <summary>RoadConstruction / BuildingConstruction / BridgeConstruction / InfrastructureRepair / WarehouseConstruction …</summary>
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("materials")] public List<TemplateMaterial> Materials { get; set; } = new List<TemplateMaterial>();
        [JsonProperty("requiredReputation")] public int RequiredReputation { get; set; }
        /// <summary>Награда = стоимость материалов × rewardFactor (не меньше minRewardFactor из конфига), округление до $500.</summary>
        [JsonProperty("rewardFactor")] public double RewardFactor { get; set; } = 2.5;
        /// <summary>Неустойка в % от награды — считается при генерации и хранится у конкретного контракта.</summary>
        [JsonProperty("penaltyPercent")] public int PenaltyPercent { get; set; } = 15;
        [JsonProperty("reputationReward")] public int ReputationReward { get; set; } = 10;
        [JsonProperty("reputationPenalty")] public int ReputationPenalty { get; set; } = 15;
        [JsonProperty("deadlineMinutes")] public int DeadlineMinutes { get; set; } = 180;
        [JsonProperty("pointName")] public string PointName { get; set; }
        [JsonProperty("pointX")] public float PointX { get; set; }
        [JsonProperty("pointY")] public float PointY { get; set; }
        [JsonProperty("pointZ")] public float PointZ { get; set; }
        /// <summary>Вес при случайном выборе шаблона генератором.</summary>
        [JsonProperty("weight")] public int Weight { get; set; } = 10;
        /// <summary>Выключенный шаблон не генерируется (но уже созданные по нему контракты работают).</summary>
        [JsonProperty("enabled")] public bool Enabled { get; set; } = true;
    }

    /// <summary>Шаблоны подрядов: settings/org_contract_templates.json.</summary>
    public static class ContractTemplates
    {
        private static string FilePath => Path.Combine("settings", "org_contract_templates.json");

        public static List<ContractTemplate> All { get; private set; } = new List<ContractTemplate>();

        public static ContractTemplate Get(string id) =>
            All.FirstOrDefault(t => t.Id == id);

        public static void Load()
        {
            List<ContractTemplate> list = null;
            try
            {
                if (File.Exists(FilePath))
                    list = JsonConvert.DeserializeObject<List<ContractTemplate>>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"Не удалось прочитать {FilePath}: {e.Message}");
            }

            if (list == null || list.Count == 0)
            {
                All = Defaults();
                Save();
            }
            else
                All = list.Where(t => !string.IsNullOrEmpty(t.Id)).ToList();
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory("settings");
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(All, Formatting.Indented));
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"Не удалось сохранить {FilePath}: {e.Message}");
            }
        }

        /// <summary>
        /// Точки сдачи — проверенные уличные места (из точек аирдропов/закладок). Администратор может
        /// перенести точку на свою позицию: /orgc point [id шаблона].
        /// </summary>
        private static List<ContractTemplate> Defaults() => new List<ContractTemplate>
        {
            new ContractTemplate
            {
                Id = "road_lamesa", Type = "RoadConstruction",
                Title = "Строительный подряд: дорожные работы",
                Description = "Доставка материалов для ремонта дорожного полотна в La Mesa.",
                Materials = new List<TemplateMaterial>
                {
                    new TemplateMaterial { Material = "asphalt", Min = 300, Max = 600 },
                    new TemplateMaterial { Material = "concrete", Min = 100, Max = 300 },
                },
                RequiredReputation = 0, RewardFactor = 2.6, PenaltyPercent = 15, ReputationReward = 10, ReputationPenalty = 12, DeadlineMinutes = 150,
                PointName = "La Mesa Road Works", PointX = 852.917f, PointY = -951.6709f, PointZ = 26.2712f, Weight = 12,
            },
            new ContractTemplate
            {
                Id = "sidewalk_vespucci", Type = "InfrastructureRepair",
                Title = "Строительный подряд: ремонт набережной",
                Description = "Доставка материалов для ремонта набережной и тротуаров у каналов Vespucci.",
                Materials = new List<TemplateMaterial>
                {
                    new TemplateMaterial { Material = "brick", Min = 200, Max = 400 },
                    new TemplateMaterial { Material = "concrete", Min = 100, Max = 200 },
                },
                RequiredReputation = 0, RewardFactor = 2.5, PenaltyPercent = 15, ReputationReward = 8, ReputationPenalty = 10, DeadlineMinutes = 120,
                PointName = "Vespucci Canals", PointX = -1034.9161f, PointY = -1067.9562f, PointZ = 3.908407f, Weight = 12,
            },
            new ContractTemplate
            {
                Id = "building_mirror", Type = "BuildingConstruction",
                Title = "Строительный подряд: жилой комплекс",
                Description = "Доставка материалов для строительства жилого комплекса в Mirror Park.",
                Materials = new List<TemplateMaterial>
                {
                    new TemplateMaterial { Material = "concrete", Min = 400, Max = 800 },
                    new TemplateMaterial { Material = "brick", Min = 300, Max = 600 },
                    new TemplateMaterial { Material = "steel", Min = 100, Max = 200 },
                },
                RequiredReputation = 50, RewardFactor = 2.7, PenaltyPercent = 15, ReputationReward = 15, ReputationPenalty = 18, DeadlineMinutes = 240,
                PointName = "Mirror Park Construction Site", PointX = 1075.2537f, PointY = -760.1058f, PointZ = 57.8116f, Weight = 10,
            },
            new ContractTemplate
            {
                Id = "warehouse_port", Type = "WarehouseConstruction",
                Title = "Строительный подряд: складской комплекс",
                Description = "Доставка материалов для нового склада в порту Elysian Island.",
                Materials = new List<TemplateMaterial>
                {
                    new TemplateMaterial { Material = "steel", Min = 150, Max = 300 },
                    new TemplateMaterial { Material = "wood", Min = 200, Max = 500 },
                    new TemplateMaterial { Material = "concrete", Min = 200, Max = 400 },
                },
                RequiredReputation = 25, RewardFactor = 2.6, PenaltyPercent = 15, ReputationReward = 12, ReputationPenalty = 15, DeadlineMinutes = 210,
                PointName = "Elysian Island Port", PointX = 1158.365f, PointY = -3209.473f, PointZ = 5.900023f, Weight = 10,
            },
            new ContractTemplate
            {
                Id = "bridge_canals", Type = "BridgeConstruction",
                Title = "Строительный подряд: ремонт моста",
                Description = "Доставка металлоконструкций и бетона для ремонта моста через каналы.",
                Materials = new List<TemplateMaterial>
                {
                    new TemplateMaterial { Material = "steel", Min = 250, Max = 450 },
                    new TemplateMaterial { Material = "concrete", Min = 400, Max = 800 },
                },
                RequiredReputation = 100, RewardFactor = 2.8, PenaltyPercent = 15, ReputationReward = 20, ReputationPenalty = 25, DeadlineMinutes = 240,
                PointName = "Vespucci Bridge Repair", PointX = -1110.4626f, PointY = -1100.8438f, PointZ = 2.152846f, Weight = 8,
            },
            new ContractTemplate
            {
                Id = "airport_infra", Type = "InfrastructureRepair",
                Title = "Строительный подряд: инфраструктура аэропорта",
                Description = "Доставка материалов для реконструкции подъездных путей аэропорта LSIA.",
                Materials = new List<TemplateMaterial>
                {
                    new TemplateMaterial { Material = "asphalt", Min = 600, Max = 1000 },
                    new TemplateMaterial { Material = "concrete", Min = 400, Max = 800 },
                    new TemplateMaterial { Material = "steel", Min = 100, Max = 250 },
                },
                RequiredReputation = 150, RewardFactor = 3.0, PenaltyPercent = 15, ReputationReward = 25, ReputationPenalty = 30, DeadlineMinutes = 300,
                PointName = "LSIA Road Works", PointX = -956.03723f, PointY = -2570.7585f, PointZ = 13.820424f, Weight = 6,
            },
        };
    }
}
