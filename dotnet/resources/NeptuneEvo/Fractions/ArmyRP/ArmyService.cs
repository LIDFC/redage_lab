using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using GTANetworkAPI;
using MySqlConnector;
using NeptuneEvo.Chars;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Fractions.Models;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using NeptuneEvo.Table.Models;
using Redage.SDK;

namespace NeptuneEvo.Fractions.ArmyRP
{
    /// <summary>Общие проверки для армейских механик.</summary>
    public static class ArmyUtil
    {
        public const int ArmyId = (int) Models.Fractions.ARMY;

        public static bool IsArmy(ExtPlayer player) => player.IsCharacterData() && player.GetFractionId() == ArmyId;

        public static bool OnDuty(ExtPlayer player) => player.GetSessionData()?.WorkData.OnDuty == true;

        /// <summary>Офицер — ранг, которому доступно принятие в армию.</summary>
        public static bool IsOfficer(ExtPlayer player) => IsArmy(player) && player.IsFractionAccess(RankToAccess.Invite, false);

        public static IEnumerable<ExtPlayer> ArmyOnDuty() =>
            Character.Repository.GetPlayers().Where(p => IsArmy(p) && OnDuty(p));

        public static void Radio(string text) => Manager.sendFractionMessage(ArmyId, "!{#6B8E23}[Армия] " + text, true);

        public static void Say(ExtPlayer player, string text, bool ok = true) =>
            Notify.Send(player, ok ? NotifyType.Info : NotifyType.Error, NotifyPosition.BottomCenter, text, 4000);
    }

    /// <summary>
    /// А. Служба и дисциплина:
    ///  /salute /attention /atease — отдать честь, смирно, вольно (повтор — выйти);
    ///  /formation — офицер собирает построение на плацу, сводка прибывших;
    ///  /post — заступить на караульный пост и сойти с него (смена засчитывается через N минут, премия);
    ///  /guardhouse id минуты причина, /unguardhouse id — гауптвахта (ArrestType 3);
    ///  /returnguns — сдать армейское оружие на оружейке, /armylog [имя] — журнал выдачи и кто не сдал.
    /// </summary>
    class ArmyService : Script
    {
        private static nLog Log => ArmyConfig.Log;
        private static ArmyConfig Cfg => ArmyConfig.Current;

        /// <summary>Гауптвахта: отдельная копия камер КПЗ Mission Row (своё измерение), выход — у штаба армии в порту.</summary>
        public const uint GuardhouseDimension = 3244600;
        public static Vector3 GuardhouseCell => Police.PrisonPosition;
        public static Vector3 GuardhouseExit => Army.ArmyCheckpoints[9] + new Vector3(0, 0, 1);
        public const sbyte GuardhouseArrestType = 3;

        private static bool _logReady;

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            try
            {
                ArmyConfig.Load();
                using (var create = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS `army_weapon_log` (
                    `id` BIGINT NOT NULL AUTO_INCREMENT,
                    `time` DATETIME NOT NULL,
                    `uuid` INT NOT NULL,
                    `name` VARCHAR(64) NOT NULL DEFAULT '',
                    `item` VARCHAR(64) NOT NULL DEFAULT '',
                    `serial` VARCHAR(32) NOT NULL DEFAULT '',
                    `action` VARCHAR(16) NOT NULL DEFAULT '',
                    PRIMARY KEY (`id`),
                    KEY `serial` (`serial`),
                    KEY `uuid_time` (`uuid`, `time`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"))
                    MySQL.Query(create);
                _logReady = true;
                CreatePostLabels();
                Timers.Start("army.service", 5000, Tick, true);
            }
            catch (Exception e)
            {
                Log.Write($"ArmyService start Exception: {e}");
            }
        }

        private static readonly List<GTANetworkAPI.TextLabel> PostLabels = new List<GTANetworkAPI.TextLabel>();

        public static void CreatePostLabels()
        {
            foreach (var label in PostLabels)
                if (label != null && label.Exists) label.Delete();
            PostLabels.Clear();
            foreach (var post in Cfg.Posts)
                PostLabels.Add(NAPI.TextLabel.CreateTextLabel($"~g~Караульный пост\n~w~{post.Name}\n/post — заступить", post.Position, 8f, 0.4f, 4, new Color(255, 255, 255), false, 0));
        }

        // ------------------------------------------------------------------ строй

        private static readonly Dictionary<string, string> Stances = new Dictionary<string, string>
        {
            { "salute", "army_salute" },
            { "attention", "army_attention" },
            { "atease", "army_atease" },
        };

        private static void Stance(ExtPlayer player, string key)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null || player.IsInVehicle || sessionData.CuffedData.Cuffed || sessionData.DeathData.InDeath)
                return;
            var anim = Stances[key];
            var current = player.HasSharedData("AnimToKey") ? player.GetSharedData<object>("AnimToKey")?.ToString() : null;
            if (current == anim)
            {
                player.SetSharedData("AnimToKey", 0);
                return;
            }
            Trigger.StopAnimation(player);
            player.SetSharedData("AnimToKey", anim);
        }

