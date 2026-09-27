using System.Collections.Concurrent;
using GTANetworkAPI;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using NeptuneEvo.Character;

namespace NeptuneEvo.Players.Tablet
{
    /// <summary>
    /// Планшет (клавиша K): видимая всем анимация с планшетом в руках. Сами приложения — CEF hudevo/tablet.
    /// </summary>
    public class Events : Script
    {
        // Игроки, которым анимацию включил планшет — чтобы при закрытии не сбить чужую анимацию
        private static readonly ConcurrentDictionary<ExtPlayer, bool> Animated = new ConcurrentDictionary<ExtPlayer, bool>();

        [RemoteEvent("server.tablet.open")]
        public void OnOpen(ExtPlayer player)
        {
            if (!player.IsCharacterData() || player.IsInVehicle)
                return;
            Trigger.PlayAnimation(player, "amb@code_human_in_bus_passenger_idles@female@tablet@base", "base", 49, true, "tablet");
            Animated[player] = true;
        }

        [RemoteEvent("server.tablet.close")]
        public void OnClose(ExtPlayer player)
        {
            if (Animated.TryRemove(player, out _) && player.IsCharacterData())
                Trigger.StopAnimation(player);
        }

        [ServerEvent(Event.PlayerDisconnected)]
        public void OnDisconnect(ExtPlayer player, DisconnectionType type, string reason) => Animated.TryRemove(player, out _);
    }
}
