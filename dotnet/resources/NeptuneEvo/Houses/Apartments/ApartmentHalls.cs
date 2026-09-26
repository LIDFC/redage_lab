using GTANetworkAPI;
using NeptuneEvo.Character;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using NeptuneEvo.Players.Popup.List.Models;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeptuneEvo.Houses.Apartments
{
    /// <summary>
    /// Подъезды и коридоры многоквартирных домов из DLC (int_mp_kor.ytyp, выгрузка CodeWalker):
    ///  - kor_elit_1 (700, 1300, -186.3)  — 10 этажей × 10 дверей, лифт;
    ///  - kor_med_1  (500, 1300, -186.3)  — 10 этажей × 10 дверей, лифт;
    ///  - kor_bichN_1 (-200, -100-10(N-1), -100) — лестничный подъезд на N этажей × 4 двери, холл на первом.
    /// Координаты дверей взяты из entities MLO (apa_p_mp_door_*), точки подхода — на 0.8 м в коридор.
    /// У каждого дома свой экземпляр коридора: измерение HallDimensionBase + id дома.
    /// </summary>
    public class ApartmentHall
    {
        public string Key;
        public string Title;
        public Vector3 Origin;
        public int Floors;
        public float FloorHeight = 3.7f;
        /// <summary>Номер этажа для игрока у нижнего ряда дверей (у малых подъездов квартиры со 2-го этажа).</summary>
        public int FirstFloorNumber = 1;
        /// <summary>Точки у дверей квартир нижнего этажа (локально относительно Origin), по порядку номеров.</summary>
        public Vector3[] Doors;
        /// <summary>Где игрок появляется, заходя с улицы, и где выход на улицу (локально).</summary>
        public Vector3 Spawn;
        /// <summary>Кнопка лифта на каждом этаже (локально, нижний этаж). null — только лестница.</summary>
        public Vector3 Elevator;

        public int Capacity => Floors * Doors.Length;

        public Vector3 World(Vector3 local, int floorIndex = 0) =>
            Origin + local + new Vector3(0, 0, FloorHeight * floorIndex);

        /// <summary>Номер последнего жилого этажа (для интерфейса).</summary>
        public int TopFloorNumber => FirstFloorNumber + Floors - 1;

        /// <summary>
        /// Номер квартиры (1..) → индекс этажа и двери. Квартиры раскладываются по этажам снизу вверх
        /// (кв. 1 — первый жилой этаж, кв. 2 — второй …), а после верхнего этажа занимают следующую дверь,
        /// чтобы дом был заселён на всю высоту, а не только на первых этажах.
        /// </summary>
        public (int floorIndex, int door) Slot(int number) =>
            ((number - 1) % Floors, (number - 1) / Floors);
    }

    public static class ApartmentHalls
    {
        public const uint HallDimensionBase = 3000000;

        public static readonly Dictionary<string, ApartmentHall> All = new Dictionary<string, ApartmentHall>();

        static ApartmentHalls()
        {
            All["elit"] = new ApartmentHall
            {
                Key = "elit",
                Title = "Элитный подъезд с лифтом",
                Origin = new Vector3(700f, 1300f, -186.3f),
                Floors = 10,
                Doors = new[]
                {
                    // Западная стена (x -7.956) и восточная (x -4.786), торцы; коридор по оси y, центр x ≈ -6.37
                    new Vector3(-7.15f, 15.425f, -0.117f), new Vector3(-5.6f, 22.415f, -0.117f),
                    new Vector3(-7.15f, 4.005f, -0.117f), new Vector3(-5.6f, 11.005f, -0.117f),
                    new Vector3(-7.15f, -7.407f, -0.117f), new Vector3(-5.6f, -11.824f, -0.117f),
                    new Vector3(-7.15f, -18.81f, -0.117f), new Vector3(-5.6f, -23.224f, -0.117f),
                    new Vector3(-6.6f, 26.85f, -0.117f), new Vector3(-5.9f, -29.0f, -0.117f),
                },
                Spawn = new Vector3(-6.37f, -1.5f, -0.117f),
                Elevator = new Vector3(-2.4f, 0.4f, -0.117f),
            };
            All["med"] = new ApartmentHall
            {
                Key = "med",
                Title = "Подъезд с лифтом",
                Origin = new Vector3(500f, 1300f, -186.3f),
                Floors = 10,
                Doors = new[]
                {
                    // Западная стена (x -2.149), восточная (x 0.941), торцы; центр коридора x ≈ -0.6
                    new Vector3(-1.35f, 12.456f, -0.178f), new Vector3(0.15f, 19.446f, -0.178f),
                    new Vector3(-1.35f, 1.046f, -0.178f), new Vector3(0.15f, 8.036f, -0.178f),
                    new Vector3(-1.35f, -10.364f, -0.178f), new Vector3(0.15f, -14.784f, -0.178f),
                    new Vector3(-1.35f, -21.764f, -0.178f), new Vector3(0.15f, -26.184f, -0.178f),
                    new Vector3(-1.1f, 23.95f, -0.178f), new Vector3(-0.1f, -31.95f, -0.178f),
                },
                Spawn = new Vector3(-0.6f, -5.5f, -0.178f),
                Elevator = new Vector3(3.0f, -4.4f, -0.178f),
            };
            for (var n = 1; n <= 5; n++)
            {
                All[$"bich{n}"] = new ApartmentHall
                {
                    Key = $"bich{n}",
                    Title = $"Подъезд на {n + 1} эт. (лестница)",
                    Origin = new Vector3(-200f, -100f - 10f * (n - 1), -100f),
                    Floors = n,
                    FirstFloorNumber = 2,
                    Doors = new[]
                    {
                        // Площадка у лестницы: двери на южной, восточной (две) и северной стенах
                        new Vector3(5.146f, -1.6f, 3.123f),
                        new Vector3(5.6f, -0.502f, 3.123f),
                        new Vector3(5.6f, 1.598f, 3.123f),
                        new Vector3(3.856f, 1.45f, 3.123f),
                    },
                    Spawn = new Vector3(-2.4f, -0.1f, 1.092f),
                    Elevator = null,
                };
            }
        }

        public static ApartmentHall Get(string key) =>
            !string.IsNullOrEmpty(key) && All.TryGetValue(key, out var hall) ? hall : null;

        /// <summary>Какой подъезд подходит дому: элитный — если есть Премиум/Люкс, иначе малый или средний по числу квартир.</summary>
        public static string Suggest(int flats, bool premium)
        {
            if (premium) return "elit";
            if (flats <= 20) return $"bich{Math.Max(1, Math.Min(5, (flats + 3) / 4))}";
            return "med";
        }

        public static uint Dimension(ApartmentBuilding building) => HallDimensionBase + (uint)building.Id;
    }
}
