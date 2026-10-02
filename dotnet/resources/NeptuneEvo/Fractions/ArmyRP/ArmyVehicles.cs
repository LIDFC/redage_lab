using System;
using System.Collections.Generic;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.Core;
using NeptuneEvo.Fractions.Models;
using NeptuneEvo.Handles;
using NeptuneEvo.VehicleData.LocalData;
using NeptuneEvo.VehicleData.LocalData.Models;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Fractions.ArmyRP
{
    /// <summary>
    /// Стоянки машин армии в Форт Занкудо.
    ///  Один раз после обновления (флаг vehiclesMoved в settings/army.json) все машины армии, кроме лодок, переставляются:
    ///  наземные — рядами у рулёжки со стороны ангаров, вертолёты и самолёты — с другой стороны рулёжки.
    ///  /armyset vehmove ground|air — выстроить ряд машин этого типа от места и направления админа.
    ///  FixedSpots — точные места отдельных машин по номеру (ARMY10 и т.д.), имеют приоритет над авторасстановкой.
    /// Позиция пишется в fractionvehicles (как /setfracveh), машина сразу возвращается на новое место.
    /// </summary>
    class ArmyVehicles : Script
    {
        private static ArmyConfig Cfg => ArmyConfig.Current;

        // Ось рулёжки по точкам патруля армии (Table/Tasks/Patrolling)
        private static readonly Vector3 AxisStart = new Vector3(-1984.64, 3278.1184, 33.0);
        private static readonly Vector3 AxisEnd = new Vector3(-1703.181, 3041.3127, 33.0);

        // Места конкретных машин по номерам (расставлены в игре). Применяются при каждом старте,
        // но только если машина стоит не там (дальше 0.5 м или повёрнута больше чем на 2°).
        private static readonly Dictionary<string, (Vector3 pos, float heading)> FixedSpots = new Dictionary<string, (Vector3, float)>
        {
            { "ARMY10", (new Vector3(-2430.7527, 3305.1768, 32.97925), -123.489174f) },
            { "ARMY11", (new Vector3(-2427.0676, 3309.7595, 32.97925), -120.13737f) },
            { "ARMY12", (new Vector3(-2420.2988, 3323.2087, 32.829575), -119.81651f) },
            { "ARMY13", (new Vector3(-2416.8306, 3329.2993, 32.829338), -117.69657f) },
            { "ARMY14", (new Vector3(-2412.9236, 3334.2834, 32.82933), -121.41506f) },
            { "ARMY15", (new Vector3(-2376.853, 3388.143, 32.833294), 152.09206f) },
            { "ARMY16", (new Vector3(-2366.6096, 3382.5833, 32.833294), 151.5993f) },
            { "ARMY17", (new Vector3(-2357.7659, 3377.2432, 32.833294), 149.94348f) },
            { "ARMY18", (new Vector3(-2348.6943, 3371.1167, 32.833298), 150.9576f) },
            { "ARMY19", (new Vector3(-2338.6072, 3365.7012, 32.832764), 151.06647f) },
            { "ARMY20", (new Vector3(-2329.4753, 3360.9016, 32.83265), 147.4912f) },
            { "ARMY21", (new Vector3(-2320.4731, 3357.031, 32.830624), 149.22153f) },
            { "ARMY26", (new Vector3(-2288.8435, 3182.5378, 32.80998), -119.53576f) },
            { "ARMY38", (new Vector3(-2144.5288, 3019.4692, 32.826588), -29.741825f) },
            { "ARMY40", (new Vector3(-2016.333, 2943.4473, 32.80987), -29.363665f) },
            { "ARMY46", (new Vector3(-1803.4293, 2976.5928, 32.80946), 66.80729f) },
            { "ARMY41", (new Vector3(-1816.7024, 2967.5752, 32.809986), 62.624702f) },
            { "ARMY43", (new Vector3(-1836.3613, 2948.5913, 32.810276), 8.142145f) },
            { "ARMY42", (new Vector3(-1834.0901, 2988.2505, 32.809944), 96.25158f) },
            { "ARMY03", (new Vector3(-2413.4668, 3272.4614, 32.831894), 62.22937f) },
            { "ARMY47", (new Vector3(-2411.7605, 3275.5774, 32.831894), 60.602146f) },
        };

        private static void ApplyFixedSpots()
        {
            var fractionData = Manager.GetFractionData((int) Models.Fractions.ARMY);
            if (fractionData == null)
                return;
            var moved = 0;
            foreach (var spot in FixedSpots)
            {
                if (!fractionData.Vehicles.TryGetValue(spot.Key, out var data))
                {
                    ArmyConfig.Log.Write($"Машина армии {spot.Key} не найдена — место не применено");
                    continue;
                }
                var heading = (spot.Value.heading % 360 + 360) % 360;
                var curHeading = data.rotation == null ? -999 : (data.rotation.Z % 360 + 360) % 360;
                var dh = Math.Abs(curHeading - heading);
                if (data.position != null && data.position.DistanceTo(spot.Value.pos) < 0.5f && Math.Min(dh, 360 - dh) < 2)
                    continue;
                Apply(spot.Key, data, spot.Value.pos, heading);
                moved++;
            }
            if (moved > 0)
                ArmyConfig.Log.Write($"Машины армии поставлены на свои места: {moved}");
        }

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            // Машины фракций грузятся из БД при старте — даём время загрузиться
            Timers.StartOnce(30000, () =>
            {
                try
                {
                    if (Cfg.VehiclesMoved)
                    {
                        ApplyFixedSpots();
                        return;
                    }
                    var moved = MoveDefault();
                    if (moved == 0)
                        return;
                    Cfg.VehiclesMoved = true;
                    ArmyConfig.Save();
                    ArmyConfig.Log.Write($"Машины армии переставлены в Форт Занкудо: {moved}");
                    ApplyFixedSpots();
                }
                catch (Exception e)
                {
                    ArmyConfig.Log.Write($"ArmyVehicles auto move Exception: {e}");
                }
            }, true);
        }

        private static bool IsAir(string model)
        {
            try
            {
                var vClass = NAPI.Vehicle.GetVehicleClass(NAPI.Util.VehicleNameToModel(model));
                return vClass == 15 || vClass == 16;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsBoat(string model)
        {
            try
            {
                return NAPI.Vehicle.GetVehicleClass(NAPI.Util.VehicleNameToModel(model)) == 14;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Машины армии нужного типа (лодки не трогаем — им место на воде).</summary>
        private static List<(string number, FractionVehicleData data)> Collect(bool air)
        {
            var fractionData = Manager.GetFractionData((int) Models.Fractions.ARMY);
            if (fractionData == null)
                return new List<(string, FractionVehicleData)>();
            return fractionData.Vehicles
                .Where(v => !IsBoat(v.Value.model) && IsAir(v.Value.model) == air)
                .OrderBy(v => v.Value.rank)
                .ThenBy(v => v.Value.model)
                .Select(v => (v.Key, v.Value))
                .ToList();
        }

        /// <summary>Авторасстановка у рулёжки. Возвращает число переставленных машин.</summary>
        private static int MoveDefault()
        {
            var d = new Vector3(AxisEnd.X - AxisStart.X, AxisEnd.Y - AxisStart.Y, 0);
            var len = Math.Sqrt(d.X * d.X + d.Y * d.Y);
            d = new Vector3(d.X / len, d.Y / len, 0);
            var n = new Vector3(-d.Y, d.X, 0); // вбок от рулёжки (+n — к полосе, −n — к ангарам)
            // Направление «лицом к рулёжке»: forward = (−sin h, cos h)
            float HeadingFor(Vector3 f) => (float) ((Math.Atan2(-f.X, f.Y) * 180 / Math.PI + 360) % 360);

            var count = 0;
            var ground = Collect(false);
            for (var i = 0; i < ground.Count; i++)
            {
                var along = 10 + (i % 25) * 4.5;
                var side = -(20 + (i / 25) * 9);
                var pos = AxisStart + d * (float) along + n * (float) side;
                Apply(ground[i].number, ground[i].data, pos, HeadingFor(n));
                count++;
            }
            var air = Collect(true);
            for (var i = 0; i < air.Count; i++)
            {
                var along = 15 + (i % 12) * 18;
                var side = 30 + (i / 12) * 20;
                var pos = AxisStart + d * (float) along + n * (float) side;
                Apply(air[i].number, air[i].data, pos, HeadingFor(new Vector3(-n.X, -n.Y, 0)));
                count++;
            }
            return count;
        }

        /// <summary>/armyset vehmove ground|air — ряд от места админа вправо, следующие ряды — позади.</summary>
        public static int MoveFromPlayer(ExtPlayer player, bool air)
        {
            var heading = player.Heading;
            var rad = heading * Math.PI / 180.0;
            var forward = new Vector3(-Math.Sin(rad), Math.Cos(rad), 0);
            var right = new Vector3(Math.Cos(rad), Math.Sin(rad), 0);
            var origin = player.Position - new Vector3(0, 0, 0.5);
            var spacing = air ? 18.0 : 4.5;
            var rowStep = air ? 20.0 : 9.0;
            var perRow = air ? 12 : 25;

            var list = Collect(air);
            for (var i = 0; i < list.Count; i++)
            {
                var pos = origin + right * (float) ((i % perRow) * spacing) - forward * (float) ((i / perRow) * rowStep);
                Apply(list[i].number, list[i].data, pos, heading);
            }
            return list.Count;
        }

        private static void Apply(string number, FractionVehicleData data, Vector3 position, float heading)
        {
            data.position = position;
            data.rotation = new Vector3(0, 0, heading);
            data.Dimension = 0;
            NeptuneEvo.Database.DbQueue.Enqueue(
                "UPDATE `fractionvehicles` SET `position`=@p, `rotation`=@r, `isDimension`=0 WHERE `number`=@n AND `fraction`=@f",
                ("@p", JsonConvert.SerializeObject(data.position)),
                ("@r", JsonConvert.SerializeObject(data.rotation)),
                ("@n", number),
                ("@f", (int) Models.Fractions.ARMY));

            var vehicle = RAGE.Entities.Vehicles.All.Cast<ExtVehicle>()
                .FirstOrDefault(v => v.VehicleLocalData != null &&
                                     v.VehicleLocalData.Access == VehicleAccess.Fraction &&
                                     v.VehicleLocalData.Fraction == (int) Models.Fractions.ARMY &&
                                     v.NumberPlate == number);
            var localData = vehicle?.GetVehicleLocalData();
            if (localData == null || localData.Occupants.Count >= 1)
                return; // занятая встанет на место при следующем респавне
            Admin.RespawnFractionCar(vehicle);
        }
    }
}