        [Command("salute")]
        public static void CMD_Salute(ExtPlayer player) => Stance(player, "salute");

        [Command("attention")]
        public static void CMD_Attention(ExtPlayer player) => Stance(player, "attention");

        [Command("atease")]
        public static void CMD_AtEase(ExtPlayer player) => Stance(player, "atease");

        // ------------------------------------------------------------------ построение

        private class Gathering
        {
            public string Kind;
            public string Caller;
            public DateTime Until;
            public Vector3 Point;
            public Dictionary<int, string> Expected = new Dictionary<int, string>();
            public Dictionary<int, TimeSpan> Arrived = new Dictionary<int, TimeSpan>();
            public DateTime Started;
        }

        private static Gathering _gathering;

        /// <summary>Общий сбор на плацу: построение (/formation) или учебная тревога (/drill).</summary>
        public static bool StartGathering(ExtPlayer officer, string kind, int minutes)
        {
            if (_gathering != null)
            {
                ArmyUtil.Say(officer, "Уже идёт сбор — дождитесь итогов", false);
                return false;
            }
            var members = ArmyUtil.ArmyOnDuty().ToList();
            if (members.Count == 0)
            {
                ArmyUtil.Say(officer, "Нет военных на смене", false);
                return false;
            }
            _gathering = new Gathering
            {
                Kind = kind,
                Caller = officer.Name,
                Until = DateTime.Now.AddMinutes(minutes),
                Point = Cfg.ParadePoint,
                Started = DateTime.Now,
            };
            foreach (var member in members)
            {
                _gathering.Expected[member.GetUUID()] = member.Name;
                Trigger.ClientEvent(member, "createWaypoint", Cfg.ParadePoint.X, Cfg.ParadePoint.Y);
                Notify.Send(member, NotifyType.Warning, NotifyPosition.Center, $"{kind}! Прибыть на плац за {minutes} мин", 8000);
                if (kind == "Учебная тревога")
                    Trigger.ClientEvent(member, "client.army.alarm");
            }
            ArmyUtil.Radio($"{officer.Name}: {kind}. Всем на плац, {minutes} мин. Ожидается {members.Count} чел.");
            return true;
        }

        [Command("formation")]
        public static void CMD_Formation(ExtPlayer player)
        {
            try
            {
                if (!ArmyUtil.IsOfficer(player))
                {
                    ArmyUtil.Say(player, "Построение объявляет офицер армии", false);
                    return;
                }
                if (StartGathering(player, "Построение", Cfg.FormationMinutes))
                    Fractions.Table.Logs.Repository.AddLogs(player, FractionLogsType.None, "Объявил построение");
            }
            catch (Exception e)
            {
                Log.Write($"CMD_Formation Exception: {e}");
            }
        }

        private static void TickGathering()
        {
            var g = _gathering;
            if (g == null)
                return;
            foreach (var player in ArmyUtil.ArmyOnDuty())
            {
                var uuid = player.GetUUID();
                if (g.Arrived.ContainsKey(uuid) || player.Position.DistanceTo2D(g.Point) > Cfg.ParadeRadius)
                    continue;
                g.Arrived[uuid] = DateTime.Now - g.Started;
                g.Expected.TryAdd(uuid, player.Name);
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Прибыл за {(int) g.Arrived[uuid].TotalMinutes}:{g.Arrived[uuid].Seconds:00}. Встаньте в строй (/attention)", 5000);
            }
            if (DateTime.Now < g.Until && g.Arrived.Count < g.Expected.Count)
                return;
            var arrived = g.Arrived.OrderBy(a => a.Value).Select(a => $"{g.Expected[a.Key]} ({(int) a.Value.TotalMinutes}:{a.Value.Seconds:00})").ToList();
            var missing = g.Expected.Where(e => !g.Arrived.ContainsKey(e.Key)).Select(e => e.Value).ToList();
            ArmyUtil.Radio($"Итоги ({g.Kind}, объявил {g.Caller}): прибыли {arrived.Count}/{g.Expected.Count}.");
            if (arrived.Count > 0) ArmyUtil.Radio("Прибыли: " + string.Join(", ", arrived.Take(20)));
            if (missing.Count > 0) ArmyUtil.Radio("Не прибыли: " + string.Join(", ", missing.Take(20)));
            _gathering = null;
        }

