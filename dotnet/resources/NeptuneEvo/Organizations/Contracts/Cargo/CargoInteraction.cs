using System;
using GTANetworkAPI;
using NeptuneEvo.Character;
using NeptuneEvo.Chars;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Organizations.Contracts.Logs;
using NeptuneEvo.Organizations.Models;
using NeptuneEvo.Organizations.Player;
using NeptuneEvo.Players;
using Redage.SDK;

namespace NeptuneEvo.Organizations.Contracts.Cargo
{
    /// <summary>
    /// Взаимодействие с грузом: [E] взять паллету (анимация переноски + коробка в руках, видно всем),
    /// положить на землю, «Положить груз в кузов» / «Взять груз из кузова» (круговое меню машины).
    /// Груз организации трогают только её участники. Все проверки — на сервере, под ContractsCore.Sync.
    /// </summary>
    public class CargoInteraction : Script
    {
        private const string AnimDict = "anim@heists@box_carry@";
        private const string AnimName = "idle";
        private const float PickupDistance = 3.5f;
        private const float VehicleDistance = 8f;

        /// <summary>Дополнительная проверка погрузки (подряды: груз нужен активному подряду). null — можно.</summary>
        public static Func<CargoUnit, string> LoadValidator;
        /// <summary>Сообщение для логов организации после погрузки.</summary>
        public static Action<ExtPlayer, CargoUnit, string> OnLoaded;

        // ------------------------------------------------------------------ взять с земли

