using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using GTANetworkAPI;
using MySqlConnector;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;

namespace NeptuneEvo.Organizations.Contracts.Cargo
{
    /// <summary>
    /// Физический агрегированный груз: паллеты (1 объект = до UnitsPerPallet единиц) на земле, в руках и в кузове.
    /// Состояние — в памяти под ContractsCore.Sync, каждая паллета — строка org_cargo.
    /// Объекты создаются только для паллет на земле: груз в кузове хранится за номером машины и объектов не плодит.
    /// </summary>
    public static class CargoManager
    {
        private static readonly Dictionary<string, CargoType> Types = new Dictionary<string, CargoType>();
        private static readonly Dictionary<int, CargoUnit> Units = new Dictionary<int, CargoUnit>();
        private static int _lastId = 0;

        /// <summary>Шаг сетки, по которой паллеты раскладываются у точки выдачи.</summary>
        private const float GridStep = 1.8f;

        // ------------------------------------------------------------------ типы груза

        public static void RegisterType(CargoType type)
        {
            if (type != null && !string.IsNullOrEmpty(type.Id))
                Types[type.Id] = type;
        }

        public static CargoType GetType(string id) =>
            id != null && Types.TryGetValue(id, out var type) ? type : null;

        public static string TypeName(string id) => GetType(id)?.Name ?? id;

        public static double Kg(CargoUnit unit) => unit.Quantity * (double)(GetType(unit.CargoType)?.KgPerUnit ?? 0);

        // ------------------------------------------------------------------ загрузка

        /// <summary>После рестарта: паллеты «в руках» возвращаются на землю там, где их взяли; «в кузове» остаются за номером машины.</summary>
        public static void Load()
        {
            lock (ContractsCore.Sync)
            {
                using (var max = ContractsRepository.Read("SELECT IFNULL(MAX(`id`), 0) AS `id` FROM `org_cargo`"))
                    _lastId = max == null || max.Rows.Count == 0 ? 0 : Convert.ToInt32(max.Rows[0]["id"]);

                using var data = ContractsRepository.Read("SELECT * FROM `org_cargo`");
                if (data == null)
                    return;
                foreach (DataRow row in data.Rows)
                {
                    var unit = new CargoUnit
                    {
                        Id = Convert.ToInt32(row["id"]),
                        OwnerType = row["owner_type"].ToString(),
                        OwnerId = Convert.ToInt32(row["owner_id"]),
                        ContractId = Convert.ToInt32(row["contract_id"]),
                        CargoType = row["cargo_type"].ToString(),
                        Quantity = Convert.ToInt32(row["quantity"]),
                        State = (CargoState)Convert.ToByte(row["state"]),
                        VehicleNumber = row["vehicle_number"].ToString(),
                        CarrierUuid = Convert.ToInt32(row["carrier_uuid"]),
                        Position = new Vector3(Convert.ToSingle(row["pos_x"]), Convert.ToSingle(row["pos_y"]), Convert.ToSingle(row["pos_z"])),
                        Dimension = (uint)Convert.ToInt32(row["dimension"]),
                        CreatedAt = Convert.ToDateTime(row["created_at"]),
                    };
                    if (unit.Quantity <= 0)
                    {
                        ContractsRepository.Enqueue(DeleteCommand(unit));
                        continue;
                    }
                    if (unit.State == CargoState.Carried)
                    {
                        unit.State = CargoState.Ground;
                        unit.CarrierUuid = 0;
                        ContractsRepository.Enqueue(SaveCommand(unit));
                    }
                    Units[unit.Id] = unit;
                    if (unit.State == CargoState.Ground)
                        Spawn(unit);
                }
            }
        }

        // ------------------------------------------------------------------ чтение (под Sync)

        public static CargoUnit Get(int id) => Units.TryGetValue(id, out var unit) ? unit : null;

        public static List<CargoUnit> GetByOwner(string ownerType, int ownerId) =>
            Units.Values.Where(u => u.OwnerType == ownerType && u.OwnerId == ownerId).ToList();

        public static List<CargoUnit> GetByContract(int contractId) =>
            Units.Values.Where(u => u.ContractId == contractId).ToList();

