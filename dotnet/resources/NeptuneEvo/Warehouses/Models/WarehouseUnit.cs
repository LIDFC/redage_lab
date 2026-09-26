namespace NeptuneEvo.Warehouses.Models
{
    public class WarehouseUnit
    {
        public int Id { get; set; }
        public int WarehouseId { get; set; }
        public int SlotNumber { get; set; }
        /// <summary>Личная ячейка: UUID персонажа-владельца</summary>
        public int? OwnerUuid { get; set; } = null;
        /// <summary>Семейная ячейка: id семьи (организации)</summary>
        public int? FamilyId { get; set; } = null;
        public bool Locked { get; set; } = true;

        public bool IsFree => !OwnerUuid.HasValue && !FamilyId.HasValue;
        public bool IsFamily => FamilyId.HasValue;

        /// <summary>Ключ хранилища предметов: publicwarehouse_{Id}</summary>
        public string InventoryId => Id.ToString();
    }
}
