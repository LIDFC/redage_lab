using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GTANetworkAPI;
using Newtonsoft.Json;

namespace NeptuneEvo.Organizations.Contracts.Config
{
    /// <summary>Материал для подрядов: описан данными, а не ItemId (груз учитывается модулем Cargo, в инвентарь не попадает).</summary>
    public class MaterialDefinition
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        /// <summary>Иконка для CEF: имя файла в interface/.../contracts/icons или emoji-заглушка.</summary>
        [JsonProperty("icon")] public string Icon { get; set; }
        [JsonProperty("kgPerUnit")] public float KgPerUnit { get; set; }
        /// <summary>Сколько единиц в одной паллете (один физический объект).</summary>
        [JsonProperty("unitsPerPallet")] public int UnitsPerPallet { get; set; }
        /// <summary>GTA-модель паллеты на земле.</summary>
        [JsonProperty("prop")] public string Prop { get; set; }
        /// <summary>Цена за единицу в государственном магазине стройматериалов.</summary>
        [JsonProperty("price")] public int Price { get; set; }
    }

    /// <summary>Грузовик, в который можно грузить паллеты. Машина при этом обязательно организационная.</summary>
    public class CargoVehicleDefinition
    {
        [JsonProperty("model")] public string Model { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        /// <summary>Сколько паллет помещается (слоты кузова).</summary>
        [JsonProperty("slots")] public int Slots { get; set; }
        [JsonProperty("maxKg")] public int MaxKg { get; set; }
    }

    /// <summary>
    /// Государственный склад стройматериалов (бизнес типа 16) и его ассортимент.
    /// Точки — «в полный рост» (как позиция игрока); при старте сервер сам создаёт бизнес, если его ещё нет.
    /// </summary>
    public class MaterialShopSpot
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("enter")] public Vector3 Enter { get; set; }
        /// <summary>Площадка погрузки: здесь появляются купленные паллеты.</summary>
        [JsonProperty("unload")] public Vector3 Unload { get; set; }
        /// <summary>Какие материалы продаёт склад (id из materials). Пусто — все.</summary>
        [JsonProperty("materials")] public List<string> Materials { get; set; } = new List<string>();
        /// <summary>Id созданного бизнеса (заполняется сервером).</summary>
        [JsonProperty("businessId")] public int BusinessId { get; set; }
        /// <summary>Создавать бизнес автоматически, если его нет.</summary>
        [JsonProperty("autoCreate")] public bool AutoCreate { get; set; } = true;

        public bool Sells(string material) => Materials == null || Materials.Count == 0 || Materials.Contains(material);
    }

    /// <summary>NPC на стройке: раз в сутки на организацию с шансом передаёт существующий подряд подходящего типа.</summary>
    public class ContractNpcDefinition
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("model")] public string Model { get; set; }
        [JsonProperty("position")] public Vector3 Position { get; set; }
        [JsonProperty("heading")] public float Heading { get; set; }
        /// <summary>Какие типы подрядов (ContractType шаблонов) он может передать.</summary>
        [JsonProperty("types")] public List<string> Types { get; set; } = new List<string>();
        [JsonProperty("phrase")] public string Phrase { get; set; }
    }

    /// <summary>
    /// Настройки строительных подрядов: settings/org_contracts.json. Если файла нет — создаётся со значениями по умолчанию.
    /// Админ меняет значения командой /orgc cfg, файл перезаписывается.
    /// </summary>
    public class ContractsConfig
    {
        private static string FilePath => Path.Combine("settings", "org_contracts.json");

        public static ContractsConfig Current { get; private set; } = new ContractsConfig();

        /// <summary>Время генерации общего набора (по времени сервера), формат HH:mm.</summary>
        [JsonProperty("generationTimes")] public List<string> GenerationTimes { get; set; } = new List<string> { "08:00", "16:00", "00:00" };
        /// <summary>Сколько контрактов создаётся за одну генерацию.</summary>
        [JsonProperty("contractsPerGeneration")] public int ContractsPerGeneration { get; set; } = 5;
        /// <summary>Сколько из них гарантированно доступны новичкам (RequiredReputation = 0), если такие шаблоны есть.</summary>
        [JsonProperty("starterContracts")] public int StarterContracts { get; set; } = 2;
        /// <summary>Максимум одновременно активных контрактов у одной организации.</summary>
        [JsonProperty("maxActivePerOrganization")] public int MaxActivePerOrganization { get; set; } = 2;
        /// <summary>Награда должна быть минимум во столько раз больше стоимости материалов (защита экономики шаблонов).</summary>
        [JsonProperty("minRewardFactor")] public double MinRewardFactor { get; set; } = 1.5;

        /// <summary>Шанс NPC-бонуса, % (попытка — раз в сутки на организацию).</summary>
        [JsonProperty("npcChance")] public int NpcChance { get; set; } = 25;

        [JsonProperty("materials")] public List<MaterialDefinition> Materials { get; set; } = new List<MaterialDefinition>();
        [JsonProperty("vehicles")] public List<CargoVehicleDefinition> Vehicles { get; set; } = new List<CargoVehicleDefinition>();
        [JsonProperty("shops")] public List<MaterialShopSpot> Shops { get; set; } = new List<MaterialShopSpot>();
        [JsonProperty("npcs")] public List<ContractNpcDefinition> Npcs { get; set; } = new List<ContractNpcDefinition>();

        public MaterialDefinition GetMaterial(string id) =>
            Materials.FirstOrDefault(m => m.Id == id);

        /// <summary>Отсортированные минуты суток, в которые идёт генерация.</summary>
        public List<int> GetGenerationMinutes()
        {
            var result = new List<int>();
            foreach (var time in GenerationTimes ?? new List<string>())
            {
                if (DateTime.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    result.Add(parsed.Hour * 60 + parsed.Minute);
            }
            return result.Distinct().OrderBy(m => m).ToList();
        }

        public static void Load()
        {
            ContractsConfig config = null;
            try
            {
                if (File.Exists(FilePath))
                    config = JsonConvert.DeserializeObject<ContractsConfig>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"Не удалось прочитать {FilePath}: {e.Message}");
            }

            var save = false;
            if (config == null)
            {
                config = new ContractsConfig();
                save = true;
            }
            if (config.Materials == null || config.Materials.Count == 0)
            {
                config.Materials = DefaultMaterials();
                save = true;
            }
            if (config.Vehicles == null || config.Vehicles.Count == 0)
            {
                config.Vehicles = DefaultVehicles();
                save = true;
            }
            if (config.Shops == null || config.Shops.Count == 0)
            {
                config.Shops = DefaultShops();
                save = true;
            }
            if (config.Npcs == null || config.Npcs.Count == 0)
            {
                config.Npcs = DefaultNpcs();
                save = true;
            }
            if (config.GetGenerationMinutes().Count == 0)
            {
                config.GenerationTimes = new List<string> { "08:00", "16:00", "00:00" };
                save = true;
            }

            Current = config;
            if (save)
                Save();
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory("settings");
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(Current, Formatting.Indented));
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"Не удалось сохранить {FilePath}: {e.Message}");
            }
        }

        private static List<MaterialDefinition> DefaultMaterials() => new List<MaterialDefinition>
        {
            new MaterialDefinition { Id = "concrete", Name = "Бетон", Icon = "concrete", KgPerUnit = 2f, UnitsPerPallet = 100, Prop = "prop_conc_sacks_02a", Price = 40 },
            new MaterialDefinition { Id = "brick", Name = "Кирпич", Icon = "brick", KgPerUnit = 2f, UnitsPerPallet = 100, Prop = "prop_conc_blocks01a", Price = 30 },
            new MaterialDefinition { Id = "steel", Name = "Металлоконструкции", Icon = "steel", KgPerUnit = 4f, UnitsPerPallet = 50, Prop = "prop_pipes_01a", Price = 90 },
            new MaterialDefinition { Id = "asphalt", Name = "Асфальтовая смесь", Icon = "asphalt", KgPerUnit = 3f, UnitsPerPallet = 100, Prop = "prop_barrel_pile_02", Price = 35 },
            new MaterialDefinition { Id = "wood", Name = "Пиломатериалы", Icon = "wood", KgPerUnit = 1.5f, UnitsPerPallet = 100, Prop = "prop_woodpile_01a", Price = 25 },
        };

        /// <summary>
        /// Склады в промзонах, рядом с точками сдачи подрядов (проверенные уличные места).
        /// Ассортимент разнесён: для большинства подрядов нужно заехать на два склада.
        /// </summary>
        private static List<MaterialShopSpot> DefaultShops() => new List<MaterialShopSpot>
        {
            new MaterialShopSpot
            {
                Id = "lamesa", Name = "Стройбаза La Mesa",
                Enter = new Vector3(863.8128, -868.2756, 25.62753), Unload = new Vector3(881.6852, -880.0532, 27.724),
                Materials = new List<string> { "concrete", "brick", "wood" },
            },
            new MaterialShopSpot
            {
                Id = "elysian", Name = "Металлобаза Elysian Island",
                Enter = new Vector3(1150.489, -3282.368, 5.900809), Unload = new Vector3(1164.095, -3309.951, 5.924438),
                Materials = new List<string> { "steel", "wood", "concrete" },
            },
            new MaterialShopSpot
            {
                Id = "lsia", Name = "Асфальтобетонный завод LSIA",
                Enter = new Vector3(-879.1289, -2523.5586, 14.857651), Unload = new Vector3(-841.52, -2500.98, 13.830637),
                Materials = new List<string> { "asphalt", "concrete" },
            },
        };

        private static List<ContractNpcDefinition> DefaultNpcs() => new List<ContractNpcDefinition>
        {
            new ContractNpcDefinition
            {
                Id = "foreman_mirror", Name = "Прораб Виктор", Model = "s_m_y_construct_01",
                Position = new Vector3(1070.7666, -712.1477, 58.49874), Heading = 180f,
                Types = new List<string> { "BuildingConstruction", "WarehouseConstruction" },
                Phrase = "Оооо, у меня как раз друг на стройке бригадир и ищет, кто ему кое-что привезёт…",
            },
            new ContractNpcDefinition
            {
                Id = "roadmaster_lamesa", Name = "Дорожный мастер Грег", Model = "s_m_y_construct_02",
                Position = new Vector3(836.23175, -875.3281, 25.22759), Heading = 90f,
                Types = new List<string> { "RoadConstruction", "InfrastructureRepair", "BridgeConstruction" },
                Phrase = "Слушай, у нас тут дорожники зашиваются — им срочно нужен подрядчик с грузовиками…",
            },
        };

        private static List<CargoVehicleDefinition> DefaultVehicles() => new List<CargoVehicleDefinition>
        {
            new CargoVehicleDefinition { Model = "bison", Name = "Bravado Bison (пикап)", Slots = 2, MaxKg = 500 },
            new CargoVehicleDefinition { Model = "rumpo", Name = "Bravado Rumpo (фургон)", Slots = 3, MaxKg = 700 },
            new CargoVehicleDefinition { Model = "speedo", Name = "Vapid Speedo (фургон)", Slots = 3, MaxKg = 700 },
            new CargoVehicleDefinition { Model = "boxville", Name = "Brute Boxville", Slots = 5, MaxKg = 1200 },
            new CargoVehicleDefinition { Model = "mule", Name = "Maibatsu Mule", Slots = 8, MaxKg = 2000 },
            new CargoVehicleDefinition { Model = "mule3", Name = "Maibatsu Mule (борт)", Slots = 8, MaxKg = 2000 },
            new CargoVehicleDefinition { Model = "benson", Name = "Vapid Benson", Slots = 10, MaxKg = 3000 },
            new CargoVehicleDefinition { Model = "flatbed", Name = "MTL Flatbed", Slots = 10, MaxKg = 3500 },
            new CargoVehicleDefinition { Model = "pounder", Name = "MTL Pounder", Slots = 14, MaxKg = 5000 },
        };
    }
}
