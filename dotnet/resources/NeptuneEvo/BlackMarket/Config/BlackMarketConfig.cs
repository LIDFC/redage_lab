using GTANetworkAPI;
using NeptuneEvo.Chars.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NeptuneEvo.BlackMarket.Config
{
    /// <summary>
    /// Настройки Чёрного рынка: settings/blackmarket.json. Если файла нет — создаётся со значениями по умолчанию,
    /// whitelist при этом строится по метаданным предметов (Chars.Repository.ItemsInfo), а не вручную.
    /// Администратор меняет значения командой /bm cfg и /bm wl, файл перезаписывается.
    /// </summary>
    public class BlackMarketConfig
    {
        private static string FilePath => Path.Combine("settings", "blackmarket.json");

        public static BlackMarketConfig Current { get; private set; } = new BlackMarketConfig();

        [JsonProperty("whitelist")] public List<int> Whitelist { get; set; } = new List<int>();

        /// <summary>С какого ранга фракции можно платить с кошелька банды и выводить из него.</summary>
        [JsonProperty("fractionPaymentRank")] public int FractionPaymentRank { get; set; } = 5;
        /// <summary>Комиссия при оплате с кошелька банды, %.</summary>
        [JsonProperty("fractionFeePercent")] public decimal FractionFeePercent { get; set; } = 5m;
        /// <summary>Комиссия P2P (берётся в BTC с покупателя), %.</summary>
        [JsonProperty("p2pFeePercent")] public decimal P2PFeePercent { get; set; } = 2m;

        [JsonProperty("lotMinHours")] public int LotMinHours { get; set; } = 5;
        [JsonProperty("lotMaxHours")] public int LotMaxHours { get; set; } = 120;
        [JsonProperty("maxPricePerUnit")] public long MaxPricePerUnit { get; set; } = 100_000_000;

        [JsonProperty("dropMinutes")] public int DropMinutes { get; set; } = 90;
        [JsonProperty("dropOfflineMinutes")] public int DropOfflineMinutes { get; set; } = 30;
        [JsonProperty("pickupSeconds")] public int PickupSeconds { get; set; } = 5;
        [JsonProperty("dropProp")] public string DropProp { get; set; } = "prop_mp_drug_package";

        /// <summary>Системный обменник: сколько $ стоит 1 BTC. 0 — обменник выключен.</summary>
        [JsonProperty("exchangeUsdPerBtc")] public decimal ExchangeUsdPerBtc { get; set; } = 10m;

        /// <summary>Обнал: грязные $ из сумки → BTC, комиссия %.</summary>
        [JsonProperty("launderFeePercent")] public decimal LaunderFeePercent { get; set; } = 5m;
        /// <summary>Обнал: BTC → $ наличными, комиссия %.</summary>
        [JsonProperty("cashoutFeePercent")] public decimal CashoutFeePercent { get; set; } = 10m;
        /// <summary>Шанс получить звезду розыска при обналичивании BTC, %.</summary>
        [JsonProperty("cashoutWantedChance")] public int CashoutWantedChance { get; set; } = 15;
        /// <summary>Где работает обнал: у NPC «Мавр» (Caleb Baker, Core/Robbery.cs).</summary>
        [JsonProperty("cashoutPoint")] public Vector3 CashoutPoint { get; set; } = new Vector3(-2.1323678, -1821.9778, 29.543238);
        [JsonProperty("cashoutRadius")] public float CashoutRadius { get; set; } = 5f;

        /// <summary>
        /// Точки закладок: на улице, на земле (позиция «в полный рост», объект ставится на ~1 м ниже).
        /// По умолчанию — точки аирдропов (проверенные уличные места вне зелёных зон) без тех, что ближе 220 м к участкам полиции.
        /// </summary>
        [JsonProperty("dropPoints")] public List<Vector3> DropPoints { get; set; } = new List<Vector3>();

        public static void Load()
        {
            BlackMarketConfig config = null;
            try
            {
                if (File.Exists(FilePath))
                    config = JsonConvert.DeserializeObject<BlackMarketConfig>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                BlackMarketCore.Log.Write($"Не удалось прочитать {FilePath}: {e.Message}");
            }

            var save = false;
            if (config == null)
            {
                config = new BlackMarketConfig();
                save = true;
            }
            if (config.Whitelist == null || config.Whitelist.Count == 0)
            {
                config.Whitelist = DefaultWhitelist();
                save = true;
            }
            if (config.DropPoints == null || config.DropPoints.Count == 0)
            {
                config.DropPoints = DefaultDropPoints.ToList();
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
                BlackMarketCore.Log.Write($"Не удалось сохранить {FilePath}: {e.Message}");
            }
        }

        public bool IsAllowed(ItemId itemId) => Whitelist.Contains((int)itemId) && Chars.Repository.ItemsInfo.ContainsKey(itemId);

        /// <summary>
        /// Всё криминальное из ItemsInfo: огнестрельное (кроме снежков, фейерверка, ракетницы, шокера), патроны,
        /// холодное (кроме фонарика и дубинки), обвесы, наркотики, бронежилет и инструменты преступника.
        /// </summary>
        public static List<int> DefaultWhitelist()
        {
            var excluded = new HashSet<ItemId> { ItemId.Ball, ItemId.Snowball, ItemId.Firework, ItemId.FlareGun, ItemId.StunGun, ItemId.Flashlight, ItemId.Nightstick };
            var extra = new HashSet<ItemId>
            {
                ItemId.Drugs, ItemId.Cocaine, ItemId.BodyArmor, ItemId.Lockpick, ItemId.ArmyLockpick,
                ItemId.Cuffs, ItemId.BagWithDrill, ItemId.RadioInterceptor, ItemId.QrFake,
            };
            return Chars.Repository.ItemsInfo
                .Where(i => !excluded.Contains(i.Key) && (extra.Contains(i.Key)
                    || i.Value.functionType == newItemType.Weapons
                    || i.Value.functionType == newItemType.Ammo
                    || i.Value.functionType == newItemType.MeleeWeapons
                    || i.Value.functionType == newItemType.Modification))
                .Select(i => (int)i.Key)
                .OrderBy(i => i)
                .ToList();
        }

        private static readonly Vector3[] DefaultDropPoints =
        {
            new Vector3(1070.7666f, -712.1477f, 58.49874f),
            new Vector3(1102.1672f, -707.12476f, 56.7596f),
            new Vector3(1075.2537f, -760.1058f, 57.8116f),
            new Vector3(1019.26447f, -711.14923f, 57.7291f),
            new Vector3(1111.0593f, -759.1508f, 57.7643f),
            new Vector3(1124.9475f, -663.4413f, 56.7671f),
            new Vector3(1159.4922f, -718.4323f, 56.826f),
            new Vector3(1051.5419f, -619.1648f, 56.876f),
            new Vector3(1014.0885f, -657.589f, 58.3552f),
            new Vector3(1147.6125f, -644.389f, 56.741f),
            new Vector3(-1726.6642f, -188.69617f, 58.2658f),
            new Vector3(-1714.655f, -235.4602f, 55.067192f),
            new Vector3(-1762.959f, -162.465f, 63.9422f),
            new Vector3(-1688.15f, -164.554f, 57.5533f),
            new Vector3(-1671.486f, -219.45708f, 55.1525f),
            new Vector3(-1731.027f, -262.871f, 51.5888f),
            new Vector3(-1803.443f, -128.03717f, 78.786f),
            new Vector3(-1688.511f, -261.73325f, 51.88331f),
            new Vector3(-1771.507f, -257.61105f, 49.332f),
            new Vector3(-1652.858f, -132.6128f, 59.6817f),
            new Vector3(863.8128f, -868.2756f, 25.62753f),
            new Vector3(852.917f, -951.6709f, 26.2712f),
            new Vector3(901.943f, -887.61664f, 41.7496f),
            new Vector3(915.0499f, -895.1392f, 53.3129f),
            new Vector3(892.5187f, -901.8795f, 43.91995f),
            new Vector3(806.24365f, -919.2496f, 25.84587f),
            new Vector3(881.6852f, -880.0532f, 27.724f),
            new Vector3(836.23175f, -875.3281f, 25.22759f),
            new Vector3(926.2606f, -879.1531f, 50.0126f),
            new Vector3(880.6848f, -934.20575f, 30.78252f),
            new Vector3(-879.1289f, -2523.5586f, 14.857651f),
            new Vector3(-841.52f, -2500.98f, 13.830637f),
            new Vector3(-882.229f, -2584.5405f, 13.827842f),
            new Vector3(-956.03723f, -2570.7585f, 13.820424f),
            new Vector3(-936.0609f, -2548.8445f, 14.015688f),
            new Vector3(-998.2625f, -2527.4517f, 13.801147f),
            new Vector3(-967.0045f, -2466.0144f, 13.976596f),
            new Vector3(-914.09424f, -2484.0857f, 14.539599f),
            new Vector3(-847.76746f, -2577.6829f, 13.759464f),
            new Vector3(-995.31024f, -2479.891f, 13.778991f),
            new Vector3(1150.489f, -3282.368f, 5.900809f),
            new Vector3(1119.217f, -3260.793f, 5.897873f),
            new Vector3(1122.711f, -3233.599f, 5.895298f),
            new Vector3(1158.365f, -3209.473f, 5.900023f),
            new Vector3(1184.569f, -3239.953f, 6.028767f),
            new Vector3(1191.856f, -3342.553f, 5.801401f),
            new Vector3(1065.062f, -3325.472f, 5.915152f),
            new Vector3(1077.113f, -3217.388f, 5.901025f),
            new Vector3(1236.264f, -3217.617f, 5.800362f),
            new Vector3(1164.095f, -3309.951f, 5.924438f),
            new Vector3(-1034.9161f, -1067.9562f, 3.908407f),
            new Vector3(-1110.4626f, -1100.8438f, 2.152846f),
            new Vector3(-1141.8715f, -1075.8899f, 2.6645648f),
            new Vector3(-1633.3508f, -3017.593f, 14.120636f),
            new Vector3(-1714.258f, -2967.838f, 14.134283f),
            new Vector3(-1642.5552f, -2911.5105f, 14.2871685f),
            new Vector3(-1503.3135f, -3000.553f, 14.310736f),
            new Vector3(-1608.9343f, -3141.3035f, 29.566256f),
            new Vector3(-1618.0619f, -3136.7424f, 31.928312f),
            new Vector3(-1644.8013f, -3128.7085f, 35.425087f),
            new Vector3(-1669.2505f, -3149.8252f, 35.423992f),
            new Vector3(-1655.9427f, -3154.4634f, 35.427883f),
            new Vector3(-1678.5167f, -3168.2393f, 35.421173f),
            new Vector3(-1566.7665f, -3095.436f, 13.944708f),
            new Vector3(-1649.9985f, -3091.158f, 13.937061f),
            new Vector3(-1626.9152f, -3133.253f, 35.413322f),
            new Vector3(-1674.7384f, -3154.949f, 35.43172f),
            new Vector3(-1619.1382f, -3173.4817f, 13.936287f),
            new Vector3(-1672.0951f, -3105.5173f, 29.561073f),
            new Vector3(-1694.1156f, -3145.3f, 29.553682f),
            new Vector3(-1591.3275f, -3084.1577f, 13.9358835f),
            new Vector3(-1622.9893f, -3061.342f, 14.138118f),
            new Vector3(-1583.3085f, -3112.012f, 13.93775f),
            new Vector3(-2302.4119f, 218.99484f, 167.60162f),
            new Vector3(-2241.3008f, 274.7148f, 174.60353f),
            new Vector3(-2212.8687f, 214.99245f, 174.58456f),
            new Vector3(-2190.8572f, 236.15755f, 184.60193f),
            new Vector3(-2209.9026f, 200.59213f, 194.59651f),
            new Vector3(-2243.3413f, 231.83043f, 190.60155f),
            new Vector3(-2242.393f, 295.31158f, 184.60014f),
            new Vector3(-2265.6143f, 310.84082f, 174.22559f),
            new Vector3(-2261.2908f, 195.84755f, 174.59409f),
            new Vector3(-2335.6958f, 244.32425f, 169.59949f),
            new Vector3(1058.4012f, 45.450047f, 81.52423f),
            new Vector3(1088.6041f, 56.241276f, 80.87874f),
            new Vector3(1203.711f, 89.279045f, 81.83645f),
            new Vector3(1030.8582f, -43.58331f, 75.068405f),
            new Vector3(1024.1509f, 29.952518f, 82.15714f),
            new Vector3(1134.4272f, 11.786752f, 81.88025f),
            new Vector3(1114.106f, -30.436384f, 81.94798f),
            new Vector3(1126.9879f, 83.781425f, 80.75532f),
            new Vector3(1104.3988f, 106.91325f, 80.89076f),
            new Vector3(1161.7772f, 107.060036f, 80.68434f),
        };
    }
}
