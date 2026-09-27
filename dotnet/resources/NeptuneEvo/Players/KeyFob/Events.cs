using System.Collections.Concurrent;
using GTANetworkAPI;
using NeptuneEvo.Character;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using Redage.SDK;

namespace NeptuneEvo.Players.KeyFob
{
    /// <summary>
    /// Анимация брелока с ключом в руке при открытии/закрытии машины клавишей L (снаружи).
    /// Сервер решает, что замок реально сработал; клиент проверяет, что анимация уместна (не в машине, не падает и т.п.).
    /// </summary>
    public class Events : Script
    {
        private const string Dict = "anim@mp_player_intmenu@key_fob@";
        private const string Anim = "fob_click";
        private const string AnimUse = Dict + "|" + Anim + "|48|carkey";

        private static readonly ConcurrentDictionary<ExtPlayer, long> LastPlay = new ConcurrentDictionary<ExtPlayer, long>();

        [RemoteEvent("server.keyfob.play")]
        public void OnPlay(ExtPlayer player)
        {
            if (!player.IsCharacterData() || player.IsInVehicle)
                return;
            var sessionData = player.GetSessionData();
            if (sessionData == null || sessionData.CuffedData.Cuffed || sessionData.DeathData.InDeath)
                return;

            // Не перебиваем другую анимацию (сидит, работает, держит предмет)
            var current = player.HasSharedData("ANIM_USE") ? player.GetSharedData<string>("ANIM_USE") : null;
            if (!string.IsNullOrEmpty(current) && current != "null" && current != AnimUse)
                return;

            var now = System.Environment.TickCount64;
            if (LastPlay.TryGetValue(player, out var last) && now - last < 1000)
                return;
            LastPlay[player] = now;

            Trigger.PlayAnimation(player, Dict, Anim, 48, true, "carkey");
            Timers.StartOnce(1300, () =>
            {
                if (!player.IsCharacterData())
                    return;
                // Снимаем только свою анимацию — если игрок уже делает что-то другое, не трогаем
                var anim = player.HasSharedData("ANIM_USE") ? player.GetSharedData<string>("ANIM_USE") : null;
                if (anim == AnimUse)
                    Trigger.StopAnimation(player, false); // клип не зациклен и сам доиграет — только сбрасываем состояние и убираем ключ
            }, true);
        }

        [ServerEvent(Event.PlayerDisconnected)]
        public void OnDisconnect(ExtPlayer player, DisconnectionType type, string reason) => LastPlay.TryRemove(player, out _);
    }
}
