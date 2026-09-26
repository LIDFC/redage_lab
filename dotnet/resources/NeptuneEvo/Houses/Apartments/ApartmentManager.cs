using GTANetworkAPI;
using MySqlConnector;
using NeptuneEvo.Character;
using NeptuneEvo.Chars;
using NeptuneEvo.Core;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using NeptuneEvo.Players.Popup.List.Models;
using Newtonsoft.Json;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace NeptuneEvo.Houses.Apartments
{
    /// <summary>
    /// Строка планировки дома: сколько квартир какого класса (HouseManager.HouseTypeList), цена и тип гаража.
    /// </summary>
    public class ApartmentPlanEntry
    {
        [JsonProperty("type")] public int Type { get; set; }
        [JsonProperty("price")] public int Price { get; set; }
        [JsonProperty("garage")] public int Garage { get; set; }
        [JsonProperty("count")] public int Count { get; set; }
    }

    public class ApartmentFlat
    {
        public int HouseId { get; set; }
        public int BuildingId { get; set; }
        public int Floor { get; set; }
        public int Number { get; set; }
        /// <summary>Интерьер из DLC (ApartmentInteriors), 0 — не назначен.</summary>
        public int Interior { get; set; }
    }

    public class ApartmentBuilding
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        /// <summary>Точка у подъезда (уровень земли, как позиции домов).</summary>
        public Vector3 Entrance { get; set; }
        /// <summary>Въезд в общий гараж, сюда же выезжают машины жильцов.</summary>
        public Vector3 GaragePos { get; set; }
        public float GarageHeading { get; set; }
        public List<ApartmentPlanEntry> Plan { get; set; } = new List<ApartmentPlanEntry>();
        public List<ApartmentFlat> Flats { get; } = new List<ApartmentFlat>();

        [JsonIgnore] public ExtColShape EntranceShape;
        [JsonIgnore] public ExtColShape GarageShape;
        [JsonIgnore] public List<Entity> WorldEntities = new List<Entity>();
        /// <summary>Тип подъезда (ApartmentHalls: elit, med, bich1..5). Пусто — только меню у входа.</summary>
        public string Hall { get; set; } = "";
        [JsonIgnore] public List<ExtColShape> HallShapes = new List<ExtColShape>();
    }

    /// <summary>
    /// Многоквартирные дома. Идея — из «Apartment System for RedAge 1.1» (koltr), переписано под v3:
    ///  - квартира — это обычный дом (House): свой интерьер, налоги, сожители, мебель, гараж, продажа и маркетплейс работают как у домов;
    ///  - у квартиры нет маркера на улице: вход через меню подъезда, выход из квартиры — к подъезду;
    ///  - гараж у всех квартир общий въезд (ColShapeEnums.ApartmentGarage), внутри каждый попадает в свой гараж;
    ///  - квартиры создаются сами при старте по планировке дома (колонка plan), таблицу houses не меняем;
    ///  - купить можно у подъезда или в риэлторском агентстве, вкладка «Многоквартирные дома».
    /// Таблицы: database/systems/apartments.sql
    /// </summary>
    class ApartmentManager : Script
    {
        private static readonly nLog Log = new nLog("Houses.Apartments");

        public const int FlatsPerFloor = 4;
        private const float InteractDistance = 5f;

        public static readonly Dictionary<int, ApartmentBuilding> Buildings = new Dictionary<int, ApartmentBuilding>();
        private static readonly HashSet<int> BlipCreated = new HashSet<int>(); // блипы клиентские, создаются один раз
        private static bool _tablesReady;
        private static bool _hasInteriorColumn;
        private static bool _hasHallColumn;
        private static readonly Random Rnd = new Random();

        /// <summary>
        /// Включены ли интерьеры из DLC GTA5RP_APARTMENT (settings/apartments.json → "dlcInteriors").
        /// Без DLC у игроков вместо квартиры будет пустота, поэтому по умолчанию выключено.
        /// </summary>
        public static bool UseDlcInteriors { get; private set; }

        private static void LoadSettings()
        {
            try
            {
                var path = System.IO.Path.Combine("settings", "apartments.json");
                if (!System.IO.File.Exists(path))
                    return;
                var json = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(path));
                UseDlcInteriors = json.Value<bool?>("dlcInteriors") ?? false;
            }
            catch (Exception e)
            {
                Log.Write($"settings/apartments.json: {e.Message}");
            }
        }

        /// <summary>
        /// Вызывается из Main после HouseManager.Init: квартиры — это уже загруженные дома.
        /// </summary>
        public static void Init()
        {
            try
            {
                var buildingsTable = MySQL.QueryRead("SELECT * FROM `apartment_buildings`");
                var flatsTable = MySQL.QueryRead("SELECT * FROM `apartment_flats`");
                if (buildingsTable == null || flatsTable == null)
                {
                    Log.Write("Таблицы apartment_buildings / apartment_flats не найдены — выполните database/systems/apartments.sql", nLog.Type.Warn);
                    return;
                }
                _tablesReady = true;

                LoadSettings();
                ApartmentInteriors.LoadOverrides();
                _hasInteriorColumn = flatsTable.Columns.Contains("interior");
                if (!_hasInteriorColumn)
                {
                    // Колонка появилась в обновлении с DLC-интерьерами; пробуем добавить сами
                    MySQL.Query("ALTER TABLE `apartment_flats` ADD COLUMN `interior` int(11) NOT NULL DEFAULT 0");
                    var check = MySQL.QueryRead("SHOW COLUMNS FROM `apartment_flats` LIKE 'interior'");
                    _hasInteriorColumn = check != null && check.Rows.Count > 0;
                    if (!_hasInteriorColumn)
                        Log.Write("Нет колонки apartment_flats.interior — выполните database/systems/apartments_interiors.sql", nLog.Type.Warn);
                }

                var hasHallColumn = buildingsTable.Columns.Contains("hall");
                if (!hasHallColumn)
                {
                    MySQL.Query("ALTER TABLE `apartment_buildings` ADD COLUMN `hall` varchar(16) NOT NULL DEFAULT ''");
                    var check = MySQL.QueryRead("SHOW COLUMNS FROM `apartment_buildings` LIKE 'hall'");
                    _hasHallColumn = check != null && check.Rows.Count > 0;
                }
                else
                    _hasHallColumn = true;

                foreach (DataRow row in buildingsTable.Rows)
                {
                    var building = new ApartmentBuilding
                    {
                        Id = Convert.ToInt32(row["id"]),
                        Name = Convert.ToString(row["name"]),
                        Address = Convert.ToString(row["address"]),
                        Entrance = JsonConvert.DeserializeObject<Vector3>(Convert.ToString(row["entrance"])),
                        GaragePos = JsonConvert.DeserializeObject<Vector3>(Convert.ToString(row["garage"])),
                        GarageHeading = Convert.ToSingle(row["garage_heading"]),
                        Hall = hasHallColumn && row["hall"] != DBNull.Value ? Convert.ToString(row["hall"]) : "",
                    };
                    try
                    {
                        building.Plan = JsonConvert.DeserializeObject<List<ApartmentPlanEntry>>(Convert.ToString(row["plan"])) ?? new List<ApartmentPlanEntry>();
                    }
                    catch (Exception e)
                    {
                        Log.Write($"Дом {building.Id}: неверный plan ({e.Message})", nLog.Type.Warn);
                    }
                    Buildings[building.Id] = building;
                }

                foreach (DataRow row in flatsTable.Rows)
                {
                    var flat = new ApartmentFlat
                    {
                        HouseId = Convert.ToInt32(row["house_id"]),
                        BuildingId = Convert.ToInt32(row["building_id"]),
                        Floor = Convert.ToInt32(row["floor"]),
                        Number = Convert.ToInt32(row["number"]),
                        Interior = flatsTable.Columns.Contains("interior") && row["interior"] != DBNull.Value ? Convert.ToInt32(row["interior"]) : 0,
                    };
                    if (!Buildings.TryGetValue(flat.BuildingId, out var building))
                        continue;

                    var house = HouseManager.Houses.FirstOrDefault(h => h.ID == flat.HouseId);
                    if (house == null)
                        continue; // дом удалили командой — квартира пропадает из списка

                    AttachFlat(building, flat, house);
                    building.Flats.Add(flat);
                }

                foreach (var building in Buildings.Values)
                {
                    building.Flats.Sort((a, b) => a.Number.CompareTo(b.Number));
                    AutoAssignHall(building);
                    CreateWorld(building);
                    GenerateMissingFlats(building);
                }

                Log.Write($"Loaded {Buildings.Count} apartment buildings, {Buildings.Values.Sum(b => b.Flats.Count)} flats.", nLog.Type.Success);
            }
            catch (Exception e)
            {
                Log.Write($"Init Exception: {e}");
            }
        }

        private static void AttachFlat(ApartmentBuilding building, ApartmentFlat flat, House house)
        {
            house.AttachToApartment(building.Id, building.Entrance);
            var garage = house.GetGarageData();
            garage?.AttachToApartment(building.GaragePos, building.GarageHeading);
            ApplyGarage(house);
            ApplyInterior(flat, house);
        }

        /// <summary>Гараж квартиры по её классу (ApartmentGarages): чем выше класс, тем больше мест.</summary>
        private static void ApplyGarage(House house)
        {
            var garage = house.GetGarageData();
            if (garage == null || garage.Type == -1 || garage.Type == 6)
                return;
            garage.SetApartmentType(ApartmentGarages.ForClass(house.Type, UseDlcInteriors));
        }

        private static void ApplyInterior(ApartmentFlat flat, House house)
        {
            if (!UseDlcInteriors || !_hasInteriorColumn)
                return;

            if (!ApartmentInteriors.IsValid(flat.Interior))
            {
                flat.Interior = ApartmentInteriors.Pick(house.Type, Rnd);
                using var cmd = new MySqlCommand { CommandText = "UPDATE `apartment_flats` SET `interior`=@interior WHERE `house_id`=@house" };
                cmd.Parameters.AddWithValue("@interior", flat.Interior);
                cmd.Parameters.AddWithValue("@house", flat.HouseId);
                MySQL.Query(cmd);
            }
            house.SetCustomInterior(ApartmentInteriors.GetPosition(flat.Interior));
        }

        /// <summary>После /aptintset — переставить точку у всех квартир с этим интерьером.</summary>
        public static void ReapplyInterior(int interiorId)
        {
            foreach (var building in Buildings.Values)
            foreach (var flat in building.Flats.Where(f => f.Interior == interiorId))
            {
                var house = HouseManager.Houses.FirstOrDefault(h => h.ID == flat.HouseId);
                if (house != null)
                    house.SetCustomInterior(ApartmentInteriors.GetPosition(interiorId));
            }
        }

        #region World
        private static void CreateWorld(ApartmentBuilding building)
        {
            DestroyWorld(building);

            building.EntranceShape = CustomColShape.CreateCylinderColShape(building.Entrance, 1.5f, 3f, 0, ColShapeEnums.ApartmentEntrance, building.Id);
            building.WorldEntities.Add(NAPI.Marker.CreateMarker(1, building.Entrance - new Vector3(0, 0, 0.1), new Vector3(), new Vector3(), 1.2f, new Color(80, 170, 255, 160), false, 0));
            building.WorldEntities.Add(NAPI.TextLabel.CreateTextLabel(Main.StringToU16($"~b~{building.Name}\n~w~Квартиры: нажмите ~y~E"), building.Entrance + new Vector3(0, 0, 1.6), 8f, 0.5f, 0, new Color(255, 255, 255), true, 0));

            if (building.GaragePos != null && building.GaragePos.Length() > 1)
            {
                building.GarageShape = CustomColShape.CreateCylinderColShape(building.GaragePos - new Vector3(0, 0, 1), 3f, 4f, 0, ColShapeEnums.ApartmentGarage, building.Id);
                building.WorldEntities.Add(NAPI.Marker.CreateMarker(1, building.GaragePos - new Vector3(0, 0, 1.1), new Vector3(), new Vector3(), 3f, new Color(80, 170, 255, 100), false, 0));
                building.WorldEntities.Add(NAPI.TextLabel.CreateTextLabel(Main.StringToU16($"~b~Гараж жильцов\n~w~{building.Name}"), building.GaragePos + new Vector3(0, 0, 0.6), 10f, 0.5f, 0, new Color(255, 255, 255), true, 0));
            }

            if (BlipCreated.Add(building.Id))
                Main.CreateBlip(new Main.BlipData(475, building.Name, building.Entrance, 3, true, 0.9f));

            CreateHall(building);
        }

        private static void DestroyWorld(ApartmentBuilding building)
        {
            CustomColShape.DeleteColShape(building.EntranceShape);
            building.EntranceShape = null;
            CustomColShape.DeleteColShape(building.GarageShape);
            building.GarageShape = null;
            foreach (var entity in building.WorldEntities)
            {
                try
                {
                    if (entity != null && entity.Exists)
                        entity.Delete();
                }
                catch { }
            }
            building.WorldEntities.Clear();
            foreach (var shape in building.HallShapes)
                CustomColShape.DeleteColShape(shape);
            building.HallShapes.Clear();
        }
        #endregion

        #region Generation
        /// <summary>
        /// Досоздаёт квартиры по планировке: для каждой строки plan — Count квартир класса Type.
        /// Каждая квартира = новый дом + гараж + счёт в банке для налогов.
        /// </summary>
        private static void GenerateMissingFlats(ApartmentBuilding building)
        {
            var toCreate = new List<ApartmentPlanEntry>();
            foreach (var entry in building.Plan)
            {
                if (entry.Type < 0 || entry.Type >= HouseManager.HouseTypeList.Count || entry.Type == 7 || !GarageManager.GarageTypes.ContainsKey(entry.Garage))
                {
                    Log.Write($"Дом {building.Id}: пропущена строка plan (type {entry.Type}, garage {entry.Garage})", nLog.Type.Warn);
                    continue;
                }
                var have = building.Flats.Count(f => HouseManager.Houses.FirstOrDefault(h => h.ID == f.HouseId)?.Type == entry.Type);
                for (var i = have; i < entry.Count; i++)
                    toCreate.Add(entry);
            }

            if (toCreate.Count == 0)
                return;

            Trigger.SetTask(async () =>
            {
                foreach (var entry in toCreate)
                {
                    try
                    {
                        var bankId = await MoneySystem.Bank.Create(string.Empty, 2, 0);
                        var done = new TaskCompletionSource<bool>();
                        NAPI.Task.Run(() =>
                        {
                            try
                            {
                                CreateFlat(building, entry, bankId);
                            }
                            catch (Exception e)
                            {
                                Log.Write($"CreateFlat Exception: {e}");
                            }
                            done.TrySetResult(true);
                        });
                        await done.Task;
                    }
                    catch (Exception e)
                    {
                        Log.Write($"GenerateMissingFlats Exception: {e}");
                    }
                }
                Log.Write($"{building.Name}: создано квартир {toCreate.Count}", nLog.Type.Success);
            });
        }

        private static void CreateFlat(ApartmentBuilding building, ApartmentPlanEntry entry, int bankId)
        {
            var garageId = 0;
            do
            {
                garageId++;
            } while (GarageManager.Garages.ContainsKey(garageId));

            var garage = new Garage(garageId, entry.Garage, building.GaragePos, new Vector3(0, 0, building.GarageHeading));
            garage.AttachToApartment(building.GaragePos, building.GarageHeading);
            garage.Dimension = GarageManager.DimensionId++;
            garage.Create();
            if (garage.Type != -1 && garage.Type != 6)
                garage.CreateInterior();
            GarageManager.Garages.Add(garageId, garage);

            var dimension = HouseManager.DimensionID++;
            var house = new House(HouseManager.GetUID(), string.Empty, entry.Type, building.Entrance, entry.Price, false, garageId, bankId, new Dictionary<string, ResidentData>(), dimension, false, false);
            house.Create();
            FurnitureManager.Create(house.ID);
            house.CreateInterior();
            HouseManager.Houses.Add(house);

            var number = building.Flats.Count == 0 ? 1 : building.Flats.Max(f => f.Number) + 1;
            var flat = new ApartmentFlat
            {
                HouseId = house.ID,
                BuildingId = building.Id,
                Number = number,
                Floor = (number - 1) / FlatsPerFloor + 2, // первый этаж — холл
            };
            AttachFlat(building, flat, house);
            building.Flats.Add(flat);
            var hallForFlat = ApartmentHalls.Get(building.Hall);
            if (hallForFlat != null)
                AddHallDoor(building, hallForFlat, flat, house);

            using var cmd = new MySqlCommand
            {
                CommandText = _hasInteriorColumn
                    ? "INSERT INTO `apartment_flats` (`house_id`, `building_id`, `floor`, `number`, `interior`) VALUES (@house, @building, @floor, @number, @interior)"
                    : "INSERT INTO `apartment_flats` (`house_id`, `building_id`, `floor`, `number`) VALUES (@house, @building, @floor, @number)"
            };
            cmd.Parameters.AddWithValue("@interior", flat.Interior);
            cmd.Parameters.AddWithValue("@house", flat.HouseId);
            cmd.Parameters.AddWithValue("@building", flat.BuildingId);
            cmd.Parameters.AddWithValue("@floor", flat.Floor);
            cmd.Parameters.AddWithValue("@number", flat.Number);
            MySQL.Query(cmd);
        }
        #endregion

        #region Data for UI
        public static int GetTax(House house) => Convert.ToInt32(house.Price / 100f * HouseManager.HouseTax);

        private static int GetCars(House house)
        {
            var garage = house.GetGarageData();
            return garage != null && GarageManager.GarageTypes.ContainsKey(garage.Type) ? GarageManager.GarageTypes[garage.Type].MaxCars : 0;
        }

        private static Dictionary<string, object> GetFlatData(ApartmentFlat flat, House house, ExtPlayer player)
        {
            var isResident = player != null && (house.Owner == player.Name || house.Roommates.ContainsKey(player.Name));
            return new Dictionary<string, object>
            {
                { "id", house.ID },
                { "number", flat.Number },
                { "floor", flat.Floor },
                { "type", house.Type },
                { "price", house.Price },
                { "tax", GetTax(house) },
                { "cars", GetCars(house) },
                { "roommates", house.Roommates.Count },
                { "maxRoommates", HouseManager.MaxRoommates[house.Type] },
                { "owner", house.Owner },
                { "isFree", string.IsNullOrEmpty(house.Owner) && !house.IsAuction && house.Price > 0 },
                { "isAuction", house.IsAuction },
                { "locked", house.Locked },
                { "isMine", isResident },
                { "layout", UseDlcInteriors && ApartmentInteriors.IsValid(flat.Interior) ? ApartmentInteriors.GetName(flat.Interior) : "" },
            };
        }

        /// <summary>Этажность дома: у подъезда из DLC — все его этажи (как в лифте), иначе — по верхней квартире.</summary>
        private static int GetFloors(ApartmentBuilding building, List<(ApartmentFlat flat, House house)> flats)
        {
            var hall = ApartmentHalls.Get(building.Hall);
            var top = flats.Count > 0 ? flats.Max(f => f.flat.Floor) : 0;
            return hall != null ? Math.Max(hall.TopFloorNumber, top) : top;
        }

        private static IEnumerable<(ApartmentFlat flat, House house)> GetFlats(ApartmentBuilding building)
        {
            foreach (var flat in building.Flats)
            {
                var house = HouseManager.Houses.FirstOrDefault(h => h.ID == flat.HouseId);
                if (house != null)
                    yield return (flat, house);
            }
        }

        /// <summary>
        /// Список домов для вкладки «Многоквартирные дома» в риэлторском агентстве.
        /// </summary>
        public static string GetRieltagencyData(ExtPlayer player)
        {
            var list = new List<object>();
            foreach (var building in Buildings.Values.OrderBy(b => b.Id))
            {
                var flats = GetFlats(building).ToList();
                if (flats.Count == 0)
                    continue;
                list.Add(new Dictionary<string, object>
                {
                    { "id", building.Id },
                    { "name", building.Name },
                    { "address", building.Address },
                    { "floors", GetFloors(building, flats) },
                    { "firstFloor", ApartmentHalls.Get(building.Hall)?.FirstFloorNumber ?? 2 },
                    { "flats", flats.Select(f => GetFlatData(f.flat, f.house, player)).ToList() },
                });
            }
            return JsonConvert.SerializeObject(list);
        }
        #endregion

        #region Entrance
        private static ApartmentBuilding GetNearBuilding(ExtPlayer player, int buildingId)
        {
            if (!Buildings.TryGetValue(buildingId, out var building))
                return null;
            if (player.Dimension != 0 || player.Position.DistanceTo(building.Entrance) > InteractDistance)
                return null;
            return building;
        }

        [Interaction(ColShapeEnums.ApartmentEntrance)]
        public static void OnEntrance(ExtPlayer player, int index)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null || !player.IsCharacterData() || player.IsInVehicle)
                    return;
                if (sessionData.CuffedData.Cuffed || sessionData.DeathData.InDeath)
                    return;

                OpenMenu(player, index);
            }
            catch (Exception e)
            {
                Log.Write($"OnEntrance Exception: {e}");
            }
        }

        public static void OpenMenu(ExtPlayer player, int buildingId)
        {
            var building = GetNearBuilding(player, buildingId);
            if (building == null)
                return;

            var data = new Dictionary<string, object>
            {
                { "id", building.Id },
                { "name", building.Name },
                { "address", building.Address },
                { "flats", GetFlats(building).Select(f => GetFlatData(f.flat, f.house, player)).ToList() },
                { "hall", ApartmentHalls.Get(building.Hall)?.Title ?? "" },
                { "floors", GetFloors(building, GetFlats(building).ToList()) },
                { "firstFloor", ApartmentHalls.Get(building.Hall)?.FirstFloorNumber ?? 2 },
            };
            Trigger.ClientEvent(player, "client.apartments.open", JsonConvert.SerializeObject(data));
        }

        [RemoteEvent("server.apartments.action")]
        public static void OnAction(ExtPlayer player, int buildingId, int houseId, string action)
        {
            try
            {
                var sessionData = player.GetSessionData();
                var characterData = player.GetCharacterData();
                if (sessionData == null || characterData == null)
                    return;

                var building = GetNearBuilding(player, buildingId);
                if (building == null)
                    return;

                if (action == "hall")
                {
                    Trigger.ClientEvent(player, "client.apartments.close");
                    EnterHall(player, building);
                    return;
                }

                var house = HouseManager.Houses.FirstOrDefault(h => h.ID == houseId && h.ApartmentId == building.Id);
                if (house == null)
                    return;

                switch (action)
                {
                    case "buy":
                        Trigger.ClientEvent(player, "client.apartments.close");
                        HouseManager.TryBuyHouse(player, house, true);
                        return;
                    case "view":
                        if (!string.IsNullOrEmpty(house.Owner))
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "У этой квартиры уже есть хозяин", 3000);
                            return;
                        }
                        Trigger.ClientEvent(player, "client.apartments.close");
                        house.SendPlayer(player);
                        return;
                    case "enter":
                        if (string.IsNullOrEmpty(house.Owner))
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Квартира продаётся — нажмите «Осмотреть»", 3000);
                            return;
                        }
                        if (sessionData.Following != null || sessionData.Follower != null)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Сначала отпустите человека", 3000);
                            return;
                        }
                        var playerHouse = HouseManager.GetHouse(player);
                        var canEnter = !house.Locked
                            || (playerHouse != null && playerHouse.ID == house.ID)
                            || sessionData.HouseData.InvitedHouseID == house.ID
                            || characterData.AdminLVL >= 5;
                        if (!canEnter)
                        {
                            Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Дверь закрыта. Позвоните хозяину или попросите приглашение", 3000);
                            return;
                        }
                        Trigger.ClientEvent(player, "client.apartments.close");
                        house.SendPlayer(player);
                        return;
                }
            }
            catch (Exception e)
            {
                Log.Write($"OnAction Exception: {e}");
            }
        }

        [Interaction(ColShapeEnums.ApartmentGarage)]
        public static void OnGarage(ExtPlayer player, int index)
        {
            try
            {
                if (!player.IsCharacterData())
                    return;

                var house = HouseManager.GetHouse(player);
                if (house == null || house.ApartmentId != index)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Гараж только для жильцов этого дома", 3000);
                    return;
                }
                GarageManager.OnEnterGarage(player, house.GarageID);
            }
            catch (Exception e)
            {
                Log.Write($"OnGarage Exception: {e}");
            }
        }
        #endregion

        #region Подъезды (ApartmentHalls)
        private static void AutoAssignHall(ApartmentBuilding building)
        {
            if (!UseDlcInteriors || !_hasHallColumn || !string.IsNullOrEmpty(building.Hall))
                return;

            var flats = Math.Max(building.Flats.Count, building.Plan.Sum(p => p.Count));
            var premium = building.Plan.Any(p => p.Type >= 5);
            building.Hall = ApartmentHalls.Suggest(flats, premium);
            var hall = ApartmentHalls.Get(building.Hall);
            if (hall != null && flats > hall.Capacity)
                building.Hall = "med";
            SaveBuilding(building);
        }

        private static void CreateHall(ApartmentBuilding building)
        {
            var hall = ApartmentHalls.Get(building.Hall);
            if (hall == null || !UseDlcInteriors)
                return;

            var dim = ApartmentHalls.Dimension(building);

            // Выход на улицу у точки появления
            var spawn = hall.World(hall.Spawn);
            building.HallShapes.Add(CustomColShape.CreateCylinderColShape(spawn, 1.2f, 2.5f, dim, ColShapeEnums.ApartmentHallExit, building.Id));
            building.WorldEntities.Add(NAPI.Marker.CreateMarker(1, spawn - new Vector3(0, 0, 1.1), new Vector3(), new Vector3(), 1f, new Color(80, 170, 255, 140), false, dim));
            building.WorldEntities.Add(NAPI.TextLabel.CreateTextLabel(Main.StringToU16($"~b~{building.Name}\n~w~Выход на улицу"), spawn + new Vector3(0, 0, 0.8), 6f, 0.4f, 0, new Color(255, 255, 255), true, dim));

            // Лифт на каждом этаже
            if (hall.Elevator != null)
            {
                for (var floor = 0; floor < hall.Floors; floor++)
                {
                    var lift = hall.World(hall.Elevator, floor);
                    building.HallShapes.Add(CustomColShape.CreateCylinderColShape(lift, 1.2f, 2.5f, dim, ColShapeEnums.ApartmentElevator, building.Id));
                    building.WorldEntities.Add(NAPI.Marker.CreateMarker(1, lift - new Vector3(0, 0, 1.1), new Vector3(), new Vector3(), 0.8f, new Color(255, 200, 80, 140), false, dim));
                    building.WorldEntities.Add(NAPI.TextLabel.CreateTextLabel(Main.StringToU16($"~y~Лифт\n~w~Этаж {hall.FirstFloorNumber + floor}"), lift + new Vector3(0, 0, 0.8), 5f, 0.4f, 0, new Color(255, 255, 255), true, dim));
                }
            }

            foreach (var (flat, house) in GetFlats(building).ToList())
                AddHallDoor(building, hall, flat, house);
        }

        /// <summary>
        /// Дверь квартиры в коридоре: обычный колшейп дома (EnterHouse) в измерении подъезда —
        /// покупка, осмотр, замок, приглашения и взлом работают как у домов. Выход из квартиры — сюда же.
        /// </summary>
        private static void AddHallDoor(ApartmentBuilding building, ApartmentHall hall, ApartmentFlat flat, House house)
        {
            if (flat.Number < 1 || flat.Number > hall.Capacity)
                return;

            var dim = ApartmentHalls.Dimension(building);
            var (floorIndex, door) = hall.Slot(flat.Number);
            var point = hall.World(hall.Doors[door], floorIndex);
            flat.Floor = hall.FirstFloorNumber + floorIndex;

            house.SetExit(point, dim);
            building.HallShapes.Add(CustomColShape.CreateCylinderColShape(point - new Vector3(0, 0, 1.1), 1.1f, 2.5f, dim, ColShapeEnums.EnterHouse, house.ID));
            building.WorldEntities.Add(NAPI.Marker.CreateMarker(1, point - new Vector3(0, 0, 1.15), new Vector3(), new Vector3(), 0.7f, new Color(255, 255, 255, 110), false, dim));
            building.WorldEntities.Add(NAPI.TextLabel.CreateTextLabel(Main.StringToU16($"~w~Кв. {flat.Number}"), point + new Vector3(0, 0, 0.9), 4f, 0.45f, 0, new Color(255, 255, 255), true, dim));
        }

        private static void EnterHall(ExtPlayer player, ApartmentBuilding building)
        {
            var hall = ApartmentHalls.Get(building.Hall);
            var characterData = player.GetCharacterData();
            var sessionData = player.GetSessionData();
            if (hall == null || characterData == null || sessionData == null) return;
            if (sessionData.Following != null || sessionData.Follower != null)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Сначала отпустите человека", 3000);
                return;
            }

            characterData.ExteriorPos = building.Entrance; // выход из игры в подъезде — к дому
            Trigger.Dimension(player, ApartmentHalls.Dimension(building));
            player.Position = hall.World(hall.Spawn) + new Vector3(0, 0, 0.2);
        }

        private static ApartmentBuilding GetHallBuilding(ExtPlayer player, int buildingId)
        {
            if (!Buildings.TryGetValue(buildingId, out var building)) return null;
            return player.Dimension == ApartmentHalls.Dimension(building) ? building : null;
        }

        [Interaction(ColShapeEnums.ApartmentHallExit)]
        public static void OnHallExit(ExtPlayer player, int index)
        {
            var building = GetHallBuilding(player, index);
            var characterData = player.GetCharacterData();
            if (building == null || characterData == null || player.IsInVehicle) return;

            characterData.ExteriorPos = new Vector3();
            Trigger.Dimension(player);
            player.Position = building.Entrance + new Vector3(0, 0, 1.12);
        }

        [Interaction(ColShapeEnums.ApartmentElevator)]
        public static void OnElevator(ExtPlayer player, int index)
        {
            var building = GetHallBuilding(player, index);
            var hall = ApartmentHalls.Get(building?.Hall);
            if (building == null || hall == null) return;

            var frameList = new FrameListData
            {
                Header = $"Лифт · {building.Name}",
                Callback = (p, item) =>
                {
                    if (item == null || !int.TryParse(item.ToString(), out var floor)) return;
                    if (GetHallBuilding(p, index) == null || floor < 0 || floor >= hall.Floors) return;
                    p.Position = hall.World(hall.Elevator, floor) + new Vector3(0, 0, 0.2);
                },
            };
            var current = (int)Math.Round((player.Position.Z - hall.World(hall.Elevator).Z) / hall.FloorHeight);
            for (var floor = 0; floor < hall.Floors; floor++)
            {
                var mine = GetFlats(building).Any(f => f.flat.Floor == hall.FirstFloorNumber + floor
                    && (f.house.Owner == player.Name || f.house.Roommates.ContainsKey(player.Name)));
                var text = $"Этаж {hall.FirstFloorNumber + floor}" + (floor == current ? " (вы здесь)" : "") + (mine ? " — ваша квартира" : "");
                frameList.List.Add(new ListData(text, floor));
            }
            Players.Popup.List.Repository.Open(player, frameList);
        }
        #endregion

        #region Realtor
        public static void BuyFromRieltagency(ExtPlayer player, int houseId)
        {
            var house = HouseManager.Houses.FirstOrDefault(h => h.ID == houseId && h.ApartmentId != -1);
            if (house == null || !Buildings.TryGetValue(house.ApartmentId, out var building))
                return;

            if (HouseManager.TryBuyHouse(player, house, false))
            {
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Квартира куплена: {building.Name}. Метка на карте", 5000);
                Trigger.ClientEvent(player, "client.rieltagency.addBlip", 475, 3, house.ID, building.Entrance.X, building.Entrance.Y, building.Name);
            }
        }
        #endregion

        #region Admin
        private static bool IsAdmin(ExtPlayer player)
        {
            var characterData = player.GetCharacterData();
            return characterData != null && characterData.AdminLVL >= 8;
        }

        private static void SaveBuilding(ApartmentBuilding building, bool insert = false)
        {
            using var cmd = new MySqlCommand
            {
                CommandText = insert
                    ? "INSERT INTO `apartment_buildings` (`id`, `name`, `address`, `entrance`, `garage`, `garage_heading`, `plan`) VALUES (@id, @name, @address, @entrance, @garage, @heading, @plan)"
                    : _hasHallColumn
                        ? "UPDATE `apartment_buildings` SET `name`=@name, `address`=@address, `entrance`=@entrance, `garage`=@garage, `garage_heading`=@heading, `plan`=@plan, `hall`=@hall WHERE `id`=@id"
                        : "UPDATE `apartment_buildings` SET `name`=@name, `address`=@address, `entrance`=@entrance, `garage`=@garage, `garage_heading`=@heading, `plan`=@plan WHERE `id`=@id"
            };
            cmd.Parameters.AddWithValue("@id", building.Id);
            cmd.Parameters.AddWithValue("@name", building.Name);
            cmd.Parameters.AddWithValue("@address", building.Address ?? "");
            cmd.Parameters.AddWithValue("@entrance", JsonConvert.SerializeObject(building.Entrance));
            cmd.Parameters.AddWithValue("@garage", JsonConvert.SerializeObject(building.GaragePos));
            cmd.Parameters.AddWithValue("@heading", building.GarageHeading);
            cmd.Parameters.AddWithValue("@plan", JsonConvert.SerializeObject(building.Plan));
            cmd.Parameters.AddWithValue("@hall", building.Hall ?? "");
            MySQL.Query(cmd);
        }

        [Command("apartlist")]
        public static void CMD_List(ExtPlayer player)
        {
            if (!IsAdmin(player)) return;
            if (!_tablesReady)
            {
                NAPI.Chat.SendChatMessageToPlayer(player, "Таблицы квартир не созданы: выполните database/systems/apartments.sql и перезапустите сервер");
                return;
            }
            foreach (var building in Buildings.Values)
            {
                var free = GetFlats(building).Count(f => string.IsNullOrEmpty(f.house.Owner));
                NAPI.Chat.SendChatMessageToPlayer(player, $"[{building.Id}] {building.Name} — квартир {building.Flats.Count}, свободно {free}");
            }
        }

        /// <summary>/apartcreate Название — новый дом у ваших ног (дальше /apartgarage и /apartaddflats).</summary>
        [Command("apartcreate", GreedyArg = true)]
        public static void CMD_Create(ExtPlayer player, string name)
        {
            if (!IsAdmin(player) || !_tablesReady) return;
            var id = Buildings.Count == 0 ? 1 : Buildings.Keys.Max() + 1;
            var building = new ApartmentBuilding
            {
                Id = id,
                Name = name,
                Address = "",
                Entrance = player.Position - new Vector3(0, 0, 1.0),
                GaragePos = new Vector3(),
            };
            Buildings[id] = building;
            SaveBuilding(building, true);
            CreateWorld(building);
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Создан многоквартирный дом [{id}] {name}. Теперь /apartgarage {id} (в машине) и /apartaddflats", 6000);
        }

        /// <summary>/apartentrance id — перенести вход к вашей позиции.</summary>
        [Command("apartentrance")]
        public static void CMD_Entrance(ExtPlayer player, int id)
        {
            if (!IsAdmin(player) || !Buildings.TryGetValue(id, out var building)) return;
            building.Entrance = player.Position - new Vector3(0, 0, 1.0);
            foreach (var (_, house) in GetFlats(building).ToList())
                house.AttachToApartment(building.Id, building.Entrance);
            SaveBuilding(building);
            CreateWorld(building);
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, "Вход перенесён (метка на карте обновится после рестарта)", 4000);
        }

        /// <summary>/apartgarage id — сидя в машине: въезд в гараж и направление выезда.</summary>
        [Command("apartgarage")]
        public static void CMD_Garage(ExtPlayer player, int id)
        {
            if (!IsAdmin(player) || !Buildings.TryGetValue(id, out var building)) return;
            if (!player.IsInVehicle)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Сядьте в машину и поставьте её так, как она должна выезжать из гаража", 4000);
                return;
            }
            building.GaragePos = player.Vehicle.Position;
            building.GarageHeading = player.Vehicle.Rotation.Z;
            foreach (var (_, house) in GetFlats(building).ToList())
                house.GetGarageData()?.AttachToApartment(building.GaragePos, building.GarageHeading);
            SaveBuilding(building);
            CreateWorld(building);
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, "Въезд в гараж сохранён", 3000);
        }

        /// <summary>/aparthall id тип — подъезд дома: elit, med, bich1..bich5, none (только меню).</summary>
        [Command("aparthall")]
        public static void CMD_Hall(ExtPlayer player, int id, string type)
        {
            if (!IsAdmin(player) || !Buildings.TryGetValue(id, out var building)) return;
            if (type != "none" && ApartmentHalls.Get(type) == null)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Типы: elit, med, bich1..bich5, none", 4000);
                return;
            }
            building.Hall = type == "none" ? "" : type;
            foreach (var (_, house) in GetFlats(building).ToList())
                house.SetExit(null, 0);
            SaveBuilding(building);
            CreateWorld(building);
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Подъезд: {ApartmentHalls.Get(building.Hall)?.Title ?? "нет"}", 3000);
        }

        /// <summary>/apartaddflats id класс цена типГаража количество — добавить квартиры в планировку и сразу создать их.</summary>
        [Command("apartaddflats")]
        public static void CMD_AddFlats(ExtPlayer player, int id, int type, int price, int garageType, int count)
        {
            if (!IsAdmin(player) || !Buildings.TryGetValue(id, out var building)) return;
            if (type < 0 || type >= HouseManager.HouseTypeList.Count || type == 7 || !GarageManager.GarageTypes.ContainsKey(garageType) || count < 1 || count > 50 || price < 1)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Неверные параметры: класс 0-9 кроме 7, гараж 0-9, количество 1-50", 4000);
                return;
            }
            var entry = building.Plan.FirstOrDefault(p => p.Type == type);
            if (entry == null)
                building.Plan.Add(new ApartmentPlanEntry { Type = type, Price = price, Garage = garageType, Count = count });
            else
            {
                entry.Price = price;
                entry.Garage = garageType;
                entry.Count += count;
            }
            SaveBuilding(building);
            GenerateMissingFlats(building);
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Добавлено квартир: {count}", 3000);
        }
        #endregion
    }
}
