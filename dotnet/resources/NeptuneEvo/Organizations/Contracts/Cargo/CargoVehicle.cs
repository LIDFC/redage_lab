using System;
using System.Collections.Generic;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.Handles;
using NeptuneEvo.Organizations.Contracts.Config;
using NeptuneEvo.VehicleData.LocalData;
using NeptuneEvo.VehicleData.LocalData.Models;

namespace NeptuneEvo.Organizations.Contracts.Cargo
{
    /// <summary>
    /// Кузов грузовика как контейнер груза: whitelist моделей из конфига, слоты (паллеты) и кг.
    /// Груз привязан к номеру машины, поэтому уничтожение/респавн/гараж его не трогают.
    /// Всё — под ContractsCore.Sync.
    /// </summary>
    public static class CargoVehicle
    {
        /// <summary>Общедоступные данные машины для круговогоменю: сколько паллет в кузове.</summary>
        public const string SharedKey = "CARGO_COUNT";

        public static CargoVehicleDefinition GetDefinition(ExtVehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists)
                return null;
            var model = vehicle.Model;
            return ContractsConfig.Current.Vehicles.FirstOrDefault(v => NAPI.Util.GetHashKey(v.Model) == model);
        }

        public static string ModelsText() =>
            string.Join(", ", ContractsConfig.Current.Vehicles.Select(v => v.Name));

        public static CargoCapacity GetCapacity(string number, CargoVehicleDefinition definition)
        {
            var units = CargoManager.GetInVehicle(number);
            return new CargoCapacity
            {
                Slots = definition?.Slots ?? 0,
                MaxKg = definition?.MaxKg ?? 0,
                UsedSlots = units.Count,
                UsedKg = units.Sum(CargoManager.Kg),
                Units = units,
            };
        }

        public static string CapacityText(CargoCapacity capacity) =>
            $"{capacity.UsedSlots}/{capacity.Slots} паллет · {Math.Round(capacity.UsedKg)}/{capacity.MaxKg} кг";

        /// <summary>Организационная машина на улице и её организация (id). 0 — не организационная.</summary>
        public static int GetOrganizationId(ExtVehicle vehicle)
        {
            var localData = vehicle?.GetVehicleLocalData();
            if (localData == null || localData.Access != VehicleAccess.Organization)
                return 0;
            return localData.Fraction;
        }

        /// <summary>
        /// Раз в несколько секунд: выставить машинам счётчик паллет (после респавна entity новый — данные теряются)
        /// и вернуть на землю груз машин, которых у организации больше нет (продана/удалена).
        /// </summary>
        public static void Sync()
        {
            var numbers = new HashSet<string>();
            foreach (var group in CargoManager.GetAllInVehicles().GroupBy(u => u.VehicleNumber))
            {
                numbers.Add(group.Key);
                var first = group.First();
                if (first.OwnerType == CargoOwner.Organization)
                {
                    var organizationData = Manager.GetOrganizationData(first.OwnerId);
                    if (organizationData == null || !organizationData.Vehicles.ContainsKey(group.Key))
                    {
                        // Машины нет — груз не пропадает: выкладывается у гаража организаций
                        foreach (var unit in group.ToList())
                            ContractsRepository.Enqueue(CargoManager.SetState(unit, CargoState.Ground,
                                position: CargoManager.FreeSpot(OrphanPoint, 0), dimension: 0));
                        ContractsCore.Log.Write($"Cargo of missing vehicle {group.Key} moved to ground ({group.Count()} pallets)");
                        continue;
                    }
                }

                var vehicle = VehicleData.LocalData.Repository.GetVehicleToNumber(VehicleAccess.Organization, group.Key);
                if (vehicle != null && vehicle.Exists)
                {
                    var count = group.Count();
                    if (!vehicle.HasSharedData(SharedKey) || vehicle.GetSharedData<int>(SharedKey) != count)
                        vehicle.SetSharedData(SharedKey, count);
                }
            }

            // Сбросить счётчик у машин, из которых всё выгрузили
            foreach (var number in Tracked.Where(n => !numbers.Contains(n)).ToList())
            {
                var vehicle = VehicleData.LocalData.Repository.GetVehicleToNumber(VehicleAccess.Organization, number);
                if (vehicle != null && vehicle.Exists)
                    vehicle.SetSharedData(SharedKey, 0);
                Tracked.Remove(number);
            }
            foreach (var number in numbers)
                Tracked.Add(number);
        }

        private static readonly HashSet<string> Tracked = new HashSet<string>();

        /// <summary>Куда выкладывается груз машины, которой больше нет (у въезда в гараж организаций).</summary>
        public static readonly Vector3 OrphanPoint = new Vector3(-800.5, 309.5, 85.7);
    }
}
