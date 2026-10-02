using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.BlackMarket;
using NeptuneEvo.BlackMarket.Config;
using NeptuneEvo.Character;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Core;
using NeptuneEvo.Crime.Weed;
using NeptuneEvo.Fractions;
using NeptuneEvo.Fractions.Models;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Accounts;
using NeptuneEvo.Handles;
using NeptuneEvo.Houses;
using NeptuneEvo.Organizations.Contracts.Config;
using NeptuneEvo.Players;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Functions
{
    /// <summary>
    /// /cfg — админ-панель в CEF для настроек сервера без правки файлов и рестарта.
    /// Вкладки: трава, Чёрный рынок, скупка, подряды (settings/*.json), экономика (таблица economy),
    /// сервер и налоги (serverSettings/pricesSettings), зарплаты фракций (fractionranks).
    /// Значения меняются в памяти сразу (модули читают их при каждом обращении), затем сохраняются.
    /// Перед сохранением — бэкап в settings/backup (10 последних на вкладку), каждое изменение — в историю
    /// (settings/cfg_history.json, откат одной кнопкой), adminlog и чат админов. Пресеты — settings/cfg_presets.
    /// Новое поле = одна строка Int(...)/Float(...)/Bool(...)/Select(...) в BuildSections.
    /// </summary>
    class ConfigPanel : Script
    {
        private static readonly nLog Log = new nLog("ConfigPanel");

        /// <summary>Смотреть панель (команда /cfg).</summary>
        public const int ViewLevel = 5;
        /// <summary>Менять обычные вкладки.</summary>
        public const int EditLevel = 8;
        /// <summary>Менять экономику, налоги и зарплаты (или логин из DirectorLogins).</summary>
        public const int EconomyLevel = 9;

        private const string BackupDir = "settings/backup";
        private const string PresetDir = "settings/cfg_presets";
        private const string HistoryPath = "settings/cfg_history.json";
        private const int BackupsPerSection = 10;
        private const int HistoryLimit = 500;
        private const int HistorySend = 150;

        private class Field
        {
            public string Key;
            public string Label;
            public string Hint;
            /// <summary>int, float, bool, select.</summary>
            public string Kind = "int";
            public List<(double value, string label)> Options;
            public double Min;
            public double Max;
            public Func<double> Get;
            public Action<double> Set;
            /// <summary>Колонка таблицы economy (сохраняется одним UPDATE).</summary>
            public string Column;
            /// <summary>Своё сохранение поля (например строка fractionranks).</summary>
            public Action<double> Persist;
        }

        private class Group
        {
            public string Title;
            public List<Field> Fields = new List<Field>();
        }

        private class Section
        {
            public string Id;
            public string Title;
            public string Source;
            public int EditLevel = ConfigPanel.EditLevel;
            /// <summary>Файлы, которые копируются в бэкап перед сохранением.</summary>
            public List<string> Files = new List<string>();
            public List<Group> Groups = new List<Group>();
            /// <summary>Проверка связанных полей после изменения; вернуть текст ошибки или null.</summary>
            public Func<string> Validate = () => null;
            public Action Save = () => { };
            /// <summary>Что обновить в игре после сохранения (цены в меню и т.п.).</summary>
            public Action Apply = () => { };
            /// <summary>Перечитать с диска/из БД; null — кнопки нет.</summary>
            public Action Reload;
            /// <summary>Подсказки «что это значит в деньгах» по текущим значениям.</summary>
            public Func<List<string>> Info = () => new List<string>();

            public IEnumerable<Field> AllFields => Groups.SelectMany(g => g.Fields);
        }

        private class HistoryEntry
        {
            public int Id;
            public string Time;
            public string Admin;
            public string Section;
            public string SectionTitle;
            public string Key;
            public string Label;
            public double Old;
            public double New;
        }

        // ------------------------------------------------------------------ конструкторы полей

        private static Field Int(string key, string label, double min, double max, Func<double> get, Action<int> set, string hint = null) =>
            new Field { Key = key, Label = label, Hint = hint, Min = min, Max = max, Get = get, Set = v => set((int)Math.Round(v)) };

        private static Field Float(string key, string label, double min, double max, Func<double> get, Action<double> set, string hint = null) =>
            new Field { Key = key, Label = label, Hint = hint, Kind = "float", Min = min, Max = max, Get = get, Set = set };

        private static Field Bool(string key, string label, Func<bool> get, Action<bool> set, string hint = null) =>
            new Field { Key = key, Label = label, Hint = hint, Kind = "bool", Min = 0, Max = 1, Get = () => get() ? 1 : 0, Set = v => set(v >= 0.5) };

        private static Field Select(string key, string label, List<(double value, string label)> options, Func<double> get, Action<double> set, string hint = null) =>
            new Field { Key = key, Label = label, Hint = hint, Kind = "select", Options = options, Min = options.Min(o => o.value), Max = options.Max(o => o.value), Get = get, Set = set };

        /// <summary>Поле из таблицы economy: значение в Main.*, колонка — для UPDATE.</summary>
        private static Field Eco(string column, string label, double min, double max, Func<int> get, Action<int> set, string hint = null)
        {
            var field = Int(column, label, min, max, () => get(), set, hint);
            field.Column = column;
            return field;
        }

        private static string ItemName(int itemId) =>
            Chars.Repository.ItemsInfo.TryGetValue((ItemId)itemId, out var info) ? info.Name : $"Предмет {itemId}";

        private static string Money(double value) => MoneySystem.Wallet.Format((long)Math.Round(value)) + "$";

        private static string Num(double value) => Math.Round(value, 4).ToString(CultureInfo.InvariantCulture);

        private static void SetMatsPrice(int id, int price)
        {
            if (Manager.FractionDataMats.TryGetValue(id, out var mats))
                mats.Price = $"{price}$";
        }

        // ------------------------------------------------------------------ схема

        /// <summary>Схема строится заново при каждом обращении: списки (покупатели, товары, ранги) могли измениться.</summary>
        private static List<Section> BuildSections()
        {
            var sections = new List<Section>();

            // --- Трава ---
            {
                WeedConfig C() => WeedConfig.Current;
                var s = new Section { Id = "weed", Title = "Трава", Source = "settings/weed.json", Save = WeedConfig.Save };
                s.Files.Add("settings/weed.json");
                var main = new Group { Title = "Цены у Мавра" };
                main.Fields.Add(Int("seedPrice", "Семена, $", 1, 1_000_000, () => C().SeedPrice, v => C().SeedPrice = v));
                main.Fields.Add(Int("waterPrice", "Бутылка воды, $", 1, 1_000_000, () => C().WaterPrice, v => C().WaterPrice = v));
                s.Groups.Add(main);

                var grow = new Group { Title = "Рост куста" };
                grow.Fields.Add(Int("growMinutes", "Рост до урожая, мин", 1, 1440, () => C().GrowMinutes, v => C().GrowMinutes = v));
                grow.Fields.Add(Int("waterMinutes", "Без полива засыхает через, мин", 1, 1440, () => C().WaterMinutes, v => C().WaterMinutes = v));
                grow.Fields.Add(Int("rotMinutes", "Созревший гниёт через, мин", 1, 10080, () => C().RotMinutes, v => C().RotMinutes = v));
                grow.Fields.Add(Int("dryMinutes", "Сушка, мин", 1, 1440, () => C().DryMinutes, v => C().DryMinutes = v));
                grow.Fields.Add(Int("yieldMin", "Урожай от, г", 1, 1000, () => C().YieldMin, v => C().YieldMin = v));
                grow.Fields.Add(Int("yieldMax", "Урожай до, г", 1, 1000, () => C().YieldMax, v => C().YieldMax = v));
                grow.Fields.Add(Int("homeMaxPlants", "Кустов в доме, макс.", 0, 100, () => C().HomeMaxPlants, v => C().HomeMaxPlants = v));
                grow.Fields.Add(Int("playerMaxPlants", "Кустов на игрока, макс.", 0, 100, () => C().PlayerMaxPlants, v => C().PlayerMaxPlants = v));
                s.Groups.Add(grow);

                var police = new Group { Title = "Риск" };
                police.Fields.Add(Int("harvestPoliceChance", "Заметят сбор на улице, %", 0, 100, () => C().HarvestPoliceChance, v => C().HarvestPoliceChance = v));
                police.Fields.Add(Int("sellPoliceChance", "Покупатель сдаст полиции, %", 0, 100, () => C().SellPoliceChance, v => C().SellPoliceChance = v));
                police.Fields.Add(Int("policeReward", "Награда полиции, $", 0, 1_000_000, () => C().PoliceReward, v => C().PoliceReward = v));
                s.Groups.Add(police);

                var buyers = C().Buyers ?? new List<WeedBuyer>();
                if (buyers.Count > 0)
                {
                    var g = new Group { Title = "Покупатели" };
                    for (var i = 0; i < buyers.Count; i++)
                    {
                        var buyer = buyers[i];
                        var name = string.IsNullOrEmpty(buyer.Name) ? $"Покупатель {i + 1}" : buyer.Name;
                        g.Fields.Add(Int($"buyer.{i}.price", $"{name}: цена за 1 г, $", 1, 100_000, () => buyer.Price, v => buyer.Price = v));
                        g.Fields.Add(Int($"buyer.{i}.dailyCap", $"{name}: берёт за сутки, г", 0, 100_000, () => buyer.DailyCap, v => buyer.DailyCap = v));
                    }
                    s.Groups.Add(g);
                }

                s.Validate = () => C().YieldMin > C().YieldMax ? "Урожай «от» больше, чем «до»" : null;
                s.Apply = WeedManager.RebindConfig;
                s.Reload = () =>
                {
                    WeedConfig.Load();
                    WeedManager.RebindConfig();
                };
                s.Info = () =>
                {
                    var c = C();
                    var list = c.Buyers ?? new List<WeedBuyer>();
                    var price = list.Count > 0 ? list.Average(b => b.Price) : 170;
                    var yield = (c.YieldMin + c.YieldMax) / 2.0;
                    var cycle = Math.Max(1, c.GrowMinutes + c.DryMinutes);
                    var waterings = Math.Max(0, (int)Math.Ceiling((double)c.GrowMinutes / Math.Max(1, c.WaterMinutes)) - 1);
                    var cost = c.SeedPrice + waterings * c.WaterPrice;
                    var profit = yield * price - cost;
                    return new List<string>
                    {
                        $"Один куст: ~{yield:0.#} г × {Money(price)} (средняя цена покупателей) − семена и вода {Money(cost)} = ~{Money(profit)} за {cycle} мин",
                        $"Игрок с {c.PlayerMaxPlants} кустами: ~{Money(profit * c.PlayerMaxPlants * 60.0 / cycle)} в час",
                        $"Все покупатели вместе берут до {list.Sum(b => b.DailyCap)} г в сутки",
                    };
                };
                sections.Add(s);
            }

            // --- Чёрный рынок ---
            {
                BlackMarketConfig C() => BlackMarketConfig.Current;
                var s = new Section { Id = "blackmarket", Title = "Чёрный рынок", Source = "settings/blackmarket.json", Save = BlackMarketConfig.Save };
                s.Files.Add("settings/blackmarket.json");
                var fees = new Group { Title = "Комиссии и курс" };
                fees.Fields.Add(Float("exchangeUsdPerBtc", "Курс обменника: $ за 1 BTC (0 — выкл.)", 0, 1_000_000, () => (double)C().ExchangeUsdPerBtc, v => C().ExchangeUsdPerBtc = (decimal)v));
                fees.Fields.Add(Float("fractionFeePercent", "Оплата с кошелька банды, %", 0, 100, () => (double)C().FractionFeePercent, v => C().FractionFeePercent = (decimal)v));
                fees.Fields.Add(Float("p2pFeePercent", "Комиссия P2P, %", 0, 100, () => (double)C().P2PFeePercent, v => C().P2PFeePercent = (decimal)v));
                fees.Fields.Add(Float("launderFeePercent", "Обнал грязных $ → BTC, %", 0, 100, () => (double)C().LaunderFeePercent, v => C().LaunderFeePercent = (decimal)v));
                fees.Fields.Add(Float("cashoutFeePercent", "Обнал BTC → $, %", 0, 100, () => (double)C().CashoutFeePercent, v => C().CashoutFeePercent = (decimal)v));
                fees.Fields.Add(Int("cashoutWantedChance", "Розыск при обнале, %", 0, 100, () => C().CashoutWantedChance, v => C().CashoutWantedChance = v));
                fees.Fields.Add(Int("fractionPaymentRank", "Ранг для кошелька банды", 1, 20, () => C().FractionPaymentRank, v => C().FractionPaymentRank = v));
                s.Groups.Add(fees);

                var lots = new Group { Title = "Лоты и закладки" };
                lots.Fields.Add(Int("lotMinHours", "Лот живёт от, ч", 1, 720, () => C().LotMinHours, v => C().LotMinHours = v));
                lots.Fields.Add(Int("lotMaxHours", "Лот живёт до, ч", 1, 720, () => C().LotMaxHours, v => C().LotMaxHours = v));
                lots.Fields.Add(Float("maxPricePerUnit", "Макс. цена за штуку", 1, 1_000_000_000, () => C().MaxPricePerUnit, v => C().MaxPricePerUnit = (long)Math.Round(v)));
                lots.Fields.Add(Int("dropMinutes", "Закладка лежит, мин", 1, 1440, () => C().DropMinutes, v => C().DropMinutes = v));
                lots.Fields.Add(Int("dropOfflineMinutes", "Закладка при офлайне, мин", 1, 1440, () => C().DropOfflineMinutes, v => C().DropOfflineMinutes = v));
                lots.Fields.Add(Int("pickupSeconds", "Подбор закладки, сек", 1, 120, () => C().PickupSeconds, v => C().PickupSeconds = v));
                lots.Fields.Add(Float("cashoutRadius", "Радиус обнала у Мавра, м", 1, 100, () => C().CashoutRadius, v => C().CashoutRadius = (float)v));
                s.Groups.Add(lots);

                s.Validate = () => C().LotMinHours > C().LotMaxHours ? "Срок лота «от» больше, чем «до»" : null;
                s.Reload = BlackMarketConfig.Load;
                s.Info = () =>
                {
                    var c = C();
                    if (c.ExchangeUsdPerBtc <= 0)
                        return new List<string> { "Обменник выключен (курс 0)" };
                    var btc = 10_000m / c.ExchangeUsdPerBtc * (1 - c.LaunderFeePercent / 100m);
                    var back = btc * c.ExchangeUsdPerBtc * (1 - c.CashoutFeePercent / 100m);
                    return new List<string>
                    {
                        $"$10 000 грязными → ~{btc:0.##} BTC → ~{Money((double)back)} чистыми (по курсу обменника, потери {100 - back / 100m:0.#}%)",
                    };
                };
                sections.Add(s);
            }

            // --- Скупщик краденого ---
            {
                FenceConfig F() => BlackMarketConfig.Current.Fence;
                var s = new Section { Id = "fence", Title = "Скупка", Source = "settings/blackmarket.json (fence)", Save = BlackMarketConfig.Save };
                s.Files.Add("settings/blackmarket.json");
                var main = new Group { Title = "Рынок" };
                main.Fields.Add(Float("minFactor", "Нижний предел цены (доля от базы)", 0.01, 1, () => F().MinFactor, v => F().MinFactor = v, "0.35 — цена не падает ниже 35%"));
                main.Fields.Add(Float("recoverPercentPerHour", "Спрос восстанавливается, % в час", 0, 100, () => F().RecoverPercentPerHour, v => F().RecoverPercentPerHour = v));
                main.Fields.Add(Float("btcBonusPercent", "Бонус при оплате в BTC, %", 0, 100, () => (double)F().BtcBonusPercent, v => F().BtcBonusPercent = (decimal)v));
                s.Groups.Add(main);

                var items = new Group { Title = "Товары" };
                foreach (var item in F().Items ?? new List<FenceItem>())
                {
                    var name = ItemName(item.ItemId);
                    items.Fields.Add(Int($"item.{item.ItemId}.basePrice", $"{name}: базовая цена, $", 1, 10_000_000, () => item.BasePrice, v => item.BasePrice = v));
                    items.Fields.Add(Int($"item.{item.ItemId}.capacity", $"{name}: насыщение рынка, шт", 1, 1_000_000, () => item.Capacity, v => item.Capacity = v));
                }
                s.Groups.Add(items);
                s.Reload = BlackMarketConfig.Load;
                s.Info = () =>
                {
                    var f = F();
                    var list = new List<string>();
                    if (f.RecoverPercentPerHour > 0)
                        list.Add($"Полностью «насыщенный» рынок восстанавливается за ~{100 / f.RecoverPercentPerHour:0.#} ч");
                    foreach (var item in f.Items ?? new List<FenceItem>())
                        list.Add($"{ItemName(item.ItemId)}: от {Money(item.BasePrice * f.MinFactor)} до {Money(item.BasePrice)} за штуку");
                    return list;
                };
                sections.Add(s);
            }

            // --- Подряды ---
            {
                ContractsConfig C() => ContractsConfig.Current;
                var s = new Section { Id = "contracts", Title = "Подряды", Source = "settings/org_contracts.json", Save = ContractsConfig.Save };
                s.Files.Add("settings/org_contracts.json");
                var main = new Group { Title = "Генерация" };
                main.Fields.Add(Int("contractsPerGeneration", "Контрактов за генерацию", 1, 100, () => C().ContractsPerGeneration, v => C().ContractsPerGeneration = v));
                main.Fields.Add(Int("starterContracts", "Из них для новичков", 0, 100, () => C().StarterContracts, v => C().StarterContracts = v));
                main.Fields.Add(Int("maxActivePerOrganization", "Активных на организацию, макс.", 1, 50, () => C().MaxActivePerOrganization, v => C().MaxActivePerOrganization = v));
                main.Fields.Add(Float("minRewardFactor", "Награда ≥ стоимость материалов ×", 1, 20, () => C().MinRewardFactor, v => C().MinRewardFactor = v));
                main.Fields.Add(Int("npcChance", "Шанс NPC-бонуса, %", 0, 100, () => C().NpcChance, v => C().NpcChance = v));
                s.Groups.Add(main);

                var materials = new Group { Title = "Материалы" };
                foreach (var m in C().Materials ?? new List<MaterialDefinition>())
                {
                    var name = m.Name ?? m.Id;
                    materials.Fields.Add(Int($"material.{m.Id}.price", $"{name}: цена за ед., $", 1, 1_000_000, () => m.Price, v => m.Price = v));
                    materials.Fields.Add(Float($"material.{m.Id}.kgPerUnit", $"{name}: вес ед., кг", 0.01, 1000, () => m.KgPerUnit, v => m.KgPerUnit = (float)v));
                    materials.Fields.Add(Int($"material.{m.Id}.unitsPerPallet", $"{name}: ед. в паллете", 1, 10_000, () => m.UnitsPerPallet, v => m.UnitsPerPallet = v));
                }
                s.Groups.Add(materials);

                var vehicles = new Group { Title = "Грузовики" };
                foreach (var v in C().Vehicles ?? new List<CargoVehicleDefinition>())
                {
                    var name = v.Name ?? v.Model;
                    vehicles.Fields.Add(Int($"vehicle.{v.Model}.slots", $"{name}: паллет", 1, 50, () => v.Slots, x => v.Slots = x));
                    vehicles.Fields.Add(Int($"vehicle.{v.Model}.maxKg", $"{name}: груз, кг", 1, 100_000, () => v.MaxKg, x => v.MaxKg = x));
                }
                s.Groups.Add(vehicles);

                s.Validate = () => C().StarterContracts > C().ContractsPerGeneration ? "Контрактов для новичков больше, чем всего за генерацию" : null;
                s.Reload = ContractsConfig.Load;
                s.Info = () => (C().Materials ?? new List<MaterialDefinition>())
                    .Select(m => $"{m.Name ?? m.Id}: паллета {m.UnitsPerPallet} ед. = {Money(m.Price * m.UnitsPerPallet)}, {m.KgPerUnit * m.UnitsPerPallet:0.#} кг")
                    .ToList();
                sections.Add(s);
            }

            // --- Экономика (таблица economy) ---
            {
                var s = new Section { Id = "economy", Title = "Экономика", Source = "БД: таблица economy", EditLevel = EconomyLevel };
                var mavr = new Group { Title = "Цены у Мавра" };
                mavr.Fields.Add(Eco("BlackMarketMedCard", "Мед. карта, $", 0, 10_000_000, () => Main.BlackMarketMedCard, v => Main.BlackMarketMedCard = v));
                mavr.Fields.Add(Eco("BlackMarketGunLic", "Лицензия на оружие, $", 0, 10_000_000, () => Main.BlackMarketGunLic, v => Main.BlackMarketGunLic = v));
                mavr.Fields.Add(Eco("BMlockpick", "Отмычка, $", 0, 10_000_000, () => Main.BlackMarketLockPick, v => Main.BlackMarketLockPick = v));
                mavr.Fields.Add(Eco("BMalockpick", "Военная отмычка, $", 0, 10_000_000, () => Main.BlackMarketArmyLockPick, v => Main.BlackMarketArmyLockPick = v));
                mavr.Fields.Add(Eco("BMdrill", "Сумка с дрелью, $", 0, 10_000_000, () => Main.BlackMarketDrill, v => Main.BlackMarketDrill = v));
                mavr.Fields.Add(Eco("BMcuffs", "Стяжки, $", 0, 10_000_000, () => Main.BlackMarketCuffs, v => Main.BlackMarketCuffs = v));
                mavr.Fields.Add(Eco("BMpocket", "Мешок на голову, $", 0, 10_000_000, () => Main.BlackMarketPocket, v => Main.BlackMarketPocket = v));
                mavr.Fields.Add(Eco("BMwanted", "Снять 1 звезду розыска, $", 0, 10_000_000, () => Main.BlackMarketWanted, v => Main.BlackMarketWanted = v));
                mavr.Fields.Add(Eco("BMuncuff", "Снять наручники, $", 0, 10_000_000, () => Main.BlackMarketUnCuff, v => Main.BlackMarketUnCuff = v));
                mavr.Fields.Add(Eco("BlackQrFake", "Поддельный QR-код, $", 0, 10_000_000, () => Main.BlackQrFake, v => Main.BlackQrFake = v));
                mavr.Fields.Add(Eco("BlackRadioInterceptord", "Перехватчик рации, $", 0, 10_000_000, () => Main.BlackRadioInterceptord, v => Main.BlackRadioInterceptord = v));
                s.Groups.Add(mavr);

                var jobs = new Group { Title = "Работы" };
                jobs.Fields.Add(Eco("colPay", "Инкассатор: $ за 100 м пути", 0, 100_000, () => Main.CollectorPayment, v => Main.CollectorPayment = v));
                jobs.Fields.Add(Eco("elecPay", "Электрик: ставка", 0, 100_000, () => Main.ElectricianPayment, v => Main.ElectricianPayment = v));
                jobs.Fields.Add(Eco("postPay", "Почтальон: ставка", 0, 100_000, () => Main.PostalPayment, v => Main.PostalPayment = v));
                jobs.Fields.Add(Eco("lawnPay", "Газонокосильщик: ставка", 0, 100_000, () => Main.LawnmowerPayment, v => Main.LawnmowerPayment = v));
                jobs.Fields.Add(Eco("busPay", "Водитель автобуса: ставка", 0, 100_000, () => Main.BusPay, v => Main.BusPay = v));
                jobs.Fields.Add(Eco("gangCarDelivery", "Банды: доставка машины, $", 0, 10_000_000, () => Main.GangCarDelivery, v => Main.GangCarDelivery = v));
                jobs.Fields.Add(Eco("mafCarDelivery", "Мафия: доставка машины, $", 0, 10_000_000, () => Main.MafiaCarDelivery, v => Main.MafiaCarDelivery = v));
                jobs.Fields.Add(Eco("policeAward", "Полиция: награда за арест, $", 0, 10_000_000, () => Main.PoliceAward, v => Main.PoliceAward = v));
                s.Groups.Add(jobs);

                var wars = new Group { Title = "Территории и бизвары" };
                wars.Fields.Add(Eco("captureWin", "Победа в захвате территории, $", 0, 100_000_000, () => Main.CaptureWin, v => Main.CaptureWin = v));
                wars.Fields.Add(Eco("bizwarWin", "Победа в войне за бизнес, $", 0, 100_000_000, () => Main.BizwarWin, v => Main.BizwarWin = v));
                wars.Fields.Add(Eco("mafiaBizAward", "Мафии с бизнеса за PayDay, $", 0, 10_000_000, () => Main.MafiaForBiz, v => Main.MafiaForBiz = v));
                wars.Fields.Add(Eco("gangPointAward", "Банде с территории за PayDay, $", 0, 10_000_000, () => Main.GangForPoint, v => Main.GangForPoint = v));
                s.Groups.Add(wars);

                var services = new Group { Title = "Услуги и лимиты" };
                services.Fields.Add(Eco("minGunLic", "Лицензия на оружие у полиции от, $", 0, 10_000_000, () => Main.MinGunLic, v => Main.MinGunLic = v));
                services.Fields.Add(Eco("maxGunLic", "Лицензия на оружие у полиции до, $", 0, 10_000_000, () => Main.MaxGunLic, v => Main.MaxGunLic = v));
                services.Fields.Add(Eco("minHeal", "Лечение у EMS от, $", 0, 10_000_000, () => Main.MinHealLimit, v => Main.MinHealLimit = v));
                services.Fields.Add(Eco("maxHeal", "Лечение у EMS до, $", 0, 10_000_000, () => Main.MaxHealLimit, v => Main.MaxHealLimit = v));
                services.Fields.Add(Eco("maxTicket", "Штраф, максимум, $", 0, 10_000_000, () => Main.TicketLimit, v => Main.TicketLimit = v));
                services.Fields.Add(Eco("hotelRent", "Отель, $ в час", 0, 1_000_000, () => Main.HotelRent, v => Main.HotelRent = v));
                services.Fields.Add(Eco("evacCar", "Эвакуация машины, $", 0, 1_000_000, () => Main.EvacCar, v => Main.EvacCar = v));
                services.Fields.Add(Eco("adCost", "Объявление, $ за символ", 0, 100_000, () => Main.AdSymbCost, v => Main.AdSymbCost = v));
                services.Fields.Add(Eco("minDice", "Кости: ставка от, $", 0, 100_000_000, () => Main.MinDice, v => Main.MinDice = v));
                services.Fields.Add(Eco("maxDice", "Кости: ставка до, $", 0, 100_000_000, () => Main.MaxDice, v => Main.MaxDice = v));
                s.Groups.Add(services);

                s.Validate = () =>
                {
                    if (Main.MinGunLic > Main.MaxGunLic) return "Лицензия у полиции: «от» больше «до»";
                    if (Main.MinHealLimit > Main.MaxHealLimit) return "Лечение: «от» больше «до»";
                    if (Main.MinDice > Main.MaxDice) return "Кости: «от» больше «до»";
                    return null;
                };
                s.Save = () =>
                {
                    var fields = s.AllFields.Where(f => f.Column != null).ToList();
                    var sql = "UPDATE `economy` SET " + string.Join(", ", fields.Select(f => $"`{f.Column}`=@{f.Column}"));
                    NeptuneEvo.Database.DbQueue.Enqueue(sql, fields.Select(f => ("@" + f.Column, (object)(int)Math.Round(f.Get()))).ToArray());
                };
                s.Apply = ApplyMavrPrices;
                s.Reload = () =>
                {
                    Economy.Init();
                    ApplyMavrPrices();
                };
                s.Info = () => new List<string>
                {
                    $"Сейчас у Мавра: мед. карта {Money(Main.BlackMarketMedCard)}, лицензия {Money(Main.BlackMarketGunLic)}",
                };
                sections.Add(s);
            }

            // --- Сервер и налоги ---
            {
                var multipliers = Enumerable.Range(1, 5).Select(i => ((double)i, $"x{i}")).ToList();
                var s = new Section { Id = "server", Title = "Сервер и налоги", Source = "settings/serverSettings.json, pricesSettings.json", EditLevel = EconomyLevel };
                s.Files.Add("settings/serverSettings.json");
                s.Files.Add("settings/pricesSettings.json");
                var mult = new Group { Title = "Множители (акции)" };
                mult.Fields.Add(Select("moneyMultiplier", "Множитель денег (зарплаты, работы, пособия)", multipliers, () => Main.ServerSettings.MoneyMultiplier, v => Main.ServerSettings.MoneyMultiplier = (int)Math.Round(v)));
                mult.Fields.Add(Select("expMultiplier", "Множитель опыта", multipliers, () => Main.ServerSettings.ExpMultiplier, v => Main.ServerSettings.ExpMultiplier = (int)Math.Round(v)));
                s.Groups.Add(mult);

                var taxes = new Group { Title = "Налоги" };
                taxes.Fields.Add(Bool("isHouseTax", "Списывать налог на дома", () => Main.ServerSettings.IsHouseTax, v => Main.ServerSettings.IsHouseTax = v));
                taxes.Fields.Add(Bool("isBusinessTax", "Списывать налог на бизнесы", () => Main.ServerSettings.IsBusinessTax, v => Main.ServerSettings.IsBusinessTax = v));
                taxes.Fields.Add(Float("houseTaxPercent", "Налог на дом, % от цены в час", 0.001, 1, () => HouseManager.HouseTax, v =>
                {
                    HouseManager.HouseTax = v;
                    Main.ServerSettings.HouseTaxPercent = v;
                }, "0.026 — дом за $1 000 000 платит $260 в час"));
                s.Groups.Add(taxes);

                var help = new Group { Title = "Пособия (за PayDay)" };
                help.Fields.Add(Int("posobieNew", "Пособие новичкам (до 30 ур.), $", 0, 1_000_000, () => Main.PricesSettings.PosobieNew, v => Main.PricesSettings.PosobieNew = v));
                help.Fields.Add(Int("posobieOld", "Пособие остальным, $", 0, 1_000_000, () => Main.PricesSettings.PosobieOld, v => Main.PricesSettings.PosobieOld = v));
                s.Groups.Add(help);

                s.Save = () =>
                {
                    Settings.Save("serverSettings", Main.ServerSettings);
                    Settings.Save("pricesSettings", Main.PricesSettings);
                };
                s.Info = () => new List<string>
                {
                    $"Дом за $1 000 000: налог {Money(1_000_000 / 100.0 * HouseManager.HouseTax)} в час, {Money(1_000_000 / 100.0 * HouseManager.HouseTax * 24)} в сутки",
                    Main.ServerSettings.MoneyMultiplier > 1 || Main.ServerSettings.ExpMultiplier > 1
                        ? $"Идёт акция: деньги x{Main.ServerSettings.MoneyMultiplier}, опыт x{Main.ServerSettings.ExpMultiplier}"
                        : "Множители обычные (x1)",
                };
                sections.Add(s);
            }

            // --- Зарплаты фракций ---
            {
                var s = new Section { Id = "salaries", Title = "Зарплаты фракций", Source = "БД: таблица fractionranks", EditLevel = EconomyLevel };
                foreach (var (fractionId, type) in Manager.FractionTypes.OrderBy(f => f.Key))
                {
                    if (type != FractionsType.Gov && type != FractionsType.Nongov)
                        continue;
                    var fractionData = Manager.GetFractionData(fractionId);
                    if (fractionData?.Ranks == null || fractionData.Ranks.Count == 0)
                        continue;
                    var fractionName = Manager.FractionNames.TryGetValue(fractionId, out var n) ? n : $"Фракция {fractionId}";
                    var group = new Group { Title = fractionName };
                    foreach (var (rank, rankData) in fractionData.Ranks.OrderBy(r => r.Key))
                    {
                        var fid = fractionId;
                        var rid = rank;
                        var field = Int($"{fid}.{rid}", $"{rid}. {rankData.Name}", 0, 1_000_000, () => rankData.Salary, v => rankData.Salary = v);
                        field.Persist = v => NeptuneEvo.Database.DbQueue.Enqueue(
                            "UPDATE `fractionranks` SET `payday`=@payday WHERE `fraction`=@fraction AND `rank`=@rank",
                            ("@payday", (int)Math.Round(v)), ("@fraction", fid), ("@rank", rid));
                        group.Fields.Add(field);
                    }
                    s.Groups.Add(group);
                }
                s.Info = () =>
                {
                    long total = 0;
                    var count = 0;
                    foreach (var player in Character.Repository.GetPlayers())
                    {
                        var member = player.GetFractionMemberData();
                        if (member == null || !Manager.FractionTypes.TryGetValue(member.Id, out var type))
                            continue;
                        if (type != FractionsType.Gov && type != FractionsType.Nongov)
                            continue;
                        var data = Manager.GetFractionData(member.Id);
                        if (data?.Ranks == null || !data.Ranks.TryGetValue(member.Rank, out var rankData))
                            continue;
                        total += rankData.Salary * Main.ServerSettings.MoneyMultiplier;
                        count++;
                    }
                    return new List<string>
                    {
                        $"Сейчас в сети {count} сотрудников гос. фракций: на ближайший PayDay уйдёт ~{Money(total)} зарплат (без VIP-бонусов)",
                        "Зарплата начисляется с учётом множителя денег; изменения действуют со следующего PayDay",
                    };
                };
                sections.Add(s);
            }

            return sections;
        }

        private static void ApplyMavrPrices()
        {
            SetMatsPrice(1, Main.BlackMarketDrill);
            SetMatsPrice(2, Main.BlackMarketLockPick);
            SetMatsPrice(3, Main.BlackMarketArmyLockPick);
            SetMatsPrice(4, Main.BlackMarketCuffs);
            SetMatsPrice(5, Main.BlackMarketPocket);
            SetMatsPrice(6, Main.BlackMarketWanted);
            SetMatsPrice(69, Main.BlackMarketUnCuff);
            SetMatsPrice(78, Main.BlackMarketGunLic);
            SetMatsPrice(79, Main.BlackMarketMedCard);
            SetMatsPrice(80, Main.BlackQrFake);
            SetMatsPrice(81, Main.BlackRadioInterceptord);
        }

        // ------------------------------------------------------------------ права

        private static int AdminLevel(ExtPlayer player) => player.GetCharacterData()?.AdminLVL ?? 0;

        private static bool IsDirector(ExtPlayer player)
        {
            var login = player.GetAccountData()?.Login;
            return login != null && CommandsAccess.LoginsDirector.Contains(login);
        }

        private static bool CanView(ExtPlayer player) =>
            player.IsCharacterData() && (AdminLevel(player) >= ViewLevel || IsDirector(player));

        private static bool CanEdit(ExtPlayer player, Section section) =>
            AdminLevel(player) >= section.EditLevel || IsDirector(player);

        private static bool CanManagePresets(ExtPlayer player) => AdminLevel(player) >= EditLevel || IsDirector(player);

        // ------------------------------------------------------------------ история, бэкапы, пресеты

        private static List<HistoryEntry> _history;

        private static List<HistoryEntry> History
        {
            get
            {
                if (_history != null)
                    return _history;
                try
                {
                    _history = File.Exists(HistoryPath)
                        ? JsonConvert.DeserializeObject<List<HistoryEntry>>(File.ReadAllText(HistoryPath))
                        : null;
                }
                catch (Exception e)
                {
                    Log.Write($"Не удалось прочитать {HistoryPath}: {e.Message}");
                }
                return _history ??= new List<HistoryEntry>();
            }
        }

        private static void SaveHistory()
        {
            try
            {
                if (History.Count > HistoryLimit)
                    History.RemoveRange(0, History.Count - HistoryLimit);
                Directory.CreateDirectory("settings");
                File.WriteAllText(HistoryPath, JsonConvert.SerializeObject(History, Formatting.Indented));
            }
            catch (Exception e)
            {
                Log.Write($"Не удалось сохранить {HistoryPath}: {e.Message}");
            }
        }

        /// <summary>Копия файлов вкладки (или снимок значений, если вкладка в БД) — 10 последних на вкладку.</summary>
        private static void Backup(Section section)
        {
            try
            {
                Directory.CreateDirectory(BackupDir);
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var snapshot = section.AllFields.ToDictionary(f => f.Key, f => Math.Round(f.Get(), 4));
                File.WriteAllText(Path.Combine(BackupDir, $"{section.Id}.{stamp}.values.json"), JsonConvert.SerializeObject(snapshot, Formatting.Indented));
                foreach (var file in section.Files.Where(File.Exists))
                    File.Copy(file, Path.Combine(BackupDir, $"{section.Id}.{stamp}.{Path.GetFileName(file)}"), true);

                // Храним 10 последних сохранений вкладки (по метке времени)
                var stamps = Directory.GetFiles(BackupDir, $"{section.Id}.*")
                    .Select(p => Path.GetFileName(p).Split('.'))
                    .Where(p => p.Length > 2)
                    .Select(p => p[1])
                    .Distinct()
                    .OrderByDescending(p => p, StringComparer.Ordinal)
                    .Skip(BackupsPerSection)
                    .ToHashSet();
                foreach (var old in Directory.GetFiles(BackupDir, $"{section.Id}.*"))
                {
                    var parts = Path.GetFileName(old).Split('.');
                    if (parts.Length > 2 && stamps.Contains(parts[1]))
                        File.Delete(old);
                }
            }
            catch (Exception e)
            {
                Log.Write($"Бэкап {section.Id}: {e.Message}");
            }
        }

        private static string PresetPath(string name)
        {
            var clean = new string((name ?? "").Trim().Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_').ToArray()).Trim();
            if (clean.Length == 0 || clean.Length > 40)
                return null;
            return Path.Combine(PresetDir, clean + ".json");
        }

        private static List<object> Presets()
        {
            var list = new List<object>();
            try
            {
                if (!Directory.Exists(PresetDir))
                    return list;
                foreach (var file in Directory.GetFiles(PresetDir, "*.json").OrderBy(f => f))
                {
                    try
                    {
                        var data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, double>>>(File.ReadAllText(file));
                        list.Add(new
                        {
                            name = Path.GetFileNameWithoutExtension(file),
                            count = data?.Sum(s => s.Value?.Count ?? 0) ?? 0,
                            changes = data,
                        });
                    }
                    catch (Exception e)
                    {
                        Log.Write($"Пресет {file}: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"Пресеты: {e.Message}");
            }
            return list;
        }

        // ------------------------------------------------------------------ отправка в CEF

        private static string BuildJson(ExtPlayer player, List<Section> sections)
        {
            var labels = sections.ToDictionary(s => s.Id, s => s.Title);
            return JsonConvert.SerializeObject(new
            {
                sections = sections.Select(s => new
                {
                    id = s.Id,
                    title = s.Title,
                    file = s.Source,
                    canEdit = CanEdit(player, s),
                    canReload = s.Reload != null && CanEdit(player, s),
                    editLevel = s.EditLevel,
                    info = SafeInfo(s),
                    groups = s.Groups.Where(g => g.Fields.Count > 0).Select(g => new
                    {
                        title = g.Title,
                        fields = g.Fields.Select(f => new
                        {
                            key = f.Key,
                            label = f.Label,
                            hint = f.Hint,
                            type = f.Kind,
                            min = f.Min,
                            max = f.Max,
                            options = f.Options?.Select(o => new { value = o.value, label = o.label }),
                            value = Math.Round(f.Get(), 4),
                        })
                    })
                }),
                history = (CanView(player) ? History.AsEnumerable().Reverse().Take(HistorySend) : Enumerable.Empty<HistoryEntry>()).Select(h => new
                {
                    id = h.Id,
                    time = h.Time,
                    admin = h.Admin,
                    section = h.Section,
                    sectionTitle = h.SectionTitle ?? (labels.TryGetValue(h.Section ?? "", out var t) ? t : h.Section),
                    key = h.Key,
                    label = h.Label,
                    old = h.Old,
                    @new = h.New,
                }),
                presets = CanView(player) ? Presets() : new List<object>(),
                canPresets = CanManagePresets(player),
                // Справочник команд (вкладка «Команды» — список приходит отдельно, client.cfgpanel.commands)
                canView = CanView(player),
                adminLevel = AdminLevel(player),
                canReloadDoc = AdminLevel(player) >= EditLevel || IsDirector(player),
            });
        }

        private static List<string> SafeInfo(Section section)
        {
            try
            {
                return section.Info();
            }
            catch (Exception e)
            {
                Log.Write($"Info {section.Id}: {e.Message}");
                return new List<string>();
            }
        }

        private static void SendResult(ExtPlayer player, bool ok, string message) =>
            Trigger.ClientEvent(player, "client.cfgpanel.result", ok, message, BuildJson(player, BuildSections()));

        // ------------------------------------------------------------------ применение изменений

        /// <summary>
        /// Общий путь для «Сохранить», пресетов и отката: проверка прав и диапазонов, бэкап, запись, история, уведомления.
        /// Возвращает текст для админа; ok=false, если хоть что-то не прошло.
        /// </summary>
        private static (bool ok, string message) ApplyChanges(ExtPlayer player, Dictionary<string, Dictionary<string, double>> changes, string reason)
        {
            var sections = BuildSections();
            var errors = new List<string>();
            var applied = new List<string>();
            var notify = new List<string>();

            foreach (var (sectionId, fields) in changes)
            {
                var section = sections.FirstOrDefault(s => s.Id == sectionId);
                if (section == null || fields == null || fields.Count == 0)
                    continue;
                if (!CanEdit(player, section))
                {
                    errors.Add($"{section.Title}: нужно {section.EditLevel} ур. админки");
                    continue;
                }

                var sectionError = false;
                foreach (var (key, raw) in fields)
                {
                    var field = section.AllFields.FirstOrDefault(f => f.Key == key);
                    if (field == null)
                        continue;
                    var badOption = field.Options != null && !field.Options.Any(o => Math.Abs(o.value - raw) < 0.00001);
                    if (double.IsNaN(raw) || double.IsInfinity(raw) || raw < field.Min || raw > field.Max || badOption)
                    {
                        errors.Add($"{section.Title}: «{field.Label}» — от {Num(field.Min)} до {Num(field.Max)}");
                        sectionError = true;
                    }
                }
                if (sectionError)
                    continue;

                var changed = fields
                    .Select(c => (field: section.AllFields.FirstOrDefault(f => f.Key == c.Key), raw: c.Value))
                    .Where(c => c.field != null)
                    .Select(c => (c.field, old: c.field.Get(), value: c.field.Kind == "float" ? c.raw : Math.Round(c.raw)))
                    .Where(c => Math.Abs(c.old - c.value) >= 0.00001)
                    .ToList();
                if (changed.Count == 0)
                    continue;

                Backup(section);
                foreach (var c in changed)
                    c.field.Set(c.value);

                var validateError = section.Validate();
                if (validateError != null)
                {
                    foreach (var c in changed)
                        c.field.Set(c.old);
                    errors.Add($"{section.Title}: {validateError}");
                    continue;
                }

                section.Save();
                foreach (var c in changed)
                    c.field.Persist?.Invoke(c.field.Get());
                section.Apply();

                var nextId = History.Count > 0 ? History.Max(h => h.Id) + 1 : 1;
                foreach (var c in changed)
                {
                    var now = c.field.Get();
                    History.Add(new HistoryEntry
                    {
                        Id = nextId++,
                        Time = DateTime.Now.ToString("dd.MM HH:mm"),
                        Admin = player.Name,
                        Section = section.Id,
                        SectionTitle = section.Title,
                        Key = c.field.Key,
                        Label = c.field.Label,
                        Old = Math.Round(c.old, 4),
                        New = Math.Round(now, 4),
                    });
                    var line = $"cfg{reason} {section.Id}.{c.field.Key}: {Num(c.old)} -> {Num(now)}";
                    GameLog.Admin(player.Name, line, "");
                    Log.Write($"{player.Name}: {line}");
                    notify.Add($"{section.Title}: «{c.field.Label}» {Num(c.old)} → {Num(now)}");
                }
                applied.Add($"{section.Title} ({changed.Count})");
            }

            if (notify.Count > 0)
            {
                SaveHistory();
                var head = $"~y~[CFG] {player.Name}{reason}:";
                if (notify.Count <= 3)
                    foreach (var line in notify)
                        Trigger.SendToAdmins(ViewLevel, $"{head} {line}");
                else
                    Trigger.SendToAdmins(ViewLevel, $"{head} изменено {notify.Count} настроек — {string.Join(", ", applied)} (подробно в /cfg → История)");
            }

            var message = applied.Count > 0 ? "Сохранено: " + string.Join(", ", applied) : (errors.Count == 0 ? "Изменений нет" : "");
            if (errors.Count > 0)
                message = (message.Length > 0 ? message + ". " : "") + "Не сохранено: " + string.Join("; ", errors);
            return (errors.Count == 0, message);
        }

        private static Dictionary<string, Dictionary<string, double>> ParseChanges(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, double>>>(json ?? "{}")
                       ?? new Dictionary<string, Dictionary<string, double>>();
            }
            catch
            {
                return new Dictionary<string, Dictionary<string, double>>();
            }
        }

        // ------------------------------------------------------------------ команды и события

        [Command(AdminCommands.cfgpanel)]
        public static void CMD_ConfigPanel(ExtPlayer player)
        {
            try
            {
                if (!player.IsCharacterData())
                    return;
                // С 1 уровня — справочник команд; настройки, история и пресеты — только при CanView
                if (AdminLevel(player) < 1 && !IsDirector(player))
                    return;
                Trigger.ClientEvent(player, "client.cfgpanel.open", BuildJson(player, CanView(player) ? BuildSections() : new List<Section>()));
                SendCommands(player);
            }
            catch (Exception e)
            {
                Log.Write($"CMD_ConfigPanel Exception: {e}");
            }
        }

        /// <summary>changesJson: {"weed": {"seedPrice": 200, ...}, ...} — только изменённые поля.</summary>
        [RemoteEvent("server.cfgpanel.save")]
        public static void Save(ExtPlayer player, string changesJson)
        {
            try
            {
                if (!CanView(player))
                    return;
                var (ok, message) = ApplyChanges(player, ParseChanges(changesJson), "");
                SendResult(player, ok, message);
            }
            catch (Exception e)
            {
                Log.Write($"Save Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ справочник команд

        /// <summary>Список команд по уровню админа — частями по 16 КБ (описания длинные).</summary>
        private static void SendCommands(ExtPlayer player)
        {
            if (!AdminCommandsDoc.Loaded)
                AdminCommandsDoc.Load();
            var level = IsDirector(player) ? Math.Max(AdminLevel(player), 9) : AdminLevel(player);
            var json = JsonConvert.SerializeObject(new
            {
                loaded = AdminCommandsDoc.Loaded,
                source = AdminCommandsDoc.Source,
                commands = AdminCommandsDoc.For(level),
            });
            const int chunk = 16000;
            var total = (json.Length + chunk - 1) / chunk;
            for (var i = 0; i < total; i++)
                Trigger.ClientEvent(player, "client.cfgpanel.commands", i, total, json.Substring(i * chunk, Math.Min(chunk, json.Length - i * chunk)));
        }

        [RemoteEvent("server.cfgpanel.docReload")]
        public static void DocReload(ExtPlayer player)
        {
            try
            {
                if (!player.IsCharacterData() || AdminLevel(player) < EditLevel && !IsDirector(player))
                    return;
                var text = AdminCommandsDoc.Load();
                SendCommands(player);
                Trigger.ClientEvent(player, "client.cfgpanel.result", AdminCommandsDoc.Loaded, text, BuildJson(player, CanView(player) ? BuildSections() : new List<Section>()));
            }
            catch (Exception e)
            {
                Log.Write($"DocReload Exception: {e}");
            }
        }

        [RemoteEvent("server.cfgpanel.rollback")]
        public static void Rollback(ExtPlayer player, int historyId)
        {
            try
            {
                if (!CanView(player))
                    return;
                var entry = History.FirstOrDefault(h => h.Id == historyId);
                if (entry == null)
                {
                    SendResult(player, false, "Запись истории не найдена");
                    return;
                }
                var changes = new Dictionary<string, Dictionary<string, double>>
                {
                    [entry.Section] = new Dictionary<string, double> { [entry.Key] = entry.Old },
                };
                var (ok, message) = ApplyChanges(player, changes, $" (откат #{entry.Id})");
                SendResult(player, ok, message);
            }
            catch (Exception e)
            {
                Log.Write($"Rollback Exception: {e}");
            }
        }

        [RemoteEvent("server.cfgpanel.reload")]
        public static void Reload(ExtPlayer player, string sectionId)
        {
            try
            {
                if (!CanView(player))
                    return;
                var section = BuildSections().FirstOrDefault(s => s.Id == sectionId);
                if (section?.Reload == null || !CanEdit(player, section))
                {
                    SendResult(player, false, "Эту вкладку нельзя перечитать");
                    return;
                }
                section.Reload();
                GameLog.Admin(player.Name, $"cfg reload {section.Id}", "");
                Trigger.SendToAdmins(ViewLevel, $"~y~[CFG] {player.Name}: вкладка «{section.Title}» перечитана с диска");
                SendResult(player, true, $"«{section.Title}» перечитано: {section.Source}");
            }
            catch (Exception e)
            {
                Log.Write($"Reload Exception: {e}");
                SendResult(player, false, "Ошибка при перечитывании, см. лог ConfigPanel");
            }
        }

        [RemoteEvent("server.cfgpanel.preset.save")]
        public static void PresetSave(ExtPlayer player, string name, string changesJson)
        {
            try
            {
                if (!CanView(player) || !CanManagePresets(player))
                    return;
                var path = PresetPath(name);
                if (path == null)
                {
                    SendResult(player, false, "Название пресета: 1–40 символов, буквы, цифры, пробел, - и _");
                    return;
                }
                var changes = ParseChanges(changesJson);
                if (changes.Sum(s => s.Value?.Count ?? 0) == 0)
                {
                    SendResult(player, false, "Пресет пустой: сначала измени нужные поля, затем «В пресет»");
                    return;
                }
                Directory.CreateDirectory(PresetDir);
                File.WriteAllText(path, JsonConvert.SerializeObject(changes, Formatting.Indented));
                GameLog.Admin(player.Name, $"cfg preset save {Path.GetFileNameWithoutExtension(path)}", "");
                SendResult(player, true, $"Пресет «{Path.GetFileNameWithoutExtension(path)}» сохранён (не применён)");
            }
            catch (Exception e)
            {
                Log.Write($"PresetSave Exception: {e}");
            }
        }

        [RemoteEvent("server.cfgpanel.preset.apply")]
        public static void PresetApply(ExtPlayer player, string name)
        {
            try
            {
                if (!CanView(player))
                    return;
                var path = PresetPath(name);
                if (path == null || !File.Exists(path))
                {
                    SendResult(player, false, "Пресет не найден");
                    return;
                }
                var changes = ParseChanges(File.ReadAllText(path));
                var (ok, message) = ApplyChanges(player, changes, $" (пресет «{Path.GetFileNameWithoutExtension(path)}»)");
                SendResult(player, ok, message);
            }
            catch (Exception e)
            {
                Log.Write($"PresetApply Exception: {e}");
            }
        }

        [RemoteEvent("server.cfgpanel.preset.delete")]
        public static void PresetDelete(ExtPlayer player, string name)
        {
            try
            {
                if (!CanView(player) || !CanManagePresets(player))
                    return;
                var path = PresetPath(name);
                if (path != null && File.Exists(path))
                {
                    File.Delete(path);
                    GameLog.Admin(player.Name, $"cfg preset delete {Path.GetFileNameWithoutExtension(path)}", "");
                }
                SendResult(player, true, "Пресет удалён");
            }
            catch (Exception e)
            {
                Log.Write($"PresetDelete Exception: {e}");
            }
        }
    }
}
