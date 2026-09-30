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
    /// Позиция пишется в fractionvehicles (как /setfracveh), машина сразу возвращается на новое место.
    /// </summary>
    class ArmyVehicles : Script
    {
        private static ArmyConfig Cfg => ArmyConfig.Current;

        // Ось рулёжки по точкам патруля армии (Table/Tasks/Patrolling)
        private static readonly Vector3 AxisStart = new Vector3(-1984.64, 3278.1184, 33.0);
        private static readonly Vector3 AxisEnd = new Vector3(-1703.181, 3041.3127, 33.0);

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            // Машины фракций грузятся из БД при старте — даём время загрузиться
            Timers.StartOnce(30000, () =>
            {
                try
                {
                    if (Cfg.VehiclesMoved)
                        return;
                    var moved = MoveDefault();
                    if (moved == 0)
                        return;
                    Cfg.VehiclesMoved = true;
                    ArmyConfig.Save();
                    ArmyConfig.Log.Write($"Машины армии переставлены в Форт Занкудо: {moved}");
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
