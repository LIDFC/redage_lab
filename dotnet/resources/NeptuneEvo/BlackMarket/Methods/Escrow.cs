using NeptuneEvo.Chars.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Handles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeptuneEvo.BlackMarket.Methods
{
    /// <summary>
    /// Эскроу предметов через существующий инвентарь (Chars.Repository): предметы лота лежат в локации
    /// <c>bmlot_{id}</c>, предметы закладки — в <c>bmdrop_{id}</c> (location = "blackmarket").
    /// Это обычные строки items_data: переживают рестарт, не видны ни в одном инвентаре, их нельзя использовать,
    /// передать или продать повторно. Данные предмета (серийник оружия, прочность брони) переносятся как есть.
    /// Вызывать только под <see cref="BlackMarketCore.Sync"/> в главном потоке.
    /// </summary>
    public static class Escrow
    {
        public const string Location = "blackmarket";
        public const int MaxSlots = 1000;
        private const int WarehouseSlots = 300;

        public static string LotContainer(int lotId) => $"bmlot_{lotId}";
        public static string DropContainer(int dropId) => $"bmdrop_{dropId}";

        /// <summary>Сколько единиц предмета лежит в контейнере.</summary>
        public static int Count(string container, ItemId itemId)
        {
            var slots = Slots(container);
            return slots.Where(s => s.ItemId == itemId).Sum(s => Units(s));
        }

        /// <summary>Сколько единиц предмета у игрока в основном инвентаре (без экипировки и быстрых слотов).</summary>
        public static int CountInInventory(ExtPlayer player, ItemId itemId) =>
            InventorySlots(player).Where(s => s.ItemId == itemId).Sum(s => Units(s));

        /// <summary>Какие разрешённые предметы есть в инвентаре: itemId → количество.</summary>
        public static Dictionary<ItemId, int> InventorySummary(ExtPlayer player, Func<ItemId, bool> allowed) =>
            InventorySlots(player)
                .Where(s => allowed(s.ItemId))
                .GroupBy(s => s.ItemId)
                .ToDictionary(g => g.Key, g => g.Sum(s => Units(s)));

        /// <summary>Забрать count единиц из инвентаря игрока в контейнер. Возвращает перенесённое количество.</summary>
        public static int TakeFromPlayer(ExtPlayer player, ItemId itemId, int count, string container)
        {
            var moved = 0;
            foreach (var slot in InventorySlots(player).Where(s => s.ItemId == itemId).OrderBy(s => s.Index).ToList())
            {
                if (moved >= count)
                    break;
                var units = Math.Min(Units(slot), count - moved);
                var data = slot.Data ?? "";
                Chars.Repository.RemoveIndex(player, "inventory", slot.Index, units);
                Put(container, itemId, units, data);
                moved += units;
            }
            return moved;
        }

        /// <summary>Переложить count единиц из одного контейнера в другой (лот → закладка).</summary>
        public static int Move(string from, string to, ItemId itemId, int count)
        {
            var moved = 0;
            foreach (var slot in Slots(from).Where(s => s.ItemId == itemId).OrderBy(s => s.Index).ToList())
            {
                if (moved >= count)
                    break;
                var units = Math.Min(Units(slot), count - moved);
                var data = slot.Data ?? "";
                Chars.Repository.Remove(null, from, Location, itemId, units, data);
                Put(to, itemId, units, data);
                moved += units;
            }
            return moved;
        }

        /// <summary>
        /// Выдать содержимое контейнера игроку в инвентарь — столько, сколько помещается.
        /// Возвращает выданное количество; остаток остаётся в контейнере.
        /// </summary>
        public static int GiveToPlayer(ExtPlayer player, string container)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null)
                return 0;

            var given = 0;
            foreach (var slot in Slots(container).OrderBy(s => s.Index).ToList())
            {
                var units = Units(slot);
                if (Chars.Repository.isFreeSlots(player, slot.ItemId, units, false) != 0)
                    continue;
                var itemId = slot.ItemId;
                var data = slot.Data ?? "";
                Chars.Repository.Remove(null, container, Location, itemId, units, data);
                Chars.Repository.AddNewItem(player, $"char_{characterData.UUID}", "inventory", itemId, units, data, MaxSlots: Chars.Repository.GetMaxSlots(player, "inventory"));
                given += units;
            }
            return given;
        }

        /// <summary>
        /// Вернуть всё из контейнера владельцу: онлайн — в инвентарь, что не влезло — на личный склад;
        /// офлайн — сразу на склад (warehouse_{uuid}). Возвращает количество единиц, которые вернуть не удалось.
        /// </summary>
        public static int ReturnToOwner(int uuid, string container)
        {
            var player = Main.GetPlayerByUUID(uuid);
            if (player != null && player.IsCharacterData())
                GiveToPlayer(player, container);

            var left = 0;
            foreach (var slot in Slots(container).OrderBy(s => s.Index).ToList())
            {
                var units = Units(slot);
                var itemId = slot.ItemId;
                var data = slot.Data ?? "";
                if (Chars.Repository.AddNewItem(null, $"warehouse_{uuid}", "warehouse", itemId, units, data, MaxSlots: WarehouseSlots) == -1)
                {
                    left += units;
                    continue;
                }
                Chars.Repository.Remove(null, container, Location, itemId, units, data);
            }
            if (left > 0)
                BlackMarketCore.Log.Write($"ReturnToOwner: склад {uuid} заполнен, {left} ед. остались в {container}");
            return left;
        }

        /// <summary>Уничтожить содержимое контейнера (закладка истекла — товар потерян).</summary>
        public static void Destroy(string container)
        {
            foreach (var slot in Slots(container).ToList())
                Chars.Repository.Remove(null, container, Location, slot.ItemId, Units(slot), slot.Data ?? "");
        }

        private static void Put(string container, ItemId itemId, int units, string data) =>
            Chars.Repository.AddNewItem(null, container, Location, itemId, units, data, MaxSlots: MaxSlots);

        public static List<InventoryItemData> Slots(string container)
        {
            if (!Chars.Repository.ItemsData.TryGetValue(container, out var locations)
                || !locations.TryGetValue(Location, out var slots))
                return new List<InventoryItemData>();
            return slots.Values.Where(s => s.ItemId != ItemId.Debug).ToList();
        }

        private static IEnumerable<InventoryItemData> InventorySlots(ExtPlayer player)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null
                || !Chars.Repository.ItemsData.TryGetValue($"char_{characterData.UUID}", out var locations)
                || !locations.TryGetValue("inventory", out var slots))
                return Enumerable.Empty<InventoryItemData>();
            return slots.Values.Where(s => s.ItemId != ItemId.Debug);
        }

        /// <summary>У нестакаемых предметов Count бывает 0 — это одна штука.</summary>
        private static int Units(InventoryItemData slot) => slot.Count <= 0 ? 1 : slot.Count;
    }
}
