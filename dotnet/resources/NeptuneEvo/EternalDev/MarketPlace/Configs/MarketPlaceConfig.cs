using GTANetworkAPI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Configs
{
    public class MarketPlaceConfig
    {
        /// <summary>
        /// Файл настроек в папке settings сервера. Если его нет — берутся значения по умолчанию из этого класса
        /// (раньше читался через ELib.JsonReader из авторской EternalCore.dll).
        /// </summary>
        public static string Path => System.IO.Path.Combine("settings", "marketplace.json");

        public static MarketPlaceConfig Load()
        {
            try
            {
                if (System.IO.File.Exists(Path))
                    return JsonConvert.DeserializeObject<MarketPlaceConfig>(System.IO.File.ReadAllText(Path)) ?? new MarketPlaceConfig();
            }
            catch (Exception e)
            {
                MarketLog.Write($"Не удалось прочитать {Path}: {e.Message}. Используются настройки по умолчанию");
            }
            return new MarketPlaceConfig();
        }

        /// <summary>
        /// Список позиций для склада
        /// </summary>
        [JsonProperty("storage_positions")]
        public StoragePlaceConfig[] StoragePostions = new StoragePlaceConfig[]
        {
            new StoragePlaceConfig(new Vector3(-826.19, -757.41, 22.31), 25f, new Vector3(-846.72, -750.43, 24.1))
        };

        /// <summary>
        /// Время длительности тестдрайва
        /// </summary>
        [JsonProperty("testdrive_time")]
        public int TestdriveTime { get; set; } = 300;

        /// <summary>
        /// Нужно ли находится рядом с автосалоном, чтобы начать тестдрайв
        /// </summary>
        [JsonProperty("need_nearest_autoshop_for_testdrive")]
        public bool NeedNearestAutoshopForTestdrive { get; set; } = true;

        /// <summary>
        /// Список позиций для тестдрайва
        /// </summary>
        [JsonProperty("testdrive_positions")]
        public TestdrivePosition[] TestdrivePositions = new TestdrivePosition[]
        {
            new TestdrivePosition(new Vector3(-816.0768, -743.8378, 23.710386), -170)
        };

        /// <summary>
        /// Список позициий для интерьера аукциона/маркетплейса
        /// </summary>
        [JsonProperty("interior_positions")]
        // Интерьер аукциона Majestic (q_auc_milo_, z = -162) — кастомная карта, в проекте её нет.
        // По умолчанию интерьер выключен, а точка площадки стоит у входа на улице (см. AuctionPositions).
        public InteriorPositionConfig[] InteriorPositions = new InteriorPositionConfig[0];

        /// <summary>
        /// Список позиций для открытия интерфейса маркетплейса (например в интерьере аукциона)
        /// </summary>
        [JsonProperty("auction_positions")]
        public AuctionPositionConfig[] AuctionPositions = new AuctionPositionConfig[]
        {
            new AuctionPositionConfig(position: new Vector3(-827.46, -699.8, 28.05), range: 2, dimension: 0)
        };

        /// <summary>
        /// Максимальное количество слотов на складе
        /// </summary>
        [JsonProperty("storage_slots")]
        public int MaxSlotsInStorage { get; set; } = 300;

        /// <summary>
        /// Настройки самого маркетплейса
        /// </summary>
        [JsonProperty("app")]
        public AppConfig App = new AppConfig();
    }
}
