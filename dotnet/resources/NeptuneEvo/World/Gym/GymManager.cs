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
    /// Пока только анимации, без прокачки характеристик.
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
                    _spots = JsonConvert.DeserializeObject<List<GymSpot>>(File.ReadAllText(FilePath)) ?? new List<GymSpot>();
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

        /// <summary>Площадка у плаца Форт Занкудо (рядом с маршрутом патруля армии).</summary>
        private static List<GymSpot> DefaultSpots()
        {
            var baseX = -2290.0;
            var baseY = 3290.0;
            var z = 32.2;
            return new List<GymSpot>
            {
                new GymSpot { Type = "chinup", Model = Models["chinup"], Position = new Vector3(baseX, baseY, z), Heading = 150f },
                new GymSpot { Type = "chinup", Model = Models["chinup"], Position = new Vector3(baseX + 3, baseY + 1.5, z), Heading = 150f },
                new GymSpot { Type = "bench", Model = Models["bench"], Position = new Vector3(baseX + 6, baseY + 3, z), Heading = 150f },
                new GymSpot { Type = "weights", Model = Models["weights"], Position = new Vector3(baseX + 9, baseY + 4.5, z), Heading = 150f },
                new GymSpot { Type = "mat", Model = Models["mat"], Position = new Vector3(baseX + 2, baseY - 3, z), Heading = 150f },
                new GymSpot { Type = "mat", Model = Models["mat"], Position = new Vector3(baseX + 4, baseY - 2, z), Heading = 150f },
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

                Busy[player] = position;
                Trigger.ClientEvent(player, "client.gym.yes", x, y, z, heading);
                Trigger.StopAnimation(player);
                player.SetSharedData("AnimToKey", animKey);
            }
            catch (Exception e)
            {
                Log.Write($"Start Exception: {e}");
            }
        }

        [RemoteEvent("server.gym.stop")]
        public static void Stop(ExtPlayer player)
        {
            try
            {
                if (!Busy.Remove(player))
                    return;
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
                    case "list":
                        player.SendChatMessage($"Своих тренажёров: {_spots.Count}. Типы: " + string.Join(", ", _spots.GroupBy(s => s.Type).Select(g => $"{g.Key} {g.Count()}")));
                        return;
                    default:
                        player.SendChatMessage("/gym add chinup|bench|weights|mat, /gym del, /gym list");
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
