using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.BlackMarket.Config;
using NeptuneEvo.Character;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Core;
using NeptuneEvo.Crime.Weed;
using NeptuneEvo.Handles;
using NeptuneEvo.Organizations.Contracts.Config;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Functions
{
    /// <summary>
    /// /cfg — админ-панель в CEF для настроек из settings/*.json (трава, Чёрный рынок, скупщик, подряды).
    /// Значения меняются в памяти сразу (модули читают XxxConfig.Current при каждом обращении),
    /// затем файл перезаписывается — рестарт не нужен. Каждое изменение пишется в adminlog.
    /// </summary>
    class ConfigPanel : Script
    {
        private static readonly nLog Log = new nLog("ConfigPanel");

        private class Field
        {
            public string Key;
            public string Label;
            public string Hint;
            /// <summary>int или float (дробное).</summary>
            public bool IsFloat;
            public double Min;
            public double Max;
            public Func<double> Get;
            public Action<double> Set;
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
            public string File;
            public List<Group> Groups = new List<Group>();
            /// <summary>Проверка связанных полей после изменения; вернуть текст ошибки или null.</summary>
            public Func<string> Validate = () => null;
            public Action Save;
            /// <summary>Что обновить в игре после сохранения (цены в меню и т.п.).</summary>
            public Action Apply = () => { };

            public IEnumerable<Field> AllFields => Groups.SelectMany(g => g.Fields);
        }

        private static Field Int(string key, string label, double min, double max, Func<double> get, Action<int> set, string hint = null) =>
            new Field { Key = key, Label = label, Hint = hint, Min = min, Max = max, Get = get, Set = v => set((int)Math.Round(v)) };

        private static Field Float(string key, string label, double min, double max, Func<double> get, Action<double> set, string hint = null) =>
            new Field { Key = key, Label = label, Hint = hint, IsFloat = true, Min = min, Max = max, Get = get, Set = set };

        private static string ItemName(int itemId) =>
            Chars.Repository.ItemsInfo.TryGetValue((ItemId)itemId, out var info) ? info.Name : $"Предмет {itemId}";

        /// <summary>Схема строится заново при каждом открытии: списки (покупатели, товары, материалы) могли измениться.</summary>
        private static List<Section> BuildSections()
        {
            var sections = new List<Section>();

            // --- Трава ---
            {
                var s = new Section { Id = "weed", Title = "Трава", File = "settings/weed.json", Save = WeedConfig.Save };
                var main = new Group { Title = "Цены у Мавра" };
                main.Fields.Add(Int("seedPrice", "Семена, $", 1, 1_000_000, () => WeedConfig.Current.SeedPrice, v => WeedConfig.Current.SeedPrice = v));
                main.Fields.Add(Int("waterPrice", "Бутылка воды, $", 1, 1_000_000, () => WeedConfig.Current.WaterPrice, v => WeedConfig.Current.WaterPrice = v));
                s.Groups.Add(main);

                var grow = new Group { Title = "Рост куста" };
                grow.Fields.Add(Int("growMinutes", "Рост до урожая, мин", 1, 1440, () => WeedConfig.Current.GrowMinutes, v => WeedConfig.Current.GrowMinutes = v));
                grow.Fields.Add(Int("waterMinutes", "Без полива засыхает через, мин", 1, 1440, () => WeedConfig.Current.WaterMinutes, v => WeedConfig.Current.WaterMinutes = v));
                grow.Fields.Add(Int("rotMinutes", "Созревший гниёт через, мин", 1, 10080, () => WeedConfig.Current.RotMinutes, v => WeedConfig.Current.RotMinutes = v));
                grow.Fields.Add(Int("dryMinutes", "Сушка, мин", 1, 1440, () => WeedConfig.Current.DryMinutes, v => WeedConfig.Current.DryMinutes = v));
                grow.Fields.Add(Int("yieldMin", "Урожай от, г", 1, 1000, () => WeedConfig.Current.YieldMin, v => WeedConfig.Current.YieldMin = v));
                grow.Fields.Add(Int("yieldMax", "Урожай до, г", 1, 1000, () => WeedConfig.Current.YieldMax, v => WeedConfig.Current.YieldMax = v));
                grow.Fields.Add(Int("homeMaxPlants", "Кустов в доме, макс.", 0, 100, () => WeedConfig.Current.HomeMaxPlants, v => WeedConfig.Current.HomeMaxPlants = v));
                grow.Fields.Add(Int("playerMaxPlants", "Кустов на игрока, макс.", 0, 100, () => WeedConfig.Current.PlayerMaxPlants, v => WeedConfig.Current.PlayerMaxPlants = v));
                s.Groups.Add(grow);

                var police = new Group { Title = "Риск" };
                police.Fields.Add(Int("harvestPoliceChance", "Заметят сбор на улице, %", 0, 100, () => WeedConfig.Current.HarvestPoliceChance, v => WeedConfig.Current.HarvestPoliceChance = v));
                police.Fields.Add(Int("sellPoliceChance", "Покупатель сдаст полиции, %", 0, 100, () => WeedConfig.Current.SellPoliceChance, v => WeedConfig.Current.SellPoliceChance = v));
                police.Fields.Add(Int("policeReward", "Награда полиции, $", 0, 1_000_000, () => WeedConfig.Current.PoliceReward, v => WeedConfig.Current.PoliceReward = v));
                s.Groups.Add(police);

                var buyers = WeedConfig.Current.Buyers ?? new List<WeedBuyer>();
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

                s.Validate = () =>
                {
                    var c = WeedConfig.Current;
                    if (c.YieldMin > c.YieldMax) return "Урожай «от» больше, чем «до»";
                    return null;
                };
                s.Apply = () =>
                {
                    // Цены семян и воды в меню Мавра
                    Fractions.Manager.FractionDataMats[502] = new Fractions.Manager.FracMatsData(502, "Семена конопли", Chars.Repository.ItemsInfo[ItemId.WeedSeed].Icon, $"{WeedConfig.Current.SeedPrice}$");
                    Fractions.Manager.FractionDataMats[503] = new Fractions.Manager.FracMatsData(503, "Бутылка воды", Chars.Repository.ItemsInfo[ItemId.WaterBottle].Icon, $"{WeedConfig.Current.WaterPrice}$");
                };
                sections.Add(s);
            }

            // --- Чёрный рынок ---
            {
                BlackMarketConfig C() => BlackMarketConfig.Current;
                var s = new Section { Id = "blackmarket", Title = "Чёрный рынок", File = "settings/blackmarket.json", Save = BlackMarketConfig.Save };
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
                sections.Add(s);
            }

            // --- Скупщик краденого ---
            {
                FenceConfig F() => BlackMarketConfig.Current.Fence;
                var s = new Section { Id = "fence", Title = "Скупка", File = "settings/blackmarket.json (fence)", Save = BlackMarketConfig.Save };
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
                sections.Add(s);
            }

            // --- Подряды ---
            {
                ContractsConfig C() => ContractsConfig.Current;
                var s = new Section { Id = "contracts", Title = "Подряды", File = "settings/org_contracts.json", Save = ContractsConfig.Save };
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
                sections.Add(s);
            }

            return sections;
        }

        private static string BuildJson(List<Section> sections) => JsonConvert.SerializeObject(sections.Select(s => new
        {
            id = s.Id,
            title = s.Title,
            file = s.File,
            groups = s.Groups.Where(g => g.Fields.Count > 0).Select(g => new
            {
                title = g.Title,
                fields = g.Fields.Select(f => new
                {
                    key = f.Key,
                    label = f.Label,
                    hint = f.Hint,
                    type = f.IsFloat ? "float" : "int",
                    min = f.Min,
                    max = f.Max,
                    value = Math.Round(f.Get(), 4),
                })
            })
        }));

        private static bool CanManage(ExtPlayer player) =>
            player.IsCharacterData() && CommandsAccess.CanUseCmd(player, AdminCommands.cfgpanel);

        [Command(AdminCommands.cfgpanel)]
        public static void CMD_ConfigPanel(ExtPlayer player)
        {
            try
            {
                if (!CanManage(player))
                    return;
                Trigger.ClientEvent(player, "client.cfgpanel.open", BuildJson(BuildSections()));
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
                if (!CanManage(player))
                    return;

                var changes = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, double>>>(changesJson ?? "{}")
                              ?? new Dictionary<string, Dictionary<string, double>>();
                var sections = BuildSections();
                var errors = new List<string>();
                var applied = new List<string>();

                foreach (var (sectionId, fields) in changes)
                {
                    var section = sections.FirstOrDefault(s => s.Id == sectionId);
                    if (section == null || fields == null || fields.Count == 0)
                        continue;

                    // Сначала всё проверяем, потом меняем; при ошибке связанных полей — откат секции целиком
                    var backup = new List<(Field field, double old)>();
                    var sectionError = false;
                    foreach (var (key, raw) in fields)
                    {
                        var field = section.AllFields.FirstOrDefault(f => f.Key == key);
                        if (field == null)
                            continue;
                        if (double.IsNaN(raw) || double.IsInfinity(raw) || raw < field.Min || raw > field.Max)
                        {
                            errors.Add($"{section.Title}: «{field.Label}» — от {field.Min.ToString(CultureInfo.InvariantCulture)} до {field.Max.ToString(CultureInfo.InvariantCulture)}");
                            sectionError = true;
                        }
                    }
                    if (sectionError)
                        continue;

                    foreach (var (key, raw) in fields)
                    {
                        var field = section.AllFields.FirstOrDefault(f => f.Key == key);
                        if (field == null)
                            continue;
                        var old = field.Get();
                        var value = field.IsFloat ? raw : Math.Round(raw);
                        if (Math.Abs(old - value) < 0.00001)
                            continue;
                        backup.Add((field, old));
                        field.Set(value);
                    }

                    var validateError = section.Validate();
                    if (validateError != null)
                    {
                        foreach (var (field, old) in backup)
                            field.Set(old);
                        errors.Add($"{section.Title}: {validateError}");
                        continue;
                    }
                    if (backup.Count == 0)
                        continue;

                    section.Save();
                    section.Apply();
                    foreach (var (field, old) in backup)
                    {
                        var line = $"cfg {section.Id}.{field.Key}: {Math.Round(old, 4).ToString(CultureInfo.InvariantCulture)} -> {Math.Round(field.Get(), 4).ToString(CultureInfo.InvariantCulture)}";
                        GameLog.Admin(player.Name, line, "");
                        Log.Write($"{player.Name}: {line}");
                    }
                    applied.Add($"{section.Title} ({backup.Count})");
                }

                var message = errors.Count > 0
                    ? "Не сохранено: " + string.Join("; ", errors)
                    : applied.Count > 0 ? "Сохранено: " + string.Join(", ", applied) : "Изменений нет";
                if (applied.Count > 0 && errors.Count > 0)
                    message = "Сохранено: " + string.Join(", ", applied) + ". " + message;

                Trigger.ClientEvent(player, "client.cfgpanel.result", errors.Count == 0, message, BuildJson(BuildSections()));
            }
            catch (Exception e)
            {
                Log.Write($"Save Exception: {e}");
            }
        }
    }
}
