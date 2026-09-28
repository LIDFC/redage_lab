using System;
using System.Collections.Generic;
using GTANetworkAPI;
using NeptuneEvo.Handles;

namespace NeptuneEvo.Organizations.Contracts.Cargo
{
    /// <summary>
    /// Тип груза (CargoType): вес единицы, сколько единиц в одной паллете и чем она выглядит в мире.
    /// Подряды регистрируют сюда свои материалы; другие работы (грузчик, снабжение…) — свои типы.
    /// </summary>
    public class CargoType
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public float KgPerUnit { get; set; }
        /// <summary>Стек: сколько единиц в одном физическом объекте (паллете) и в одном слоте кузова.</summary>
        public int UnitsPerPallet { get; set; }
        public string Prop { get; set; }
    }

    public enum CargoState : byte
    {
        /// <summary>Лежит на земле (у магазина или там, где его положили).</summary>
        Ground = 0,
        /// <summary>В руках у игрока.</summary>
        Carried = 1,
        /// <summary>В кузове машины (привязан к номеру, не к entity: переживает уничтожение и респавн машины).</summary>
        InVehicle = 2,
    }

    /// <summary>
    /// Одна паллета (CargoStack): агрегированный груз — один объект в мире на UnitsPerPallet единиц.
    /// Владелец задаётся парой OwnerType/OwnerId ("org"/id организации) — модуль не привязан к подрядам.
    /// </summary>
    public class CargoUnit
    {
        public int Id { get; set; }
        public string OwnerType { get; set; } = CargoOwner.Organization;
        public int OwnerId { get; set; }
        /// <summary>Для подрядов — id контракта (груз можно сдать только в этот контракт). 0 — без привязки.</summary>
        public int ContractId { get; set; }
        public string CargoType { get; set; }
        public int Quantity { get; set; }
        public CargoState State { get; set; }
        public string VehicleNumber { get; set; } = "";
        public int CarrierUuid { get; set; }
        public Vector3 Position { get; set; } = new Vector3();
        public uint Dimension { get; set; }
        public DateTime CreatedAt { get; set; }

        // Только в памяти
        public ExtObject Object { get; set; }
        public ExtTextLabel Label { get; set; }
        public ExtColShape Shape { get; set; }
    }

    public static class CargoOwner
    {
        public const string Organization = "org";
    }

    /// <summary>Контейнер (CargoContainer): кузов машины — слоты (паллеты) и грузоподъёмность.</summary>
    public class CargoCapacity
    {
        public int Slots { get; set; }
        public int MaxKg { get; set; }
        public int UsedSlots { get; set; }
        public double UsedKg { get; set; }
        public List<CargoUnit> Units { get; set; } = new List<CargoUnit>();
    }
}
