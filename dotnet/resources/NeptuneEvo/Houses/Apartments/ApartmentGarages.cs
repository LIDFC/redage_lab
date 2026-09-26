using GTANetworkAPI;
using System.Collections.Generic;

namespace NeptuneEvo.Houses.Apartments
{
    /// <summary>
    /// Гаражи квартир по классу: чем выше класс квартиры, тем больше гараж.
    /// Эконом и Комфорт — стандартные гаражи GTA (2 и 6 мест), от Комфорт+ — гаражи из DLC GTA5RP_APARTMENT
    /// (int_garage.ytyp, MLO в (650, 500, -50), 5 залов по оси Y), без DLC — большие гаражи GTA (офис, казино).
    /// Места машин расставлены вдоль стен залов по выгрузке CodeWalker: двери лифта и верстаки не перекрываются.
    /// </summary>
    static class ApartmentGarages
    {
        public const int FirstDlcType = 10;

        private static readonly Vector3 Origin = new Vector3(650f, 500f, -50f);
        private const float CarStep = 3.8f;

        /// <summary>Тип гаража (GarageManager.GarageTypes) для класса квартиры (HouseManager.HouseTypeList).</summary>
        public static int ForClass(int houseType, bool dlc)
        {
            switch (houseType)
            {
                case 0:
                case 1:
                case 2: return 0;                          // Эконом, Эконом+: 2 места (GTA)
                case 3: return 4;                          // Комфорт: 6 мест (GTA)
                case 4: return dlc ? 10 : 5;               // Комфорт+: 12 (DLC) / 10 (GTA)
                case 5: return dlc ? 11 : 5;               // Премиум: 15 / 10
                case 6: return dlc ? 12 : 7;               // Премиум+: 20 / 15 (гараж офиса)
                case 8: return dlc ? 13 : 8;               // Премиум++: 28 / 23 (казино)
                case 9: return dlc ? 14 : 9;               // Люкс: 29 / 38 (казино)
                default: return 0;
            }
        }

        /// <summary>Гаражи из DLC: ключ — тип гаража.</summary>
        public static Dictionary<int, GarageType> Build()
        {
            var list = new Dictionary<int, GarageType>();

            // Зал 2 (x ±11, y 51.9..99.5, пол +0.14): лифт у северной стены, верстаки посередине (y 67..74)
            {
                var g = new Layout(0.14f);
                foreach (var y in new[] { 56f, 59.8f, 63.6f, 78f, 81.8f, 85.6f })
                {
                    g.Car(-8.3f, y, 270f);
                    g.Car(8.3f, y, 90f);
                }
                list[FirstDlcType] = g.Build(new Vector3(0f, 94.3f, 0f));
            }

            // Зал 1 (x -16.9..10.5, y -23.4..21.7, пол +0.12): лифт у южной стены, верстаки у восточной (y 4.8..15.5)
            {
                var g = new Layout(0.12f);
                for (var i = 0; i < 10; i++)
                    g.Car(-14.1f, -16f + CarStep * i, 270f);
                for (var i = 0; i < 5; i++)
                    g.Car(7.8f, -16f + CarStep * i, 90f);
                list[FirstDlcType + 1] = g.Build(new Vector3(0.2f, -18.2f, 0f));
            }

            // Зал 3 (x ±15.4, y 126.6..176.4, пол +0.18): два лифта у северной стены (x ±11.4), верстаки в северо-западном углу
            {
                var g = new Layout(0.18f);
                for (var i = 0; i < 10; i++)
                {
                    g.Car(-12.7f, 130f + CarStep * i, 270f);
                    g.Car(12.7f, 130f + CarStep * i, 90f);
                }
                list[FirstDlcType + 2] = g.Build(new Vector3(11.4f, 171.3f, 0f));
            }

            // Зал 5 (x ±35.3, y 285.6..312, пол +0.15): лифты у южной стены (x ±7.8), верстаки по центру у северной
            {
                var g = new Layout(0.15f);
                for (var i = 0; i < 8; i++)
                {
                    g.Car(-33.4f + CarStep * i, 309.3f, 180f);
                    g.Car(33.4f - CarStep * i, 309.3f, 180f);
                }
                for (var i = 0; i < 6; i++)
                {
                    g.Car(-33.4f + CarStep * i, 288.4f, 0f);
                    g.Car(33.4f - CarStep * i, 288.4f, 0f);
                }
                list[FirstDlcType + 3] = g.Build(new Vector3(0f, 291.5f, 0f));
            }

            // Зал 4 (x ±37.2, y 207.1..240.2, пол +0.15): лифты у южной стены (x ±7.8), верстаки в юго-восточном углу
            {
                var g = new Layout(0.15f);
                for (var i = 0; i < 19; i++)
                    g.Car(-34.2f + CarStep * i, 237.5f, 180f);
                for (var i = 0; i < 6; i++)
                    g.Car(-34.2f + CarStep * i, 209.9f, 0f);
                for (var i = 0; i < 4; i++)
                    g.Car(13.3f + CarStep * i, 209.9f, 0f);
                list[FirstDlcType + 4] = g.Build(new Vector3(0f, 212.8f, 0f));
            }

            return list;
        }

        private class Layout
        {
            private readonly float _floor;
            private readonly List<Vector3> _positions = new List<Vector3>();
            private readonly List<Vector3> _rotations = new List<Vector3>();

            public Layout(float floor) => _floor = floor;

            public void Car(float x, float y, float heading)
            {
                _positions.Add(Origin + new Vector3(x, y, _floor + 0.5f));
                _rotations.Add(new Vector3(0, 0, heading));
            }

            /// <summary>entry — точка у лифта, где игрок появляется и выходит (локально, z от пола).</summary>
            public GarageType Build(Vector3 entry) =>
                new GarageType(Origin + new Vector3(entry.X, entry.Y, _floor + 1.0f), _positions, _rotations, _positions.Count);
        }
    }
}