        // ------------------------------------------------------------------ караульные посты

        private class PostDuty
        {
            public int PostIndex;
            public DateTime Since;
            public int Shifts;
        }

        private static readonly Dictionary<ExtPlayer, PostDuty> OnPost = new Dictionary<ExtPlayer, PostDuty>();

        [Command("post")]
        public static void CMD_Post(ExtPlayer player)
        {
            try
            {
                if (!ArmyUtil.IsArmy(player) || !ArmyUtil.OnDuty(player))
                {
                    ArmyUtil.Say(player, "Заступить на пост может военный на смене", false);
                    return;
                }
                if (OnPost.TryGetValue(player, out var duty))
                {
                    OnPost.Remove(player);
                    ArmyUtil.Say(player, $"Вы сошли с поста «{Cfg.Posts.ElementAtOrDefault(duty.PostIndex)?.Name}». Смен отстояно: {duty.Shifts}");
                    return;
                }
                var index = Cfg.Posts.FindIndex(p => p.Position.DistanceTo(player.Position) <= p.Radius);
                if (index == -1)
                {
                    ArmyUtil.Say(player, Cfg.Posts.Count == 0 ? "Посты не расставлены (/armyset post add)" : "Рядом нет караульного поста", false);
                    return;
                }
                if (OnPost.Any(p => p.Value.PostIndex == index))
                {
                    ArmyUtil.Say(player, "Этот пост уже занят", false);
                    return;
                }
                OnPost[player] = new PostDuty { PostIndex = index, Since = DateTime.Now };
                ArmyUtil.Say(player, $"Заступили на пост «{Cfg.Posts[index].Name}». Не покидайте его {Cfg.PostMinutes} мин. Сойти — /post");
                ArmyUtil.Radio($"{player.Name} заступил на пост «{Cfg.Posts[index].Name}»");
            }
            catch (Exception e)
            {
                Log.Write($"CMD_Post Exception: {e}");
            }
        }

        private static void TickPosts()
        {
            foreach (var (player, duty) in OnPost.ToList())
            {
                var post = Cfg.Posts.ElementAtOrDefault(duty.PostIndex);
                if (post == null || !ArmyUtil.IsArmy(player) || !ArmyUtil.OnDuty(player))
                {
                    OnPost.Remove(player);
                    continue;
                }
                if (player.Position.DistanceTo(post.Position) > post.Radius + 2)
                {
                    OnPost.Remove(player);
                    ArmyUtil.Say(player, "Вы покинули пост — смена не засчитана", false);
                    ArmyUtil.Radio($"{player.Name} самовольно покинул пост «{post.Name}»");
                    continue;
                }
                if ((DateTime.Now - duty.Since).TotalMinutes < Cfg.PostMinutes)
                    continue;
                duty.Since = DateTime.Now;
                duty.Shifts++;
                if (Cfg.PostReward > 0)
                {
                    MoneySystem.Wallet.Change(player, Cfg.PostReward);
                    GameLog.Money("server", $"player({player.GetUUID()})", Cfg.PostReward, "armyPost");
                }
                Fractions.Table.Logs.Repository.AddLogs(player, FractionLogsType.None, $"Отстоял смену на посту «{post.Name}»");
                ArmyUtil.Say(player, $"Смена на посту засчитана (+{Cfg.PostReward}$). Можно стоять дальше или сойти — /post");
            }
        }

        // ------------------------------------------------------------------ гауптвахта

