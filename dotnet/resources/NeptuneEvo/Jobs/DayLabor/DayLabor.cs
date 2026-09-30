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

namespace NeptuneEvo.Jobs.DayLabor
{
    public class DayLaborPoint
    {
        [JsonProperty("position")] public Vector3 Position { get; set; }
        [JsonProperty("heading")] public float Heading { get; set; }
    }

    /// <summary>Настройки подработок: settings/daylabor.json. Точки переставляются в игре: /daylabor.</summary>
    public class DayLaborConfig
    {
        // --- Порт: грузчик
        [JsonProperty("portForeman")] public DayLaborPoint PortForeman { get; set; } = new DayLaborPoint { Position = new Vector3(1150.489, -3282.368, 5.9), Heading = 90f };
        [JsonProperty("portPickups")] public List<Vector3> PortPickups { get; set; } = new List<Vector3> { new Vector3(1119.2, -3260.8, 5.9), new Vector3(1122.7, -3233.6, 5.9) };
        [JsonProperty("portDrops")] public List<Vector3> PortDrops { get; set; } = new List<Vector3> { new Vector3(1191.9, -3342.6, 5.9), new Vector3(1164.1, -3310.0, 5.9) };
        [JsonProperty("portPayPerBox")] public int PortPayPerBox { get; set; } = 120;
        /// <summary>Минимум секунд от взятия ящика до сдачи (защита от спама).</summary>
        [JsonProperty("portMinCarrySeconds")] public int PortMinCarrySeconds { get; set; } = 8;

        // --- Ферма: посадка рассады
        [JsonProperty("farmForeman")] public DayLaborPoint FarmForeman { get; set; } = new DayLaborPoint { Position = new Vector3(2031.4, 4987.9, 41.1), Heading = 225f };
        [JsonProperty("farmBeds")] public List<Vector3> FarmBeds { get; set; } = new List<Vector3>();
        [JsonProperty("farmPayPerBed")] public int FarmPayPerBed { get; set; } = 250;
        /// <summary>Мини-игра быстрее этого — не засчитывается.</summary>
        [JsonProperty("farmMinGameSeconds")] public int FarmMinGameSeconds { get; set; } = 8;
        /// <summary>Сколько секунд грядка «отдыхает» после посадки.</summary>
        [JsonProperty("farmBedCooldownSeconds")] public int FarmBedCooldownSeconds { get; set; } = 180;
    }

    /// <summary>
    /// Подработки без трудоустройства (не зависят от основной работы WorkID):
    ///  Порт — у прораба E → смена грузчика; ящик со склада (E) несёшь в руках к контейнеру (E) → оплата за ящик.
    ///  Ферма — у прораба E → смена; у грядки E → окно мини-игры (закопать рассаду лопаткой, полить из лейки) → оплата.
    /// Прорабы и точки — settings/daylabor.json, админ: /daylabor.
    /// </summary>
    class DayLabor : Script
    {
        private static readonly nLog Log = new nLog("Jobs.DayLabor");
        private static string FilePath => Path.Combine("settings", "daylabor.json");
        public static DayLaborConfig Cfg { get; private set; } = new DayLaborConfig();

        private const string Port = "port";
        private const string Farm = "farm";
        private const int CheckpointUid = 9917;

        private class Shift
        {
            public string Job;
            public bool Carrying;
            public DateTime CarryStart;
            public int TargetIndex = -1;
            public int Done;
            public int Earned;
            public int GameBed = -1;
            public DateTime GameStart;
        }

        private static readonly Dictionary<ExtPlayer, Shift> Shifts = new Dictionary<ExtPlayer, Shift>();
        private static readonly Dictionary<ExtPlayer, string> PendingDialog = new Dictionary<ExtPlayer, string>();
        private static readonly Dictionary<int, DateTime> BedCooldown = new Dictionary<int, DateTime>();
        private static readonly Random Rnd = new Random();

        private static readonly List<ExtColShape> Shapes = new List<ExtColShape>();
        private static readonly List<Marker> Markers = new List<Marker>();
        private static readonly List<ExtPed> Peds = new List<ExtPed>();
        private static readonly List<Blip> Blips = new List<Blip>();

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            try
            {
                Load();
                CreatePoints();
            }
            catch (Exception e)
            {
                Log.Write($"OnResourceStart Exception: {e}");
            }
        }