        public static List<CargoUnit> GetInVehicle(string number) =>
            Units.Values.Where(u => u.State == CargoState.InVehicle && u.VehicleNumber == number).OrderBy(u => u.Id).ToList();

        public static List<CargoUnit> GetAllInVehicles() =>
            Units.Values.Where(u => u.State == CargoState.InVehicle && !string.IsNullOrEmpty(u.VehicleNumber)).ToList();

        public static CargoUnit GetCarried(int uuid) =>
            Units.Values.FirstOrDefault(u => u.State == CargoState.Carried && u.CarrierUuid == uuid);

        // ------------------------------------------------------------------ создание / удаление (под Sync)

        /// <summary>
        /// Разбить количество на паллеты и выложить их у точки. Возвращает команды вставки —
        /// вызывающий кладёт их в одну транзакцию со списанием денег.
        /// </summary>
        public static List<MySqlCommand> CreateStacks(string ownerType, int ownerId, int contractId, string cargoType, int quantity,
            Vector3 point, uint dimension, string ownerLabel, out List<CargoUnit> created)
        {
            created = new List<CargoUnit>();
            var commands = new List<MySqlCommand>();
            var type = GetType(cargoType);
            if (type == null || quantity <= 0)
                return commands;

            var perPallet = Math.Max(1, type.UnitsPerPallet);
            while (quantity > 0)
            {
                var amount = Math.Min(perPallet, quantity);
                quantity -= amount;
                var unit = new CargoUnit
                {
                    Id = ++_lastId,
                    OwnerType = ownerType,
                    OwnerId = ownerId,
                    ContractId = contractId,
                    CargoType = cargoType,
                    Quantity = amount,
                    State = CargoState.Ground,
                    Position = FreeSpot(point, dimension),
                    Dimension = dimension,
                    CreatedAt = DateTime.Now,
                };
                Units[unit.Id] = unit;
                Spawn(unit, ownerLabel);
                commands.Add(SaveCommand(unit));
                created.Add(unit);
            }
            return commands;
        }

        /// <summary>Убрать паллету совсем (сдана). Возвращает команду удаления для транзакции.</summary>
        public static MySqlCommand Remove(CargoUnit unit)
        {
            Despawn(unit);
            Units.Remove(unit.Id);
            return DeleteCommand(unit);
        }

        /// <summary>Сменить состояние паллеты (земля/руки/кузов) и пересоздать объект при необходимости.</summary>
        public static MySqlCommand SetState(CargoUnit unit, CargoState state, int carrierUuid = 0, string vehicleNumber = "", Vector3 position = null, uint dimension = 0)
        {
            unit.State = state;
            unit.CarrierUuid = state == CargoState.Carried ? carrierUuid : 0;
            unit.VehicleNumber = state == CargoState.InVehicle ? vehicleNumber ?? "" : "";
            if (position != null)
            {
                unit.Position = position;
                unit.Dimension = dimension;
            }
            if (state == CargoState.Ground)
                Spawn(unit);
            else
                Despawn(unit);
            return SaveCommand(unit);
        }

        /// <summary>Сохранить изменённую паллету (количество/привязку) — команда для транзакции.</summary>
        public static MySqlCommand Touch(CargoUnit unit) => SaveCommand(unit);

        /// <summary>Отвязать груз от контракта (контракт закрыт) — груз остаётся владельцу.</summary>
        public static MySqlCommand Unbind(CargoUnit unit)
        {
            unit.ContractId = 0;
            return SaveCommand(unit);
        }

        /// <summary>Первое свободное место в сетке 3×N вокруг точки (чтобы паллеты не вставали друг в друга).</summary>
        public static Vector3 FreeSpot(Vector3 point, uint dimension)
        {
            var ground = Units.Values.Where(u => u.State == CargoState.Ground && u.Dimension == dimension).Select(u => u.Position).ToList();
            for (var i = 0; i < 60; i++)
            {
                var column = i % 3 - 1;
                var row = i / 3;
                var candidate = new Vector3(point.X + column * GridStep, point.Y + row * GridStep, point.Z);
                if (ground.All(p => p.DistanceTo2D(candidate) > GridStep * 0.8f))
                    return candidate;
            }
            return point;
        }

