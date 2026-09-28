using System;
using System.Collections.Generic;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.BlackMarket.Crypto;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Organizations.Player;
using NeptuneEvo.Players;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Table.Tasks.Models;
using NeptuneEvo.Table.Tasks.Player;
using NeptuneEvo.VehicleData.LocalData;
using NeptuneEvo.VehicleData.LocalData.Models;
using Redage.SDK;

namespace NeptuneEvo.Crime.CarTheft
{
    /// <summary>
    /// Миссия банды «Угон» (меню банды, пункт 0): машина стоит на случайной уличной точке (точки закладок Чёрного рынка),
    /// при посадке с шансом срабатывает сигнализация (звёзды розыска), машину гонят к Мавру и разбирают —
    /// игрок получает «Детали угнанного авто», которые сдаёт Мавру в «Скупке краденого».
    /// </summary>
    public class CarTheftManager : Script
    {
        private const int CooldownMinutes = 5;
        private const int AlarmChance = 30;
        private const int ChopSeconds = 10;
        private const float ChopRadius = 20f;

        private class Theft
        {
            public int Fraction;
            public string Crew;
            public int PlayerUuid;
            public bool Entered;
            public bool Chopping;
            public int Parts;
            /// <summary>Дорогая машина — электронный замок, только программатор.</summary>
            public bool Expensive;
            /// <summary>Эксклюзив (кастомные элитные модели): программатор 7×7, розыск 90%, бонус Мавра.</summary>
            public bool Super;
            public string Name;
            public bool Unlocked;
            public bool Busy;
        }

        private static readonly Dictionary<ExtVehicle, Theft> Thefts = new Dictionary<ExtVehicle, Theft>();
        private static readonly Dictionary<string, DateTime> NextTheft = new Dictionary<string, DateTime>();

        /// <summary>«Команда» угонщика: банда/мафия/байкеры, иначе криминальная организация, иначе сам игрок.</summary>
        private static string CrewKey(ExtPlayer player)
        {
            var fracId = player.GetFractionId();
            if (fracId > 0)
                return $"f{fracId}";
            var orgId = player.GetOrganizationMemberData()?.Id ?? 0;
            return orgId > 0 ? $"o{orgId}" : $"p{player.GetUUID()}";
        }

        /// <summary>Минут до следующего заказа для команды игрока (0 — можно брать).</summary>
        public static int CooldownLeft(ExtPlayer player) =>
            NextTheft.TryGetValue(CrewKey(player), out var next) && next > DateTime.Now ? (int)Math.Ceiling((next - DateTime.Now).TotalMinutes) : 0;

        /// <summary>Программатор у Мавра.</summary>
        public const int ProgrammerPrice = 3000;

        private enum Tier { Cheap, Expensive, Super }

        /// <summary>Шанс эксклюзивного и дорогого заказа, %. Остальное — дешёвые машины.</summary>
        private const int SuperChance = 10;
        private const int ExpensiveChance = 30;
        /// <summary>Эксклюзив: шанс розыска при угоне и наличный бонус Мавра при разборке.</summary>
        private const int SuperWantedChance = 90;
        public const int SuperBonus = 12000;

