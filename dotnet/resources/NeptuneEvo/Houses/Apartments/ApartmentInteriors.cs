using GTANetworkAPI;
using NeptuneEvo.Character;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using Newtonsoft.Json;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NeptuneEvo.Houses.Apartments
{
    /// <summary>
    /// Интерьеры квартир из DLC GTA5RP_APARTMENT (client_packages/dlcpacks/GTA5RP_APARTMENT).
    /// Данные взяты из выгрузки CodeWalker (int_ap_house.ytyp + int_ap_house_1_*_milo_.ymap):
    ///  - 5 стилей отделки — 5 MLO: (250|285|320|355|380, 0, -50);
    ///  - в каждом MLO 16 планировок (комнаты House_S_N), разнесённых по оси Y.
    /// Id интерьера = (стиль - 1) * 16 + планировка (1..80).
    /// Точку появления можно поправить в игре: /aptint id (посмотреть), /aptintset id (сохранить свою позицию),
    /// правки лежат в settings/apartment_interiors.json.
    /// </summary>
    public static class ApartmentInteriors
    {
        private static readonly nLog Log = new nLog("Houses.ApartmentInteriors");

        public const int Styles = 5;
        public const int Layouts = 16;
        private static readonly float[] StyleX = { 250f, 285f, 320f, 355f, 380f };
        private const float MloZ = -50f;
        private static string OverridesPath => Path.Combine("settings", "apartment_interiors.json");

        // Комнаты House_S_N из int_ap_house.ytyp (одинаковы для всех стилей): minY, maxY, minZ, ширина X
        private static readonly (float minY, float maxY, float minZ, float halfX)[] Rooms =
        {
            (-4.0f, 4.0f, -1.5f, 5.5f),      // 1  студия 11×8
            (28.9f, 41.1f, -1.5f, 4.1f),     // 2
            (65.3f, 74.7f, -1.5f, 5.0f),     // 3
            (98.3f, 111.7f, -3.8f, 5.7f),    // 4  два уровня
            (133.9f, 146.1f, -1.5f, 4.6f),   // 5
            (169.3f, 180.7f, -1.5f, 6.0f),   // 6
            (204.7f, 215.3f, -1.5f, 5.3f),   // 7
            (237.6f, 252.4f, -3.2f, 7.0f),   // 8  два уровня
            (272.8f, 287.2f, -1.8f, 5.0f),   // 9
            (307.7f, 322.3f, -1.8f, 5.7f),   // 10
            (342.4f, 357.6f, -1.8f, 6.3f),   // 11
            (377.1f, 392.9f, -3.5f, 8.4f),   // 12 два уровня
            (411.1f, 428.9f, -1.7f, 8.0f),   // 13
            (445.0f, 465.0f, -3.9f, 8.3f),   // 14 два уровня
            (479.7f, 500.3f, -3.5f, 10.0f),  // 15 два уровня
            (513.3f, 536.7f, -4.6f, 10.3f),  // 16 два уровня, самая большая
        };

        // Какие планировки подходят классу дома (HouseManager.HouseTypeList): от площади
        private static readonly Dictionary<int, int[]> LayoutsByClass = new Dictionary<int, int[]>
        {
            { 0, new[] { 1, 3 } },
            { 1, new[] { 1, 3 } },
            { 2, new[] { 1, 3 } },         // Эконом+
            { 3, new[] { 2, 5, 7 } },      // Комфорт
            { 4, new[] { 6, 9, 10 } },     // Комфорт+
            { 5, new[] { 4, 11, 13 } },    // Премиум
            { 6, new[] { 8, 12 } },        // Премиум+
            { 8, new[] { 8, 12 } },        // Премиум++
            { 9, new[] { 14, 15, 16 } },   // Люкс
        };

        private static Dictionary<int, Vector3> _overrides = new Dictionary<int, Vector3>();

        public static bool IsValid(int id) => id >= 1 && id <= Styles * Layouts;

        public static void LoadOverrides()
        {
            try
            {
                if (File.Exists(OverridesPath))
                    _overrides = JsonConvert.DeserializeObject<Dictionary<int, Vector3>>(File.ReadAllText(OverridesPath)) ?? new Dictionary<int, Vector3>();
            }
            catch (Exception e)
            {
                Log.Write($"Не удалось прочитать {OverridesPath}: {e.Message}");
            }
        }

        private static void SaveOverrides()
        {
            try
            {
                Directory.CreateDirectory("settings");
                File.WriteAllText(OverridesPath, JsonConvert.SerializeObject(_overrides, Formatting.Indented));
            }
            catch (Exception e)
            {
                Log.Write($"Не удалось сохранить {OverridesPath}: {e.Message}");
            }
        }

        /// <summary>Точка «на полу» (как HouseTypeList.Position): игрок появляется на +1.12.</summary>
        public static Vector3 GetPosition(int id)
        {
            if (_overrides.TryGetValue(id, out var custom))
                return custom;

            var style = (id - 1) / Layouts;
            var layout = (id - 1) % Layouts;
            var room = Rooms[layout];
            // Ближе к краю комнаты (у стены с меньшим Y), а не в центре, чтобы не попасть в мебель
            var y = room.minY + Math.Min(2.2f, (room.maxY - room.minY) / 2f);
            return new Vector3(StyleX[style], y, MloZ + room.minZ + 0.05f);
        }

        public static string GetName(int id)
        {
            if (!IsValid(id)) return "—";
            return $"стиль {(id - 1) / Layouts + 1}, планировка {(id - 1) % Layouts + 1}";
        }

        public static int Pick(int houseType, Random random)
        {
            if (!LayoutsByClass.TryGetValue(houseType, out var layouts))
                layouts = LayoutsByClass[4];
            var layout = layouts[random.Next(layouts.Length)];
            var style = random.Next(Styles);
            return style * Layouts + layout;
        }

        public static void SetOverride(int id, Vector3 floorPosition)
        {
            _overrides[id] = floorPosition;
            SaveOverrides();
        }
    }

    class ApartmentInteriorCommands : Script
    {
        private static bool IsAdmin(ExtPlayer player)
        {
            var characterData = player.GetCharacterData();
            return characterData != null && characterData.AdminLVL >= 8;
        }

        /// <summary>/aptint id — телепорт в интерьер квартиры (своё измерение) для проверки.</summary>
        [Command("aptint")]
        public static void CMD_Teleport(ExtPlayer player, int id)
        {
            if (!IsAdmin(player)) return;
            if (!ApartmentInteriors.IsValid(id))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Интерьеры 1..{ApartmentInteriors.Styles * ApartmentInteriors.Layouts}", 3000);
                return;
            }
            Trigger.Dimension(player, (uint)(5000000 + player.Value));
            player.Position = ApartmentInteriors.GetPosition(id) + new Vector3(0, 0, 1.12);
            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"Интерьер {id}: {ApartmentInteriors.GetName(id)}. Выход: /dim 0 и телепорт", 5000);
        }

        /// <summary>/aptintset id — сохранить текущую позицию как точку входа/выхода интерьера.</summary>
        [Command("aptintset")]
        public static void CMD_Set(ExtPlayer player, int id)
        {
            if (!IsAdmin(player) || !ApartmentInteriors.IsValid(id)) return;
            ApartmentInteriors.SetOverride(id, player.Position - new Vector3(0, 0, 1.12));
            ApartmentManager.ReapplyInterior(id);
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Точка интерьера {id} сохранена", 3000);
        }
    }
}