        // ------------------------------------------------------------------ мир

        private static void Spawn(CargoUnit unit, string ownerLabel = null)
        {
            Despawn(unit);
            var type = GetType(unit.CargoType);
            var prop = type?.Prop ?? "prop_boxpile_07d";
            var ground = unit.Position - new Vector3(0, 0, 0.98);
            unit.Object = (ExtObject)NAPI.Object.CreateObject(NAPI.Util.GetHashKey(prop), ground, new Vector3(0, 0, (unit.Id * 37) % 360), 255, unit.Dimension);
            // Клиент по этим данным кладёт паллету на землю и подсвечивает «свой» груз (src_client/table/cargo.js)
            unit.Object.SetSharedData("cargoPallet", $"{unit.OwnerType}:{unit.OwnerId}:{unit.Quantity}:{type?.Name ?? unit.CargoType}");

            ownerLabel ??= OwnerName(unit);
            var text = $"~y~{type?.Name ?? unit.CargoType} ~w~×{unit.Quantity}" + (string.IsNullOrEmpty(ownerLabel) ? "" : $"\n~c~{ownerLabel}");
            unit.Label = (ExtTextLabel)NAPI.TextLabel.CreateTextLabel(Main.StringToU16(text), unit.Position + new Vector3(0, 0, 0.4), 8f, 0.4f, 4, new Color(255, 255, 255), true, unit.Dimension);
            // Колшейп «[E] Взять груз» — Index = id паллеты
            unit.Shape = CustomColShape.CreateCylinderColShape(unit.Position - new Vector3(0, 0, 1.2), 1.6f, 2.6f, unit.Dimension, ColShapeEnums.CargoPallet, unit.Id);
            OnSpawned?.Invoke(unit);
        }

        private static void Despawn(CargoUnit unit)
        {
            if (unit.Object != null && unit.Object.Exists)
                unit.Object.Delete();
            unit.Object = null;
            if (unit.Label != null && unit.Label.Exists)
                unit.Label.Delete();
            unit.Label = null;
            if (unit.Shape != null)
                CustomColShape.DeleteColShape(unit.Shape);
            unit.Shape = null;
        }

        /// <summary>Хук для модуля взаимодействия (колшейп «взять груз») — подключается в фазе переноски.</summary>
        public static Action<CargoUnit> OnSpawned;

        private static string OwnerName(CargoUnit unit) =>
            unit.OwnerType == CargoOwner.Organization ? Manager.GetOrganizationData(unit.OwnerId)?.Name ?? "" : "";

        // ------------------------------------------------------------------ БД

        private static MySqlCommand SaveCommand(CargoUnit unit) =>
            ContractsRepository.Command(
                "INSERT INTO `org_cargo` (`id`,`owner_type`,`owner_id`,`contract_id`,`cargo_type`,`quantity`,`state`,`vehicle_number`,`carrier_uuid`,`pos_x`,`pos_y`,`pos_z`,`dimension`,`created_at`) " +
                "VALUES (@id,@otype,@oid,@contract,@ctype,@qty,@state,@veh,@carrier,@px,@py,@pz,@dim,@created) " +
                "ON DUPLICATE KEY UPDATE `contract_id`=@contract,`quantity`=@qty,`state`=@state,`vehicle_number`=@veh,`carrier_uuid`=@carrier,`pos_x`=@px,`pos_y`=@py,`pos_z`=@pz,`dimension`=@dim",
                ("@id", unit.Id), ("@otype", unit.OwnerType), ("@oid", unit.OwnerId), ("@contract", unit.ContractId), ("@ctype", unit.CargoType),
                ("@qty", unit.Quantity), ("@state", (byte)unit.State), ("@veh", unit.VehicleNumber ?? ""), ("@carrier", unit.CarrierUuid),
                ("@px", unit.Position.X), ("@py", unit.Position.Y), ("@pz", unit.Position.Z), ("@dim", (int)unit.Dimension), ("@created", unit.CreatedAt));

        private static MySqlCommand DeleteCommand(CargoUnit unit) =>
            ContractsRepository.Command("DELETE FROM `org_cargo` WHERE `id`=@id", ("@id", unit.Id));
    }
}