        [Command("guardhouse", GreedyArg = true)]
        public static void CMD_Guardhouse(ExtPlayer player, int targetId, int minutes, string reason = "")
        {
            try
            {
                if (!ArmyUtil.IsOfficer(player) || !ArmyUtil.OnDuty(player))
                {
                    ArmyUtil.Say(player, "Отправить на гауптвахту может офицер на смене", false);
                    return;
                }
                var target = Main.GetPlayerByID(targetId);
                if (target == null || !target.IsCharacterData() || target == player)
                {
                    ArmyUtil.Say(player, "Игрок не найден", false);
                    return;
                }
                if (!ArmyUtil.IsArmy(target))
                {
                    ArmyUtil.Say(player, "На гауптвахту сажают только военнослужащих", false);
                    return;
                }
                if (minutes < 1 || minutes > Cfg.GuardhouseMaxMinutes)
                {
                    ArmyUtil.Say(player, $"Срок от 1 до {Cfg.GuardhouseMaxMinutes} мин", false);
                    return;
                }
                if (player.Position.DistanceTo(target.Position) > 3)
                {
                    ArmyUtil.Say(player, "Военнослужащий должен быть рядом", false);
                    return;
                }
                var targetSession = target.GetSessionData();
                var targetCharacter = target.GetCharacterData();
                if (targetSession == null || targetCharacter == null)
                    return;
                if (!targetSession.CuffedData.Cuffed)
                {
                    ArmyUtil.Say(player, "Сначала наденьте наручники", false);
                    return;
                }
                if (targetCharacter.ArrestTime != 0)
                {
                    ArmyUtil.Say(player, "Он уже под арестом", false);
                    return;
                }
                if (string.IsNullOrWhiteSpace(reason))
                    reason = "нарушение устава";
                if (targetSession.Following != null)
                    FractionCommands.unFollow(targetSession.Following, target);
                FractionCommands.unCuffPlayer(target);
                targetSession.CuffedData.CuffedByCop = false;

                targetCharacter.ArrestTime = minutes * 60;
                targetCharacter.ArrestType = GuardhouseArrestType;
                FractionCommands.arrestPlayer(target);
                Commands.RPChat("sme", player, " отправил {name} на гауптвахту", target);
                ArmyUtil.Radio($"{player.Name} отправил {target.Name} на гауптвахту на {minutes} мин: {reason}");
                Fractions.Table.Logs.Repository.AddLogs(player, FractionLogsType.Arrest, $"Гауптвахта {target.Name} ({target.GetUUID()}) на {minutes} мин: {reason}");
                Notify.Send(target, NotifyType.Warning, NotifyPosition.Center, $"Гауптвахта {minutes} мин: {reason}", 8000);
            }
            catch (Exception e)
            {
                Log.Write($"CMD_Guardhouse Exception: {e}");
            }
        }

