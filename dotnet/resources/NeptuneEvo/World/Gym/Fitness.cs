using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using GTANetworkAPI;
using MySqlConnector;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using NeptuneEvo.Quests;
using NeptuneEvo.Quests.Models;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.World.Gym
{
    public class GymPaidZone
    {
        [JsonProperty("name")] public string Name { get; set; } = "Muscle Beach";
        [JsonProperty("center")] public Vector3 Center { get; set; }
        [JsonProperty("radius")] public float Radius { get; set; } = 30f;
        [JsonProperty("price")] public int Price { get; set; } = 2500;
        [JsonProperty("days")] public int Days { get; set; } = 7;
        [JsonProperty("npcPosition")] public Vector3 NpcPosition { get; set; }
        [JsonProperty("npcHeading")] public float NpcHeading { get; set; }
        [JsonProperty("npcModel")] public string NpcModel { get; set; } = "a_m_y_musclbeac_01";
        /// <summary>Тарифы абонемента в окне тренера (CEF GymTrainer).</summary>
        [JsonProperty("plans")] public List<GymPlan> Plans { get; set; } = new List<GymPlan>();
    }

    public class GymPlan
    {
        [JsonProperty("days")] public int Days { get; set; }
        [JsonProperty("price")] public int Price { get; set; }
    }

    /// <summary>Настройки фитнеса и платных залов: settings/gym_fitness.json (отдельно от списка тренажёров gym.json).</summary>
    public class FitnessConfig
    {
        [JsonProperty("tickSeconds")] public int TickSeconds { get; set; } = 30;
        /// <summary>Сколько очков показателя можно получить за час (на каждый показатель).</summary>
        [JsonProperty("hourLimit")] public int HourLimit { get; set; } = 2;
        [JsonProperty("decayPerDay")] public int DecayPerDay { get; set; } = 1;
        [JsonProperty("decayFloor")] public int DecayFloor { get; set; } = 30;
        /// <summary>Максимальная прибавка к урону кулаком/холодным при силе 100 (0.25 = +25%).</summary>
        [JsonProperty("meleeMaxBonus")] public double MeleeMaxBonus { get; set; } = 0.25;
        [JsonProperty("paidZones")] public List<GymPaidZone> PaidZones { get; set; } = new List<GymPaidZone>();
    }

    /// <summary>
    /// Сила и выносливость от тренировок + абонемент в платные залы.
    ///  Пока игрок занимается (GymManager), каждые tickSeconds +1 к показателю упражнения (не больше hourLimit в час на показатель).
    ///  Турник/скамья/штанга — сила, йога — выносливость, пресс/отжимания — по очереди, полоса армии — выносливость.
    ///  Без тренировок показатели медленно падают (decayPerDay в сутки, не ниже decayFloor).
    ///  Выносливость → статистика SP0/MP0_STAMINA (дольше бег), сила → STRENGTH и shared data fitStr (урон кулаком чуть выше).
    ///  Платные зоны (по умолчанию Muscle Beach): заниматься можно с абонементом, его продаёт тренер-NPC.
    /// </summary>
    class Fitness : Script
    {
        private static readonly nLog Log = new nLog("World.Fitness");
        private static string ConfigPath => Path.Combine("settings", "gym_fitness.json");
        public static FitnessConfig Cfg { get; private set; } = new FitnessConfig();

        private class Data
        {
            public int Stamina = 30;
            public int Strength = 30;
            public int HourGainStamina;
            public int HourGainStrength;
            public DateTime HourKey = DateTime.MinValue;
            public DateTime LastTraining = DateTime.Now;
            public DateTime MemberUntil = DateTime.MinValue;
            public bool Dirty;
        }

        private static readonly Dictionary<int, Data> Players = new Dictionary<int, Data>();
        /// <summary>Кто сейчас тренируется: игрок → упражнение, счётчик для чередования.</summary>
        private static readonly Dictionary<ExtPlayer, (string exercise, int counter)> Training = new Dictionary<ExtPlayer, (string, int)>();
        private static readonly List<ExtPed> Trainers = new List<ExtPed>();
        private static bool _ready;

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            try
            {
                LoadConfig();
                using (var create = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS `player_fitness` (
                    `uuid` INT NOT NULL,
                    `stamina` INT NOT NULL DEFAULT 30,
                    `strength` INT NOT NULL DEFAULT 30,
                    `last_training` DATETIME NOT NULL,
                    `member_until` DATETIME NULL,
                    PRIMARY KEY (`uuid`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"))
                    MySQL.Query(create);
                _ready = true;
                SpawnTrainers();
                Timers.Start("gym.fitness", Math.Max(10, Cfg.TickSeconds) * 1000, Tick, true);
            }
            catch (Exception e)
            {
                Log.Write($"Fitness start Exception: {e}");
            }
        }

        private static void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                    Cfg = JsonConvert.DeserializeObject<FitnessConfig>(File.ReadAllText(ConfigPath)) ?? new FitnessConfig();
                if (Cfg.PaidZones == null || Cfg.PaidZones.Count == 0 && !File.Exists(ConfigPath))
                {
                    // Muscle Beach (Веспуччи): открытый зал у пляжа
                    Cfg.PaidZones = new List<GymPaidZone>
                    {
                        new GymPaidZone
                        {
                            Name = "Muscle Beach",
                            Center = new Vector3(-1203.5, -1567.5, 4.6),
                            Radius = 28f,
                            NpcPosition = new Vector3(-1195.9, -1576.6, 4.6),
                            NpcHeading = 40f,
                        },
                    };
                }
                foreach (var zone in Cfg.PaidZones)
                    if (zone.Plans == null || zone.Plans.Count == 0)
                        zone.Plans = new List<GymPlan>
                        {
                            new GymPlan { Days = 1, Price = 500 },
                            new GymPlan { Days = 7, Price = 2500 },
                            new GymPlan { Days = 30, Price = 8000 },
                        };
                SaveConfig();
            }
            catch (Exception e)
            {
                Log.Write($"LoadConfig Exception: {e.Message}");
            }
        }

        public static void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory("settings");
                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(Cfg, Formatting.Indented));
            }
            catch (Exception e)
            {
                Log.Write($"SaveConfig Exception: {e.Message}");
            }
        }

        public static void SpawnTrainers()
        {
            foreach (var ped in Trainers)
                PedSystem.Repository.DestroyQuest(ped);
            Trainers.Clear();
            for (var i = 0; i < Cfg.PaidZones.Count; i++)
            {
                var zone = Cfg.PaidZones[i];
                if (zone.NpcPosition == null)
                    continue;
                Trainers.Add(PedSystem.Repository.CreateQuest(zone.NpcModel ?? "a_m_y_musclbeac_01", zone.NpcPosition, zone.NpcHeading, 0, QuestName,
                    ColShapeEnums.GymTrainer, $"~g~Тренер\n~w~{zone.Name}", false));
            }
        }

        // ------------------------------------------------------------------ данные

        private static Data Get(ExtPlayer player)
        {
            var uuid = player.GetUUID();
            if (Players.TryGetValue(uuid, out var data))
                return data;
            data = new Data();
            if (_ready)
            {
                using var table = NeptuneEvo.Database.DbQueue.Read("SELECT `stamina`,`strength`,`last_training`,`member_until` FROM `player_fitness` WHERE `uuid`=@u", ("@u", uuid));
                if (table != null && table.Rows.Count > 0)
                {
                    var row = table.Rows[0];
                    data.Stamina = Convert.ToInt32(row["stamina"]);
                    data.Strength = Convert.ToInt32(row["strength"]);
                    data.LastTraining = Convert.ToDateTime(row["last_training"]);
                    data.MemberUntil = row["member_until"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(row["member_until"]);
                    // Без тренировок форма уходит: за каждые полные сутки простоя (после первых) — минус decayPerDay
                    var idleDays = (int) (DateTime.Now - data.LastTraining).TotalDays - 1;
                    if (idleDays > 0 && Cfg.DecayPerDay > 0)
                    {
                        data.Stamina = Decay(data.Stamina, idleDays * Cfg.DecayPerDay);
                        data.Strength = Decay(data.Strength, idleDays * Cfg.DecayPerDay);
                        data.LastTraining = DateTime.Now.AddDays(-1);
                        data.Dirty = true;
                    }
                }
                else
                    data.Dirty = true;
            }
            Players[uuid] = data;
            return data;
        }

        private static int Decay(int value, int by) => value <= Cfg.DecayFloor ? value : Math.Max(Cfg.DecayFloor, value - by);

        private static void Save(int uuid, Data data)
        {
            if (!_ready || !data.Dirty)
                return;
            data.Dirty = false;
            NeptuneEvo.Database.DbQueue.Enqueue(
                @"INSERT INTO `player_fitness` (`uuid`,`stamina`,`strength`,`last_training`,`member_until`) VALUES (@u,@s,@st,@l,@m)
                  ON DUPLICATE KEY UPDATE `stamina`=VALUES(`stamina`),`strength`=VALUES(`strength`),`last_training`=VALUES(`last_training`),`member_until`=VALUES(`member_until`)",
                ("@u", uuid), ("@s", data.Stamina), ("@st", data.Strength), ("@l", data.LastTraining),
                ("@m", data.MemberUntil == DateTime.MinValue ? (object) DBNull.Value : data.MemberUntil));
        }

        private static void Apply(ExtPlayer player, Data data)
        {
            Trigger.ClientEvent(player, "client.fitness.apply", data.Stamina, data.Strength, Cfg.MeleeMaxBonus);
            player.SetSharedData("fitStr", data.Strength);
        }

        /// <summary>+1 к показателю с учётом часового лимита. stat: "stamina" | "strength".</summary>
        public static void Gain(ExtPlayer player, string stat)
        {
            var data = Get(player);
            var hour = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, 0, 0);
            if (data.HourKey != hour)
            {
                data.HourKey = hour;
                data.HourGainStamina = 0;
                data.HourGainStrength = 0;
            }
            data.LastTraining = DateTime.Now;
            data.Dirty = true;
            if (stat == "stamina")
            {
                if (data.HourGainStamina >= Cfg.HourLimit || data.Stamina >= 100)
                    return;
                data.HourGainStamina++;
                data.Stamina++;
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Выносливость +1 ({data.Stamina}/100)", 2500);
            }
            else
            {
                if (data.HourGainStrength >= Cfg.HourLimit || data.Strength >= 100)
                    return;
                data.HourGainStrength++;
                data.Strength++;
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Сила +1 ({data.Strength}/100)", 2500);
            }
            Apply(player, data);
        }

        // ------------------------------------------------------------------ тренировки (из GymManager)

        public static GymPaidZone PaidZoneAt(Vector3 position) =>
            Cfg.PaidZones.FirstOrDefault(z => z.Center != null && z.Center.DistanceTo2D(position) <= z.Radius);

        public static bool HasMembership(ExtPlayer player) => Get(player).MemberUntil > DateTime.Now;

        public static void OnTrainingStart(ExtPlayer player, string exercise) => Training[player] = (exercise, 0);

        public static void OnTrainingStop(ExtPlayer player) => Training.Remove(player);

        private static void Tick()
        {
            try
            {
                foreach (var (player, (exercise, counter)) in Training.ToList())
                {
                    if (player == null || !player.IsCharacterData())
                    {
                        Training.Remove(player);
                        continue;
                    }
                    Training[player] = (exercise, counter + 1);
                    switch (exercise)
                    {
                        case "chinup":
                            // Подтягивания тяжелее: 1 очко силы за 3 подхода (тика)
                            if ((counter + 1) % 3 == 0)
                                Gain(player, "strength");
                            break;
                        case "bench":
                        case "weights":
                        case "curls":
                            Gain(player, "strength");
                            break;
                        case "yoga":
                        case "stretch":
                        case "jog":
                            Gain(player, "stamina");
                            break;
                        default: // пресс, отжимания — по очереди
                            Gain(player, counter % 2 == 0 ? "strength" : "stamina");
                            break;
                    }
                }
                // Новым игрокам в сети — применить показатели; изменённое — сохранить
                foreach (var player in Character.Repository.GetPlayers())
                {
                    if (!player.IsCharacterData())
                        continue;
                    var uuid = player.GetUUID();
                    var isNew = !Players.ContainsKey(uuid);
                    var data = Get(player);
                    if (isNew)
                        Apply(player, data);
                    Save(uuid, data);
                }
            }
            catch (Exception e)
            {
                Log.Write($"Fitness Tick Exception: {e}");
            }
        }

        [ServerEvent(Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(ExtPlayer player, DisconnectionType type, string reason)
        {
            try
            {
                Training.Remove(player);
                var uuid = player.GetUUID();
                if (Players.TryGetValue(uuid, out var data))
                {
                    Save(uuid, data);
                    Players.Remove(uuid);
                }
            }
            catch (Exception e)
            {
                Log.Write($"Fitness disconnect Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ абонемент

        private static readonly Dictionary<ExtPlayer, GymPaidZone> PendingPurchase = new Dictionary<ExtPlayer, GymPaidZone>();

        [Interaction(ColShapeEnums.GymTrainer)]
        public static void OnTrainer(ExtPlayer player, int pedIndex)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null || !player.IsCharacterData() || sessionData.CuffedData.Cuffed || sessionData.DeathData.InDeath)
                    return;
                var zone = PaidZoneAt(player.Position) ?? Cfg.PaidZones.OrderBy(z => z.NpcPosition?.DistanceTo(player.Position) ?? 9999).FirstOrDefault();
                if (zone == null)
                    return;
                var data = Get(player);
                ResetHourIfNeeded(data);
                PendingPurchase[player] = zone;
                // Окно в стиле «Центра занятости» (CEF GymTrainer, клиент world/gym.js)
                Trigger.ClientEvent(player, "client.gym.trainer.open", TrainerJson(player, zone));
            }
            catch (Exception e)
            {
                Log.Write($"OnTrainer Exception: {e}");
            }
        }

        public const string QuestName = "npc_gym";

        private static string TrainerJson(ExtPlayer player, GymPaidZone zone)
        {
            var data = Get(player);
            ResetHourIfNeeded(data);
            return JsonConvert.SerializeObject(new
            {
                zone = zone.Name,
                member = data.MemberUntil > DateTime.Now ? data.MemberUntil.ToString("dd.MM.yyyy HH:mm") : "",
                plans = zone.Plans,
                str = data.Strength,
                sta = data.Stamina,
                strLeft = data.Strength >= 100 ? 0 : Math.Max(0, Cfg.HourLimit - data.HourGainStrength),
                staLeft = data.Stamina >= 100 ? 0 : Math.Max(0, Cfg.HourLimit - data.HourGainStamina),
                hourLimit = Cfg.HourLimit,
                money = player.GetCharacterData()?.Money ?? 0,
            });
        }

        /// <summary>Окно тренера → «Купить»/«Продлить» выбранный тариф.</summary>
        [RemoteEvent("server.gym.buy")]
        public static void OnBuyPlan(ExtPlayer player, int planIndex)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null || !PendingPurchase.TryGetValue(player, out var zone))
                    return;
                void Reply(string text, bool ok) =>
                    Trigger.ClientEvent(player, "client.gym.trainer.update", TrainerJson(player, zone), text, ok);
                if (zone.NpcPosition != null && player.Position.DistanceTo(zone.NpcPosition) > 6f)
                {
                    Reply("Подойдите ближе к тренеру", false);
                    return;
                }
                if (planIndex < 0 || planIndex >= zone.Plans.Count)
                    return;
                var plan = zone.Plans[planIndex];
                if (characterData.Money < plan.Price)
                {
                    Reply("Недостаточно наличных", false);
                    return;
                }
                MoneySystem.Wallet.Change(player, -plan.Price);
                GameLog.Money($"player({characterData.UUID})", "server", plan.Price, $"gymMembership({plan.Days}d)");
                var data = Get(player);
                var from = data.MemberUntil > DateTime.Now ? data.MemberUntil : DateTime.Now;
                data.MemberUntil = from.AddDays(plan.Days);
                data.Dirty = true;
                Save(characterData.UUID, data);
                Reply($"Абонемент действует до {data.MemberUntil:dd.MM.yyyy HH:mm}", true);
            }
            catch (Exception e)
            {
                Log.Write($"OnBuyPlan Exception: {e}");
            }
        }

        [RemoteEvent("server.gym.trainer.close")]
        public static void OnTrainerClose(ExtPlayer player) => PendingPurchase.Remove(player);

        private static void ResetHourIfNeeded(Data data)
        {
            var hour = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, 0, 0);
            if (data.HourKey == hour)
                return;
            data.HourKey = hour;
            data.HourGainStamina = 0;
            data.HourGainStrength = 0;
        }

        /// <summary>Показатели для F3 → Навыки: сила, выносливость и сколько ещё можно прибавить в этот час.</summary>
        public static (int strength, int stamina, int strengthLeft, int staminaLeft) GetStats(ExtPlayer player)
        {
            var data = Get(player);
            ResetHourIfNeeded(data);
            return (data.Strength, data.Stamina,
                data.Strength >= 100 ? 0 : Math.Max(0, Cfg.HourLimit - data.HourGainStrength),
                data.Stamina >= 100 ? 0 : Math.Max(0, Cfg.HourLimit - data.HourGainStamina));
        }

        /// <summary>Кнопка «Купить абонемент» в окне тренера (qMain → server.quest.perform).</summary>
        public static void BuyMembership(ExtPlayer player)
        {
            try
            {
                if (!PendingPurchase.Remove(player, out var zone))
                    return;
                var characterData = player.GetCharacterData();
                if (characterData == null)
                    return;
                if (characterData.Money < zone.Price)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Недостаточно денег", 3000);
                    return;
                }
                MoneySystem.Wallet.Change(player, -zone.Price);
                GameLog.Money($"player({characterData.UUID})", "server", zone.Price, "gymMembership");
                var data = Get(player);
                var from = data.MemberUntil > DateTime.Now ? data.MemberUntil : DateTime.Now;
                data.MemberUntil = from.AddDays(zone.Days);
                data.Dirty = true;
                Save(characterData.UUID, data);
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Абонемент в {zone.Name} до {data.MemberUntil:dd.MM HH:mm}", 5000);
            }
            catch (Exception e)
            {
                Log.Write($"BuyMembership Exception: {e}");
            }
        }

        [Command("fitness")]
        public static void CMD_Fitness(ExtPlayer player)
        {
            if (!player.IsCharacterData())
                return;
            var data = Get(player);
            player.SendChatMessage($"~y~[Форма] ~w~Сила {data.Strength}/100, выносливость {data.Stamina}/100. " +
                                   (data.MemberUntil > DateTime.Now ? $"Абонемент до {data.MemberUntil:dd.MM HH:mm}" : "Абонемента нет"));
        }
    }
}
