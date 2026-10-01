using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.World.Gym
{
    /// <summary>
    /// Уличная качалка: турники, скамьи, стойки со штангой и коврики по карте.
    /// Клиент (src_client/world/gym.js) сам находит тренажёр рядом (объекты мира Muscle Beach и тюрьмы + наши из settings/gym.json)
    /// и просит начать упражнение; сервер проверяет и включает анимацию через shared data AnimToKey (видят все).
    /// /gym add chinup|bench|weights|mat — поставить тренажёр перед собой, /gym del — убрать ближайший, /gym list.
    /// Прокачка силы/выносливости и платные зоны (абонемент) — World/Gym/Fitness.cs.
    /// </summary>
    class GymManager : Script
    {
        private static readonly nLog Log = new nLog("World.Gym");
        private static string FilePath => Path.Combine("settings", "gym.json");

        public class GymSpot
        {
            [JsonProperty("type")] public string Type { get; set; }
            [JsonProperty("model")] public string Model { get; set; }
            [JsonProperty("position")] public Vector3 Position { get; set; }
            [JsonProperty("heading")] public float Heading { get; set; }
        }

        /// <summary>Тип тренажёра → модель объекта (клиент узнаёт тренажёр по модели).</summary>
        private static readonly Dictionary<string, string> Models = new Dictionary<string, string>
        {
            { "chinup", "prop_a_base_bars_01" },
            { "bench", "prop_muscle_bench_03" },
            { "weights", "prop_weight_rack_02" },
            { "mat", "prop_yoga_mat_02" },
        };

        /// <summary>Упражнения, которые клиент может запросить → ключ анимации (synchronization/animation.js).</summary>
        private static readonly Dictionary<string, string> Exercises = new Dictionary<string, string>
        {
            { "chinup", "gym_chinup" },
            { "bench", "gym_bench" },
            { "weights", "gym_weights" },
            { "situps", "gym_situps" },
            { "pushups", "gym_pushups" },
            { "yoga", "gym_yoga" },
            { "stretch", "gym_stretch" },
            { "flex", "gym_flex" },
            { "jog", "gym_jog" },
        };

        private static List<GymSpot> _spots = new List<GymSpot>();
        private static readonly List<GTANetworkAPI.Object> Objects = new List<GTANetworkAPI.Object>();
        /// <summary>Кто сейчас занимается и где (чтобы двое не встали в одну точку).</summary>
        private static readonly Dictionary<ExtPlayer, Vector3> Busy = new Dictionary<ExtPlayer, Vector3>();

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    _spots = JsonConvert.DeserializeObject<List<GymSpot>>(File.ReadAllText(FilePath)) ?? new List<GymSpot>();
                    // Старая площадка у плаца (-2290, 3290) заменена на 4 тренажёра у казарм — переносим автоматически
                    var oldCenter = new Vector3(-2290.0, 3290.0, 32.2);
                    if (_spots.RemoveAll(s => s.Position != null && s.Position.DistanceTo2D(oldCenter) < 15f) > 0)
                    {
                        _spots.AddRange(DefaultSpots());
                        Save();
                    }
                }
                else
                {
                    _spots = DefaultSpots();
                    Save();
                }
                SpawnAll();
                Log.Write($"Качалка: своих тренажёров {_spots.Count}", nLog.Type.Success);
            }
            catch (Exception e)
            {
                Log.Write($"OnResourceStart Exception: {e}");
            }
        }

        /// <summary>Форт Занкудо: 2 турника, скамья и стойка со штангой (точки от администрации, z — земля = позиция игрока − 1).</summary>
        private static List<GymSpot> DefaultSpots()
        {
            GymSpot Spot(string type, double x, double y, float heading) =>
                new GymSpot { Type = type, Model = Models[type], Position = new Vector3(x, y, 32.96023 - 1.0), Heading = heading };
            return new List<GymSpot>
            {
                Spot("chinup", -1930.082, 3300.6873, 58.528038f),
                Spot("chinup", -1926.9496, 3305.2122, 55.852325f),
                Spot("bench", -1924.4175, 3308.5217, 52.93026f),
                Spot("weights", -1922.2251, 3311.5046, 56.035675f),
            };
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory("settings");
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(_spots, Formatting.Indented));
            }
            catch (Exception e)
            {
                Log.Write($"Не удалось сохранить {FilePath}: {e.Message}");
            }
        }

        private static void SpawnAll()
        {
            foreach (var obj in Objects)
                if (obj != null && obj.Exists)
                    obj.Delete();
            Objects.Clear();
            foreach (var spot in _spots)
            {
                if (spot?.Position == null || string.IsNullOrEmpty(spot.Model))
                    continue;
                Objects.Add(NAPI.Object.CreateObject(NAPI.Util.GetHashKey(spot.Model), spot.Position, new Vector3(0, 0, spot.Heading), 255, 0));
            }
        }

        [RemoteEvent("server.gym.start")]
        public static void Start(ExtPlayer player, string exercise, float x, float y, float z, float heading)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null || !player.IsCharacterData())
                    return;
                if (!Exercises.TryGetValue(exercise ?? "", out var animKey))
                    return;
                if (player.IsInVehicle || sessionData.CuffedData.Cuffed || sessionData.DeathData.InDeath)
                    return;

                var position = new Vector3(x, y, z);
                if (player.Position.DistanceTo(position) > 3.5f)
                    return;
                if (Busy.Any(b => b.Key != player && b.Value.DistanceTo(position) < 0.7f))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Тренажёр занят", 3000);
                    return;
                }

                var paidZone = Fitness.PaidZoneAt(position);
                if (paidZone != null && !Fitness.IsLoaded(player))
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Секунду, загружаем ваш абонемент — нажмите ещё раз", 2500);
                    return;
                }
                if (paidZone != null && !Fitness.HasMembership(player))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Здесь нужен абонемент — купите его у тренера ({paidZone.Name})", 4000);
                    return;
                }

                Busy[player] = position;
                Fitness.OnTrainingStart(player, exercise);
                Trigger.ClientEvent(player, "client.gym.yes", x, y, z, heading);
                Trigger.StopAnimation(player);
                player.SetSharedData("AnimToKey", animKey);
            }
            catch (Exception e)
            {
                Log.Write($"Start Exception: {e}");
            }
        }

        /// <summary>Стрелки ← → во время упражнения: другое упражнение на том же снаряде.</summary>
        [RemoteEvent("server.gym.switch")]
        public static void Switch(ExtPlayer player, string exercise)
        {
            try
            {
                if (!Busy.ContainsKey(player) || !player.IsCharacterData())
                    return;
                if (!Exercises.TryGetValue(exercise ?? "", out var animKey))
                    return;
                Fitness.OnTrainingStart(player, exercise);
                Trigger.StopAnimation(player);
                player.SetSharedData("AnimToKey", animKey);
            }
            catch (Exception e)
            {
                Log.Write($"Switch Exception: {e}");
            }
        }

        [RemoteEvent("server.gym.stop")]
        public static void Stop(ExtPlayer player)
        {
            try
            {
                if (!Busy.Remove(player))
                    return;
                Fitness.OnTrainingStop(player);
                if (player.IsCharacterData())
                    player.SetSharedData("AnimToKey", 0);
            }
            catch (Exception e)
            {
                Log.Write($"Stop Exception: {e}");
            }
        }

        private static void ForceStop(ExtPlayer player)
        {
            if (!Busy.ContainsKey(player))
                return;
            Stop(player);
            Trigger.ClientEvent(player, "client.gym.stopped");
        }

        [ServerEvent(Event.PlayerDeath)]
        public void OnPlayerDeath(ExtPlayer player, ExtPlayer killer, uint weapon) => ForceStop(player);

        [ServerEvent(Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(ExtPlayer player, DisconnectionType type, string reason) => Busy.Remove(player);

        [Command(AdminCommands.gym, GreedyArg = true)]
        public static void CMD_Gym(ExtPlayer player, string args = "")
        {
            try
            {
                if (!player.IsCharacterData() || !CommandsAccess.CanUseCmd(player, AdminCommands.gym))
                    return;
                var parts = (args ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var sub = parts.Length > 0 ? parts[0].ToLower() : "";
                switch (sub)
                {
                    case "add":
                        {
                            var type = parts.Length > 1 ? parts[1].ToLower() : "";
                            if (!Models.ContainsKey(type))
                            {
                                player.SendChatMessage("/gym add chinup|bench|weights|mat — тренажёр встанет в 1 м перед тобой");
                                return;
                            }
                            var heading = player.Heading;
                            var rad = heading * Math.PI / 180.0;
                            var pos = player.Position;
                            // Перед игроком на 1 м, на уровне земли (позиция игрока — на ~1 м выше земли)
                            var spot = new GymSpot
                            {
                                Type = type,
                                Model = Models[type],
                                Position = new Vector3(pos.X - Math.Sin(rad), pos.Y + Math.Cos(rad), pos.Z - 1.0),
                                Heading = heading,
                            };
                            _spots.Add(spot);
                            Objects.Add(NAPI.Object.CreateObject(NAPI.Util.GetHashKey(spot.Model), spot.Position, new Vector3(0, 0, spot.Heading), 255, 0));
                            Save();
                            GameLog.Admin(player.Name, $"gym add {type}", "");
                            player.SendChatMessage($"Тренажёр «{type}» поставлен и сохранён ({_spots.Count} всего)");
                            return;
                        }
                    case "del":
                        {
                            var nearest = _spots
                                .Select((s, i) => (s, i, d: s.Position.DistanceTo(player.Position)))
                                .Where(t => t.d < 4f)
                                .OrderBy(t => t.d)
                                .FirstOrDefault();
                            if (nearest.s == null)
                            {
                                player.SendChatMessage("Рядом (4 м) нет поставленных тренажёров. Объекты мира не удаляются.");
                                return;
                            }
                            _spots.RemoveAt(nearest.i);
                            Save();
                            SpawnAll();
                            GameLog.Admin(player.Name, $"gym del {nearest.s.Type}", "");
                            player.SendChatMessage($"Тренажёр «{nearest.s.Type}» убран");
                            return;
                        }
                    case "zone":
                        {
                            // /gym zone add цена дни [радиус] — платная зона с центром и тренером на месте админа; /gym zone del — ближайшую
                            var action = parts.Length > 1 ? parts[1].ToLower() : "";
                            if (action == "add" && parts.Length >= 4 && int.TryParse(parts[2], out var price) && int.TryParse(parts[3], out var days) && price >= 0 && days > 0)
                            {
                                var radius = parts.Length > 4 && float.TryParse(parts[4], out var r) ? r : 25f;
                                Fitness.Cfg.PaidZones.Add(new GymPaidZone
                                {
                                    Name = $"Зал {Fitness.Cfg.PaidZones.Count + 1}",
                                    Center = player.Position,
                                    Radius = radius,
                                    Price = price,
                                    Days = days,
                                    NpcPosition = player.Position,
                                    NpcHeading = player.Heading,
                                });
                                Fitness.SaveConfig();
                                Fitness.SpawnTrainers();
                                GameLog.Admin(player.Name, $"gym zone add {price} {days} {radius}", "");
                                player.SendChatMessage($"Платная зона добавлена: ${price} за {days} дн., радиус {radius} м, тренер на твоём месте. Отойди — NPC появится.");
                                return;
                            }
                            if (action == "del")
                            {
                                var zone = Fitness.Cfg.PaidZones.OrderBy(z => z.Center.DistanceTo(player.Position)).FirstOrDefault();
                                if (zone == null || zone.Center.DistanceTo(player.Position) > zone.Radius + 10)
                                {
                                    player.SendChatMessage("Рядом нет платной зоны");
                                    return;
                                }
                                Fitness.Cfg.PaidZones.Remove(zone);
                                Fitness.SaveConfig();
                                Fitness.SpawnTrainers();
                                GameLog.Admin(player.Name, $"gym zone del {zone.Name}", "");
                                player.SendChatMessage($"Платная зона «{zone.Name}» удалена — тренажёры там теперь бесплатные");
                                return;
                            }
                            player.SendChatMessage("/gym zone add цена дни [радиус], /gym zone del. Зоны: " +
                                                   string.Join(", ", Fitness.Cfg.PaidZones.Select(z => $"{z.Name} ${z.Price}/{z.Days}д")));
                            return;
                        }
                    case "list":
                        player.SendChatMessage($"Своих тренажёров: {_spots.Count}. Типы: " + string.Join(", ", _spots.GroupBy(s => s.Type).Select(g => $"{g.Key} {g.Count()}")));
                        return;
                    default:
                        player.SendChatMessage("/gym add chinup|bench|weights|mat, /gym del, /gym list, /gym zone");
                        return;
                }
            }
            catch (Exception e)
            {
                Log.Write($"CMD_Gym Exception: {e}");
            }
        }
    }
}
