using GTANetworkAPI;
using NeptuneEvo.Handles;
using NeptuneEvo.Chars;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Functions;
using NeptuneEvo.MoneySystem;
using NeptuneEvo.Organizations.Player;
using NeptuneEvo.Players;
using NeptuneEvo.Table.Models;
using NeptuneEvo.Warehouses.Models;
using Newtonsoft.Json;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace NeptuneEvo.Warehouses
{
    /// <summary>
    /// Общественные склады (drainerw/Advanced-Family-Personal-Warehouse-Storage-RedAge-v3-), доработано под проект:
    ///  - личные и семейные ячейки (семейную покупает/продаёт владелец семьи, пользуются члены с правом OpenStock);
    ///  - проверка расстояния до склада на всех серверных событиях (иначе можно было телепортироваться в ячейку откуда угодно);
    ///  - выход из игры внутри ячейки возвращает к входу через CharacterData.ExteriorPos.
    /// Таблицы: database/systems/warehouse.sql
    /// </summary>
    class WarehouseManager : Script
    {
        private static readonly nLog Log = new nLog("Warehouses.Manager");

        public const uint BaseDimension = 10000;
        public const int StorageSlots = 300;          // слотов в ячейке (как у склада семьи)
        private const float InteractDistance = 5f;
        private const string InventoryName = "publicwarehouse";

        public static List<WarehouseBuilding> Warehouses = new List<WarehouseBuilding>();
        public static Dictionary<int, WarehouseUnit> Units = new Dictionary<int, WarehouseUnit>();

        private static readonly List<Marker> Markers = new List<Marker>();
        private static readonly List<TextLabel> Labels = new List<TextLabel>();
        private static readonly HashSet<int> BlipCreated = new HashSet<int>(); // блипы клиентские, создаём один раз
        private static readonly List<ExtColShape> ColShapes = new List<ExtColShape>();

        // Интерьер склада (одинаковый для всех ячеек, разные измерения)
        private static readonly Vector3 ExitPosition = new Vector3(1048.0, -3097.0, -40.0);
        private static readonly Vector3 StoragePosition = new Vector3(1060.0, -3100.0, -40.0);

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            try
            {
                LoadWarehouses();
            }
            catch (Exception e)
            {
                Log.Write($"OnResourceStart Exception: {e}");
            }
        }

        [Command("reloadwarehouses")]
        public static void CMD_ReloadWarehouses(ExtPlayer player)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null || characterData.AdminLVL < 8) return;
            LoadWarehouses();
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, "Склады перезагружены", 3000);
        }

        public static void LoadWarehouses()
        {
            foreach (var marker in Markers) if (marker.Exists) marker.Delete();
            foreach (var label in Labels) if (label.Exists) label.Delete();
            foreach (var shape in ColShapes) CustomColShape.DeleteColShape(shape);

            Markers.Clear();
            Labels.Clear();
            ColShapes.Clear();
            Warehouses.Clear();
            Units.Clear();

            DataTable result = MySQL.QueryRead("SELECT * FROM `public_warehouses`");
            if (result == null)
            {
                Log.Write("Таблица public_warehouses не найдена — выполните database/systems/warehouse.sql");
                return;
            }

            foreach (DataRow row in result.Rows)
            {
                var building = new WarehouseBuilding
                {
                    Id = Convert.ToInt32(row["id"]),
                    Name = Convert.ToString(row["name"]),
                    Address = Convert.ToString(row["address"]),
                    Price = Convert.ToInt32(row["price"]),
                    Capacity = Convert.ToInt32(row["capacity"]),
                    TotalUnits = Convert.ToInt32(row["total_units"]),
                    Position = JsonConvert.DeserializeObject<Vector3>(Convert.ToString(row["pos"])),
                    InteriorPos = JsonConvert.DeserializeObject<Vector3>(Convert.ToString(row["interior_pos"]))
                };

                ColShapes.Add(CustomColShape.CreateCylinderColShape(building.Position, 1.5f, 2f, 0, ColShapeEnums.PublicWarehouse, building.Id, 0));
                Markers.Add(NAPI.Marker.CreateMarker(1, building.Position - new Vector3(0, 0, 0.9), new Vector3(), new Vector3(), 1.5f, new Color(255, 165, 0, 150), false, 0));
                Labels.Add(NAPI.TextLabel.CreateTextLabel($"~o~{building.Name}\n~w~Аренда ячеек", building.Position + new Vector3(0, 0, 0.5), 5f, 0.5f, 0, new Color(255, 255, 255), true, 0));
                if (BlipCreated.Add(building.Id))
                    Main.CreateBlip(new Main.BlipData(473, building.Name, building.Position, 47, true));

                Warehouses.Add(building);
            }

            result = MySQL.QueryRead("SELECT * FROM `public_warehouse_units`") ?? new DataTable();
            foreach (DataRow row in result.Rows)
            {
                var unit = new WarehouseUnit
                {
                    Id = Convert.ToInt32(row["id"]),
                    WarehouseId = Convert.ToInt32(row["warehouse_id"]),
                    SlotNumber = Convert.ToInt32(row["slot_number"]),
                    OwnerUuid = row["owner_uuid"] == DBNull.Value ? (int?)null : Convert.ToInt32(row["owner_uuid"]),
                    FamilyId = row["family_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(row["family_id"]),
                    Locked = Convert.ToBoolean(row["locked"])
                };

                var building = Warehouses.FirstOrDefault(w => w.Id == unit.WarehouseId);
                if (building != null)
                {
                    building.Units[unit.SlotNumber] = unit;
                    Units[unit.Id] = unit;
                }
            }

            // Выход и шкаф внутри — во всех измерениях
            ColShapes.Add(CustomColShape.CreateCylinderColShape(ExitPosition, 1.5f, 2f, uint.MaxValue, ColShapeEnums.PublicWarehouse, 0, 1));
            Markers.Add(NAPI.Marker.CreateMarker(1, ExitPosition, new Vector3(), new Vector3(), 1.0f, new Color(255, 165, 0, 150), false, uint.MaxValue));
            Labels.Add(NAPI.TextLabel.CreateTextLabel("~o~Выход", ExitPosition + new Vector3(0, 0, 0.5), 5f, 0.5f, 0, new Color(255, 255, 255), true, uint.MaxValue));

            ColShapes.Add(CustomColShape.CreateCylinderColShape(StoragePosition, 1.5f, 2f, uint.MaxValue, ColShapeEnums.PublicWarehouse, 0, 2));
            Markers.Add(NAPI.Marker.CreateMarker(1, StoragePosition, new Vector3(), new Vector3(), 1.0f, new Color(255, 165, 0, 150), false, uint.MaxValue));
            Labels.Add(NAPI.TextLabel.CreateTextLabel("~o~Склад", StoragePosition + new Vector3(0, 0, 0.5), 5f, 0.5f, 0, new Color(255, 255, 255), true, uint.MaxValue));

            Log.Write($"Loaded {Warehouses.Count} warehouses and {Units.Count} units.");
        }

        #region Доступ

        private static int GetFamilyId(ExtPlayer player)
        {
            var member = player.GetOrganizationMemberData();
            return member != null && member.Id > 0 ? member.Id : 0;
        }

        private static bool IsFamilyOwner(ExtPlayer player, int familyId)
        {
            var characterData = player.GetCharacterData();
            var organizationData = player.GetOrganizationData();
            return characterData != null && organizationData != null && GetFamilyId(player) == familyId && organizationData.OwnerUUID == characterData.UUID;
        }

        /// <summary>Может ли игрок входить в ячейку и пользоваться хранилищем</summary>
        private static bool CanUse(ExtPlayer player, WarehouseUnit unit, bool notify)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null) return false;

            if (unit.OwnerUuid.HasValue)
                return unit.OwnerUuid.Value == characterData.UUID;

            if (unit.FamilyId.HasValue && GetFamilyId(player) == unit.FamilyId.Value)
                return player.IsOrganizationAccess(RankToAccess.OpenStock, notify);

            return false;
        }

        /// <summary>Может ли игрок продать ячейку</summary>
        private static bool CanManage(ExtPlayer player, WarehouseUnit unit)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null) return false;

            if (unit.OwnerUuid.HasValue)
                return unit.OwnerUuid.Value == characterData.UUID;

            return unit.FamilyId.HasValue && IsFamilyOwner(player, unit.FamilyId.Value);
        }

        private static bool IsNear(ExtPlayer player, WarehouseBuilding building) =>
            player.Dimension == 0 && player.Position.DistanceTo(building.Position) <= InteractDistance;

        private static WarehouseUnit GetCurrentUnit(ExtPlayer player)
        {
            if (player.Dimension <= BaseDimension) return null;
            int unitId = (int)(player.Dimension - BaseDimension);
            return Units.TryGetValue(unitId, out var unit) ? unit : null;
        }

        #endregion

        [Interaction(ColShapeEnums.PublicWarehouse)]
        public static void Interact(ExtPlayer player, int buildingId, int state)
        {
            try
            {
                if (state == 1)
                {
                    ExitUnit(player);
                    return;
                }
                if (state == 2)
                {
                    OpenStorage(player);
                    return;
                }

                OpenMenu(player, buildingId);
            }
            catch (Exception e)
            {
                Log.Write($"Interact Exception: {e}");
            }
        }

        private static void OpenMenu(ExtPlayer player, int buildingId)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null) return;

            var building = Warehouses.FirstOrDefault(w => w.Id == buildingId);
            if (building == null) return;

            var familyId = GetFamilyId(player);
            var organizationData = player.GetOrganizationData();

            var unitsData = new List<object>();
            for (int i = 1; i <= building.TotalUnits; i++)
            {
                if (building.Units.TryGetValue(i, out var unit))
                {
                    unitsData.Add(new
                    {
                        id = unit.Id,
                        slot = unit.SlotNumber,
                        isFree = false,
                        isFamily = unit.IsFamily,
                        isMine = CanUse(player, unit, false),
                        canSell = CanManage(player, unit),
                    });
                }
                else
                {
                    unitsData.Add(new { slot = i, isFree = true });
                }
            }

            Trigger.ClientEvent(player, "client.warehouse.open", JsonConvert.SerializeObject(new
            {
                id = building.Id,
                name = building.Name,
                address = building.Address,
                price = building.Price,
                sellPrice = building.Price / 2,
                capacity = StorageSlots,
                units = unitsData,
                hasPersonal = Units.Values.Any(u => u.OwnerUuid == characterData.UUID),
                familyName = familyId > 0 && organizationData != null ? organizationData.Name : null,
                canBuyFamily = familyId > 0 && IsFamilyOwner(player, familyId) && !Units.Values.Any(u => u.FamilyId == familyId),
            }));
        }

        public static void ExitUnit(ExtPlayer player)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null) return;

            var unit = GetCurrentUnit(player);
            var building = unit != null ? Warehouses.FirstOrDefault(w => w.Id == unit.WarehouseId) : null;

            player.Dimension = 0;
            player.Position = building != null ? building.Position : (Warehouses.FirstOrDefault()?.Position ?? new Vector3(899.12f, -2985.34f, 5.9f));
            characterData.ExteriorPos = new Vector3();

            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Вы вышли на улицу", 3000);
        }

        public static void OpenStorage(ExtPlayer player)
        {
            var unit = GetCurrentUnit(player);
            if (unit == null) return;

            if (!CanUse(player, unit, true))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "У вас нет доступа к этому складу", 3000);
                return;
            }

            var title = unit.IsFamily ? $"Семейный склад #{unit.SlotNumber}" : $"Склад #{unit.SlotNumber}";
            Chars.Repository.LoadOtherItemsData(player, InventoryName, unit.InventoryId, 13, StorageSlots);
            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, title, 2000);
        }

        [RemoteEvent("server.warehouse.buy")]
        public static void BuyUnit(ExtPlayer player, int buildingId, int slot, bool forFamily)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                var building = Warehouses.FirstOrDefault(w => w.Id == buildingId);
                if (building == null || !IsNear(player, building)) return;

                if (slot < 1 || slot > building.TotalUnits || building.Units.ContainsKey(slot))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Эта ячейка уже занята", 3000);
                    return;
                }

                int familyId = 0;
                if (forFamily)
                {
                    familyId = GetFamilyId(player);
                    if (familyId == 0 || !IsFamilyOwner(player, familyId))
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Купить ячейку для семьи может только её владелец", 3000);
                        return;
                    }
                    if (Units.Values.Any(u => u.FamilyId == familyId))
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "У вашей семьи уже есть ячейка", 3000);
                        return;
                    }
                }
                else if (Units.Values.Any(u => u.OwnerUuid == characterData.UUID))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "У вас уже есть личная ячейка", 3000);
                    return;
                }

                if (UpdateData.CanIChange(player, building.Price, true) != 255)
                    return;

                Wallet.Change(player, -building.Price);
                GameLog.Money($"player({characterData.UUID})", "server", building.Price, $"buyWarehouseUnit({buildingId}:{slot}{(forFamily ? $":org{familyId}" : "")})");

                var owner = forFamily ? "NULL" : characterData.UUID.ToString();
                var family = forFamily ? familyId.ToString() : "NULL";
                var inserted = MySQL.QueryRead($"INSERT INTO `public_warehouse_units` (`warehouse_id`, `slot_number`, `owner_uuid`, `family_id`, `locked`) VALUES ({buildingId}, {slot}, {owner}, {family}, 1); SELECT LAST_INSERT_ID();");
                int unitId = Convert.ToInt32(inserted.Rows[0][0]);

                var unit = new WarehouseUnit
                {
                    Id = unitId,
                    WarehouseId = buildingId,
                    SlotNumber = slot,
                    OwnerUuid = forFamily ? (int?)null : characterData.UUID,
                    FamilyId = forFamily ? familyId : (int?)null,
                    Locked = true
                };

                building.Units[slot] = unit;
                Units[unitId] = unit;

                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter,
                    forFamily ? $"Вы купили ячейку №{slot} для семьи" : $"Вы купили ячейку №{slot}", 3000);
                OpenMenu(player, buildingId);
            }
            catch (Exception e)
            {
                Log.Write($"BuyUnit Exception: {e}");
            }
        }

        [RemoteEvent("server.warehouse.enter")]
        public static void EnterUnit(ExtPlayer player, int unitId)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                if (!Units.TryGetValue(unitId, out var unit)) return;
                var building = Warehouses.FirstOrDefault(w => w.Id == unit.WarehouseId);
                if (building == null || !IsNear(player, building)) return;

                if (!CanUse(player, unit, true))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "У вас нет доступа к этой ячейке", 3000);
                    return;
                }

                // Если выйдет из игры внутри — появится у входа (Character.Repository.SavePlayerPosition)
                characterData.ExteriorPos = building.Position;
                player.Position = building.InteriorPos;
                player.Dimension = BaseDimension + (uint)unit.Id;

                Trigger.ClientEvent(player, "client.warehouse.close");
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"Вы вошли в ячейку №{unit.SlotNumber}", 3000);
            }
            catch (Exception e)
            {
                Log.Write($"EnterUnit Exception: {e}");
            }
        }

        [RemoteEvent("server.warehouse.sell")]
        public static void SellUnit(ExtPlayer player, int unitId)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                if (!Units.TryGetValue(unitId, out var unit)) return;
                var building = Warehouses.FirstOrDefault(w => w.Id == unit.WarehouseId);
                if (building == null || !IsNear(player, building)) return;

                if (!CanManage(player, unit))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Продать ячейку может только её владелец", 3000);
                    return;
                }

                // Нельзя продать ячейку с вещами — иначе они пропадут
                var locationName = $"{InventoryName}_{unit.InventoryId}";
                if (Chars.Repository.ItemsData.TryGetValue(locationName, out var locations) &&
                    locations.TryGetValue(InventoryName, out var items) &&
                    items.Values.Any(i => i.ItemId != ItemId.Debug))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Сначала освободите ячейку от вещей", 3000);
                    return;
                }

                int sellPrice = building.Price / 2;
                Wallet.Change(player, sellPrice);
                GameLog.Money("server", $"player({characterData.UUID})", sellPrice, $"sellWarehouseUnit({unit.Id})");

                MySQL.Query($"DELETE FROM `public_warehouse_units` WHERE `id` = {unit.Id}");

                building.Units.Remove(unit.SlotNumber);
                Units.Remove(unit.Id);

                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы продали ячейку №{unit.SlotNumber} за ${Wallet.Format(sellPrice)}", 3000);
                OpenMenu(player, building.Id);
            }
            catch (Exception e)
            {
                Log.Write($"SellUnit Exception: {e}");
            }
        }
    }
}
