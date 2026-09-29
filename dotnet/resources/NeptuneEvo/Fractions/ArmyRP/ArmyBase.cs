using System;
using System.Collections.Generic;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.Chars;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Character.Models;
using NeptuneEvo.Core;
using NeptuneEvo.Fractions.Models;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using Redage.SDK;

namespace NeptuneEvo.Fractions.ArmyRP
{
    /// <summary>
    /// Б. Режимная зона Форт Занкудо и КПП.
    ///  /basepass id часы — офицер выдаёт пропуск (предмет ArmyPass, привязан к владельцу и сроку);
    ///  посторонний в зоне без пропуска: предупреждение → через N секунд метка у военных на смене и розыск;
    ///  /gate — военный рядом со шлагбаумом открывает/закрывает его.
    /// </summary>
    class ArmyBase : Script
    {
        private static nLog Log => ArmyConfig.Log;
        private static ArmyConfig Cfg => ArmyConfig.Current;

        private class Intruder
        {
            public DateTime Entered;
            public bool Flagged;
        }

        private static readonly Dictionary<ExtPlayer, Intruder> Intruders = new Dictionary<ExtPlayer, Intruder>();
        private static readonly List<GTANetworkAPI.Object> BarrierObjects = new List<GTANetworkAPI.Object>();
        private static readonly HashSet<int> OpenBarriers = new HashSet<int>();

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            try
            {
                // Конфиг грузит ArmyService (порядок ResourceStart не гарантирован — страхуемся)
                if (ArmyConfig.Current.Zone.Count < 3)
                    ArmyConfig.Load();
                SpawnBarriers();
                Timers.Start("army.zone", 2000, Tick, true);
            }
            catch (Exception e)
            {
                Log.Write($"ArmyBase start Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ пропуск

        public static bool HasValidPass(ExtPlayer player)
        {
            var uuid = player.GetUUID();
            if (!Chars.Repository.ItemsData.TryGetValue($"char_{uuid}", out var locations))
                return false;
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            foreach (var slots in locations.Values)
                foreach (var item in slots.Values)
                {
                    if (item == null || item.ItemId != ItemId.ArmyPass)
                        continue;
                    var parts = (item.Data ?? "").Split('|');
                    if (parts.Length >= 2 && int.TryParse(parts[0], out var owner) && owner == uuid
                        && long.TryParse(parts[1], out var until) && until > now)
                        return true;
                }
            return false;
        }

        [Command("basepass")]
        public static void CMD_BasePass(ExtPlayer player, int targetId, int hours)
        {
            try
            {
                if (!ArmyUtil.IsOfficer(player) || !ArmyUtil.OnDuty(player))
                {
                    ArmyUtil.Say(player, "Пропуск выдаёт офицер армии на смене", false);
                    return;
                }
                if (hours < 1 || hours > Cfg.PassMaxHours)
                {
                    ArmyUtil.Say(player, $"Срок пропуска от 1 до {Cfg.PassMaxHours} ч", false);
                    return;
                }
                var target = Main.GetPlayerByID(targetId);
                if (target == null || !target.IsCharacterData() || target.Position.DistanceTo(player.Position) > 3)
                {
                    ArmyUtil.Say(player, "Человек должен стоять рядом", false);
                    return;
                }
                var until = DateTimeOffset.Now.AddHours(hours);
                var data = $"{target.GetUUID()}|{until.ToUnixTimeSeconds()}|{target.Name}";
                if (Chars.Repository.AddNewItem(target, $"char_{target.GetUUID()}", "inventory", ItemId.ArmyPass, 1, data) == -1)
                {
                    ArmyUtil.Say(player, "У него нет места в инвентаре", false);
                    return;
                }
                Commands.RPChat("sme", player, " выписал пропуск на базу и передал {name}", target);
                ArmyUtil.Say(target, $"Пропуск на Форт Занкудо до {until.LocalDateTime:dd.MM HH:mm}");
                ArmyUtil.Radio($"{player.Name} выдал пропуск {target.Name} на {hours} ч");
                Fractions.Table.Logs.Repository.AddLogs(player, FractionLogsType.None, $"Пропуск на базу {target.Name} ({target.GetUUID()}) на {hours} ч");
            }
            catch (Exception e)
            {
                Log.Write($"CMD_BasePass Exception: {e}");
            }
        }

        // ------------------------------------------------------------------ режимная зона

        private static bool Exempt(ExtPlayer player)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null)
                return true;
            if (characterData.AdminLVL > 0 || ArmyUtil.IsArmy(player))
                return true;
            if (characterData.ArrestTime > 0 || characterData.DemorganTime > 0)
                return true;
            return false;
        }

