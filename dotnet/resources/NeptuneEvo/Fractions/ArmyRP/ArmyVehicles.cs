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
    /// Стоянки машин армии. Места машин хранятся в БД (fractionvehicles.position/rotation):
    /// для новой установки — database/main.sql, для работающей базы — скрипты database/updates/*.sql.
    ///  /armyset vehmove ground|air — выстроить ряд машин этого типа от места и направления админа (вручную).
    /// Позиция пишется в fractionvehicles (как /setfracveh), машина сразу возвращается на новое место.
    /// </summary>
    class ArmyVehicles : Script
    {

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