        private static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    Cfg = JsonConvert.DeserializeObject<DayLaborConfig>(File.ReadAllText(FilePath)) ?? new DayLaborConfig();
            }
            catch (Exception e)
            {
                Log.Write($"Не удалось прочитать {FilePath}: {e.Message}");
            }
            Cfg.PortPickups ??= new List<Vector3>();
            Cfg.PortDrops ??= new List<Vector3>();
            if (Cfg.FarmBeds == null || Cfg.FarmBeds.Count == 0)
            {
                // Грядки рядом с прорабом: два ряда по 4
                var c = Cfg.FarmForeman.Position;
                Cfg.FarmBeds = new List<Vector3>();
                for (var row = 0; row < 2; row++)
                for (var i = 0; i < 4; i++)
                    Cfg.FarmBeds.Add(new Vector3(c.X + 8 + i * 4, c.Y - 6 - row * 5, c.Z));
            }
            Save();
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory("settings");
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(Cfg, Formatting.Indented));
            }
            catch (Exception e)
            {
                Log.Write($"Не удалось сохранить {FilePath}: {e.Message}");
            }
        }

        private static void CreatePoints()
        {
            foreach (var shape in Shapes) CustomColShape.DeleteColShape(shape);
            foreach (var marker in Markers) if (marker != null && marker.Exists) marker.Delete();
            foreach (var ped in Peds) PedSystem.Repository.DestroyQuest(ped);
            foreach (var blip in Blips) if (blip != null && blip.Exists) blip.Delete();
            Shapes.Clear();
            Markers.Clear();
            Peds.Clear();
            Blips.Clear();

            try
            {
                CreatePortPoints();
            }
            catch (Exception e)
            {
                Log.Write($"CreatePortPoints Exception: {e}");
            }
            try
            {
                CreateFarmPoints();
            }
            catch (Exception e)
            {
                Log.Write($"CreateFarmPoints Exception: {e}");
            }
        }

        private static void CreatePortPoints()
        {
            Peds.Add(PedSystem.Repository.CreateQuest("s_m_m_dockwork_01", Cfg.PortForeman.Position, Cfg.PortForeman.Heading, 0, null, ColShapeEnums.PortForeman, "~y~Прораб порта\n~w~Подработка грузчиком", false));
            Blips.Add(NAPI.Blip.CreateBlip(478, Cfg.PortForeman.Position, 0.8f, 46, "Подработка: грузчик", 255, 0, true, 0, 0));
            for (var i = 0; i < Cfg.PortPickups.Count; i++)
            {
                Shapes.Add(CustomColShape.CreateCylinderColShape(Cfg.PortPickups[i], 1.5f, 2, 0, ColShapeEnums.PortPickup, i));
                Markers.Add(NAPI.Marker.CreateMarker(1, Cfg.PortPickups[i] - new Vector3(0, 0, 1), new Vector3(), new Vector3(), 1.2f, new Color(245, 196, 0, 120)));
            }
            for (var i = 0; i < Cfg.PortDrops.Count; i++)
                Shapes.Add(CustomColShape.CreateCylinderColShape(Cfg.PortDrops[i], 1.8f, 2, 0, ColShapeEnums.PortDrop, i));
        }

        private static void CreateFarmPoints()
        {
            Peds.Add(PedSystem.Repository.CreateQuest("a_m_m_farmer_01", Cfg.FarmForeman.Position, Cfg.FarmForeman.Heading, 0, null, ColShapeEnums.FarmForeman, "~g~Фермер\n~w~Подработка на ферме", false));
            Blips.Add(NAPI.Blip.CreateBlip(85, Cfg.FarmForeman.Position, 0.8f, 25, "Подработка: ферма", 255, 0, true, 0, 0));
            for (var i = 0; i < Cfg.FarmBeds.Count; i++)
                Shapes.Add(CustomColShape.CreateCylinderColShape(Cfg.FarmBeds[i], 1.6f, 2, 0, ColShapeEnums.FarmBed, i));
        }

        // ------------------------------------------------------------------ смена

        private static Shift GetShift(ExtPlayer player, string job) =>
            Shifts.TryGetValue(player, out var shift) && shift.Job == job ? shift : null;

        private static void OpenForeman(ExtPlayer player, string job)
        {
            if (!player.IsCharacterData())
                return;
            if (Shifts.TryGetValue(player, out var current) && current.Job != job)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Сначала закончите смену {(current.Job == Port ? "в порту" : "на ферме")}", 4000);
                return;
            }
            PendingDialog[player] = job;
            string text;
            if (current != null)
                text = $"Закончить смену? Сделано: {current.Done}, заработано ${current.Earned}.";
            else if (job == Port)
                text = $"Поработать грузчиком? Носите ящики со склада к контейнерам — ${Cfg.PortPayPerBox} за ящик.";
            else
                text = $"Поработать на ферме? Посадите рассаду на грядках: закопать и полить — ${Cfg.FarmPayPerBed} за грядку.";
            Trigger.ClientEvent(player, "openDialog", "DayLabor", text);
        }

        [Interaction(ColShapeEnums.PortForeman)]
        public static void OnPortForeman(ExtPlayer player, int _) => OpenForeman(player, Port);

        [Interaction(ColShapeEnums.FarmForeman)]
        public static void OnFarmForeman(ExtPlayer player, int _) => OpenForeman(player, Farm);

        /// <summary>Из Main.dialogCallback, case "DayLabor".</summary>
        public static void OnDialogYes(ExtPlayer player)
        {
            try
            {
                if (!PendingDialog.Remove(player, out var job) || !player.IsCharacterData())
                    return;
                if (Shifts.TryGetValue(player, out var current))
                {
                    if (current.Job == job)
                        EndShift(player, true);
                    return;
                }
                var shift = new Shift { Job = job };
                Shifts[player] = shift;
                if (job == Port)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Смена началась: возьмите ящик на складе (метка на карте)", 5000);
                    SetTarget(player, shift, Cfg.PortPickups);
                }
                else
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Смена началась: подойдите к грядке и нажмите E", 5000);
                    SetFarmTarget(player, shift);
                }
            }
            catch (Exception e)
            {
                Log.Write($"OnDialogYes Exception: {e}");
            }
        }

        private static void EndShift(ExtPlayer player, bool notify)
        {
            if (!Shifts.Remove(player, out var shift))
                return;
            if (player == null || !player.IsCharacterData())
                return;
            if (shift.Carrying || shift.GameBed != -1)
            {
                player.SetSharedData("AnimToKey", 0);
                Trigger.ClientEvent(player, "blockMove", false);
                Trigger.ClientEvent(player, "client.daylabor.carry", false);
            }
            if (shift.GameBed != -1)
                Trigger.ClientEvent(player, "client.daylabor.farm.close");
            Trigger.ClientEvent(player, "deleteCheckpoint", CheckpointUid);
            if (notify)
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Смена окончена: сделано {shift.Done}, заработано ${shift.Earned}", 5000);
        }

        private static void SetTarget(ExtPlayer player, Shift shift, List<Vector3> points)
        {
            if (points.Count == 0)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Точки не настроены — сообщите администрации", 4000);
                return;
            }
            shift.TargetIndex = Rnd.Next(points.Count);
            var pos = points[shift.TargetIndex];
            Trigger.ClientEvent(player, "createCheckpoint", CheckpointUid, 1, pos - new Vector3(0, 0, 1.2), 1.4f, 0, 245, 196, 0);
            Trigger.ClientEvent(player, "createWaypoint", pos.X, pos.Y);
        }

        private static void SetFarmTarget(ExtPlayer player, Shift shift)
        {
            var free = Enumerable.Range(0, Cfg.FarmBeds.Count).Where(BedFree).ToList();
            if (free.Count == 0)
            {
                Trigger.ClientEvent(player, "deleteCheckpoint", CheckpointUid);
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Свободных грядок пока нет — подождите немного", 4000);
                return;
            }
            var pos = player.Position;
            shift.TargetIndex = free.OrderBy(i => Cfg.FarmBeds[i].DistanceTo(pos)).First();
            var bed = Cfg.FarmBeds[shift.TargetIndex];
            Trigger.ClientEvent(player, "createCheckpoint", CheckpointUid, 1, bed - new Vector3(0, 0, 1.2), 1.4f, 0, 60, 200, 80);
        }

        private static bool BedFree(int index) =>
            (!BedCooldown.TryGetValue(index, out var until) || until <= DateTime.Now) &&
            !Shifts.Values.Any(s => s.GameBed == index);

        private static void Pay(ExtPlayer player, Shift shift, int amount, string comment)
        {
            MoneySystem.Wallet.Change(player, amount);
            GameLog.Money("server", $"player({player.GetUUID()})", amount, comment);
            shift.Done++;
            shift.Earned += amount;
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"+${amount} (всего за смену ${shift.Earned})", 3000);
        }

        // ------------------------------------------------------------------ порт

        [Interaction(ColShapeEnums.PortPickup)]
        public static void OnPortPickup(ExtPlayer player, int index)
        {
            try
            {
                var shift = GetShift(player, Port);
                if (shift == null)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Возьмите смену у прораба порта", 3000);
                    return;
                }
                var sessionData = player.GetSessionData();
                if (shift.Carrying || sessionData == null || player.IsInVehicle || sessionData.CuffedData.Cuffed || sessionData.DeathData.InDeath)
                    return;
                shift.Carrying = true;
                shift.CarryStart = DateTime.Now;
                Trigger.StopAnimation(player);
                player.SetSharedData("AnimToKey", "labor_box");
                Trigger.ClientEvent(player, "client.daylabor.carry", true);
                SetTarget(player, shift, Cfg.PortDrops);
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Отнесите ящик к контейнеру (метка на карте)", 3000);
            }
            catch (Exception e)
            {
                Log.Write($"OnPortPickup Exception: {e}");
            }
        }

        [Interaction(ColShapeEnums.PortDrop)]
        public static void OnPortDrop(ExtPlayer player, int index)
        {
            try
            {
                var shift = GetShift(player, Port);
                if (shift == null || !shift.Carrying || player.IsInVehicle)
                    return;
                if ((DateTime.Now - shift.CarryStart).TotalSeconds < Cfg.PortMinCarrySeconds)
                    return;
                shift.Carrying = false;
                player.SetSharedData("AnimToKey", 0);
                Trigger.ClientEvent(player, "client.daylabor.carry", false);
                Pay(player, shift, Cfg.PortPayPerBox, "dayLaborPort");
                if (shift.Done % 5 == 0)
                    World.Gym.Fitness.Gain(player, "strength");
                SetTarget(player, shift, Cfg.PortPickups);
            }
            catch (Exception e)
            {
                Log.Write($"OnPortDrop Exception: {e}");
            }
        }

        /// <summary>Клиент: сел в машину / упал с ящиком — ящик потерян.</summary>
        [RemoteEvent("server.daylabor.drop")]
        public static void OnDropBox(ExtPlayer player)
        {
            var shift = GetShift(player, Port);
            if (shift == null || !shift.Carrying)
                return;
            shift.Carrying = false;
            player.SetSharedData("AnimToKey", 0);
            Trigger.ClientEvent(player, "client.daylabor.carry", false);
            Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "Ящик уронили — возьмите новый на складе", 3000);
            SetTarget(player, shift, Cfg.PortPickups);
        }

        // ------------------------------------------------------------------ ферма

        [Interaction(ColShapeEnums.FarmBed)]
        public static void OnFarmBed(ExtPlayer player, int index)
        {
            try
            {
                var shift = GetShift(player, Farm);
                if (shift == null)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Возьмите смену у фермера", 3000);
                    return;
                }
                var sessionData = player.GetSessionData();
                if (shift.GameBed != -1 || sessionData == null || player.IsInVehicle || sessionData.CuffedData.Cuffed || sessionData.DeathData.InDeath)
                    return;
                if (!BedFree(index))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Эта грядка уже засажена — идите к соседней", 3000);
                    return;
                }
                shift.GameBed = index;
                shift.GameStart = DateTime.Now;
                Trigger.StopAnimation(player);
                player.SetSharedData("AnimToKey", "labor_plant");
                Trigger.ClientEvent(player, "blockMove", true);
                Trigger.ClientEvent(player, "client.daylabor.farm.open");
            }
            catch (Exception e)
            {
                Log.Write($"OnFarmBed Exception: {e}");
            }
        }

        private static void StopFarmGame(ExtPlayer player, Shift shift)
        {
            shift.GameBed = -1;
            player.SetSharedData("AnimToKey", 0);
            Trigger.ClientEvent(player, "blockMove", false);
        }

        [RemoteEvent("server.daylabor.farm.finished")]
        public static void OnFarmFinished(ExtPlayer player)
        {
            try
            {
                var shift = GetShift(player, Farm);
                if (shift == null || shift.GameBed == -1)
                    return;
                var bed = shift.GameBed;
                StopFarmGame(player, shift);
                if ((DateTime.Now - shift.GameStart).TotalSeconds < Cfg.FarmMinGameSeconds || bed >= Cfg.FarmBeds.Count || player.Position.DistanceTo(Cfg.FarmBeds[bed]) > 4f)
                    return;
                BedCooldown[bed] = DateTime.Now.AddSeconds(Cfg.FarmBedCooldownSeconds);
                Pay(player, shift, Cfg.FarmPayPerBed, "dayLaborFarm");
                SetFarmTarget(player, shift);
            }
            catch (Exception e)
            {
                Log.Write($"OnFarmFinished Exception: {e}");
            }
        }

        [RemoteEvent("server.daylabor.farm.exit")]
        public static void OnFarmExit(ExtPlayer player)
        {
            var shift = GetShift(player, Farm);
            if (shift == null || shift.GameBed == -1)
                return;
            StopFarmGame(player, shift);
        }

        // ------------------------------------------------------------------ служебное

        [ServerEvent(Event.PlayerDeath)]
        public void OnPlayerDeath(ExtPlayer player, ExtPlayer killer, uint weapon)
        {
            try
            {
                if (!Shifts.TryGetValue(player, out var shift))
                    return;
                if (shift.Carrying)
                    OnDropBox(player);
                if (shift.GameBed != -1)
                {
                    StopFarmGame(player, shift);
                    Trigger.ClientEvent(player, "client.daylabor.farm.close");
                }
            }
            catch (Exception e)
            {
                Log.Write($"OnPlayerDeath Exception: {e}");
            }
        }

        [ServerEvent(Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(ExtPlayer player, DisconnectionType type, string reason)
        {
            Shifts.Remove(player);
            PendingDialog.Remove(player);
        }

        [Command(AdminCommands.daylabor, GreedyArg = true)]
        public static void CMD_DayLabor(ExtPlayer player, string args = "")
        {
            try
            {
                if (!player.IsCharacterData() || !CommandsAccess.CanUseCmd(player, AdminCommands.daylabor))
                    return;
                var parts = (args ?? "").ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var job = parts.Length > 0 ? parts[0] : "";
                var what = parts.Length > 1 ? parts[1] : "";
                var action = parts.Length > 2 ? parts[2] : "add";
                var pos = player.Position;
                string done = null;
                if (job == Port && what == "foreman")
                {
                    Cfg.PortForeman = new DayLaborPoint { Position = pos, Heading = player.Heading };
                    done = "прораб порта перенесён";
                }
                else if (job == Farm && what == "foreman")
                {
                    Cfg.FarmForeman = new DayLaborPoint { Position = pos, Heading = player.Heading };
                    done = "фермер перенесён";
                }
                else if (job == Port && (what == "pickup" || what == "drop"))
                {
                    var list = what == "pickup" ? Cfg.PortPickups : Cfg.PortDrops;
                    if (action == "clear") list.Clear(); else list.Add(pos);
                    done = $"{(what == "pickup" ? "склад" : "контейнер")}: {(action == "clear" ? "очищено" : "точка добавлена")} ({list.Count})";
                }
                else if (job == Farm && what == "bed")
                {
                    if (action == "clear") Cfg.FarmBeds.Clear(); else Cfg.FarmBeds.Add(pos);
                    BedCooldown.Clear();
                    done = $"грядки: {(action == "clear" ? "очищено" : "добавлена")} ({Cfg.FarmBeds.Count})";
                }
                if (done == null)
                {
                    player.SendChatMessage("/daylabor port foreman | port pickup [clear] | port drop [clear] | farm foreman | farm bed [clear]");
                    return;
                }
                Save();
                CreatePoints();
                GameLog.Admin(player.Name, $"daylabor {args}", "");
                player.SendChatMessage($"Подработки: {done}. Отойдите — NPC появится на новом месте.");
            }
            catch (Exception e)
            {
                Log.Write($"CMD_DayLabor Exception: {e}");
            }
        }
    }
}