        /// <summary>
        /// Дешёвые — обычный замок (отмычка), дорогие — электронный (программатор),
        /// эксклюзив — кастомные элитные модели (лежат на сервере), программатор 7×7, деталей в разы больше.
        /// </summary>
        private static readonly (string model, string name, int parts, Tier tier)[] Cars =
        {
            ("primo", "Albany Primo", 2, Tier.Cheap), ("tailgater", "Obey Tailgater", 3, Tier.Cheap), ("oracle", "Ubermacht Oracle", 3, Tier.Cheap),
            ("felon", "Lampadati Felon", 3, Tier.Cheap), ("jackal", "Ocelot Jackal", 3, Tier.Cheap), ("buffalo", "Bravado Buffalo", 3, Tier.Cheap),
            ("fugitive", "Cheval Fugitive", 2, Tier.Cheap), ("washington", "Albany Washington", 2, Tier.Cheap), ("sultan", "Karin Sultan", 3, Tier.Cheap),
            ("zion", "Ubermacht Zion", 3, Tier.Cheap),
            ("schafter2", "Benefactor Schafter", 5, Tier.Expensive), ("exemplar", "Dewbauchee Exemplar", 5, Tier.Expensive),
            ("f620", "Ocelot F620", 6, Tier.Expensive), ("cogcabrio", "Enus Cognoscenti Cabrio", 6, Tier.Expensive),
            ("cognoscenti", "Enus Cognoscenti", 5, Tier.Expensive), ("sentinel", "Ubermacht Sentinel", 5, Tier.Expensive),
            ("comet2", "Pfister Comet", 6, Tier.Expensive), ("feltzer2", "Benefactor Feltzer", 6, Tier.Expensive),
            ("carbonizzare", "Grotti Carbonizzare", 6, Tier.Expensive),
            ("lx570", "Lexus LX570", 14, Tier.Super), ("g636x6", "Mercedes G63 6x6", 16, Tier.Super),
            ("mb63gls", "Mercedes GLS 63", 15, Tier.Super), ("bmwx6", "BMW X6M", 15, Tier.Super),
        };

        private static Vector3 ChopPoint => BlackMarket.Config.BlackMarketConfig.Current.ChopPoint ?? BlackMarket.Config.BlackMarketConfig.Current.CashoutPoint;

        /// <summary>Из Main после BlackMarketManager.Init (нужна точка Мавра из конфига).</summary>
        public static void Init()
        {
            try
            {
                CustomColShape.CreateCylinderColShape(ChopPoint - new Vector3(0, 0, 2), ChopRadius, 6, 0, ColShapeEnums.ChopShop);
                NAPI.TextLabel.CreateTextLabel(Main.StringToU16("~o~Разборка~w~\nугнанная машина — [E] за рулём"), ChopPoint + new Vector3(0, 0, 2.2), 15f, 0.35f, 4, new Color(255, 255, 255), false, 0);
            }
            catch (Exception e)
            {
                CrimeCore.Log.Write($"CarTheft Init Exception: {e}");
            }
        }

        public static bool IsTheft(ExtVehicle vehicle) => vehicle != null && Thefts.ContainsKey(vehicle);

