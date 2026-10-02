using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using GTANetworkAPI;
using MySqlConnector;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Fractions.ArmyRP
{
    /// <summary>
    /// «Объявления» на доске нарядов (вкладка в CEF ArmyDuty): командиры пишут о строях, тренировках, учениях.
    ///  Писать — офицеры (ArmyUtil.IsOfficer), удалять — автор или ранг 9+.
    ///  Хранение — army_announcements, в памяти последние MaxCount. После публикации — рация всей армии
    ///  и обновление списка у всех военных онлайн (client.army.board.list — окно доски, если открыто, обновится).
    /// </summary>
    class ArmyBoard : Script
    {
        private static nLog Log => ArmyConfig.Log;
        private const int MaxCount = 30;
        private const int TitleMax = 60;
        private const int TextMax = 600;
        private const int ModerateRank = 9;

        private class Announcement
        {
            public long Id;
            public int AuthorUuid;
            public string Author;
            public string Rank;
            public string Title;
            public string Text;
            public DateTime Created;
        }

        private static readonly List<Announcement> List = new List<Announcement>();
        private static readonly Dictionary<int, DateTime> LastPost = new Dictionary<int, DateTime>();
        private static bool _ready;

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            try
            {
                using (var create = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS `army_announcements` (
                    `id` BIGINT NOT NULL,
                    `author_uuid` INT NOT NULL,
                    `author_name` VARCHAR(64) NOT NULL,
                    `rank_name` VARCHAR(64) NOT NULL DEFAULT '',
                    `title` VARCHAR(80) NOT NULL,
                    `text` TEXT NOT NULL,
                    `created` DATETIME NOT NULL,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"))
                    MySQL.Query(create);

                NeptuneEvo.Database.DbQueue.ReadThen(
                    $"SELECT * FROM `army_announcements` ORDER BY `id` DESC LIMIT {MaxCount}",
                    table =>
                    {
                        if (table != null)
                        {
                            List.Clear();
                            foreach (DataRow row in table.Rows)
                            {
                                List.Add(new Announcement
                                {
                                    Id = Convert.ToInt64(row["id"]),
                                    AuthorUuid = Convert.ToInt32(row["author_uuid"]),
                                    Author = Convert.ToString(row["author_name"]),
                                    Rank = Convert.ToString(row["rank_name"]),
                                    Title = Convert.ToString(row["title"]),
                                    Text = Convert.ToString(row["text"]),
                                    Created = Convert.ToDateTime(row["created"]),
                                });
                            }
                        }
                        _ready = true;
                    });
            }
            catch (Exception e)
            {
                Log.Write($"ArmyBoard start Exception: {e}");
            }
        }

        private static int Rank(ExtPlayer player) => player.GetFractionMemberData()?.Rank ?? 0;

        public static bool CanModerate(ExtPlayer player) => ArmyUtil.IsArmy(player) && Rank(player) >= ModerateRank;

        /// <summary>Список для окна: у каждого объявления флаг canDelete для этого игрока.</summary>
        public static object ListFor(ExtPlayer player)
        {
            var uuid = player.GetUUID();
            var moderate = CanModerate(player);
            return List.Select(a => new
            {
                id = a.Id.ToString(),
                author = a.Author.Replace('_', ' '),
                rank = a.Rank,
                title = a.Title,
                text = a.Text,
                date = a.Created.ToString("dd.MM HH:mm"),
                canDelete = moderate || a.AuthorUuid == uuid,
            }).ToList();
        }

        private static void Broadcast()
        {
            foreach (var p in Character.Repository.GetPlayers().Where(ArmyUtil.IsArmy))
                Trigger.ClientEvent(p, "client.army.board.list", JsonConvert.SerializeObject(ListFor(p)));
        }

        private static void Result(ExtPlayer player, string text, bool ok) =>
            Trigger.ClientEvent(player, "client.army.board.result", text, ok);

        private static string Clean(string s, int max)
        {
            s = (s ?? "").Replace("\r", "").Trim();
            s = new string(s.Where(c => c == '\n' || !char.IsControl(c)).ToArray());
            return s.Length > max ? s.Substring(0, max) : s;
        }

        [RemoteEvent("server.army.board.post")]
        public static void OnPost(ExtPlayer player, string title, string text)
        {
            try
            {
                if (!ArmyUtil.IsOfficer(player))
                {
                    Result(player, "Писать объявления могут только офицеры", false);
                    return;
                }
                if (!_ready)
                {
                    Result(player, "Доска ещё загружается — повторите через секунду", false);
                    return;
                }
                title = Clean(title, TitleMax);
                text = Clean(text, TextMax);
                if (title.Length < 3 || text.Length < 5)
                {
                    Result(player, "Заполните заголовок (от 3 символов) и текст (от 5 символов)", false);
                    return;
                }
                var uuid = player.GetUUID();
                if (LastPost.TryGetValue(uuid, out var last) && (DateTime.Now - last).TotalSeconds < 60)
                {
                    Result(player, "Не чаще одного объявления в минуту", false);
                    return;
                }
                LastPost[uuid] = DateTime.Now;

                var a = new Announcement
                {
                    Id = DateTime.UtcNow.Ticks,
                    AuthorUuid = uuid,
                    Author = player.Name,
                    Rank = Manager.GetFractionRankName(ArmyUtil.ArmyId, Rank(player)) ?? "",
                    Title = title,
                    Text = text,
                    Created = DateTime.Now,
                };
                List.Insert(0, a);
                while (List.Count > MaxCount)
                    List.RemoveAt(List.Count - 1);
                NeptuneEvo.Database.DbQueue.Enqueue(
                    "INSERT INTO `army_announcements` (`id`,`author_uuid`,`author_name`,`rank_name`,`title`,`text`,`created`) VALUES (@i,@u,@n,@r,@t,@x,@c)",
                    ("@i", a.Id), ("@u", a.AuthorUuid), ("@n", a.Author), ("@r", a.Rank), ("@t", a.Title), ("@x", a.Text), ("@c", a.Created));

                Result(player, "Объявление опубликовано", true);
                Broadcast();
                ArmyUtil.Radio($"Новое объявление на доске нарядов: «{title}» ({a.Rank} {player.Name.Replace('_', ' ')})");
            }
            catch (Exception e)
            {
                Log.Write($"ArmyBoard OnPost Exception: {e}");
            }
        }

        [RemoteEvent("server.army.board.delete")]
        public static void OnDelete(ExtPlayer player, string idText)
        {
            try
            {
                if (!ArmyUtil.IsArmy(player) || !long.TryParse(idText, out var id))
                    return;
                var a = List.FirstOrDefault(x => x.Id == id);
                if (a == null)
                    return;
                if (!CanModerate(player) && a.AuthorUuid != player.GetUUID())
                {
                    Result(player, "Удалять можно свои объявления (чужие — с 9 ранга)", false);
                    return;
                }
                List.Remove(a);
                NeptuneEvo.Database.DbQueue.Enqueue("DELETE FROM `army_announcements` WHERE `id`=@i", ("@i", id));
                Result(player, "Объявление удалено", true);
                Broadcast();
            }
            catch (Exception e)
            {
                Log.Write($"ArmyBoard OnDelete Exception: {e}");
            }
        }
    }
}
