using System;
using System.Collections.Concurrent;
using GTANetworkAPI;
using NeptuneEvo.Character;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using NeptuneEvo.VehicleData.LocalData;
using NeptuneEvo.VehicleData.LocalData.Models;
using Redage.SDK;

namespace NeptuneEvo.Core
{
    /// <summary>
    /// Панель автомобиля в приложении телефона «Авто» (CEF phonenew/components/cars/panel.svelte, клиент src_client/phone/cars.js).
    /// Двигатель, замок и поворотники идут через уже существующие события; здесь — то, чего раньше не было:
    /// двери по отдельности, окна, фары (выкл/ближний/дальний), салонный свет, режим езды.
    /// Всё проверяется на сервере: игрок в этой машине, водитель — для света/режима/чужих дверей,
    /// пассажир — только своя дверь и своё окно. Состояния синхронизируются shared data машины.
    /// </summary>
    public class VehiclePanel : Script
    {
        private static readonly nLog Log = new nLog("Core.VehiclePanel");

        /// <summary>Режимы езды: 0 — эко, 1 — комфорт (по умолчанию), 2 — спорт.</summary>
        public const int DriveEco = 0, DriveComfort = 1, DriveSport = 2;

        // Счётчики тиков расхода для режимов (эко пропускает каждый 5-й тик, спорт добавляет каждый 3-й)
        private static readonly ConcurrentDictionary<ExtVehicle, int> FuelTicks = new ConcurrentDictionary<ExtVehicle, int>();
        // Режим езды дублируется здесь: таймер расхода топлива работает не в главном потоке и не должен читать shared data
        private static readonly ConcurrentDictionary<ExtVehicle, int> DriveModes = new ConcurrentDictionary<ExtVehicle, int>();

        [RemoteEvent("server.vehicle.panel")]
        public static void OnPanel(ExtPlayer player, string action, int value)
        {
            try
            {
                if (!player.IsCharacterData() || !player.IsInVehicle)
                    return;
                var vehicle = (ExtVehicle)player.Vehicle;
                if (vehicle == null || !vehicle.Exists)
                    return;

                var seat = player.VehicleSeat - (int)VehicleSeat.Driver; // 0 — водитель, 1..3 — пассажиры
                var isDriver = seat == 0;

                switch (action)
                {
                    case "door":
                        {
                            if (value < 0 || value > 5)
                                return;
                            // Пассажир открывает только свою дверь; капот и багажник — водитель
                            if (!isDriver && value != seat)
                                return;
                            var door = (DoorId)value;
                            var open = VehicleStreaming.GetDoorState(vehicle, door) == DoorState.DoorClosed;
                            if (open && door == DoorId.DoorHood && VehicleStreaming.GetLockState(vehicle))
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Машина закрыта", 2500);
                                return;
                            }
                            if (open && door == DoorId.DoorTrunk && !VehicleManager.canAccessByNumber(player, vehicle.NumberPlate))
                            {
                                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Нет ключей от этой машины", 2500);
                                return;
                            }
                            VehicleStreaming.SetDoorState(vehicle, door, open ? DoorState.DoorOpen : DoorState.DoorClosed);
                            if (!open && door == DoorId.DoorTrunk)
                                Chars.Repository.ItemsAllClose(VehicleManager.GetVehicleToInventory(vehicle.NumberPlate));
                        }
                        break;
                    case "window":
                        {
                            if (value < 0 || value > 3 || (!isDriver && value != seat))
                                return;
                            var mask = vehicle.HasSharedData("vWindows") ? vehicle.GetSharedData<int>("vWindows") : 0;
                            mask ^= 1 << value;
                            vehicle.SetSharedData("vWindows", mask);
                        }
                        break;
                    case "lights":
                        if (!isDriver || value < 0 || value > 2)
                            return;
                        vehicle.SetSharedData("vLights", value);
                        break;
                    case "interior":
                        if (!isDriver)
                            return;
                        vehicle.SetSharedData("vInterior", value == 1);
                        break;
                    case "drive":
                        if (!isDriver || value < DriveEco || value > DriveSport)
                            return;
                        vehicle.SetSharedData("vDriveMode", value);
                        if (value == DriveComfort)
                            DriveModes.TryRemove(vehicle, out _);
                        else
                            DriveModes[vehicle] = value;
                        Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter,
                            value == DriveEco ? "Режим «Эко»: экономия топлива" : value == DriveSport ? "Режим «Спорт»: мощнее, но больше расход" : "Режим «Комфорт»", 2500);
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Write($"OnPanel Exception: {e}");
            }
        }

        /// <summary>
        /// Поправка расхода топлива на режим езды (вызывается из VehicleManager.FuelControl на каждый тик):
        /// эко — каждый 5-й тик бесплатный (≈×0,8), спорт — каждый 3-й тик двойной (≈×1,33).
        /// </summary>
        public static int FuelRate(ExtVehicle vehicle, int baseRate)
        {
            try
            {
                if (baseRate <= 0 || !DriveModes.TryGetValue(vehicle, out var mode))
                    return baseRate;
                var tick = FuelTicks.AddOrUpdate(vehicle, 1, (_, t) => t + 1);
                if (mode == DriveEco)
                    return tick % 5 == 0 ? 0 : baseRate;
                return tick % 3 == 0 ? baseRate * 2 : baseRate;
            }
            catch
            {
                return baseRate;
            }
        }

        [ServerEvent(Event.VehicleDeath)]
        public void OnVehicleDeath(Vehicle vehicle)
        {
            if (vehicle is ExtVehicle ext)
            {
                FuelTicks.TryRemove(ext, out _);
                DriveModes.TryRemove(ext, out _);
            }
        }
    }
}
