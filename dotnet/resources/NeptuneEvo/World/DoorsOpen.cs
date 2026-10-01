using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.Core;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Character;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.World
{
    /// <summary>
    /// Двери, которые администратор открыл навсегда (например, закрытая по умолчанию дверь в подвале штаба армии).
    /// /dooropen — посмотреть на дверь и открыть её для всех; /doorclose — убрать ближайшую из списка.
    /// Список: settings/doors_open.json, клиент — src_client/world/doors.js.
    /// </summary>
    class DoorsOpen : Script
    {
        private static readonly nLog Log = new nLog("World.DoorsOpen");
        private static string FilePath => Path.Combine("settings", "doors_open.json");

        public class OpenDoor
        {
            [JsonProperty("hash")] public uint Hash { get; set; }
            [JsonProperty("x")] public float X { get; set; }
            [JsonProperty("y")] public float Y { get; set; }
            [JsonProperty("z")] public float Z { get; set; }
        }

        private static List<OpenDoor> _doors = new List<OpenDoor>();
        private static readonly HashSet<ExtPlayer> Picking = new HashSet<ExtPlayer>();

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            try
            {
                if (File.Exists(FilePath))
                    _doors = JsonConvert.DeserializeObject<List<OpenDoor>>(File.ReadAllText(FilePath)) ?? new List<OpenDoor>();
            }
            catch (Exception e)
            {
                Log.Write($"Не удалось прочитать {FilePath}: {e.Message}");
            }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory("settings");
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(_doors, Formatting.Indented));
            }
            catch (Exception e)
            {
                Log.Write($"Не удалось сохранить {FilePath}: {e.Message}");
            }
        }

        public static void SendTo(ExtPlayer player) =>
            Trigger.ClientEvent(player, "client.doors.opened", JsonConvert.SerializeObject(_doors));

        private static void SendAll() =>
            Trigger.ClientEventForAll("client.doors.opened", JsonConvert.SerializeObject(_doors));

        [Command(AdminCommands.dooropen)]
        public static void CMD_DoorOpen(ExtPlayer player)
        {
            if (!player.IsCharacterData() || !CommandsAccess.CanUseCmd(player, AdminCommands.dooropen))
                return;
            Picking.Add(player);
            Trigger.ClientEvent(player, "client.doors.pick");
        }

        [RemoteEvent("server.doors.picked")]
        public static void OnPicked(ExtPlayer player, string model, float x, float y, float z)
        {
            try
            {
                if (!Picking.Remove(player))
                    return;
                if (!uint.TryParse(model, out var hash) || hash == 0)
                {
                    player.SendChatMessage("Дверь не найдена — подойдите ближе и смотрите прямо на неё (до 10 м)");
                    return;
                }
                var position = new Vector3(x, y, z);
                if (_doors.Any(d => d.Hash == hash && new Vector3(d.X, d.Y, d.Z).DistanceTo(position) < 0.5f))
                {
                    player.SendChatMessage("Эта дверь уже открыта навсегда");
                    return;
                }
                _doors.Add(new OpenDoor { Hash = hash, X = x, Y = y, Z = z });
                Save();
                SendAll();
                GameLog.Admin(player.Name, $"dooropen {hash} {x:0.00} {y:0.00} {z:0.00}", "");
                player.SendChatMessage($"Дверь открыта для всех и сохранена (модель {hash}, {x:0.0} {y:0.0} {z:0.0})");
            }
            catch (Exception e)
            {
                Log.Write($"OnPicked Exception: {e}");
            }
        }

        [Command("doorclose")]
        public static void CMD_DoorClose(ExtPlayer player)
        {
            if (!player.IsCharacterData() || !CommandsAccess.CanUseCmd(player, AdminCommands.dooropen))
                return;
            var nearest = _doors.OrderBy(d => new Vector3(d.X, d.Y, d.Z).DistanceTo(player.Position)).FirstOrDefault();
            if (nearest == null || new Vector3(nearest.X, nearest.Y, nearest.Z).DistanceTo(player.Position) > 5f)
            {
                player.SendChatMessage("Рядом (5 м) нет открытых навсегда дверей");
                return;
            }
            _doors.Remove(nearest);
            Save();
            SendAll();
            player.SendChatMessage("Дверь убрана из списка — после перезахода она снова будет закрыта как в игре");
        }

        [ServerEvent(Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(ExtPlayer player, DisconnectionType type, string reason) => Picking.Remove(player);
    }
}
