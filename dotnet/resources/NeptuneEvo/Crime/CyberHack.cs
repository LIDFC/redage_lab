using System;
using System.Collections.Generic;
using GTANetworkAPI;
using NeptuneEvo.Character;
using NeptuneEvo.Chars;
using NeptuneEvo.Handles;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Crime
{
    /// <summary>
    /// Мини-игра программатора «Breach protocol» (CEF PlayerCyberHack, на основе присланного пакета cyber-hack):
    /// матрица HEX-кодов, собрать последовательность за время. Используется для электронных замков дорогих машин.
    /// Результат проверяется на сервере (минимальное время, дистанция), затем вызывается onSuccess / onFail.
    /// </summary>
    public class CyberHack : Script
    {
        private class Session
        {
            public Vector3 Position;
            public uint Dimension;
            public DateTime Started;
            public Action<ExtPlayer> OnSuccess;
            public Action<ExtPlayer, string> OnFail;
        }

        private static readonly Dictionary<int, Session> Sessions = new Dictionary<int, Session>();
        private const double MinSeconds = 2.0;
        private const float MaxDistance = 3.5f;
        private static readonly uint TabletProp = NAPI.Util.GetHashKey("tablet");

        public static bool IsBusy(ExtPlayer player) => Sessions.ContainsKey(player.GetUUID());

        /// <summary>size — размер матрицы (4–7), seconds — время. onFail получает "fail", "cancel" или "moved".</summary>
        public static bool Start(ExtPlayer player, int size, int seconds, string title,
            Action<ExtPlayer> onSuccess, Action<ExtPlayer, string> onFail = null)
        {
            if (IsBusy(player) || LockBreak.IsBusy(player))
                return false;
            Sessions[player.GetUUID()] = new Session
            {
                Position = player.Position,
                Dimension = player.Dimension,
                Started = DateTime.Now,
                OnSuccess = onSuccess,
                OnFail = onFail,
            };
            Trigger.PlayAnimation(player, "amb@code_human_in_bus_passenger_idles@female@tablet@base", "base", 49);
            Attachments.AddAttachment(player, TabletProp);
            Trigger.ClientEvent(player, "client.cyberhack.open", JsonConvert.SerializeObject(new { difficulty = size, timeSeconds = seconds, title }));
            return true;
        }

        private static void Close(ExtPlayer player)
        {
            Sessions.Remove(player.GetUUID());
            Trigger.ClientEvent(player, "client.cyberhack.close");
            Trigger.StopAnimation(player);
            Attachments.RemoveAttachment(player, TabletProp);
        }

        [RemoteEvent("server.cyberhack.result")]
        public static void OnResult(ExtPlayer player, bool success)
        {
            try
            {
                if (!player.IsCharacterData() || !Sessions.TryGetValue(player.GetUUID(), out var session))
                    return;
                Close(player);
                if (player.Dimension != session.Dimension || player.Position.DistanceTo(session.Position) > MaxDistance)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Вы отошли от машины — соединение потеряно", 3000);
                    session.OnFail?.Invoke(player, "moved");
                    return;
                }
                if (success && (DateTime.Now - session.Started).TotalSeconds >= MinSeconds)
                    session.OnSuccess?.Invoke(player);
                else
                    session.OnFail?.Invoke(player, success ? "fail" : "fail");
            }
            catch (Exception e)
            {
                Debugs.Repository.Exception(e);
            }
        }

        [RemoteEvent("server.cyberhack.cancel")]
        public static void OnCancel(ExtPlayer player)
        {
            if (!player.IsCharacterData() || !Sessions.TryGetValue(player.GetUUID(), out var session))
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
