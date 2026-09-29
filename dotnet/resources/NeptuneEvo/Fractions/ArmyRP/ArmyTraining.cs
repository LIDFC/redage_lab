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
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using NeptuneEvo.VehicleData.LocalData;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Fractions.ArmyRP
{
    /// <summary>
    /// Г. Подготовка и техника:
    ///  /range — стрельбы на огневом рубеже (мишени ставит клиент, результат — попадания и точность);
    ///  /course — полоса препятствий по чекпоинтам на время;
    ///  /armyfile [id] — личное дело (лучшие результаты и зачёты), при повышении офицер видит подсказку;
    ///  ремонт техники на базе — мини-игра (CEF ArmyRepair) вместо мгновенного;
    ///  /drill — учебная тревога: сирена у всех на смене, сбор на плацу, итоги.
    /// </summary>
    class ArmyTraining : Script
    {
        private static nLog Log => ArmyConfig.Log;
        private static ArmyConfig Cfg => ArmyConfig.Current;
        private static bool _ready;

        private class Attempt
        {
            public string Kind;
            public DateTime Started;
        }

        private static readonly Dictionary<ExtPlayer, Attempt> Attempts = new Dictionary<ExtPlayer, Attempt>();

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            try
            {
                using (var create = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS `army_training` (
                    `uuid` INT NOT NULL,
                    `kind` VARCHAR(16) NOT NULL,
                    `best` DOUBLE NOT NULL DEFAULT 0,
                    `passes` INT NOT NULL DEFAULT 0,
                    `attempts` INT NOT NULL DEFAULT 0,
                    `last` DATETIME NOT NULL,
                    PRIMARY KEY (`uuid`, `kind`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"))
                    MySQL.Query(create);
                _ready = true;
            }
            catch (Exception e)
            {
                Log.Write($"ArmyTraining start Exception: {e}");
            }
        }

        /// <summary>kind: range (best — попадания, больше лучше), course (best — секунды, меньше лучше).</summary>
        private static void SaveResult(ExtPlayer player, string kind, double value, bool passed)
        {
            if (!_ready)
                return;
            var better = kind == "course" ? "LEAST" : "GREATEST";
            NeptuneEvo.Database.DbQueue.Enqueue(
                $@"INSERT INTO `army_training` (`uuid`,`kind`,`best`,`passes`,`attempts`,`last`) VALUES (@u,@k,@v,@p,1,@t)
                   ON DUPLICATE KEY UPDATE `best` = IF(`best` = 0, VALUES(`best`), {better}(`best`, VALUES(`best`))),
                   `passes` = `passes` + VALUES(`passes`), `attempts` = `attempts` + 1, `last` = VALUES(`last`)",
                ("@u", player.GetUUID()), ("@k", kind), ("@v", value), ("@p", passed ? 1 : 0), ("@t", DateTime.Now));
        }

        private static Dictionary<string, (double best, int passes, int attempts, DateTime last)> ReadFile(int uuid)
        {
            var result = new Dictionary<string, (double, int, int, DateTime)>();
            using var table = NeptuneEvo.Database.DbQueue.Read("SELECT `kind`,`best`,`passes`,`attempts`,`last` FROM `army_training` WHERE `uuid` = @u", ("@u", uuid));
            if (table == null)
                return result;
            foreach (DataRow row in table.Rows)
                result[row["kind"].ToString()] = (Convert.ToDouble(row["best"]), Convert.ToInt32(row["passes"]), Convert.ToInt32(row["attempts"]), Convert.ToDateTime(row["last"]));
            return result;
        }

        private static bool CanTrain(ExtPlayer player)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null || !ArmyUtil.IsArmy(player))
            {
                ArmyUtil.Say(player, "Полигон только для военнослужащих", false);
                return false;
            }
            if (player.IsInVehicle || sessionData.CuffedData.Cuffed || sessionData.DeathData.InDeath)
                return false;
            if (Attempts.ContainsKey(player))
            {
                ArmyUtil.Say(player, "Вы уже проходите упражнение", false);
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ тир

        [Command("range")]
        public static void CMD_Range(ExtPlayer player)
        {
            try
            {
                if (!CanTrain(player))
                    return;
                if (player.Position.DistanceTo(Cfg.RangePoint) > 5f)
                {
                    Trigger.ClientEvent(player, "createWaypoint", Cfg.RangePoint.X, Cfg.RangePoint.Y);
                    ArmyUtil.Say(player, "Встаньте на огневой рубеж (метка на карте)", false);
                    return;
                }
                Attempts[player] = new Attempt { Kind = "range", Started = DateTime.Now };
                Trigger.ClientEvent(player, "client.army.range.start", Cfg.RangeTargets, Cfg.RangeSeconds,
                    Cfg.RangePoint.X, Cfg.RangePoint.Y, Cfg.RangePoint.Z, Cfg.RangeHeading);
                Commands.RPChat("sme", player, " занял огневой рубеж");
            }
            catch (Exception e)
            {
                Log.Write($"CMD_Range Exception: {e}");
            }
        }

        [RemoteEvent("server.army.range.result")]
        public static void OnRangeResult(ExtPlayer player, int hits, int shots)
        {
            try
            {
                if (!Attempts.TryGetValue(player, out var attempt) || attempt.Kind != "range")
                    return;
                Attempts.Remove(player);
                var elapsed = (DateTime.Now - attempt.Started).TotalSeconds;
                // Мишени появляются по очереди — быстрее ~1 с на мишень не бывает; попаданий не больше мишеней и выстрелов
                hits = Math.Max(0, Math.Min(hits, Math.Min(Cfg.RangeTargets, Math.Max(shots, 0))));
                if (elapsed < Math.Min(Cfg.RangeSeconds, hits * 0.8))
                    hits = 0;
                var accuracy = shots > 0 ? hits * 100.0 / shots : 0;
                var passed = hits >= Cfg.RangePass;
                SaveResult(player, "range", hits, passed);
                ArmyUtil.Say(player, $"Стрельбы: {hits}/{Cfg.RangeTargets} мишеней, точность {accuracy:0}% — {(passed ? "ЗАЧЁТ" : $"незачёт (нужно {Cfg.RangePass})")}", passed);
                ArmyUtil.Radio($"{player.Name}: стрельбы {hits}/{Cfg.RangeTargets}, точность {accuracy:0}%{(passed ? " — зачёт" : "")}");
            }
            catch (Exception e)
            {
                Log.Write($"OnRangeResult Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ полоса препятствий

        [Command("course")]
        public static void CMD_Course(ExtPlayer player)
        {
            try
            {
                if (!CanTrain(player))
                    return;
                if (Cfg.Course.Count < 2)
                {
                    ArmyUtil.Say(player, "Полоса не размечена (/armyset course add)", false);
                    return;
                }
                if (player.Position.DistanceTo(Cfg.Course[0]) > 5f)
                {
                    Trigger.ClientEvent(player, "createWaypoint", Cfg.Course[0].X, Cfg.Course[0].Y);
                    ArmyUtil.Say(player, "Встаньте на старт полосы (метка на карте)", false);
                    return;
                }
                Attempts[player] = new Attempt { Kind = "course", Started = DateTime.Now };
                Trigger.ClientEvent(player, "client.army.course.start", JsonConvert.SerializeObject(Cfg.Course.Select(p => new[] { p.X, p.Y, p.Z })));
            }
            catch (Exception e)
            {
                Log.Write($"CMD_Course Exception: {e}");
            }
        }

        [RemoteEvent("server.army.course.result")]
        public static void OnCourseResult(ExtPlayer player, bool finished)
        {
            try
            {
                if (!Attempts.TryGetValue(player, out var attempt) || attempt.Kind != "course")
                    return;
                Attempts.Remove(player);
                if (!finished)
                {
                    ArmyUtil.Say(player, "Полоса не пройдена", false);
                    return;
                }
                // Время — по часам сервера; финиш засчитывается только у последней точки
                var seconds = (DateTime.Now - attempt.Started).TotalSeconds;
                if (player.Position.DistanceTo(Cfg.Course.Last()) > 8f || seconds < Cfg.Course.Count)
                    return;
                var passed = seconds <= Cfg.CoursePassSeconds;
                SaveResult(player, "course", Math.Round(seconds, 1), passed);
                World.Gym.Fitness.Gain(player, "stamina");
                ArmyUtil.Say(player, $"Полоса: {(int) seconds / 60}:{(int) seconds % 60:00} — {(passed ? "ЗАЧЁТ" : $"незачёт (норматив {Cfg.CoursePassSeconds / 60}:{Cfg.CoursePassSeconds % 60:00})")}", passed);
                ArmyUtil.Radio($"{player.Name}: полоса препятствий за {(int) seconds / 60}:{(int) seconds % 60:00}{(passed ? " — зачёт" : "")}");
            }
            catch (Exception e)
            {
                Log.Write($"OnCourseResult Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ личное дело

        private static string FileText(Dictionary<string, (double best, int passes, int attempts, DateTime last)> file, string kind)
        {
            if (!file.TryGetValue(kind, out var r))
                return kind == "range" ? "Стрельбы: не сдавал" : "Полоса: не сдавал";
            return kind == "range"
                ? $"Стрельбы: лучший {r.best:0}/{Cfg.RangeTargets}, зачётов {r.passes} из {r.attempts}, последний {r.last:dd.MM}"
                : $"Полоса: лучшее {(int) r.best / 60}:{(int) r.best % 60:00}, зачётов {r.passes} из {r.attempts}, последний {r.last:dd.MM}";
        }

        [Command("armyfile")]
        public static void CMD_ArmyFile(ExtPlayer player, int targetId = -1)
        {
            try
            {
                if (!ArmyUtil.IsArmy(player))
                    return;
                var target = targetId == -1 ? player : Main.GetPlayerByID(targetId);
                if (target == null || !ArmyUtil.IsArmy(target))
                {
                    ArmyUtil.Say(player, "Военнослужащий не найден", false);
                    return;
                }
                if (target != player && !ArmyUtil.IsOfficer(player))
                {
                    ArmyUtil.Say(player, "Чужое личное дело смотрит офицер", false);
                    return;
                }
                var file = ReadFile(target.GetUUID());
                player.SendChatMessage($"~y~[Личное дело] {target.Name}, ранг {Manager.GetFractionRankName(ArmyUtil.ArmyId, target.GetFractionMemberData()?.Rank ?? 0)}");
                player.SendChatMessage(FileText(file, "range"));
                player.SendChatMessage(FileText(file, "course"));
            }
            catch (Exception e)
            {
                Log.Write($"CMD_ArmyFile Exception: {e}");
            }
        }

        /// <summary>Из SetFracRank: при повышении военного офицер видит, есть ли у бойца зачёты.</summary>
        public static void PromotionHint(ExtPlayer officer, ExtPlayer target)
        {
            try
            {
                if (!ArmyUtil.IsArmy(target))
                    return;
                var file = ReadFile(target.GetUUID());
                var range = file.TryGetValue("range", out var r) && r.passes > 0;
                var course = file.TryGetValue("course", out var c) && c.passes > 0;
                if (range && course)
                    return;
                officer.SendChatMessage($"~y~[Личное дело] У {target.Name} нет зачёта: {string.Join(", ", new[] { range ? null : "стрельбы (/range)", course ? null : "полоса (/course)" }.Where(s => s != null))}");
            }
            catch (Exception e)
            {
                Log.Write($"PromotionHint Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ учебная тревога

        [Command("drill")]
        public static void CMD_Drill(ExtPlayer player)
        {
            try
            {
                if (!ArmyUtil.IsOfficer(player))
                {
                    ArmyUtil.Say(player, "Учебную тревогу объявляет офицер", false);
                    return;
                }
                if (ArmyService.StartGathering(player, "Учебная тревога", Cfg.DrillMinutes))
                    Fractions.Table.Logs.Repository.AddLogs(player, FractionLogsType.None, "Объявил учебную тревогу");
            }
            catch (Exception e)
            {
                Log.Write($"CMD_Drill Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ ремонт мини-игрой

        private class Repair
        {
            public ExtVehicle Vehicle;
            public bool Air;
            public DateTime Started;
        }

        private static readonly Dictionary<ExtPlayer, Repair> Repairs = new Dictionary<ExtPlayer, Repair>();

        private static readonly string[] GroundParts = { "Двигатель", "Трансмиссия", "Тормоза", "Подвеска", "Электрика", "Топливная система" };
        private static readonly string[] AirParts = { "Двигатель", "Несущий винт", "Гидравлика", "Авионика", "Шасси", "Топливная система" };

        /// <summary>Вместо мгновенного ремонта (Army.cs, точки ремонта): мини-игра, по завершении — ремонт.</summary>
        public static void StartRepair(ExtPlayer player, ExtVehicle vehicle, bool air)
        {
            if (Repairs.ContainsKey(player))
                return;
            var parts = air ? AirParts : GroundParts;
            var rnd = new Random();
            var broken = Enumerable.Range(0, parts.Length).OrderBy(_ => rnd.Next()).Take(2).ToList();
            Repairs[player] = new Repair { Vehicle = vehicle, Air = air, Started = DateTime.Now };
            Trigger.ClientEvent(player, "client.army.repair.open", JsonConvert.SerializeObject(new
            {
                title = air ? "Ремонт воздушной техники" : "Ремонт наземной техники",
                parts = parts.Select((name, i) => new { name, wear = broken.Contains(i) ? rnd.Next(78, 97) : rnd.Next(5, 35) }),
                seconds = Cfg.RepairSeconds,
            }));
            Commands.RPChat("sme", player, air ? " начал осмотр воздушной техники" : " открыл капот и начал осмотр");
        }

        [RemoteEvent("server.army.repair.done")]
        public static void OnRepairDone(ExtPlayer player)
        {
            try
            {
                if (!Repairs.Remove(player, out var repair))
                    return;
                var vehicle = repair.Vehicle;
                if ((DateTime.Now - repair.Started).TotalSeconds < Cfg.RepairSeconds * 0.5
                    || vehicle == null || !vehicle.Exists || vehicle.Position.DistanceTo(player.Position) > 8f)
                {
                    ArmyUtil.Say(player, "Ремонт не завершён", false);
                    return;
                }
                if (repair.Air) Main.NextFixcarPlane = DateTime.Now.AddMinutes(3);
                else Main.NextFixcarVeh = DateTime.Now.AddMinutes(3);
                VehicleManager.RepairCar(vehicle);
                Commands.RPChat("sme", player, " закончил ремонт техники");
                ArmyUtil.Say(player, "Техника отремонтирована");
            }
            catch (Exception e)
            {
                Log.Write($"OnRepairDone Exception: {e}");
            }
        }

        [RemoteEvent("server.army.repair.cancel")]
        public static void OnRepairCancel(ExtPlayer player) => Repairs.Remove(player);

        [RemoteEvent("server.army.training.cancel")]
        public static void OnTrainingCancel(ExtPlayer player) => Attempts.Remove(player);

        [ServerEvent(Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(ExtPlayer player, DisconnectionType type, string reason)
        {
            Attempts.Remove(player);
            Repairs.Remove(player);
        }
    }
}
