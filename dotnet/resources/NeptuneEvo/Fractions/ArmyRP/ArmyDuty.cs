using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using GTANetworkAPI;
using MySqlConnector;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Fractions.Models;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Fractions.ArmyRP
{
    /// <summary>
    /// Наряды на кухню и уборку — через интерфейс (CEF ArmyDuty), без команд для игроков.
    ///  Доска нарядов на базе: E — окно: «Заступить» на кухню/уборку; офицер ещё назначает других (наказание — без премии).
    ///  На точках наряда: E — работа с анимацией несколько секунд, затем следующая точка; в конце — премия и запись в лог.
    ///  Назначение хранится в army_duty_orders и живёт час ОНЛАЙНА игрока (время идёт, только пока он в игре):
    ///  напоминание при входе и каждые 10 мин, блок «Ваш наряд» в окне; не выполнил — офицерам в рацию.
    /// </summary>
    class ArmyDuty : Script
    {
        private static nLog Log => ArmyConfig.Log;
        private static ArmyConfig Cfg => ArmyConfig.Current;

        public const string Kitchen = "kitchen";
        public const string Clean = "clean";
        private const int CleanIndexOffset = 1000;

        private class Order
        {
            public string Type;
            public string Officer;
            public bool Punishment;
            public int SecondsLeft;
            public int Step;
            public DateTime LastReminder = DateTime.Now;
            public bool Working;
        }

        /// <summary>uuid → наряд. Загружается из БД при первом появлении игрока в сети.</summary>
        private static readonly Dictionary<int, Order> Orders = new Dictionary<int, Order>();
        private static readonly HashSet<int> Loaded = new HashSet<int>();
        private static readonly List<ExtColShape> Shapes = new List<ExtColShape>();
        private static readonly List<GTANetworkAPI.TextLabel> Labels = new List<GTANetworkAPI.TextLabel>();
        private static readonly List<GTANetworkAPI.Marker> Markers = new List<GTANetworkAPI.Marker>();
        private static bool _ready;

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            try
            {
                if (ArmyConfig.Current.DutyBoard == null)
                    ArmyConfig.Load();
                using (var create = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS `army_duty_orders` (
                    `uuid` INT NOT NULL,
                    `type` VARCHAR(16) NOT NULL,
                    `officer` VARCHAR(64) NOT NULL DEFAULT '',
                    `punishment` TINYINT(1) NOT NULL DEFAULT 0,
                    `seconds_left` INT NOT NULL DEFAULT 3600,
                    `step` INT NOT NULL DEFAULT 0,
                    PRIMARY KEY (`uuid`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"))
                    MySQL.Query(create);
                _ready = true;
                CreatePoints();
                Timers.Start("army.duty", 10000, Tick, true);
            }
            catch (Exception e)
            {
                Log.Write($"ArmyDuty start Exception: {e}");
            }
        }

        public static void CreatePoints()
        {
            foreach (var shape in Shapes) CustomColShape.DeleteColShape(shape);
            foreach (var label in Labels) if (label != null && label.Exists) label.Delete();
            foreach (var marker in Markers) if (marker != null && marker.Exists) marker.Delete();
            Shapes.Clear();
            Labels.Clear();
            Markers.Clear();

            if (Cfg.DutyBoard != null)
            {
                Shapes.Add(CustomColShape.CreateCylinderColShape(Cfg.DutyBoard, 1.5f, 2, 0, ColShapeEnums.ArmyDutyBoard, 0));
                Labels.Add(NAPI.TextLabel.CreateTextLabel("~g~Доска нарядов", Cfg.DutyBoard + new Vector3(0, 0, 1), 8f, 0.4f, 4, new Color(255, 255, 255), false, 0));
                Markers.Add(NAPI.Marker.CreateMarker(1, Cfg.DutyBoard - new Vector3(0, 0, 1), new Vector3(), new Vector3(), 1f, new Color(107, 142, 35, 180)));
            }
            for (var i = 0; i < Cfg.KitchenPoints.Count; i++)
                Shapes.Add(CustomColShape.CreateCylinderColShape(Cfg.KitchenPoints[i], 1.2f, 2, uint.MaxValue, ColShapeEnums.ArmyDutyPoint, i));
            for (var i = 0; i < Cfg.CleanPoints.Count; i++)
                Shapes.Add(CustomColShape.CreateCylinderColShape(Cfg.CleanPoints[i], 1.5f, 2, 0, ColShapeEnums.ArmyDutyPoint, CleanIndexOffset + i));
        }

        private static List<Vector3> Points(string type) => type == Kitchen ? Cfg.KitchenPoints : Cfg.CleanPoints;
        private static string TypeName(string type) => type == Kitchen ? "кухня" : "уборка территории";

        // ------------------------------------------------------------------ хранение

        private static void Persist(int uuid, Order order)
        {
            if (!_ready)
                return;
            if (order == null)
            {
                NeptuneEvo.Database.DbQueue.Enqueue("DELETE FROM `army_duty_orders` WHERE `uuid`=@u", ("@u", uuid));
                return;
            }
            NeptuneEvo.Database.DbQueue.Enqueue(
                @"INSERT INTO `army_duty_orders` (`uuid`,`type`,`officer`,`punishment`,`seconds_left`,`step`) VALUES (@u,@t,@o,@p,@s,@st)
                  ON DUPLICATE KEY UPDATE `type`=VALUES(`type`),`officer`=VALUES(`officer`),`punishment`=VALUES(`punishment`),`seconds_left`=VALUES(`seconds_left`),`step`=VALUES(`step`)",
                ("@u", uuid), ("@t", order.Type), ("@o", order.Officer ?? ""), ("@p", order.Punishment ? 1 : 0), ("@s", order.SecondsLeft), ("@st", order.Step));
        }

        private static void LoadFor(ExtPlayer player)
        {
            var uuid = player.GetUUID();
            if (!Loaded.Add(uuid) || !_ready)
                return;
            using var table = NeptuneEvo.Database.DbQueue.Read("SELECT `type`,`officer`,`punishment`,`seconds_left`,`step` FROM `army_duty_orders` WHERE `uuid`=@u", ("@u", uuid));
            if (table == null || table.Rows.Count == 0)
                return;
            var row = table.Rows[0];
            var order = new Order
            {
                Type = row["type"].ToString(),
                Officer = row["officer"].ToString(),
                Punishment = Convert.ToInt32(row["punishment"]) == 1,
                SecondsLeft = Convert.ToInt32(row["seconds_left"]),
                Step = Convert.ToInt32(row["step"]),
            };
            Orders[uuid] = order;
            Remind(player, order, true);
        }

        private static void Remind(ExtPlayer player, Order order, bool withWaypoint)
        {
            order.LastReminder = DateTime.Now;
            var points = Points(order.Type);
            var left = Math.Max(1, order.SecondsLeft / 60);
            Notify.Send(player, NotifyType.Warning, NotifyPosition.Center,
                $"Вам назначен наряд: {TypeName(order.Type)}{(string.IsNullOrEmpty(order.Officer) ? "" : $" (назначил {order.Officer})")}. Осталось {left} мин онлайна", 8000);
            if (withWaypoint && order.Step < points.Count)
                Trigger.ClientEvent(player, "createWaypoint", points[order.Step].X, points[order.Step].Y);
        }

        // ------------------------------------------------------------------ окно

        [Interaction(ColShapeEnums.ArmyDutyBoard)]
        public static void OnBoard(ExtPlayer player, int _)
        {
            try
            {
                if (!ArmyUtil.IsArmy(player))
                {
                    ArmyUtil.Say(player, "Доска нарядов только для военнослужащих", false);
                    return;
                }
                LoadFor(player);
                Trigger.ClientEvent(player, "client.army.duty.open", BuildJson(player));
            }
            catch (Exception e)
            {
                Log.Write($"OnBoard Exception: {e}");
            }
        }

        private static string BuildJson(ExtPlayer player)
        {
            Orders.TryGetValue(player.GetUUID(), out var order);
            var officer = ArmyUtil.IsOfficer(player);
            return JsonConvert.SerializeObject(new
            {
                kitchen = new { points = Cfg.KitchenPoints.Count, reward = Cfg.DutyReward },
                clean = new { points = Cfg.CleanPoints.Count, reward = Cfg.DutyReward },
                order = order == null ? null : new
                {
                    type = order.Type,
                    name = TypeName(order.Type),
                    officer = order.Officer,
                    punishment = order.Punishment,
                    minutesLeft = Math.Max(1, order.SecondsLeft / 60),
                    step = order.Step,
                    total = Points(order.Type).Count,
                },
                isOfficer = officer,
                soldiers = officer
                    ? ArmyUtil.ArmyOnDuty().Where(p => p != player).Select(p => new
                    {
                        id = p.Value,
                        name = p.Name,
                        rank = Manager.GetFractionRankName(ArmyUtil.ArmyId, p.GetFractionMemberData()?.Rank ?? 0),
                        busy = Orders.ContainsKey(p.GetUUID()),
                    }).ToList<object>()
                    : new List<object>(),
            });
        }

        private static void Refresh(ExtPlayer player, string message, bool ok = true)
        {
            Trigger.ClientEvent(player, "client.army.duty.update", BuildJson(player), message ?? "", ok);
        }

        private static bool Assign(ExtPlayer target, string type, string officer, bool punishment, out string error)
        {
            error = null;
            if (type != Kitchen && type != Clean)
            {
                error = "Неизвестный наряд";
                return false;
            }
            if (Points(type).Count == 0)
            {
                error = type == Kitchen ? "Кухня не оборудована (админ: /armyset kitchen add)" : "Точки уборки не заданы";
                return false;
            }
            var uuid = target.GetUUID();
            LoadFor(target);
            if (Orders.ContainsKey(uuid))
            {
                error = $"{target.Name} уже в наряде";
                return false;
            }
            var order = new Order
            {
                Type = type,
                Officer = officer,
                Punishment = punishment,
                SecondsLeft = Cfg.DutyOnlineMinutes * 60,
            };
            Orders[uuid] = order;
            Persist(uuid, order);
            Remind(target, order, true);
            return true;
        }

        [RemoteEvent("server.army.duty.take")]
        public static void OnTake(ExtPlayer player, string type)
        {
            try
            {
                if (!ArmyUtil.IsArmy(player))
                    return;
                if (!ArmyUtil.OnDuty(player))
                {
                    Refresh(player, "Наряд берут на смене (начните рабочий день)", false);
                    return;
                }
                if (!Assign(player, type, null, false, out var error))
                {
                    Refresh(player, error, false);
                    return;
                }
                ArmyUtil.Radio($"{player.Name} заступил в наряд: {TypeName(type)}");
                Refresh(player, "Вы заступили в наряд. Метка — на первой точке");
            }
            catch (Exception e)
            {
                Log.Write($"OnTake Exception: {e}");
            }
        }

        [RemoteEvent("server.army.duty.assign")]
        public static void OnAssign(ExtPlayer player, int targetId, string type, bool punishment)
        {
            try
            {
                if (!ArmyUtil.IsOfficer(player))
                    return;
                var target = Main.GetPlayerByID(targetId);
                if (target == null || !ArmyUtil.IsArmy(target))
                {
                    Refresh(player, "Военнослужащий не найден", false);
                    return;
                }
                if (!Assign(target, type, player.Name, punishment, out var error))
                {
                    Refresh(player, error, false);
                    return;
                }
                ArmyUtil.Radio($"{player.Name} назначил {target.Name} в наряд: {TypeName(type)}{(punishment ? " (взыскание)" : "")}");
                Fractions.Table.Logs.Repository.AddLogs(player, FractionLogsType.None, $"Наряд {TypeName(type)} для {target.Name}{(punishment ? " (взыскание)" : "")}");
                Refresh(player, $"{target.Name} назначен: {TypeName(type)}");
            }
            catch (Exception e)
            {
                Log.Write($"OnAssign Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ работа на точках

        [Interaction(ColShapeEnums.ArmyDutyPoint)]
        public static void OnPoint(ExtPlayer player, int index)
        {
            try
            {
                if (!ArmyUtil.IsArmy(player))
                    return;
                LoadFor(player);
                var uuid = player.GetUUID();
                if (!Orders.TryGetValue(uuid, out var order))
                {
                    ArmyUtil.Say(player, "У вас нет наряда — возьмите его у доски нарядов", false);
                    return;
                }
                var type = index >= CleanIndexOffset ? Clean : Kitchen;
                var pointIndex = index >= CleanIndexOffset ? index - CleanIndexOffset : index;
                if (type != order.Type)
                {
                    ArmyUtil.Say(player, $"Ваш наряд — {TypeName(order.Type)}", false);
                    return;
                }
                if (pointIndex != order.Step)
                {
                    ArmyUtil.Say(player, $"Сначала точка {order.Step + 1} (метка на карте)", false);
                    return;
                }
                if (order.Working)
                    return;
                var sessionData = player.GetSessionData();
                if (sessionData == null || player.IsInVehicle || sessionData.CuffedData.Cuffed)
                    return;

                order.Working = true;
                Trigger.StopAnimation(player);
                player.SetSharedData("AnimToKey", type == Kitchen ? "army_kitchen" : "army_broom");
                Trigger.ClientEvent(player, "blockMove", true);
                Timers.StartOnce(Cfg.DutyActionSeconds * 1000, () => FinishPoint(player, uuid), true);
            }
            catch (Exception e)
            {
                Log.Write($"OnPoint Exception: {e}");
            }
        }

        private static void FinishPoint(ExtPlayer player, int uuid)
        {
            try
            {
                if (!Orders.TryGetValue(uuid, out var order))
                    return;
                order.Working = false;
                if (player == null || !player.IsCharacterData())
                    return;
                player.SetSharedData("AnimToKey", 0);
                Trigger.ClientEvent(player, "blockMove", false);
                order.Step++;
                var points = Points(order.Type);
                if (order.Step < points.Count)
                {
                    Persist(uuid, order);
                    Trigger.ClientEvent(player, "createWaypoint", points[order.Step].X, points[order.Step].Y);
                    ArmyUtil.Say(player, $"Готово {order.Step}/{points.Count}. Дальше — следующая точка (метка на карте)");
                    return;
                }
                Orders.Remove(uuid);
                Persist(uuid, null);
                if (!order.Punishment && Cfg.DutyReward > 0)
                {
                    MoneySystem.Wallet.Change(player, Cfg.DutyReward);
                    GameLog.Money("server", $"player({uuid})", Cfg.DutyReward, "armyDuty");
                }
                Fractions.Table.Logs.Repository.AddLogs(player, FractionLogsType.None, $"Выполнил наряд: {TypeName(order.Type)}");
                ArmyUtil.Radio($"{player.Name} выполнил наряд: {TypeName(order.Type)}");
                ArmyUtil.Say(player, order.Punishment ? "Наряд выполнен. Взыскание снято" : $"Наряд выполнен! Премия +{Cfg.DutyReward}$");
            }
            catch (Exception e)
            {
                Log.Write($"FinishPoint Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ таймер онлайна

        private static void Tick()
        {
            try
            {
                foreach (var player in ArmyUtil.ArmyOnDuty().Concat(Character.Repository.GetPlayers().Where(ArmyUtil.IsArmy)).Distinct())
                {
                    LoadFor(player);
                    var uuid = player.GetUUID();
                    if (!Orders.TryGetValue(uuid, out var order))
                        continue;
                    order.SecondsLeft -= 10;
                    if (order.SecondsLeft <= 0)
                    {
                        Orders.Remove(uuid);
                        Persist(uuid, null);
                        ArmyUtil.Radio($"{player.Name} не выполнил наряд ({TypeName(order.Type)}){(string.IsNullOrEmpty(order.Officer) ? "" : $", назначил {order.Officer}")}");
                        ArmyUtil.Say(player, "Срок наряда истёк — наряд не выполнен", false);
                        continue;
                    }
                    if (order.SecondsLeft % 60 < 10)
                        Persist(uuid, order);
                    if ((DateTime.Now - order.LastReminder).TotalMinutes >= 10)
                        Remind(player, order, false);
                }
            }
            catch (Exception e)
            {
                Log.Write($"ArmyDuty Tick Exception: {e}");
            }
        }

        [ServerEvent(Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(ExtPlayer player, DisconnectionType type, string reason)
        {
            try
            {
                var uuid = player.GetUUID();
                if (Orders.TryGetValue(uuid, out var order))
                {
                    order.Working = false;
                    Persist(uuid, order);
                }
                Orders.Remove(uuid);
                Loaded.Remove(uuid);
            }
            catch (Exception e)
            {
                Log.Write($"ArmyDuty disconnect Exception: {e}");
            }
        }
    }
}
