using System;
using System.Collections.Generic;
using System.Linq;
using GTANetworkAPI;
using MySqlConnector;
using NeptuneEvo.Character;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Organizations.Contracts.Cargo;
using NeptuneEvo.Organizations.Contracts.Audit;
using NeptuneEvo.Organizations.Contracts.Models;
using NeptuneEvo.Organizations.Models;
using NeptuneEvo.Organizations.Player;
using NeptuneEvo.Players;
using NeptuneEvo.VehicleData.LocalData.Models;
using Redage.SDK;

namespace NeptuneEvo.Organizations.Contracts.Methods
{
    /// <summary>
    /// Сдача груза на точке подряда: зона «[E] Разгрузить груз» у каждой точки, где есть активный подряд,
    /// метки на карте для участников организации, поэтапная разгрузка кузова (паллета за паллетой):
    /// игрок может прервать в любой момент — сдано ровно столько, сколько успели выгрузить.
    /// </summary>
    public class Delivery : Script
    {
        private const float ZoneRadius = 14f;
        private const int SecondsPerPallet = 3;
        private const string BlipPrefix = "orgcontract_";
        private const int BlipSprite = 566;
        private const int BlipColor = 47;

        private class Zone
        {
            public int Id;
            public Vector3 Position;
            public string Name;
            public ExtColShape Shape;
            public ExtMarker Marker;
            public ExtTextLabel Label;
        }

        /// <summary>Зоны сдачи (одна на точку, даже если там несколько подрядов разных организаций).</summary>
        private static readonly Dictionary<int, Zone> Zones = new Dictionary<int, Zone>();
        private static int _lastZoneId = 0;

        /// <summary>Идёт разгрузка: uuid → номер машины.</summary>
        private static readonly Dictionary<int, string> Unloading = new Dictionary<int, string>();

        // ------------------------------------------------------------------ зоны

        /// <summary>Привести зоны в соответствие с активными подрядами (под Sync, главный поток).</summary>
        public static void Reconcile()
        {
            var needed = ContractsManager.GetAll().Where(c => c.IsActive).Select(c => (c.DeliveryPosition, c.PointName)).ToList();

            foreach (var zone in Zones.Values.ToList())
            {
                if (needed.Any(n => n.DeliveryPosition.DistanceTo(zone.Position) < 1f))
                    continue;
                if (zone.Shape != null) CustomColShape.DeleteColShape(zone.Shape);
                if (zone.Marker != null && zone.Marker.Exists) zone.Marker.Delete();
                if (zone.Label != null && zone.Label.Exists) zone.Label.Delete();
                Zones.Remove(zone.Id);
            }

            foreach (var (position, name) in needed)
            {
                if (Zones.Values.Any(z => z.Position.DistanceTo(position) < 1f))
                    continue;
                var zone = new Zone { Id = ++_lastZoneId, Position = position, Name = name };
                zone.Shape = CustomColShape.CreateCylinderColShape(position - new Vector3(0, 0, 2f), ZoneRadius, 8f, 0, ColShapeEnums.ContractDelivery, zone.Id);
                zone.Marker = (ExtMarker)NAPI.Marker.CreateMarker(1, position - new Vector3(0, 0, 1.2f), new Vector3(), new Vector3(), ZoneRadius * 2f,
                    new Color(245, 165, 36, 40), false, 0);
                zone.Label = (ExtTextLabel)NAPI.TextLabel.CreateTextLabel(Main.StringToU16($"~y~{name}\n~w~Приёмка стройматериалов\n~c~Заезжайте грузовиком — [E] Разгрузить груз"),
                    position + new Vector3(0, 0, 1.2f), 25f, 0.5f, 4, new Color(255, 255, 255), true, 0);
                Zones[zone.Id] = zone;
            }
        }

        // ------------------------------------------------------------------ метки на карте

        public static void ShowBlip(ExtPlayer player, Contract contract)
        {
            Trigger.ClientEvent(player, "createBlip", BlipPrefix + contract.Id, $"Подряд #{contract.Id}: {contract.PointName}", BlipSprite, contract.DeliveryPosition, 1f, BlipColor);
        }

        public static void HideBlip(int orgId, int contractId)
        {
            foreach (var player in Character.Repository.GetPlayers())
            {
                if (player.GetOrganizationMemberData()?.Id == orgId)
                    Trigger.ClientEvent(player, "deleteBlip", BlipPrefix + contractId);
            }
        }

        public static void ShowBlipsForOrganization(Contract contract)
        {
            foreach (var player in Character.Repository.GetPlayers())
            {
                if (player.GetOrganizationMemberData()?.Id == contract.OrganizationId)
                    ShowBlip(player, contract);
            }
        }