        [Command("unguardhouse")]
        public static void CMD_Unguardhouse(ExtPlayer player, int targetId)
        {
            try
            {
                if (!ArmyUtil.IsOfficer(player))
                {
                    ArmyUtil.Say(player, "Освободить может офицер армии", false);
                    return;
                }
                var target = Main.GetPlayerByID(targetId);
                var targetCharacter = target?.GetCharacterData();
                if (targetCharacter == null || targetCharacter.ArrestType != GuardhouseArrestType || targetCharacter.ArrestTime == 0)
                {
                    ArmyUtil.Say(player, "Этот игрок не на гауптвахте", false);
                    return;
                }
                FractionCommands.freePlayer(target, false);
                ArmyUtil.Radio($"{player.Name} освободил {target.Name} с гауптвахты");
            }
            catch (Exception e)
            {
                Log.Write($"CMD_Unguardhouse Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ журнал оружия

        /// <summary>Серийник армейского оружия: WeaponRepository.GetSerial — 100000000 + 14*100000 + n.</summary>
        private static bool IsArmySerial(string data)
        {
            var serial = (data ?? "").Split('_')[0];
            return serial.Length == 9 && serial.StartsWith((100000000 + ArmyUtil.ArmyId * 100000).ToString().Substring(0, 4));
        }

        public static void LogWeapon(ExtPlayer player, string item, string serial, string action)
        {
            if (!_logReady)
                return;
            BlackMarket.BlackMarketRepository.Enqueue(
                "INSERT INTO `army_weapon_log` (`time`,`uuid`,`name`,`item`,`serial`,`action`) VALUES (@t,@u,@n,@i,@s,@a)",
                ("@t", DateTime.Now), ("@u", player.GetUUID()), ("@n", player.Name), ("@i", item ?? ""), ("@s", serial ?? ""), ("@a", action));
        }

        [Command("returnguns")]
        public static void CMD_ReturnGuns(ExtPlayer player)
        {
            try
            {
                if (!ArmyUtil.IsArmy(player))
                    return;
                if (player.Position.DistanceTo(Army.ArmyCheckpoints[0]) > 5)
                {
                    ArmyUtil.Say(player, "Сдавать оружие нужно на оружейном складе", false);
                    return;
                }
                var uuid = player.GetUUID();
                var locationName = $"char_{uuid}";
                if (!Chars.Repository.ItemsData.TryGetValue(locationName, out var locations))
                    return;
                var returned = new List<string>();
                foreach (var location in new[] { "fastSlots", "inventory" })
                {
                    if (!locations.TryGetValue(location, out var slots))
                        continue;
                    foreach (var (slot, item) in slots.ToList())
                    {
                        if (item == null || item.ItemId == ItemId.Debug || !Chars.Repository.ItemsInfo.TryGetValue(item.ItemId, out var info))
                            continue;
                        if (info.functionType != newItemType.Weapons || !IsArmySerial(item.Data))
                            continue;
                        var serial = item.Data.Split('_')[0];
                        Chars.Repository.RemoveIndex(player, location, slot);
                        LogWeapon(player, item.ItemId.ToString(), serial, "сдал");
                        returned.Add($"{item.ItemId} ({serial})");
                    }
                }
                if (returned.Count == 0)
                {
                    ArmyUtil.Say(player, "У вас нет выданного армейского оружия", false);
                    return;
                }
                Fractions.Table.Logs.Repository.AddLogs(player, FractionLogsType.TakeStock, "Сдал оружие: " + string.Join(", ", returned));
                ArmyUtil.Say(player, "Сдано: " + string.Join(", ", returned));
            }
            catch (Exception e)
            {
                Log.Write($"CMD_ReturnGuns Exception: {e}");
            }
        }

        [Command("armylog", GreedyArg = true)]
        public static void CMD_ArmyLog(ExtPlayer player, string name = "")
        {
            try
            {
                if (!ArmyUtil.IsOfficer(player))
                {
                    ArmyUtil.Say(player, "Журнал оружия доступен офицерам", false);
                    return;
                }
                using var table = BlackMarket.BlackMarketRepository.Read(
                    "SELECT `time`,`name`,`item`,`serial`,`action` FROM `army_weapon_log` WHERE `time` > @since ORDER BY `id` DESC LIMIT 500",
                    ("@since", DateTime.Now.AddDays(-7)));
                if (table == null)
                    return;
                var rows = table.Rows.Cast<DataRow>().ToList();
                var filter = (name ?? "").Trim();
                var returned = new HashSet<string>(rows.Where(r => r["action"].ToString() == "сдал").Select(r => r["serial"].ToString()));
                var notReturned = rows
                    .Where(r => r["action"].ToString() == "выдано" && !returned.Contains(r["serial"].ToString()))
                    .Where(r => filter.Length == 0 || r["name"].ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                player.SendChatMessage($"~y~[Журнал оружия] Не сдано за 7 дней: {notReturned.Count}{(filter.Length > 0 ? $" (фильтр «{filter}»)" : "")}");
                foreach (var r in notReturned.Take(15))
                    player.SendChatMessage($"{Convert.ToDateTime(r["time"]):dd.MM HH:mm} {r["name"]} — {r["item"]} ({r["serial"]})");
                var recent = rows.Where(r => filter.Length == 0 || r["name"].ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).Take(8).ToList();
                if (recent.Count > 0)
                {
                    player.SendChatMessage("~y~Последние записи:");
                    foreach (var r in recent)
                        player.SendChatMessage($"{Convert.ToDateTime(r["time"]):dd.MM HH:mm} {r["name"]} {r["action"]} {r["item"]} ({r["serial"]})");
                }
            }
            catch (Exception e)
            {
                Log.Write($"CMD_ArmyLog Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ таймер

        private static void Tick()
        {
            try
            {
                TickGathering();
                TickPosts();
            }
            catch (Exception e)
            {
                Log.Write($"ArmyService Tick Exception: {e}");
            }
        }

        [ServerEvent(Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(ExtPlayer player, DisconnectionType type, string reason) => OnPost.Remove(player);
    }
}