        private static void Tick()
        {
            try
            {
                if (Cfg.Zone.Count < 3)
                    return;
                foreach (var player in Character.Repository.GetPlayers())
                {
                    if (!player.IsCharacterData() || player.Dimension != 0 || Exempt(player)
                        || !Cfg.InZone(player.Position) || HasValidPass(player))
                    {
                        Intruders.Remove(player);
                        continue;
                    }
                    if (!Intruders.TryGetValue(player, out var intruder))
                    {
                        Intruders[player] = new Intruder { Entered = DateTime.Now };
                        Notify.Send(player, NotifyType.Warning, NotifyPosition.Center,
                            $"Режимная зона Форт Занкудо! Покиньте территорию за {Cfg.ZoneWarnSeconds} сек, иначе будет применена сила", 8000);
                        Trigger.ClientEvent(player, "client.army.zoneWarning", Cfg.ZoneWarnSeconds);
                        continue;
                    }
                    if (intruder.Flagged || (DateTime.Now - intruder.Entered).TotalSeconds < Cfg.ZoneWarnSeconds)
                        continue;
                    intruder.Flagged = true;
                    Flag(player);
                }
                // Нарушителям — обновить метку для военных
                foreach (var (player, intruder) in Intruders.Where(i => i.Value.Flagged).ToList())
                    foreach (var soldier in ArmyUtil.ArmyOnDuty())
                        Trigger.ClientEvent(soldier, "client.army.intruder", player.Value, player.Position.X, player.Position.Y, player.Position.Z);
            }
            catch (Exception e)
            {
                Log.Write($"ArmyBase Tick Exception: {e}");
            }
        }

        private static void Flag(ExtPlayer player)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null)
                return;
            ArmyUtil.Radio($"Тревога! Посторонний на территории базы: {player.Name} ({player.Value}). Метка на карте.");
            Notify.Send(player, NotifyType.Error, NotifyPosition.Center, "Вы незаконно находитесь на военном объекте. Военные предупреждены.", 8000);
            var level = Math.Max(Cfg.ZoneWantedLevel, characterData.WantedLVL?.Level ?? 0);
            if (Cfg.ZoneWantedLevel > 0 && (characterData.WantedLVL == null || characterData.WantedLVL.Level < level))
                Police.setPlayerWantedLevel(player, new WantedLevel(level, "Армия", DateTime.Now, "Проникновение на военный объект"));
        }

        [ServerEvent(Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(ExtPlayer player, DisconnectionType type, string reason) => Intruders.Remove(player);

        // ------------------------------------------------------------------ шлагбаумы

        public static void SpawnBarriers()
        {
            foreach (var obj in BarrierObjects)
                if (obj != null && obj.Exists)
                    obj.Delete();
            BarrierObjects.Clear();
            OpenBarriers.Clear();
            for (var i = 0; i < Cfg.Barriers.Count; i++)
                BarrierObjects.Add(CreateBarrier(Cfg.Barriers[i]));
        }

        private static GTANetworkAPI.Object CreateBarrier(ArmyBarrier barrier) =>
            NAPI.Object.CreateObject(NAPI.Util.GetHashKey(Cfg.BarrierModel), barrier.Position, new Vector3(0, 0, barrier.Heading), 255, 0);

        [Command("gate")]
        public static void CMD_Gate(ExtPlayer player)
        {
            try
            {
                if (!ArmyUtil.IsArmy(player) || !ArmyUtil.OnDuty(player))
                {
                    ArmyUtil.Say(player, "Шлагбаумом управляет военный на смене", false);
                    return;
                }
                var index = Cfg.Barriers.FindIndex(b => b.Position.DistanceTo(player.Position) < 10f);
                if (index == -1)
                {
                    ArmyUtil.Say(player, Cfg.Barriers.Count == 0 ? "Шлагбаумы не поставлены (/armyset barrier add)" : "Рядом нет шлагбаума", false);
                    return;
                }
                if (OpenBarriers.Remove(index))
                {
                    BarrierObjects[index] = CreateBarrier(Cfg.Barriers[index]);
                    Commands.RPChat("sme", player, " опустил шлагбаум");
                }
                else
                {
                    if (BarrierObjects[index] != null && BarrierObjects[index].Exists)
                        BarrierObjects[index].Delete();
                    OpenBarriers.Add(index);
                    Commands.RPChat("sme", player, " поднял шлагбаум");
                }
            }
            catch (Exception e)
            {
                Log.Write($"CMD_Gate Exception: {e}");
            }
        }
    }
}