        /// <summary>Вход в игру: вернуть метки активных подрядов организации.</summary>
        public static void OnCharacterLoaded(ExtPlayer player)
        {
            try
            {
                var memberData = player.GetOrganizationMemberData();
                if (memberData == null || !ContractsManager.Ready)
                    return;
                foreach (var contract in ContractsManager.GetActive(memberData.Id))
                    ShowBlip(player, contract);
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"OnCharacterLoaded Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ разгрузка

        [Interaction(ColShapeEnums.ContractDelivery)]
        public static void OnDeliveryPress(ExtPlayer player, int index)
        {
            try
            {
                if (!player.IsCharacterData() || !ContractsCore.AntiSpam(player, 800))
                    return;
                var uuid = player.GetUUID();

                lock (ContractsCore.Sync)
                {
                    if (Unloading.Remove(uuid))
                    {
                        Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Разгрузка остановлена", 2500);
                        return;
                    }
                }

                var error = Start(player, index, out var vehicle);
                if (error != null)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, error, 4000);
                    return;
                }
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"Разгрузка {vehicle.NumberPlate}… [E] ещё раз — остановить", 3000);
                Schedule(player, index);
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"OnDeliveryPress Exception: {e}");
            }
        }

        [Interaction(ColShapeEnums.ContractDelivery, Out: true)]
        public static void OnDeliveryOut(ExtPlayer player, int index)
        {
            lock (ContractsCore.Sync)
            {
                if (Unloading.Remove(player.GetUUID()))
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Разгрузка остановлена: вы покинули площадку", 3000);
            }
        }

        /// <summary>Найти грузовик организации с подходящим грузом и начать разгрузку.</summary>
        private static string Start(ExtPlayer player, int zoneId, out ExtVehicle vehicle)
        {
            vehicle = null;
            var memberData = player.GetOrganizationMemberData();
            if (memberData == null)
                return "Вы не состоите в организации";

            lock (ContractsCore.Sync)
            {
                if (!Zones.TryGetValue(zoneId, out var zone))
                    return "Точка больше не принимает груз";
                var contracts = ContractsAt(memberData.Id, zone);
                if (contracts.Count == 0)
                    return "У вашей организации нет активного подряда на этой точке";

                vehicle = FindTruck(player, memberData.Id, zone);
                if (vehicle == null)
                    return "Подгоните грузовик организации с грузом на площадку";
                if (NextUnit(vehicle.NumberPlate, contracts) == null)
                    return "В кузове нет материалов, которые нужны подрядам на этой точке";

                Unloading[player.GetUUID()] = vehicle.NumberPlate;
            }
            return null;
        }

        private static void Schedule(ExtPlayer player, int zoneId)
        {
            if (!player.IsInVehicle)
                Trigger.TaskPlayAnim(player, "anim@heists@box_carry@", "idle", 49);
            Timers.StartOnce(SecondsPerPallet * 1000, () => Step(player, zoneId), true);
        }

        /// <summary>Сдать одну паллету; если есть ещё — запланировать следующую.</summary>
        private static void Step(ExtPlayer player, int zoneId)
        {
            try
            {
                if (player == null || !player.IsCharacterData())
                    return;
                var uuid = player.GetUUID();
                var memberData = player.GetOrganizationMemberData();

                string message = null;
                string error = null;
                var more = false;
                Contract completed = null;

                lock (ContractsCore.Sync)
                {
                    if (!Unloading.TryGetValue(uuid, out var number))
                        return;

                    if (memberData == null || !Zones.TryGetValue(zoneId, out var zone))
                        error = "Разгрузка прервана";
                    else if (player.Position.DistanceTo(zone.Position) > ZoneRadius + 4f)
                        error = "Разгрузка прервана: вы покинули площадку";
                    else
                    {
                        var vehicle = VehicleData.LocalData.Repository.GetVehicleToNumber(VehicleAccess.Organization, number);
                        var contracts = ContractsAt(memberData.Id, zone);
                        var pick = NextUnit(number, contracts);
                        if (vehicle == null || !vehicle.Exists || vehicle.Position.DistanceTo(zone.Position) > ZoneRadius + 4f)
                            error = "Разгрузка прервана: грузовик уехал с площадки";
                        else if (pick == null)
                            error = "Подходящий груз закончился";
                        else
                        {
                            var (unit, contract) = pick.Value;
                            var material = contract.GetMaterial(unit.CargoType);
                            var amount = Math.Min(unit.Quantity, material.Required - material.Delivered);
                            material.Delivered += amount;
                            unit.Quantity -= amount;
                            if (unit.ContractId != contract.Id && unit.Quantity > 0)
                                unit.ContractId = contract.Id;

                            var commands = new List<MySqlCommand> { ContractsRepository.SaveCommand(contract) };
                            commands.Add(unit.Quantity <= 0 ? CargoManager.Remove(unit) : CargoManager.Touch(unit));
                            ContractsRepository.EnqueueTransaction(commands.ToArray());

                            var left = CargoManager.GetInVehicle(number).Count;
                            vehicle.SetSharedData(CargoVehicle.SharedKey, left);

                            var name = CargoManager.TypeName(unit.CargoType);
                            message = $"Сдано: {name} ×{amount} — {material.Delivered}/{material.Required}. Подряд #{contract.Id}: {contract.ProgressPercent}%";
                            ContractAudit.OrgLog(contract.OrganizationId, uuid, player.Name, OrganizationLogsType.ContractDeliver,
                                $"Доставил: {name} ×{amount} (подряд #{contract.Id}, {material.Delivered}/{material.Required})");
                            ContractAudit.Write("deliver", contract.Id, contract.OrganizationId, uuid, amount,
                                details: new { unit.Id, unit.CargoType, number, delivered = material.Delivered, material.Required, rest = unit.Quantity });

                            if (contract.IsAllDelivered)
                            {
                                completed = contract;
                                ContractsManager.Complete(contract.Id, uuid, player.Name);
                            }
                            more = NextUnit(number, ContractsAt(memberData.Id, zone)) != null;
                        }
                    }

                    if (error != null || !more)
                        Unloading.Remove(uuid);
                }

                if (message != null)
                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, message, 3500);
                if (completed != null)
                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter,
                        $"Подряд #{completed.Id} выполнен! Организация получила {ContractsCore.Money(completed.Reward)}, репутация +{completed.ReputationReward}", 6000);

                if (error != null)
                {
                    Trigger.StopAnimation(player);
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, error, 3500);
                }
                else if (more)
                    Schedule(player, zoneId);
                else
                {
                    Trigger.StopAnimation(player);
                    if (completed == null)
                        Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Разгрузка завершена", 2500);
                }
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"Step Exception: {e}");
            }
        }

        /// <summary>Активные подряды организации с точкой сдачи в этой зоне.</summary>
        private static List<Contract> ContractsAt(int orgId, Zone zone) =>
            ContractsManager.GetActive(orgId).Where(c => c.DeliveryPosition.DistanceTo(zone.Position) < 1f && !c.IsAllDelivered).ToList();

        /// <summary>
        /// Следующая паллета к сдаче: сначала груз, купленный под конкретный подряд, затем свободный груз
        /// организации под недостающий материал любого подряда на этой точке.
        /// </summary>
        private static (CargoUnit, Contract)? NextUnit(string number, List<Contract> contracts)
        {
            var units = CargoManager.GetInVehicle(number);
            foreach (var unit in units)
            {
                var own = contracts.FirstOrDefault(c => c.Id == unit.ContractId);
                if (own != null && Needs(own, unit.CargoType))
                    return (unit, own);
            }
            foreach (var unit in units)
            {
                if (unit.ContractId > 0 && ContractsManager.Get(unit.ContractId)?.IsActive == true && contracts.All(c => c.Id != unit.ContractId))
                    continue; // груз другого активного подряда — не трогаем
                var target = contracts.FirstOrDefault(c => Needs(c, unit.CargoType));
                if (target != null)
                    return (unit, target);
            }
            return null;
        }

        private static bool Needs(Contract contract, string material)
        {
            var m = contract.GetMaterial(material);
            return m != null && m.Delivered < m.Required;
        }

        /// <summary>Грузовик для разгрузки: машина, в которой сидит игрок, иначе ближайшая машина организации с грузом на площадке.</summary>
        private static ExtVehicle FindTruck(ExtPlayer player, int orgId, Zone zone)
        {
            if (player.IsInVehicle && player.Vehicle is ExtVehicle current && CargoVehicle.GetOrganizationId(current) == orgId)
                return current;

            ExtVehicle best = null;
            var bestDistance = float.MaxValue;
            foreach (var unit in CargoManager.GetAllInVehicles().Where(u => u.OwnerType == CargoOwner.Organization && u.OwnerId == orgId))
            {
                var vehicle = VehicleData.LocalData.Repository.GetVehicleToNumber(VehicleAccess.Organization, unit.VehicleNumber);
                if (vehicle == null || !vehicle.Exists || vehicle.Position.DistanceTo(zone.Position) > ZoneRadius + 2f)
                    continue;
                var distance = vehicle.Position.DistanceTo(player.Position);
                if (distance < bestDistance && distance < 15f)
                {
                    best = vehicle;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
