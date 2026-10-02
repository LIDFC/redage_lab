using System;
using System.Collections.Generic;
using System.Linq;
using GTANetworkAPI;
using Localization;
using MySqlConnector;
using NeptuneEvo.Character;
using NeptuneEvo.Chars;
using NeptuneEvo.Core;
using NeptuneEvo.Fractions.Models;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Fractions.Wardrobe
{
    /// <summary>
    /// Гардероб фракций: сотрудник сам собирает форму из разрешённых вещей своей фракции (FractionClothingSetsData)
    /// и выбирает торс (компонент 3 — отображение рук/тела). Образ хранится за игроком (fraction_outfits).
    /// Образы фракции (fraction_presets): с 9 ранга сохраняются готовые комплекты для всех членов фракции,
    /// любой сотрудник примеряет их в окне и надевает как свой образ.
    /// Окно — CEF FractionWardrobe (src_cef/src/views/fractions/wardrobe), клиент — src_client/fractions/wardrobe.js.
    /// Надевается теми же «спец-вещами», что и старые наборы (SetSpecialClothes/SetSpecialAccessories), инвентарь не трогается.
    /// </summary>
    class Wardrobe : Script
    {
        private static readonly nLog Log = new nLog("Fractions.Wardrobe");

        /// <summary>Имя «набора» в WorkData.OnDutyName, когда на смене в своём образе.</summary>
        public const string DutyName = "outfit";

        /// <summary>Категории окна по порядку: компонент одежды → подпись.</summary>
        private static readonly (ClothesComponent component, string title)[] Categories =
        {
            (ClothesComponent.Tops, "Верх"),
            (ClothesComponent.Undershort, "Футболка"),
            (ClothesComponent.Legs, "Низ"),
            (ClothesComponent.Shoes, "Обувь"),
            (ClothesComponent.Hat, "Головной убор"),
            (ClothesComponent.Glasses, "Очки"),
            (ClothesComponent.Masks, "Маска"),
            (ClothesComponent.Accessories, "Аксессуары"),
            (ClothesComponent.BodyArmors, "Бронежилет"),
            (ClothesComponent.Decals, "Нашивки"),
            (ClothesComponent.Bugs, "Сумка"),
            (ClothesComponent.Ears, "Наушники"),
            (ClothesComponent.Watches, "Часы"),
            (ClothesComponent.Bracelets, "Браслеты"),
        };

        public class Outfit
        {
            /// <summary>Компонент (имя enum) → [id вещи, текстура].</summary>
            [JsonProperty("items")] public Dictionary<string, int[]> Items { get; set; } = new Dictionary<string, int[]>();
            /// <summary>Торс: [drawable, текстура] или null — как подбирает игра под верх.</summary>
            [JsonProperty("torso")] public int[] Torso { get; set; }
        }

        private static readonly Dictionary<(int uuid, int fraction, bool gender), Outfit> Outfits = new Dictionary<(int, int, bool), Outfit>();
        private static readonly HashSet<int> Loaded = new HashSet<int>();
        /// <summary>Торс, выбранный в гардеробе, — ставится поверх расчёта в ClothesComponents.SetTop.</summary>
        private static readonly Dictionary<ExtPlayer, (int drawable, int texture)> TorsoOverride = new Dictionary<ExtPlayer, (int, int)>();
        private static bool _ready;

        /// <summary>Образ фракции — общий комплект, сохранённый рангом 9+.</summary>
        private class Preset
        {
            public long Id;
            public int Fraction;
            public bool Gender;
            public string Name;
            public string Author;
            public Outfit Outfit;
        }

        private const int PresetRank = 9;
        private const int PresetMax = 30;
        private static readonly List<Preset> Presets = new List<Preset>();
        private static bool _presetsReady;

        // ------------------------------------------------------------------ каталог (только AdminLVL 9)
        private const int CatalogAdmin = 9;
        private const int CatalogPage = 60;
        /// <summary>Вещи, добавленные из каталога (fraction, gender, component, id) — их можно убрать; вещи из кода — нет.</summary>
        private static readonly HashSet<(int, bool, ClothesComponent, int)> Extras = new HashSet<(int, bool, ClothesComponent, int)>();
        private static bool _extraReady;

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            try
            {
                using var create = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS `fraction_outfits` (
                    `uuid` INT NOT NULL,
                    `fraction` INT NOT NULL,
                    `gender` TINYINT(1) NOT NULL,
                    `outfit` TEXT NOT NULL,
                    PRIMARY KEY (`uuid`, `fraction`, `gender`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");
                MySQL.Query(create);
                _ready = true;

                using var createPresets = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS `fraction_presets` (
                    `id` BIGINT NOT NULL,
                    `fraction` INT NOT NULL,
                    `gender` TINYINT(1) NOT NULL,
                    `name` VARCHAR(40) NOT NULL,
                    `author` VARCHAR(64) NOT NULL DEFAULT '',
                    `outfit` TEXT NOT NULL,
                    PRIMARY KEY (`id`),
                    KEY `fraction` (`fraction`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");
                MySQL.Query(createPresets);

                using var createExtra = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS `fraction_clothes_extra` (
                    `fraction` INT NOT NULL,
                    `gender` TINYINT(1) NOT NULL,
                    `component` VARCHAR(32) NOT NULL,
                    `clothes_id` INT NOT NULL,
                    PRIMARY KEY (`fraction`, `gender`, `component`, `clothes_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");
                MySQL.Query(createExtra);
                // Читаем после старта: к этому времени FractionClothingSets уже собрал базовые списки
                NeptuneEvo.Database.DbQueue.ReadThen("SELECT * FROM `fraction_clothes_extra`", table =>
                {
                    var count = 0;
                    if (table != null)
                        foreach (System.Data.DataRow row in table.Rows)
                        {
                            if (!Enum.TryParse<ClothesComponent>(row["component"].ToString(), out var component))
                                continue;
                            if (AddExtra(Convert.ToInt32(row["fraction"]), Convert.ToInt32(row["gender"]) == 1, component, Convert.ToInt32(row["clothes_id"])))
                                count++;
                        }
                    _extraReady = true;
                    if (count > 0)
                        Log.Write($"Каталог: добавлено вещей в формы фракций из БД — {count}");
                });
                NeptuneEvo.Database.DbQueue.ReadThen("SELECT * FROM `fraction_presets` ORDER BY `id`", table =>
                {
                    if (table != null)
                        foreach (System.Data.DataRow row in table.Rows)
                        {
                            try
                            {
                                var outfit = JsonConvert.DeserializeObject<Outfit>(row["outfit"].ToString());
                                if (outfit == null)
                                    continue;
                                Presets.Add(new Preset
                                {
                                    Id = Convert.ToInt64(row["id"]),
                                    Fraction = Convert.ToInt32(row["fraction"]),
                                    Gender = Convert.ToInt32(row["gender"]) == 1,
                                    Name = row["name"].ToString(),
                                    Author = row["author"].ToString(),
                                    Outfit = outfit,
                                });
                            }
                            catch (Exception e)
                            {
                                Log.Write($"Presets row Exception: {e.Message}");
                            }
                        }
                    _presetsReady = true;
                });
            }
            catch (Exception e)
            {
                Log.Write($"OnResourceStart Exception: {e}");
            }
        }

        public static bool TryGetTorso(ExtPlayer player, out int drawable, out int texture)
        {
            var sessionData = player?.GetSessionData();
            if (sessionData != null && sessionData.WorkData.OnDutyName == DutyName && TorsoOverride.TryGetValue(player, out var torso))
            {
                drawable = torso.drawable;
                texture = torso.texture;
                return true;
            }
            drawable = texture = 0;
            return false;
        }

        // ------------------------------------------------------------------ хранение

        /// <summary>Образ из памяти (загружается в фоне при первом открытии гардероба — LoadOutfits).</summary>
        private static Outfit GetOutfit(int uuid, int fraction, bool gender) =>
            Outfits.TryGetValue((uuid, fraction, gender), out var result) ? result : null;

        private static readonly HashSet<int> LoadingOutfits = new HashSet<int>();

        /// <summary>Образы игрока из БД — в фоне, затем onDone в игровом потоке. true — уже в памяти.</summary>
        private static bool LoadOutfits(ExtPlayer player, int uuid, Action onDone)
        {
            if (!_ready || Loaded.Contains(uuid))
                return true;
            if (!LoadingOutfits.Add(uuid))
                return false;
            NeptuneEvo.Database.DbQueue.ReadThen("SELECT `fraction`,`gender`,`outfit` FROM `fraction_outfits` WHERE `uuid`=@u", table =>
            {
                LoadingOutfits.Remove(uuid);
                if (!player.IsCharacterData() || player.GetUUID() != uuid)
                    return;
                Loaded.Add(uuid);
                if (table != null)
                    foreach (System.Data.DataRow row in table.Rows)
                    {
                        try
                        {
                            var outfit = JsonConvert.DeserializeObject<Outfit>(row["outfit"].ToString());
                            var key = (uuid, Convert.ToInt32(row["fraction"]), Convert.ToInt32(row["gender"]) == 1);
                            if (outfit != null && !Outfits.ContainsKey(key))
                                Outfits[key] = outfit;
                        }
                        catch (Exception e)
                        {
                            Log.Write($"LoadOutfits row Exception: {e.Message}");
                        }
                    }
                onDone?.Invoke();
            }, ("@u", uuid));
            return false;
        }

        private static void SaveOutfit(int uuid, int fraction, bool gender, Outfit outfit)
        {
            Outfits[(uuid, fraction, gender)] = outfit;
            NeptuneEvo.Database.DbQueue.Enqueue(
                @"INSERT INTO `fraction_outfits` (`uuid`,`fraction`,`gender`,`outfit`) VALUES (@u,@f,@g,@o)
                  ON DUPLICATE KEY UPDATE `outfit`=VALUES(`outfit`)",
                ("@u", uuid), ("@f", fraction), ("@g", gender ? 1 : 0), ("@o", JsonConvert.SerializeObject(outfit)));
        }

        // ------------------------------------------------------------------ окно

        private static bool NearCloakroom(ExtPlayer player, int fraction) =>
            FractionClothingSets.FractionMainCloakrooms.TryGetValue(fraction, out var main) && player.Position.DistanceTo(main) < 5 ||
            FractionClothingSets.FractionSecondCloakrooms.TryGetValue(fraction, out var second) && player.Position.DistanceTo(second) < 5;

        private static Dictionary<ClothesComponent, List<FractionClothesData>> Allowed(int fraction, bool gender) =>
            FractionClothingSets.FractionAvailableSets.TryGetValue(gender, out var byFraction) &&
            byFraction.TryGetValue((Models.Fractions) fraction, out var byComponent)
                ? byComponent
                : new Dictionary<ClothesComponent, List<FractionClothesData>>();

        /// <summary>Раздевалка фракции → окно гардероба (вызывается из FractionClothingSets.OpenFractionClothingSetsMenu).</summary>
        public static void Open(ExtPlayer player)
        {
            try
            {
                var sessionData = player.GetSessionData();
                var characterData = player.GetCharacterData();
                var memberFractionData = player.GetFractionMemberData();
                if (sessionData == null || characterData == null || memberFractionData == null)
                    return;
                if (!NearCloakroom(player, memberFractionData.Id))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.TooFar), 3000);
                    return;
                }
                // Сохранённый образ — из БД в фоне; как загрузится, окно откроется само
                if (!LoadOutfits(player, characterData.UUID, () => Open(player)))
                    return;
                var gender = characterData.Gender;
                var allowed = Allowed(memberFractionData.Id, gender);
                var categories = new List<object>();
                foreach (var (component, title) in Categories)
                {
                    if (!allowed.TryGetValue(component, out var list) || list.Count == 0)
                        continue;
                    var isProp = Chars.Repository.ClothesComponentToPropId.ContainsKey(component);
                    var slot = isProp
                        ? Chars.Repository.ClothesComponentToPropId[component].SlotId
                        : Chars.Repository.ClothesComponentToComponentId.TryGetValue(component, out var c) ? c.SlotId : -1;
                    if (slot == -1)
                        continue;
                    ClothesComponents.ClothesComponentData.TryGetValue(gender, out var byComponent);
                    var data = byComponent != null && byComponent.TryGetValue(component, out var d) ? d : null;
                    categories.Add(new
                    {
                        key = component.ToString(),
                        title,
                        isProp,
                        slot,
                        items = list.Select(item =>
                        {
                            ClothesData clothes = null;
                            data?.TryGetValue(item.DrawableId, out clothes);
                            // Компактно: [id, drawable, torso, [текстуры], название|null, tname] — у армии сотни вещей
                            return new object[]
                            {
                                item.DrawableId,
                                clothes?.Variation ?? item.DrawableId,
                                clothes?.Torso ?? -1,
                                item.Textures.Distinct().OrderBy(t => t).ToList(),
                                ClothesComponents.GetClothesName(gender, component, item.DrawableId),
                                string.IsNullOrEmpty(clothes?.TName) ? null : clothes.TName,
                            };
                        }).ToList(),
                    });
                }

                var outfit = GetOutfit(characterData.UUID, memberFractionData.Id, gender) ?? new Outfit();
                var json = JsonConvert.SerializeObject(new
                {
                    fraction = memberFractionData.Id,
                    gender,
                    onDuty = sessionData.WorkData.OnDuty,
                    categories,
                    outfit,
                    presets = PresetsFor(memberFractionData.Id, gender),
                    canPreset = memberFractionData.Rank >= PresetRank,
                    // Каталог всей одежды сервера — только высший уровень админки (страницами, по запросу)
                    catalog = characterData.AdminLVL >= CatalogAdmin
                        ? Categories.Select(c => new { key = c.component.ToString(), title = c.title }).ToList<object>()
                        : null,
                });
                // Частями по 16 КБ — большие события клиент может не принять
                const int chunk = 16000;
                var total = (json.Length + chunk - 1) / chunk;
                for (var i = 0; i < total; i++)
                    Trigger.ClientEvent(player, "client.wardrobe.part", i, total, json.Substring(i * chunk, Math.Min(chunk, json.Length - i * chunk)));
                Log.Write($"Wardrobe.Open: {player.Name}, фракция {memberFractionData.Id}, категорий {categories.Count}, размер {json.Length} ({total} ч.)");
                if (categories.Count == 0)
                    Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "Для вашей фракции не задан список формы — доступен только торс", 5000);
            }
            catch (Exception e)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Гардероб: ошибка сервера ({e.GetType().Name})", 5000);
                Log.Write($"Open Exception: {e}");
            }
        }

        /// <summary>Проверка образа: только вещи и цвета из списка фракции, торс в разумных пределах.</summary>
        private static Outfit Validate(string json, int fraction, bool gender, out string error)
        {
            error = null;
            Outfit outfit;
            try
            {
                outfit = JsonConvert.DeserializeObject<Outfit>(json) ?? new Outfit();
            }
            catch
            {
                error = "Ошибка данных образа";
                return null;
            }
            var allowed = Allowed(fraction, gender);
            var clean = new Outfit();
            foreach (var (key, value) in outfit.Items ?? new Dictionary<string, int[]>())
            {
                if (value == null || value.Length < 2 || !Enum.TryParse<ClothesComponent>(key, out var component))
                    continue;
                if (!allowed.TryGetValue(component, out var list))
                    continue;
                var item = list.FirstOrDefault(i => i.DrawableId == value[0]);
                if (item == null || !item.Textures.Contains(value[1]))
                    continue;
                clean.Items[component.ToString()] = new[] { value[0], value[1] };
            }
            if (outfit.Torso != null && outfit.Torso.Length >= 2 && outfit.Torso[0] >= 0 && outfit.Torso[0] < 512 && outfit.Torso[1] >= 0 && outfit.Torso[1] < 32)
                clean.Torso = new[] { outfit.Torso[0], outfit.Torso[1] };

            var main = new[] { ClothesComponent.Tops, ClothesComponent.Undershort, ClothesComponent.Legs, ClothesComponent.Shoes };
            if (main.Count(m => clean.Items.ContainsKey(m.ToString())) < 2)
                error = "Форма не собрана: выберите хотя бы верх (или футболку), низ и обувь";
            return clean;
        }

        /// <summary>Надеть сохранённый образ (смена, респавн). false — образа нет.</summary>
        public static bool Apply(ExtPlayer player, int fraction, bool gender, bool isDutySet)
        {
            try
            {
                var sessionData = player.GetSessionData();
                var characterData = player.GetCharacterData();
                if (sessionData == null || characterData == null)
                    return false;
                var outfit = GetOutfit(characterData.UUID, fraction, gender);
                if (outfit == null || outfit.Items.Count == 0)
                    return false;

                var onDuty = sessionData.WorkData.OnDuty;
                sessionData.WorkData.OnDuty = false;
                player.ClearAccessories();
                foreach (var (key, value) in outfit.Items)
                {
                    if (!Enum.TryParse<ClothesComponent>(key, out var component))
                        continue;
                    if (Chars.Repository.ClothesComponentToComponentId.TryGetValue(component, out var comp))
                        ClothesComponents.SetSpecialClothes(player, comp.SlotId, value[0], value[1]);
                    else if (Chars.Repository.ClothesComponentToPropId.TryGetValue(component, out var prop))
                        ClothesComponents.SetSpecialAccessories(player, prop.SlotId, value[0], value[1]);
                }
                if (outfit.Torso != null)
                    TorsoOverride[player] = (outfit.Torso[0], outfit.Torso[1]);
                else
                    TorsoOverride.Remove(player);
                Chars.Repository.LoadAccessories(player);
                if (outfit.Torso != null)
                    ClothesComponents.SetClothes(player, 3, outfit.Torso[0], outfit.Torso[1]);

                sessionData.WorkData.OnDuty = onDuty;
                if (isDutySet && !onDuty)
                {
                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.StartWorkDay), 3000);
                    sessionData.WorkData.OnDuty = true;
                }
                sessionData.WorkData.OnDutyName = DutyName;
                return true;
            }
            catch (Exception e)
            {
                Log.Write($"Apply Exception: {e}");
            }
            return false;
        }

        private static void Reply(ExtPlayer player, string text, bool ok) =>
            Trigger.ClientEvent(player, "client.wardrobe.result", text, ok);

        [RemoteEvent("server.wardrobe.save")]
        public static void OnSave(ExtPlayer player, string json, bool duty)
        {
            try
            {
                var characterData = player.GetCharacterData();
                var memberFractionData = player.GetFractionMemberData();
                if (characterData == null || memberFractionData == null)
                    return;
                if (!NearCloakroom(player, memberFractionData.Id))
                {
                    Reply(player, "Вы отошли от раздевалки", false);
                    return;
                }
                var outfit = Validate(json, memberFractionData.Id, characterData.Gender, out var error);
                if (outfit == null)
                {
                    Reply(player, error, false);
                    return;
                }
                if (duty && error != null)
                {
                    Reply(player, error, false);
                    return;
                }
                SaveOutfit(characterData.UUID, memberFractionData.Id, characterData.Gender, outfit);
                var sessionData = player.GetSessionData();
                if (duty || sessionData != null && sessionData.WorkData.OnDuty && sessionData.WorkData.OnDutyName == DutyName)
                {
                    Apply(player, memberFractionData.Id, characterData.Gender, true);
                    Trigger.ClientEvent(player, "client.wardrobe.close", true);
                    return;
                }
                Reply(player, "Образ сохранён. Нажмите «Заступить на смену», чтобы надеть", true);
            }
            catch (Exception e)
            {
                Log.Write($"OnSave Exception: {e}");
            }
        }

        [RemoteEvent("server.wardrobe.takeoff")]
        public static void OnTakeoff(ExtPlayer player)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null || !sessionData.WorkData.OnDuty)
                    return;
                sessionData.WorkData.OnDuty = false;
                sessionData.WorkData.OnDutyName = string.Empty;
                TorsoOverride.Remove(player);
                player.ClearAccessories();
                Customization.ApplyCharacter(player);
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.EndWorkDay), 3000);
                Trigger.ClientEvent(player, "client.wardrobe.close", true);
            }
            catch (Exception e)
            {
                Log.Write($"OnTakeoff Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ образы фракции (ранг 9+)

        private static List<object> PresetsFor(int fraction, bool gender) =>
            Presets.Where(p => p.Fraction == fraction && p.Gender == gender)
                .Select(p => (object) new { id = p.Id.ToString(), name = p.Name, author = p.Author.Replace('_', ' '), outfit = p.Outfit })
                .ToList();

        private static void SendPresets(int fraction, bool gender)
        {
            var json = JsonConvert.SerializeObject(PresetsFor(fraction, gender));
            foreach (var p in Character.Repository.GetPlayers())
            {
                var member = p.GetFractionMemberData();
                var character = p.GetCharacterData();
                if (member != null && character != null && member.Id == fraction && character.Gender == gender)
                    Trigger.ClientEvent(p, "client.wardrobe.presets", json);
            }
        }

        [RemoteEvent("server.wardrobe.presetSave")]
        public static void OnPresetSave(ExtPlayer player, string name, string json)
        {
            try
            {
                var characterData = player.GetCharacterData();
                var memberFractionData = player.GetFractionMemberData();
                if (characterData == null || memberFractionData == null)
                    return;
                if (memberFractionData.Rank < PresetRank)
                {
                    Reply(player, $"Образы фракции сохраняются с {PresetRank} ранга", false);
                    return;
                }
                if (!_presetsReady)
                {
                    Reply(player, "Образы ещё загружаются — повторите через секунду", false);
                    return;
                }
                if (!NearCloakroom(player, memberFractionData.Id))
                {
                    Reply(player, "Вы отошли от раздевалки", false);
                    return;
                }
                name = new string((name ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
                if (name.Length < 2 || name.Length > 40)
                {
                    Reply(player, "Название образа — от 2 до 40 символов", false);
                    return;
                }
                var outfit = Validate(json, memberFractionData.Id, characterData.Gender, out var error);
                if (outfit == null || error != null)
                {
                    Reply(player, error ?? "Ошибка данных образа", false);
                    return;
                }
                var gender = characterData.Gender;
                var same = Presets.FirstOrDefault(p => p.Fraction == memberFractionData.Id && p.Gender == gender && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                if (same == null && Presets.Count(p => p.Fraction == memberFractionData.Id && p.Gender == gender) >= PresetMax)
                {
                    Reply(player, $"Не больше {PresetMax} образов — удалите ненужный", false);
                    return;
                }
                var preset = same ?? new Preset { Id = DateTime.UtcNow.Ticks, Fraction = memberFractionData.Id, Gender = gender };
                preset.Name = name;
                preset.Author = player.Name;
                preset.Outfit = outfit;
                if (same == null)
                    Presets.Add(preset);
                NeptuneEvo.Database.DbQueue.Enqueue(
                    @"INSERT INTO `fraction_presets` (`id`,`fraction`,`gender`,`name`,`author`,`outfit`) VALUES (@i,@f,@g,@n,@a,@o)
                      ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`author`=VALUES(`author`),`outfit`=VALUES(`outfit`)",
                    ("@i", preset.Id), ("@f", preset.Fraction), ("@g", gender ? 1 : 0), ("@n", name), ("@a", preset.Author), ("@o", JsonConvert.SerializeObject(outfit)));
                Reply(player, same == null ? $"Образ фракции «{name}» сохранён — его видят все сотрудники" : $"Образ фракции «{name}» обновлён", true);
                SendPresets(memberFractionData.Id, gender);
            }
            catch (Exception e)
            {
                Log.Write($"OnPresetSave Exception: {e}");
            }
        }

        [RemoteEvent("server.wardrobe.presetDelete")]
        public static void OnPresetDelete(ExtPlayer player, string idText)
        {
            try
            {
                var memberFractionData = player.GetFractionMemberData();
                if (memberFractionData == null || !long.TryParse(idText, out var id))
                    return;
                if (memberFractionData.Rank < PresetRank)
                {
                    Reply(player, $"Удалять образы фракции можно с {PresetRank} ранга", false);
                    return;
                }
                var preset = Presets.FirstOrDefault(p => p.Id == id && p.Fraction == memberFractionData.Id);
                if (preset == null)
                    return;
                Presets.Remove(preset);
                NeptuneEvo.Database.DbQueue.Enqueue("DELETE FROM `fraction_presets` WHERE `id`=@i", ("@i", id));
                Reply(player, $"Образ «{preset.Name}» удалён", true);
                SendPresets(preset.Fraction, preset.Gender);
            }
            catch (Exception e)
            {
                Log.Write($"OnPresetDelete Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ каталог

        private static ClothesComponent? Component(string key) =>
            Enum.TryParse<ClothesComponent>(key, out var c) && Categories.Any(x => x.component == c) ? c : (ClothesComponent?) null;

        private static (int slot, bool isProp) SlotOf(ClothesComponent component)
        {
            if (Chars.Repository.ClothesComponentToPropId.TryGetValue(component, out var prop))
                return (prop.SlotId, true);
            if (Chars.Repository.ClothesComponentToComponentId.TryGetValue(component, out var comp))
                return (comp.SlotId, false);
            return (-1, false);
        }

        /// <summary>Добавить вещь каталога в список формы фракции (в памяти). true — добавлена новая.</summary>
        private static bool AddExtra(int fraction, bool gender, ClothesComponent component, int id)
        {
            if (!ClothesComponents.ClothesComponentData.TryGetValue(gender, out var byComponent) ||
                !byComponent.TryGetValue(component, out var data) || !data.TryGetValue(id, out var clothes))
                return false;
            var textures = clothes.Textures != null && clothes.Textures.Count > 0 ? clothes.Textures.Distinct().ToList() : new List<int> { 0 };
            if (!FractionClothingSets.FractionAvailableSets.TryGetValue(gender, out var byFraction))
                FractionClothingSets.FractionAvailableSets[gender] = byFraction = new Dictionary<Models.Fractions, Dictionary<ClothesComponent, List<FractionClothesData>>>();
            if (!byFraction.TryGetValue((Models.Fractions) fraction, out var components))
                byFraction[(Models.Fractions) fraction] = components = new Dictionary<ClothesComponent, List<FractionClothesData>>();
            if (!components.TryGetValue(component, out var list))
                components[component] = list = new List<FractionClothesData>();
            var existing = list.FirstOrDefault(i => i.DrawableId == id);
            if (existing != null)
            {
                // Уже есть (из кода) — добавим недостающие цвета, но убрать такую вещь из каталога нельзя
                foreach (var t in textures.Where(t => !existing.Textures.Contains(t)))
                    existing.Textures.Add(t);
                return false;
            }
            list.Add(new FractionClothesData { DrawableId = id, Textures = textures });
            Extras.Add((fraction, gender, component, id));
            return true;
        }

        [RemoteEvent("server.wardrobe.catalog")]
        public static void OnCatalog(ExtPlayer player, string key, int page, string search)
        {
            try
            {
                var characterData = player.GetCharacterData();
                var memberFractionData = player.GetFractionMemberData();
                if (characterData == null || memberFractionData == null || characterData.AdminLVL < CatalogAdmin)
                    return;
                var component = Component(key);
                if (component == null)
                    return;
                var gender = characterData.Gender;
                var (slot, isProp) = SlotOf(component.Value);
                if (!ClothesComponents.ClothesComponentData.TryGetValue(gender, out var byComponent) ||
                    !byComponent.TryGetValue(component.Value, out var data))
                {
                    Trigger.ClientEvent(player, "client.wardrobe.catalogPage", JsonConvert.SerializeObject(new { key, slot, isProp, page = 0, pages = 0, total = 0, items = new List<object>() }));
                    return;
                }
                var allowed = Allowed(memberFractionData.Id, gender);
                allowed.TryGetValue(component.Value, out var allowedList);
                search = (search ?? "").Trim().ToLower();
                var all = data.OrderBy(d => d.Key)
                    .Select(d => (id: d.Key, clothes: d.Value, name: ClothesComponents.GetClothesName(gender, component.Value, d.Key)))
                    .Where(x => search.Length == 0 || x.id.ToString() == search || (x.name ?? "").ToLower().Contains(search))
                    .ToList();
                var pages = Math.Max(1, (all.Count + CatalogPage - 1) / CatalogPage);
                page = Math.Max(0, Math.Min(page, pages - 1));
                var items = all.Skip(page * CatalogPage).Take(CatalogPage).Select(x => new object[]
                {
                    x.id,
                    x.clothes.Variation,
                    x.clothes.Torso,
                    (x.clothes.Textures ?? new List<int> { 0 }).Distinct().OrderBy(t => t).ToList(),
                    x.name,
                    string.IsNullOrEmpty(x.clothes.TName) ? null : x.clothes.TName,
                    allowedList != null && allowedList.Any(i => i.DrawableId == x.id),
                    Extras.Contains((memberFractionData.Id, gender, component.Value, x.id)),
                }).ToList();
                Trigger.ClientEvent(player, "client.wardrobe.catalogPage", JsonConvert.SerializeObject(new { key, slot, isProp, page, pages, total = all.Count, items }));
            }
            catch (Exception e)
            {
                Log.Write($"OnCatalog Exception: {e}");
            }
        }

        [RemoteEvent("server.wardrobe.catalogToggle")]
        public static void OnCatalogToggle(ExtPlayer player, string key, int id, bool add)
        {
            try
            {
                var characterData = player.GetCharacterData();
                var memberFractionData = player.GetFractionMemberData();
                if (characterData == null || memberFractionData == null || characterData.AdminLVL < CatalogAdmin)
                    return;
                if (!NearCloakroom(player, memberFractionData.Id))
                {
                    Reply(player, "Вы отошли от раздевалки", false);
                    return;
                }
                if (!_extraReady)
                {
                    Reply(player, "Каталог ещё загружается — повторите через секунду", false);
                    return;
                }
                var component = Component(key);
                if (component == null)
                    return;
                var fraction = memberFractionData.Id;
                var gender = characterData.Gender;
                var name = ClothesComponents.GetClothesName(gender, component.Value, id) ?? $"#{id}";
                if (add)
                {
                    if (!AddExtra(fraction, gender, component.Value, id))
                    {
                        Reply(player, $"«{name}» уже есть в форме фракции", false);
                        return;
                    }
                    NeptuneEvo.Database.DbQueue.Enqueue(
                        "INSERT IGNORE INTO `fraction_clothes_extra` (`fraction`,`gender`,`component`,`clothes_id`) VALUES (@f,@g,@c,@i)",
                        ("@f", fraction), ("@g", gender ? 1 : 0), ("@c", component.Value.ToString()), ("@i", id));
                    Log.Write($"Каталог: {player.Name} добавил {component} #{id} во форму фракции {fraction} ({(gender ? "м" : "ж")})");
                    Reply(player, $"«{name}» добавлено в форму фракции. В списке слева появится после переоткрытия гардероба", true);
                }
                else
                {
                    if (!Extras.Remove((fraction, gender, component.Value, id)))
                    {
                        Reply(player, "Убрать можно только вещи, добавленные из каталога", false);
                        return;
                    }
                    var allowed = Allowed(fraction, gender);
                    if (allowed.TryGetValue(component.Value, out var list))
                        list.RemoveAll(i => i.DrawableId == id);
                    NeptuneEvo.Database.DbQueue.Enqueue(
                        "DELETE FROM `fraction_clothes_extra` WHERE `fraction`=@f AND `gender`=@g AND `component`=@c AND `clothes_id`=@i",
                        ("@f", fraction), ("@g", gender ? 1 : 0), ("@c", component.Value.ToString()), ("@i", id));
                    Log.Write($"Каталог: {player.Name} убрал {component} #{id} из формы фракции {fraction}");
                    Reply(player, $"«{name}» убрано из формы фракции", true);
                }
                Trigger.ClientEvent(player, "client.wardrobe.catalogChanged", key, id, add);
            }
            catch (Exception e)
            {
                Log.Write($"OnCatalogToggle Exception: {e}");
            }
        }

        /// <summary>Снятие формы старым путём (меню и т.п.) — торс тоже сбрасываем.</summary>
        public static void ClearTorso(ExtPlayer player) => TorsoOverride.Remove(player);

        [ServerEvent(Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(ExtPlayer player, DisconnectionType type, string reason)
        {
            TorsoOverride.Remove(player);
            var uuid = player.GetUUID();
            Loaded.Remove(uuid);
            foreach (var key in Outfits.Keys.Where(k => k.uuid == uuid).ToList())
                Outfits.Remove(key);
        }
    }
}
