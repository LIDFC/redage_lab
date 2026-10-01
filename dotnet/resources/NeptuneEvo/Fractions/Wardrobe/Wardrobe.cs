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
