using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Chars
{
    /// <summary>
    /// Сдвиг кастомной одежды под версию GTA.
    /// Кастомная вещь (variation = -1) получает номер «стандартных в игре + cvariation − 1» (у причёсок — без −1),
    /// поэтому после обновления GTA, когда Rockstar добавляет одежду, всё кастомное съезжает на чужие модели.
    ///
    /// /clothoff — клиент админа считает в игре общее число моделей по слотам (стандартные + наши dlcpacks),
    /// сервер вычитает наши (CustomClothesCount — посчитано по clothespack*/dlc.rpf) и показывает разницу.
    /// /clothoff apply — сохранить в settings/clothesOffsets.json и перечитать одежду без рестарта.
    /// При старте значения из файла перекрывают таблицы MaxClothesComponent / MaxBarberComponent.
    /// </summary>
    partial class ClothesComponents
    {
        private const string OffsetsPath = "settings/clothesOffsets.json";

        /// <summary>Слот сервера → (реквизит?, номер компонента/реквизита GTA).</summary>
        private static readonly Dictionary<ClothesComponent, (bool prop, int id)> GameSlots = new Dictionary<ClothesComponent, (bool, int)>
        {
            { ClothesComponent.Masks, (false, 1) },
            { ClothesComponent.Torsos, (false, 3) },
            { ClothesComponent.Legs, (false, 4) },
            { ClothesComponent.Bugs, (false, 5) },
            { ClothesComponent.Shoes, (false, 6) },
            { ClothesComponent.Accessories, (false, 7) },
            { ClothesComponent.Undershirts, (false, 8) },
            { ClothesComponent.BodyArmors, (false, 9) },
            { ClothesComponent.Decals, (false, 10) },
            { ClothesComponent.Tops, (false, 11) },
            { ClothesComponent.Hat, (true, 0) },
            { ClothesComponent.Glasses, (true, 1) },
            { ClothesComponent.Ears, (true, 2) },
            { ClothesComponent.Watches, (true, 6) },
            { ClothesComponent.Bracelets, (true, 7) },
        };

        private const int HairComponentId = 2;

        /// <summary>
        /// Сколько моделей в наших dlcpacks (коллекции mp_m_clothespack / mp_f_clothespack, паки clothespack … clothespack47).
        /// Добавили новую кастомную одежду в пак — увеличить число здесь (или в settings/clothesOffsets.json → custom).
        /// </summary>
        public static Dictionary<bool, Dictionary<string, int>> CustomClothesCount = new Dictionary<bool, Dictionary<string, int>>
        {
            {
                true, new Dictionary<string, int>
                {
                    { "Masks", 71 }, { "Torsos", 0 }, { "Legs", 69 }, { "Bugs", 34 }, { "Shoes", 29 }, { "Accessories", 66 },
                    { "Undershirts", 33 }, { "BodyArmors", 4 }, { "Decals", 12 }, { "Tops", 212 },
                    { "Hat", 42 }, { "Glasses", 12 }, { "Ears", 0 }, { "Watches", 1 }, { "Bracelets", 1 }, { "Hair", 42 },
                }
            },
            {
                false, new Dictionary<string, int>
                {
                    { "Masks", 71 }, { "Torsos", 3 }, { "Legs", 79 }, { "Bugs", 34 }, { "Shoes", 28 }, { "Accessories", 60 },
                    { "Undershirts", 22 }, { "BodyArmors", 4 }, { "Decals", 11 }, { "Tops", 207 },
                    { "Hat", 41 }, { "Glasses", 10 }, { "Ears", 0 }, { "Watches", 1 }, { "Bracelets", 1 }, { "Hair", 44 },
                }
            },
        };

        private class OffsetsFile
        {
            /// <summary>Стандартных моделей в игре по слотам (= MaxClothesComponent). Hair — число стандартных причёсок.</summary>
            public Dictionary<string, int> Male = new Dictionary<string, int>();
            public Dictionary<string, int> Female = new Dictionary<string, int>();
            /// <summary>Необязательно: своё число кастомных моделей (если пак пополнили).</summary>
            public Dictionary<string, int> CustomMale;
            public Dictionary<string, int> CustomFemale;
            public string Updated;
            public string Admin;
        }

        /// <summary>Результат последнего замера по админу (uuid) — ждёт /clothoff apply.</summary>
        private static readonly Dictionary<int, OffsetsFile> PendingOffsets = new Dictionary<int, OffsetsFile>();

        /// <summary>Номера из кода (до сдвига) — с ними собраны json одежды в интерфейсе.</summary>
        private static Dictionary<bool, Dictionary<string, int>> _baseVanilla;
        private static string _shiftJson;

        /// <summary>При входе: сдвиг номеров кастомной одежды для магазина в CEF (src_cef/src/json/clothes.js).</summary>
        public static void SendClothesShift(ExtPlayer player)
        {
            if (!string.IsNullOrEmpty(_shiftJson))
                Trigger.ClientEvent(player, "client.clothes.shift", _shiftJson);
        }

        private static void BuildShift()
        {
            var shift = new Dictionary<string, Dictionary<string, int[]>>();
            foreach (var gender in new[] { true, false })
            {
                var name = gender ? "Male" : "Female";
                var current = CurrentVanilla(gender);
                foreach (var (key, baseValue) in _baseVanilla[gender])
                {
                    if (!current.TryGetValue(key, out var value) || value == baseValue)
                        continue;
                    if (!shift.ContainsKey(name))
                        shift[name] = new Dictionary<string, int[]>();
                    shift[name][key] = new[] { baseValue, value - baseValue };
                }
            }
            _shiftJson = shift.Count > 0 ? JsonConvert.SerializeObject(shift) : null;
            if (_shiftJson != null)
                Log.Write($"Сдвиг одежды для интерфейса: {_shiftJson}");
        }

        private static void LoadClothesOffsets()
        {
            if (_baseVanilla == null)
                _baseVanilla = new Dictionary<bool, Dictionary<string, int>> { { true, CurrentVanilla(true) }, { false, CurrentVanilla(false) } };
            try
            {
                if (!File.Exists(OffsetsPath))
                    return;
                var file = JsonConvert.DeserializeObject<OffsetsFile>(File.ReadAllText(OffsetsPath));
                if (file == null)
                    return;
                ApplyOffsets(true, file.Male, file.CustomMale);
                ApplyOffsets(false, file.Female, file.CustomFemale);
                BuildShift();
                Log.Write($"Сдвиг кастомной одежды из {OffsetsPath} (замер {file.Updated}, {file.Admin})", nLog.Type.Success);
            }
            catch (Exception e)
            {
                Log.Write($"LoadClothesOffsets Exception: {e}");
            }
        }

        private static void ApplyOffsets(bool gender, Dictionary<string, int> vanilla, Dictionary<string, int> custom)
        {
            if (custom != null)
                foreach (var (key, value) in custom)
                    if (value >= 0 && CustomClothesCount[gender].ContainsKey(key))
                        CustomClothesCount[gender][key] = value;
            if (vanilla == null)
                return;
            foreach (var (key, value) in vanilla)
            {
                if (value <= 0)
                    continue;
                if (key == "Hair")
                    MaxBarberComponent[gender][BarberComponent.Hair] = value - 1; // у причёсок формула «Max + cvariation» без −1
                else if (Enum.TryParse<ClothesComponent>(key, out var component) && MaxClothesComponent[gender].ContainsKey(component))
                    MaxClothesComponent[gender][component] = value;
            }
        }

        private static Dictionary<string, int> CurrentVanilla(bool gender)
        {
            var result = MaxClothesComponent[gender].ToDictionary(p => p.Key.ToString(), p => p.Value);
            result["Hair"] = MaxBarberComponent[gender][BarberComponent.Hair] + 1;
            return result;
        }

        /// <summary>Сколько стандартных моделей уже использует база (vanilla-вещи) — меньше этого быть не может.</summary>
        private static int UsedVanilla(bool gender, string key)
        {
            try
            {
                if (key == "Hair")
                {
                    var max = MaxBarberComponent[gender][BarberComponent.Hair];
                    var list = BarberComponentData[gender][BarberComponent.Hair].Values.Select(v => v.Variation).Where(v => v <= max).ToList();
                    return list.Count > 0 ? list.Max() + 1 : 0;
                }
                if (!Enum.TryParse<ClothesComponent>(key, out var component) || !ClothesComponentData.ContainsKey(gender) || !ClothesComponentData[gender].ContainsKey(component))
                    return 0;
                var old = MaxClothesComponent[gender][component];
                var used = ClothesComponentData[gender][component].Values.Select(v => v.Variation).Where(v => v < old).ToList();
                return used.Count > 0 ? used.Max() + 1 : 0;
            }
            catch
            {
                return 0;
            }
        }

        [Command("clothoff")]
        public void CMD_clothesOffsets(ExtPlayer player, string action = "")
        {
            try
            {
                if (!player.IsCharacterData()) return;
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Tsc)) return;
                var uuid = player.GetUUID();

                if (action == "apply")
                {
                    if (!PendingOffsets.TryGetValue(uuid, out var pending))
                    {
                        player.SendChatMessage("Сначала /clothoff — замер в игре");
                        return;
                    }
                    try
                    {
                        if (File.Exists(OffsetsPath))
                            File.Copy(OffsetsPath, OffsetsPath + ".bak", true);
                    }
                    catch (Exception e)
                    {
                        Log.Write($"clothesOffsets backup: {e.Message}");
                    }
                    File.WriteAllText(OffsetsPath, JsonConvert.SerializeObject(pending, Formatting.Indented));
                    PendingOffsets.Remove(uuid);
                    OnResourceStart(); // применит файл и перечитает одежду/причёски, JSON магазинов пересоберутся
                    GameLog.Admin(player.Name, "clothoff apply", "");
                    Trigger.SendToAdmins(6, $"~y~[CLOTHES] {player.Name} сдвинул номера кастомной одежды под текущую версию GTA. Игрокам — перезайти или переодеться.");
                    player.SendChatMessage($"Сохранено в {OffsetsPath}, одежда перечитана. Проверь кастомные вещи в магазине одежды и в инвентаре.");
                    return;
                }
                if (action == "reset")
                {
                    if (File.Exists(OffsetsPath))
                        File.Move(OffsetsPath, OffsetsPath + ".off." + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                    player.SendChatMessage("Файл сдвига отключён. Значения из кода вернутся после рестарта сервера.");
                    return;
                }

                player.SendChatMessage("Замер одежды в игре... (несколько секунд)");
                Trigger.ClientEvent(player, "clothes.getOffsets");
            }
            catch (Exception e)
            {
                Log.Write($"CMD_clothesOffsets Exception: {e}");
            }
        }

        /// <summary>Клиент: {"male":{"c":{"1":n,..},"p":{"0":n,..}},"female":{...}} — всего моделей в игре.</summary>
        /// <summary>
        /// Ручная калибровка из гардероба (Каталог, админ 9): админ подбирает сдвиг на себе, пока кастомная вещь
        /// не совпадёт со своим названием, и сохраняет. vanilla — сколько стандартных моделей в игре в этой категории
        /// (0 — вернуть значение из кода). Меняются все кастомные вещи категории — и в магазине (client.clothes.shift).
        /// </summary>
        [RemoteEvent("server.clothes.calibrate")]
        public void OnCalibrate(ExtPlayer player, string key, bool gender, int vanilla)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null || characterData.AdminLVL < 9)
                    return;
                if (!Enum.TryParse<ClothesComponent>(key, out var component) || !MaxClothesComponent[gender].ContainsKey(component))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Эту категорию калибровать нельзя", 4000);
                    return;
                }
                var used = UsedVanilla(gender, key);
                if (vanilla != 0 && (vanilla < 1 || vanilla < used))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Слишком мало: стандартные вещи базы используют номера до {used - 1}", 5000);
                    return;
                }
                OffsetsFile file = null;
                try
                {
                    if (File.Exists(OffsetsPath))
                    {
                        file = JsonConvert.DeserializeObject<OffsetsFile>(File.ReadAllText(OffsetsPath));
                        File.Copy(OffsetsPath, OffsetsPath + ".bak", true);
                    }
                }
                catch (Exception e)
                {
                    Log.Write($"calibrate read: {e.Message}");
                }
                file ??= new OffsetsFile();
                var dict = gender ? (file.Male ??= new Dictionary<string, int>()) : (file.Female ??= new Dictionary<string, int>());
                var baseValue = _baseVanilla != null && _baseVanilla[gender].TryGetValue(key, out var b) ? b : MaxClothesComponent[gender][component];
                if (vanilla == 0 || vanilla == baseValue)
                    dict.Remove(key);
                else
                    dict[key] = vanilla;
                file.Updated = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                file.Admin = player.Name;
                File.WriteAllText(OffsetsPath, JsonConvert.SerializeObject(file, Formatting.Indented));
                // Значение из кода вернётся только после рестарта (как /clothoff reset) — поэтому ставим его сразу и здесь
                MaxClothesComponent[gender][component] = vanilla == 0 ? baseValue : vanilla;
                OnResourceStart();
                BuildShift();
                foreach (var p in Character.Repository.GetPlayers())
                    SendClothesShift(p);
                if (string.IsNullOrEmpty(_shiftJson))
                    foreach (var p in Character.Repository.GetPlayers())
                        Trigger.ClientEvent(p, "client.clothes.shift", "{}");
                var now = MaxClothesComponent[gender][component];
                GameLog.Admin(player.Name, $"clothes calibrate {(gender ? "M" : "F")} {key}={now}", "");
                Trigger.SendToAdmins(6, $"~y~[CLOTHES] {player.Name} откалибровал кастомную одежду: {(gender ? "муж." : "жен.")} {key} — стандартных моделей {now} (было в коде {baseValue}). Игрокам — переодеться.");
                Trigger.ClientEvent(player, "client.wardrobe.calibrated", key, now);
            }
            catch (Exception e)
            {
                Log.Write($"OnCalibrate Exception: {e}");
            }
        }

        [RemoteEvent("server.clothes.offsets")]
        public void OnClothesOffsets(ExtPlayer player, string json)
        {
            try
            {
                if (!player.IsCharacterData()) return;
                if (!CommandsAccess.CanUseCmd(player, AdminCommands.Tsc)) return;

                var data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, int>>>>(json ?? "{}");
                var result = new OffsetsFile
                {
                    Updated = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
                    Admin = player.Name,
                };
                var changed = 0;
                var warnings = new List<string>();

                foreach (var gender in new[] { true, false })
                {
                    var name = gender ? "male" : "female";
                    if (data == null || !data.TryGetValue(name, out var totals) || totals == null)
                    {
                        player.SendChatMessage($"Нет данных для {name}");
                        return;
                    }
                    var target = gender ? result.Male : result.Female;
                    var current = CurrentVanilla(gender);

                    int Total(bool prop, int id) =>
                        totals.TryGetValue(prop ? "p" : "c", out var list) && list != null && list.TryGetValue(id.ToString(), out var n) ? n : -1;

                    var slots = GameSlots.Select(s => (key: s.Key.ToString(), s.Value.prop, s.Value.id)).ToList();
                    slots.Add(("Hair", false, HairComponentId));

                    var lines = new List<string>();
                    foreach (var (key, prop, id) in slots)
                    {
                        var total = Total(prop, id);
                        var custom = CustomClothesCount[gender].TryGetValue(key, out var c) ? c : 0;
                        if (total <= 0)
                        {
                            warnings.Add($"{name} {key}: игра вернула {total}");
                            continue;
                        }
                        var vanilla = total - custom;
                        var used = UsedVanilla(gender, key);
                        if (vanilla < used)
                            warnings.Add($"{name} {key}: стандартных {vanilla}, а база уже использует номер {used - 1} — dlcpacks загружены не все или неверное число кастомных");
                        target[key] = vanilla;
                        var old = current.TryGetValue(key, out var o) ? o : 0;
                        if (old != vanilla)
                        {
                            changed++;
                            lines.Add($"{key} {old}→{vanilla} ({(vanilla - old >= 0 ? "+" : "")}{vanilla - old})");
                        }
                    }
                    player.SendChatMessage(lines.Count == 0
                        ? $"~g~[{(gender ? "Мужская" : "Женская")}] совпадает с игрой"
                        : $"~y~[{(gender ? "Мужская" : "Женская")}] " + string.Join(", ", lines));
                }

                foreach (var warning in warnings.Take(6))
                    player.SendChatMessage($"~r~{warning}");
                Log.Write($"clothoff {player.Name}: {JsonConvert.SerializeObject(result)}");

                if (changed == 0 && warnings.Count == 0)
                {
                    player.SendChatMessage("~g~Номера кастомной одежды уже соответствуют этой версии GTA.");
                    return;
                }
                PendingOffsets[player.GetUUID()] = result;
                player.SendChatMessage(warnings.Count > 0
                    ? "~r~Есть предупреждения — сначала проверь, что у тебя загружены все dlcpacks сервера. Применить всё равно: /clothoff apply"
                    : $"Изменений: {changed}. Применить: /clothoff apply (сохранится в {OffsetsPath}, без рестарта)");
            }
            catch (Exception e)
            {
                Log.Write($"OnClothesOffsets Exception: {e}");
            }
        }
    }
}
