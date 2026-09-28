using System;
using System.Collections.Generic;
using GTANetworkAPI;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Handles;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Crime
{
    /// <summary>
    /// Общая мини-игра взлома замка отмычкой (CEF PlayerLockBreak, по мотивам rage-lock-break).
    /// Сломанная в игре отмычка снимается из инвентаря; открытие проверяется на сервере
    /// (минимальное время, дистанция до цели) — только после этого вызывается onSuccess.
    /// </summary>
    public class LockBreak : Script
    {
        private class Session
        {
            public string Kind;
            public int TargetId;
            public Vector3 Position;
            public uint Dimension;
            public DateTime Started;
            public Action<ExtPlayer> OnSuccess;
            public Action<ExtPlayer, string> OnFail;
        }

        private static readonly Dictionary<int, Session> Sessions = new Dictionary<int, Session>();
        /// <summary>Отвёртка-отмычка в руке (src_client/inventory/attachments.js "crime_lockpick").</summary>
        private static readonly uint LockpickProp = NAPI.Util.GetHashKey("crime_lockpick");
        private const double MinSeconds = 2.5;
        private const float MaxDistance = 3.5f;

        public static int CountPicks(ExtPlayer player) =>
            Chars.Repository.getCountItem($"char_{player.GetUUID()}", ItemId.Lockpick, false);

        public static bool IsBusy(ExtPlayer player) => Sessions.ContainsKey(player.GetUUID());

        /// <summary>
        /// Начать взлом. difficulty — точность угла в градусах (2 — очень сложно, 10 — легко).
        /// onFail получает причину: "cancel", "nopicks", "moved".
        /// </summary>
        public static bool Start(ExtPlayer player, string kind, int targetId, int difficulty, string title,
            Action<ExtPlayer> onSuccess, Action<ExtPlayer, string> onFail = null)
        {
            var picks = CountPicks(player);
            if (picks <= 0)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Нужна отмычка", 3000);
                return false;
            }
            if (IsBusy(player) || CyberHack.IsBusy(player))
                return false;

            Sessions[player.GetUUID()] = new Session
            {
                Kind = kind,
                TargetId = targetId,
                Position = player.Position,
                Dimension = player.Dimension,
                Started = DateTime.Now,
                OnSuccess = onSuccess,
                OnFail = onFail,
            };
            // Возня с замком двери/машины на уровне груди + отвёртка в руке
            Trigger.PlayAnimation(player, "anim@amb@clubhouse@tutorial@bkr_tut_ig3@", "machinic_loop_mechandplayer", 1);
            Chars.Attachments.AddAttachment(player, LockpickProp);
            Trigger.ClientEvent(player, "client.lockbreak.open", JsonConvert.SerializeObject(new { difficulty, picks, title }));
            return true;
        }

        private static void Close(ExtPlayer player)
        {
            Sessions.Remove(player.GetUUID());
            Trigger.ClientEvent(player, "client.lockbreak.close");
            Trigger.StopAnimation(player);
            Chars.Attachments.RemoveAttachment(player, LockpickProp);
        }

        private static bool TryGet(ExtPlayer player, out Session session)
        {
            session = null;
            return player.IsCharacterData() && Sessions.TryGetValue(player.GetUUID(), out session);
        }

        private static bool Moved(ExtPlayer player, Session session) =>
            player.Dimension != session.Dimension || player.Position.DistanceTo(session.Position) > MaxDistance;

        [RemoteEvent("server.lockbreak.opened")]
        public static void OnOpened(ExtPlayer player)
        {
            try
            {
                if (!TryGet(player, out var session))
                    return;
                if ((DateTime.Now - session.Started).TotalSeconds < MinSeconds)
                    return; // слишком быстро — ждём честного открытия
                Close(player);
                if (Moved(player, session))
                {
                    session.OnFail?.Invoke(player, "moved");
                    return;
                }
                session.OnSuccess?.Invoke(player);
            }
            catch (Exception e)
            {
                Debugs.Repository.Exception(e);
            }
        }

        [RemoteEvent("server.lockbreak.broken")]
        public static void OnBroken(ExtPlayer player)
        {
            try
            {
                if (!TryGet(player, out var session))
                    return;
                if (CountPicks(player) > 0)
                    Chars.Repository.Remove(player, $"char_{player.GetUUID()}", "inventory", ItemId.Lockpick, 1);
                var left = CountPicks(player);
                if (left <= 0 || Moved(player, session))
                {
                    Close(player);
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, left <= 0 ? "Отмычки закончились" : "Вы отошли от замка", 3000);
                    session.OnFail?.Invoke(player, left <= 0 ? "nopicks" : "moved");
                    return;
                }
                Trigger.ClientEvent(player, "client.lockbreak.newpick", left);
            }
            catch (Exception e)
            {
                Debugs.Repository.Exception(e);
            }
        }

        [RemoteEvent("server.lockbreak.cancel")]
        public static void OnCancel(ExtPlayer player)
        {
            if (!TryGet(player, out var session))
                return;
            Close(player);
            session.OnFail?.Invoke(player, "cancel");
        }

        [ServerEvent(Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(ExtPlayer player, DisconnectionType type, string reason)
        {
            if (player != null)
                Sessions.Remove(player.GetUUID());
        }

        [ServerEvent(Event.PlayerDeath)]
        public void OnPlayerDeath(ExtPlayer player, ExtPlayer killer, uint reason)
        {
            if (player != null && Sessions.ContainsKey(player.GetUUID()))
                Close(player);
        }
    }
}