        /// <summary>Взять заказ на угон (вызывается из меню банды).</summary>
        public static void Start(ExtPlayer player, int fracId)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null)
                return;
            if (!CrimeCore.IsCriminal(player))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "— Я с тобой не работаю.", 3000);
                return;
            }
            var crew = CrewKey(player);
            if (NextTheft.TryGetValue(crew, out var next) && DateTime.Now < next)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Следующий заказ через {Math.Ceiling((next - DateTime.Now).TotalMinutes)} мин", 3000);
                return;
            }
            if (sessionData.DeliveryData.Vehicle != null)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Сначала закончите с предыдущей машиной", 3000);
                return;
            }
            var points = BlackMarket.Config.BlackMarketConfig.Current.DropPoints
                .Where(p => p.DistanceTo(ChopPoint) > 900)
                .ToList();
            if (points.Count == 0)
                points = BlackMarket.Config.BlackMarketConfig.Current.DropPoints.ToList();
            if (points.Count == 0)
                return;
            var point = points[CrimeCore.Rnd.Next(points.Count)];
            var roll = CrimeCore.Rnd.Next(100);
            var tier = roll < SuperChance ? Tier.Super : roll < SuperChance + ExpensiveChance ? Tier.Expensive : Tier.Cheap;
            var pool = Cars.Where(c => c.tier == tier).ToArray();
            var car = pool.Length > 0 ? pool[CrimeCore.Rnd.Next(pool.Length)] : Cars[CrimeCore.Rnd.Next(Cars.Length)];
            var expensive = car.tier != Tier.Cheap;
            var super = car.tier == Tier.Super;
            var plate = $"{(char)('A' + CrimeCore.Rnd.Next(26))}{CrimeCore.Rnd.Next(100, 999)}{(char)('A' + CrimeCore.Rnd.Next(26))}{(char)('A' + CrimeCore.Rnd.Next(26))}";
            var vehicle = (ExtVehicle)VehicleStreaming.CreateVehicle(NAPI.Util.GetHashKey(car.model), point + new Vector3(0, 0, 0.3), CrimeCore.Rnd.Next(0, 360),
                CrimeCore.Rnd.Next(0, 100), CrimeCore.Rnd.Next(0, 100), plate, locked: true, acc: VehicleAccess.DeliveryGang, petrol: 60);
            VehicleStreaming.SetLockStatus(vehicle, true);
            VehicleStreaming.SetEngineState(vehicle, false);
            var vehicleLocalData = vehicle.GetVehicleLocalData();
            if (vehicleLocalData != null)
            {
                vehicleLocalData.DeliveryData.End = 0;
                vehicleLocalData.DeliveryData.Fraction = fracId;
                vehicleLocalData.DeliveryData.JStage = false;
                vehicleLocalData.DeliveryData.WhosVeh = player;
            }
            Thefts[vehicle] = new Theft { Fraction = fracId, Crew = crew, PlayerUuid = player.GetUUID(), Parts = car.parts, Expensive = expensive, Super = super, Name = car.name };
            sessionData.DeliveryData.Vehicle = vehicle;
            sessionData.DeliveryData.Point = -1;
            NextTheft[crew] = DateTime.Now.AddMinutes(CooldownMinutes);
            Trigger.ClientEvent(player, "createWaypoint", point.X, point.Y);
            Notify.Send(player, super ? NotifyType.Warning : NotifyType.Info, NotifyPosition.BottomCenter, super
                ? $"ЭКСКЛЮЗИВНЫЙ ЗАКАЗ: {car.name} ({plate}). Только программатор, полиция почти наверняка узнает! Награда в разы выше"
                : $"Заказ: {car.name} ({plate}). Машина отмечена в GPS — угоните её и пригоните к Мавру на разборку", 8000);
            var tool = expensive ? "программатор (продаёт Мавр)" : "отмычка";
            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter,
                $"Нужна {tool}. У машины: G → «Взломать транспорт» (или инвентарь → Использовать)", 9000);
            Trigger.SendChatMessage(player, "!{#f5a524}[Угон]!{#ffffff} " + (super
                ? $"Эксклюзив {car.name}: электронный замок повышенной защиты — только программатор. При угоне 90% розыск. Бонус от Мавра ${SuperBonus} + много деталей."
                : expensive
                    ? $"{car.name}: электронный замок — нужен программатор (продаёт Мавр)."
                    : $"{car.name}: обычный замок — хватит отмычки.") + " Подойдите к двери: G → «Взломать транспорт».");
            BlackMarket.Audit.AuditLog.Write("theft_start", player.GetUUID(), details: new { fraction = fracId, car = car.model, tier = car.tier.ToString(), x = point.X, y = point.Y });
        }

        /// <summary>
        /// «Использовать» отмычку или программатор из инвентаря (хук в Chars.Repository.ItemsUse).
        /// true — предмет обработан здесь (рядом машина заказа); false — отмычку можно использовать как обычно.
        /// </summary>
        public static bool OnUseTool(ExtPlayer player, ItemId itemId)
        {
            var programmer = itemId == ItemId.CarProgrammer;
            if (player.IsInVehicle)
            {
                if (programmer)
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Выйдите из машины", 3000);
                return programmer;
            }
            var found = Thefts
                .Where(t => t.Key != null && t.Key.Exists && t.Key.Dimension == player.Dimension && t.Key.Position.DistanceTo(player.Position) < 3.5f)
                .OrderBy(t => t.Key.Position.DistanceTo(player.Position))
                .FirstOrDefault();
            var vehicle = found.Key;
            if (vehicle == null)
            {
                if (programmer)
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Подойдите вплотную к машине из заказа на угон", 3000);
                return programmer;
            }
            StartBreak(player, vehicle, found.Value, programmer);
            return true;
        }

        /// <summary>
        /// G → «Взломать транспорт» у машины заказа (перехват в Core/Selecting.cs vehicleSelected case 6):
        /// прибор выбирается сам — отмычка для обычного замка, программатор для электронного.
        /// </summary>
        public static void BreakIn(ExtPlayer player, ExtVehicle vehicle)
        {
            if (!Thefts.TryGetValue(vehicle, out var theft) || player.IsInVehicle)
                return;
            if (player.Position.DistanceTo(vehicle.Position) > 3.5f)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Подойдите ближе к двери", 3000);
                return;
            }
            var location = $"char_{player.GetUUID()}";
            var hasProgrammer = Chars.Repository.getCountItem(location, ItemId.CarProgrammer, false) > 0;
            var hasPick = Chars.Repository.getCountItem(location, ItemId.Lockpick, false) > 0;
            if (theft.Expensive && !hasProgrammer)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Электронный замок — нужен программатор (продаёт Мавр)", 4000);
                return;
            }
            if (!theft.Expensive && !hasPick && !hasProgrammer)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Нужна отмычка (продаёт Мавр)", 3500);
                return;
            }
            StartBreak(player, vehicle, theft, theft.Expensive || !hasPick);
        }

        /// <summary>Общий запуск взлома: отмычка (LockBreak) или программатор (CyberHack).</summary>
        private static void StartBreak(ExtPlayer player, ExtVehicle vehicle, Theft theft, bool programmer)
        {
            var crew = CrewKey(player);
            if (theft.Crew != crew)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Это чужой заказ", 3000);
                return;
            }
            if (theft.Unlocked || !VehicleStreaming.GetLockState(vehicle))
            {
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Машина уже открыта — садитесь", 3000);
                return;
            }
            if (theft.Busy || LockBreak.IsBusy(player) || CyberHack.IsBusy(player))
                return;
            if (theft.Expensive && !programmer)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Электронный замок — отмычка не поможет. Нужен программатор (Мавр)", 4000);
                return;
            }

            theft.Busy = true;
            if (programmer)
            {
                var size = theft.Super ? 7 : vehicle.Model == NAPI.Util.GetHashKey("comet2") || vehicle.Model == NAPI.Util.GetHashKey("carbonizzare") ? 6 : 5;
                if (!CyberHack.Start(player, size, theft.Super ? 30 : 35, theft.Super ? "Эксклюзивный замок" : "Электронный замок",
                    p => { theft.Busy = false; Unlock(p, vehicle, theft); },
                    (p, reason) =>
                    {
                        theft.Busy = false;
                        if (reason == "cancel")
                            return;
                        // Провал: программатор сгорает, срабатывает сигнализация
                        if (Chars.Repository.getCountItem($"char_{p.GetUUID()}", ItemId.CarProgrammer, false) > 0)
                            Chars.Repository.Remove(p, $"char_{p.GetUUID()}", "inventory", ItemId.CarProgrammer, 1);
                        Notify.Send(p, NotifyType.Error, NotifyPosition.BottomCenter, "Защита сработала — программатор сгорел, включилась сигнализация!", 5000);
                        if (vehicle.Exists)
                        {
                            Trigger.ClientEventInRange(vehicle.Position, 80f, "client.crime.alarm", vehicle);
                            CrimeCore.CallPolice(p, vehicle.Position, $"theft_{vehicle.Value}", "Сработала автосигнализация дорогого автомобиля — попытка угона", 2, "Угон автомобиля");
                        }
                    }))
                    theft.Busy = false;
                return;
            }

            if (!LockBreak.Start(player, "car", vehicle.Value, 7, "Взлом замка машины",
                p => { theft.Busy = false; Unlock(p, vehicle, theft); },
                (p, reason) => theft.Busy = false))
                theft.Busy = false;
            return;
        }

        private static void Unlock(ExtPlayer player, ExtVehicle vehicle, Theft theft)
        {
            if (vehicle == null || !vehicle.Exists || !Thefts.ContainsKey(vehicle))
                return;
            theft.Unlocked = true;
            VehicleStreaming.SetLockStatus(vehicle, false);
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, "Замок открыт! Садитесь за руль — двигатель заведёте, замкнув провода", 4000);
            Commands.RPChat("sme", player, "вскрывает дверь автомобиля");
        }

        /// <summary>Программатор у Мавра (пункт 505).</summary>
        public static void BuyProgrammer(ExtPlayer player)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null)
                return;
            if (!CrimeCore.IsCriminal(player))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "— Такие игрушки только для своих.", 3000);
                return;
            }
            if (characterData.Money < ProgrammerPrice)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Недостаточно денег", 3000);
                return;
            }
            if (Chars.Repository.isFreeSlots(player, ItemId.CarProgrammer) != 0)
                return;
            if (Chars.Repository.AddNewItem(player, $"char_{characterData.UUID}", "inventory", ItemId.CarProgrammer, 1) == -1)
                return;
            MoneySystem.Wallet.Change(player, -ProgrammerPrice);
            GameLog.Money($"player({characterData.UUID})", "server", ProgrammerPrice, "buyMavr(programmer)");
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, "Программатор куплен. Нужен для дорогих машин из заказов на угон; при провале взлома сгорает", 5000);
        }

        /// <summary>Посадка в машину угона (из CrimeMissions.Event_PlayerEnterVehicle). true — обработано здесь.</summary>
        public static bool OnEnter(ExtPlayer player, ExtVehicle vehicle)
        {
            if (!Thefts.TryGetValue(vehicle, out var theft))
                return false;
            if (CrimeCore.IsPolice(player))
            {
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Машина в угоне. Отвезите её в участок", 3000);
                return true;
            }
            if (CrewKey(player) != theft.Crew)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Это не ваш заказ", 3000);
                VehicleManager.WarpPlayerOutOfVehicle(player);
                return true;
            }
            Trigger.ClientEvent(player, "createWaypoint", ChopPoint.X, ChopPoint.Y);
            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Гоните машину к Мавру: встаньте рядом и нажмите E — разборка", 4000);
            if (!theft.Entered && player.VehicleSeat == (int)VehicleSeat.Driver)
            {
                theft.Entered = true;
                VehicleStreaming.SetEngineState(vehicle, true);
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, "Вы замкнули провода — двигатель заведён", 3000);
                // Эксклюзив: владельцы таких машин следят за ними — почти всегда розыск
                if (theft.Super && CrimeCore.Roll(SuperWantedChance))
                {
                    Trigger.ClientEventInRange(vehicle.Position, 80f, "client.crime.alarm", vehicle);
                    CrimeCore.CallPolice(player, vehicle.Position, $"theft_{vehicle.Value}", $"Угнан элитный автомобиль {theft.Name} — трекер передаёт координаты", 3, "Угон элитного автомобиля");
                }
                // Программатор отключает сигнализацию; у дешёвых машин она может сработать
                else if (!theft.Expensive && CrimeCore.Roll(AlarmChance))
                {
                    Trigger.ClientEvent(player, "client.crime.alarm", vehicle);
                    CrimeCore.CallPolice(player, vehicle.Position, $"theft_{vehicle.Value}", "Сработала автосигнализация — угон автомобиля", 2, "Угон автомобиля");
                }
            }
            return true;
        }

        [Interaction(ColShapeEnums.ChopShop)]
        public static void OnChop(ExtPlayer player)
        {
            try
            {
                var characterData = player.GetCharacterData();
                var sessionData = player.GetSessionData();
                if (characterData == null || sessionData == null)
                    return;
                if (!player.IsInVehicle)
                {
                    // Зона разборки накрывает Мавра — пешком E работает как обычное меню Мавра
                    if (CashOut.AtPoint(player) && player.Position.DistanceTo(BlackMarket.Config.BlackMarketConfig.Current.CashoutPoint) <= 3f)
                        SafeMain.OnBlackMarket(player);
                    return;
                }
                var vehicle = (ExtVehicle)player.Vehicle;
                if (!Thefts.TryGetValue(vehicle, out var theft))
                {
                    var local = vehicle.GetVehicleLocalData();
                    if (local != null && local.Access == VehicleAccess.DeliveryGang)
                        Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "— Эту тачку заказчик ждёт целой. Вези по адресу.", 3500);
                    else
                        Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "— Разбираю только то, что заказал.", 3000);
                    return;
                }
                if (player.VehicleSeat != (int)VehicleSeat.Driver || CrewKey(player) != theft.Crew || theft.Chopping)
                    return;
                theft.Chopping = true;
                Trigger.ClientEvent(player, "blockMove", true);
                VehicleStreaming.SetEngineState(vehicle, false);
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"Ребята Мавра разбирают машину… ({ChopSeconds} с)", ChopSeconds * 1000);
                NAPI.Task.Run(() => FinishChop(player, vehicle, theft), ChopSeconds * 1000);
            }
            catch (Exception e)
            {
                CrimeCore.Log.Write($"CarTheft OnChop Exception: {e}");
            }
        }

        private static void FinishChop(ExtPlayer player, ExtVehicle vehicle, Theft theft)
        {
            try
            {
                theft.Chopping = false;
                if (player == null || !player.IsCharacterData())
                    return;
                Trigger.ClientEvent(player, "blockMove", false);
                if (!Thefts.ContainsKey(vehicle) || !player.IsInVehicle || player.Vehicle != vehicle || vehicle.Position.DistanceTo(ChopPoint) > ChopRadius + 3)
                    return;
                var characterData = player.GetCharacterData();
                var sessionData = player.GetSessionData();
                var health = Math.Max(0.3, Math.Min(1.0, vehicle.Health / 1000.0));
                var parts = Math.Max(1, (int)Math.Round(theft.Parts * health));
                if (Chars.Repository.isFreeSlots(player, ItemId.StolenCarParts, parts) != 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Освободите место в инвентаре под детали", 3000);
                    return;
                }
                Chars.Repository.AddNewItem(player, $"char_{characterData.UUID}", "inventory", ItemId.StolenCarParts, parts);
                Remove(vehicle);
                var owner = Main.GetPlayerByUUID(theft.PlayerUuid);
                var ownerSession = owner?.GetSessionData();
                if (ownerSession != null && ownerSession.DeliveryData.Vehicle == vehicle)
                    ownerSession.DeliveryData.Vehicle = null;
                if (sessionData.DeliveryData.Vehicle == vehicle)
                    sessionData.DeliveryData.Vehicle = null;
                player.AddTableScore(TableTaskId.Item27);
                var bonus = 0;
                if (theft.Super)
                {
                    // Эксклюзив: Мавр доплачивает наличными сразу (коэффициент — как у остальных криминальных выплат)
                    bonus = CrimeCore.Payout(player, SuperBonus);
                    MoneySystem.Wallet.Change(player, bonus);
                    GameLog.Money("server", $"player({characterData.UUID})", bonus, $"theftSuper({theft.Name})");
                }
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, bonus > 0
                    ? $"Эксклюзив разобран: {parts} дет. и ${bonus} от Мавра{CrimeCore.FundSuffix(player)}. Детали продайте в «Скупке краденого»"
                    : $"Машина разобрана: {parts} дет. Продайте их Мавру в «Скупке краденого»", 6000);
                BlackMarket.Audit.AuditLog.Write("theft_chop", characterData.UUID, itemId: (int)ItemId.StolenCarParts, count: parts,
                    amount: bonus, details: new { fraction = theft.Fraction, car = theft.Name, super = theft.Super });
            }
            catch (Exception e)
            {
                CrimeCore.Log.Write($"CarTheft FinishChop Exception: {e}");
            }
        }

        private static void Remove(ExtVehicle vehicle)
        {
            Thefts.Remove(vehicle);
            VehicleStreaming.DeleteVehicle(vehicle);
        }

        [ServerEvent(Event.VehicleDeath)]
        public void OnVehicleDeath(ExtVehicle vehicle)
        {
            if (vehicle != null && Thefts.ContainsKey(vehicle))
                NAPI.Task.Run(() =>
                {
                    if (Thefts.ContainsKey(vehicle))
                        Remove(vehicle);
                }, 10000);
        }

        /// <summary>Игрок ушёл — его заказ удаляет CrimeMissions.Event_PlayerDisconnected, здесь только чистим учёт.</summary>
        public static void Forget(ExtVehicle vehicle)
        {
            if (vehicle != null)
                Thefts.Remove(vehicle);
        }
    }
}
