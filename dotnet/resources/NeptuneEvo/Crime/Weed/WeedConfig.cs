using System;
using System.Collections.Generic;
using System.IO;
using GTANetworkAPI;
using Newtonsoft.Json;

namespace NeptuneEvo.Crime.Weed
{
    public class WeedBuyer
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("model")] public string Model { get; set; } = "g_m_y_famca_01";
        [JsonProperty("position")] public Vector3 Position { get; set; }
        [JsonProperty("heading")] public float Heading { get; set; }
        /// <summary>Цена за 1 грамм (ItemId.Drugs).</summary>
        [JsonProperty("price")] public int Price { get; set; } = 170;
        /// <summary>Сколько покупатель берёт за сутки (у всех игроков вместе).</summary>
        [JsonProperty("dailyCap")] public int DailyCap { get; set; } = 60;
    }

    /// <summary>Настройки травы: settings/weed.json (создаётся со значениями по умолчанию).</summary>
    public class WeedConfig
    {
        private static string FilePath => Path.Combine("settings", "weed.json");
        public static WeedConfig Current { get; private set; } = new WeedConfig();

        [JsonProperty("seedPrice")] public int SeedPrice { get; set; } = 150;
        [JsonProperty("waterPrice")] public int WaterPrice { get; set; } = 30;
        /// <summary>Сколько минут растёт куст до урожая.</summary>
        [JsonProperty("growMinutes")] public int GrowMinutes { get; set; } = 20;
        /// <summary>Не поливали дольше — куст засыхает.</summary>
        [JsonProperty("waterMinutes")] public int WaterMinutes { get; set; } = 10;
        /// <summary>Созревший куст гниёт, если не собрать за это время.</summary>
        [JsonProperty("rotMinutes")] public int RotMinutes { get; set; } = 60;
        [JsonProperty("dryMinutes")] public int DryMinutes { get; set; } = 10;
        [JsonProperty("yieldMin")] public int YieldMin { get; set; } = 10;
        [JsonProperty("yieldMax")] public int YieldMax { get; set; } = 16;
        [JsonProperty("homeMaxPlants")] public int HomeMaxPlants { get; set; } = 3;
        [JsonProperty("playerMaxPlants")] public int PlayerMaxPlants { get; set; } = 6;
        /// <summary>Шанс, что заметят сбор урожая на улице, %.</summary>
        [JsonProperty("harvestPoliceChance")] public int HarvestPoliceChance { get; set; } = 10;
        /// <summary>Шанс, что покупатель сдаст полиции, %.</summary>
        [JsonProperty("sellPoliceChance")] public int SellPoliceChance { get; set; } = 12;
        [JsonProperty("policeReward")] public int PoliceReward { get; set; } = 300;

        /// <summary>Скрытые точки посадки (в лесах и полях, далеко от дорог).</summary>
        [JsonProperty("spots")] public List<Vector3> Spots { get; set; } = new List<Vector3>();
        /// <summary>Покупатели по районам. Пусто — сервер сам ставит их у задних дворов магазинов 24/7 в разных районах.</summary>
        [JsonProperty("buyers")] public List<WeedBuyer> Buyers { get; set; } = new List<WeedBuyer>();

        public static void Load()
        {
            WeedConfig config = null;
            try
            {
                if (File.Exists(FilePath))
                    config = JsonConvert.DeserializeObject<WeedConfig>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                CrimeCore.Log.Write($"Не удалось прочитать {FilePath}: {e.Message}");
            }
            var save = false;
            if (config == null)
            {
                config = new WeedConfig();
                save = true;
            }
            if (config.Spots == null || config.Spots.Count == 0)
            {
                config.Spots = DefaultSpots();
                save = true;
            }
            config.Buyers ??= new List<WeedBuyer>();
            // Старые значения по умолчанию (рост 60 мин) — слишком долго, переводим на новые, если их не меняли вручную
            if (config.GrowMinutes == 60 && config.WaterMinutes == 25 && config.RotMinutes == 120 && config.DryMinutes == 20)
            {
                config.GrowMinutes = 20;
                config.WaterMinutes = 10;
                config.RotMinutes = 60;
                config.DryMinutes = 10;
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
                CrimeCore.Log.Write($"Не удалось сохранить {FilePath}: {e.Message}");
            }
        }

        /// <summary>
        /// По 5 точек в четырёх лесных массивах (бывшие делянки лесорубов): Grapeseed-восток, Paleto-лес,
        /// Zancudo/Tongva и северное побережье. Клиент кладёт куст на землю, Z — примерный.
        /// </summary>
        private static List<Vector3> DefaultSpots()
        {
            var centers = new[]
            {
                new Vector3(3370.0408, 4945.8154, 33.202995),
                new Vector3(-1319.7822, 4444.8164, 23.27308),
                new Vector3(-1988.7891, 2584.667, 3.311179),
                new Vector3(160.4275, 6895.9033, 20.979313),
            };
            var list = new List<Vector3>();
            foreach (var center in centers)
            {
                for (var i = 0; i < 5; i++)
                {
                    var angle = i * Math.PI * 2 / 5 + 0.4;
                    var radius = 12 + i * 3;
                    list.Add(new Vector3(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius, center.Z));
                }
            }
            return list;
        }
    }
}