        [Interaction(ColShapeEnums.CargoPallet)]
        public static void OnPalletPress(ExtPlayer player, int index)
        {
            if (!player.IsCharacterData() || !ContractsCore.AntiSpam(player, 800))
                return;
            var error = CanAct(player);
            CargoUnit unit = null;
            if (error == null)
            {
                lock (ContractsCore.Sync)
                {
                    unit = CargoManager.Get(index);
                    if (CargoManager.GetCarried(player.GetUUID()) != null)
                        error = "Руки заняты — сначала положите груз";
                    else if (unit == null || unit.State != CargoState.Ground)
                        error = "Этот груз уже забрали";
                    else if (unit.Dimension != player.Dimension || unit.Position.DistanceTo(player.Position) > PickupDistance)
                        error = "Подойдите ближе к грузу";
                    else if (!IsOwner(player, unit))
                        error = "Это груз другой организации";
                    else
                        ContractsRepository.Enqueue(CargoManager.SetState(unit, CargoState.Carried, player.GetUUID()));
                }
            }
            if (error != null)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, error, 3000);
                return;
            }
            StartCarry(player, unit);
            ContractAudit.Write("cargo_pickup", unit.ContractId, unit.OwnerId, player.GetUUID(), unit.Quantity, details: new { unit.Id, unit.CargoType });
        }

        // ------------------------------------------------------------------ положить на землю

        [RemoteEvent("server.cargo.drop")]
        public static void OnDrop(ExtPlayer player)
        {
            if (!player.IsCharacterData())
                return;
            var heading = player.Heading * Math.PI / 180.0;
            var front = player.Position + new Vector3(-Math.Sin(heading) * 0.9, Math.Cos(heading) * 0.9, 0);
            if (DropCarried(player, front))
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Груз на земле — [E] чтобы поднять", 2500);
        }

        /// <summary>Положить груз из рук (смерть, выход, падение, E). Возвращает true, если груз был.</summary>
        public static bool DropCarried(ExtPlayer player, Vector3 position)
        {
            CargoUnit unit;
            lock (ContractsCore.Sync)
            {
                unit = CargoManager.GetCarried(player.GetUUID());
                if (unit == null)
                    return false;
                ContractsRepository.Enqueue(CargoManager.SetState(unit, CargoState.Ground, position: position, dimension: player.Dimension));
            }
            StopCarry(player);
            return true;
        }

        [ServerEvent(Event.PlayerDeath)]
        public void OnPlayerDeath(ExtPlayer player, ExtPlayer killer, uint reason)
        {
            try
            {
                if (player != null && player.IsCharacterData())
                    DropCarried(player, player.Position);
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"OnPlayerDeath Exception: {e}");
            }
        }

        [ServerEvent(Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(ExtPlayer player, DisconnectionType type, string reason)
        {
            try
            {
                if (player == null)
                    return;
                var uuid = player.GetUUID();
                lock (ContractsCore.Sync)
                {
                    var unit = CargoManager.GetCarried(uuid);
                    if (unit != null)
                        ContractsRepository.Enqueue(CargoManager.SetState(unit, CargoState.Ground, position: player.Position, dimension: player.Dimension));
                }
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"OnPlayerDisconnected Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ кузов

        [RemoteEvent("server.cargo.vehicle")]
        public static void OnVehicleAction(ExtPlayer player, ExtVehicle vehicle, string action)
        {
            try
            {
                if (!player.IsCharacterData() || !ContractsCore.AntiSpam(player, 800))
                    return;
                var error = CanAct(player);
                if (error == null && (vehicle == null || !vehicle.Exists || vehicle.Dimension != player.Dimension || vehicle.Position.DistanceTo(player.Position) > VehicleDistance))
                    error = "Подойдите ближе к машине";
                if (error != null)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, error, 3000);
                    return;
                }
                if (action == "put")
                    Put(player, vehicle);
                else if (action == "take")
                    Take(player, vehicle);
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"OnVehicleAction Exception: {e}");
            }
        }

        private static void Put(ExtPlayer player, ExtVehicle vehicle)
        {
            string error = null;
            CargoUnit unit;
            CargoCapacity capacity = null;
            var number = vehicle.NumberPlate;
            lock (ContractsCore.Sync)
            {
                unit = CargoManager.GetCarried(player.GetUUID());
                var definition = CargoVehicle.GetDefinition(vehicle);
                var orgId = CargoVehicle.GetOrganizationId(vehicle);
                if (unit == null)
                    error = "У вас нет груза в руках";
                else if (orgId == 0 || orgId != player.GetOrganizationMemberData()?.Id || (unit.OwnerType == CargoOwner.Organization && unit.OwnerId != orgId))
                    error = "Нужна машина вашей организации (из гаража организации)";
                else if (definition == null)
                    error = $"Эта машина не подходит для груза. Подходят: {CargoVehicle.ModelsText()}";
                else
                {
                    capacity = CargoVehicle.GetCapacity(number, definition);
                    var kg = CargoManager.Kg(unit);
                    if (capacity.UsedSlots + 1 > capacity.Slots)
                        error = $"Кузов заполнен: {CargoVehicle.CapacityText(capacity)}";
                    else if (capacity.UsedKg + kg > capacity.MaxKg)
                        error = $"Перегруз: паллета {Math.Round(kg)} кг, в кузове {CargoVehicle.CapacityText(capacity)}";
                    else
                        error = LoadValidator?.Invoke(unit);

                    if (error == null)
                    {
                        ContractsRepository.Enqueue(CargoManager.SetState(unit, CargoState.InVehicle, vehicleNumber: number));
                        capacity.UsedSlots++;
                        capacity.UsedKg += kg;
                        vehicle.SetSharedData(CargoVehicle.SharedKey, capacity.UsedSlots);
                    }
                }
            }
            if (error != null)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, error, 4000);
                return;
            }

            StopCarry(player);
            Trigger.TaskPlayAnim(player, "anim@heists@narcotics@trash", "drop_side", 48);
            var name = CargoManager.TypeName(unit.CargoType);
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"{name} ×{unit.Quantity} в кузове. {CargoVehicle.CapacityText(capacity)}", 4000);
            ContractAudit.Write("cargo_load", unit.ContractId, unit.OwnerId, player.GetUUID(), unit.Quantity, details: new { unit.Id, unit.CargoType, number });
            OnLoaded?.Invoke(player, unit, number);
        }

        private static void Take(ExtPlayer player, ExtVehicle vehicle)
        {
            string error = null;
            CargoUnit unit = null;
            var number = vehicle.NumberPlate;
            lock (ContractsCore.Sync)
            {
                var orgId = CargoVehicle.GetOrganizationId(vehicle);
                if (CargoManager.GetCarried(player.GetUUID()) != null)
                    error = "Руки заняты — сначала положите груз";
                else if (orgId == 0 || orgId != player.GetOrganizationMemberData()?.Id)
                    error = "Это не машина вашей организации";
                else
                {
                    var units = CargoManager.GetInVehicle(number);
                    if (units.Count == 0)
                        error = "Кузов пуст";
                    else
                    {
                        unit = units[units.Count - 1];
                        ContractsRepository.Enqueue(CargoManager.SetState(unit, CargoState.Carried, player.GetUUID()));
                        vehicle.SetSharedData(CargoVehicle.SharedKey, units.Count - 1);
                    }
                }
            }
            if (error != null)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, error, 3000);
                return;
            }
            StartCarry(player, unit);
            ContractAudit.Write("cargo_unload_hand", unit.ContractId, unit.OwnerId, player.GetUUID(), unit.Quantity, details: new { unit.Id, unit.CargoType, number });
        }

        // ------------------------------------------------------------------ переноска

        private static void StartCarry(ExtPlayer player, CargoUnit unit)
        {
            Attachments.AddAttachment(player, Attachments.AttachmentsName.CargoBox);
            Trigger.PlayAnimation(player, AnimDict, AnimName, 49);
            Main.OnAntiAnim(player);
            Trigger.ClientEvent(player, "client.cargo.carry", true, CargoManager.TypeName(unit.CargoType), unit.Quantity);
        }

        private static void StopCarry(ExtPlayer player)
        {
            Attachments.RemoveAttachment(player, Attachments.AttachmentsName.CargoBox);
            Trigger.StopAnimation(player);
            Main.OffAntiAnim(player);
            Trigger.ClientEvent(player, "client.cargo.carry", false, "", 0);
        }

        private static string CanAct(ExtPlayer player)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null)
                return "Недоступно";
            if (player.IsInVehicle)
                return "Выйдите из машины";
            if (sessionData.DeathData.InDeath || sessionData.DeathData.IsDying)
                return "Недоступно";
            if (sessionData.CuffedData.Cuffed)
                return "Вы в наручниках";
            return null;
        }

        private static bool IsOwner(ExtPlayer player, CargoUnit unit)
        {
            if (unit.OwnerType == CargoOwner.Organization)
                return player.GetOrganizationMemberData()?.Id == unit.OwnerId;
            return true;
        }
    }
}
