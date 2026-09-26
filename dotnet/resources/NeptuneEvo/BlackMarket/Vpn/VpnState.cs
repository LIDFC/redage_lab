using NeptuneEvo.Handles;
using System.Collections.Concurrent;

namespace NeptuneEvo.BlackMarket.Vpn
{
    /// <summary>
    /// VPN в телефоне: только на время сессии. Ключ — объект подключения, поэтому после перезахода
    /// (новое подключение) VPN всегда выключен, а в БД ничего не пишется.
    /// Сервер проверяет VPN перед каждым действием Чёрного рынка — CEF тут ничего не решает.
    /// </summary>
    public static class VpnState
    {
        private static readonly ConcurrentDictionary<ExtPlayer, bool> Enabled = new ConcurrentDictionary<ExtPlayer, bool>();

        public static bool IsOn(ExtPlayer player) =>
            player != null && Enabled.TryGetValue(player, out var on) && on;

        public static bool Toggle(ExtPlayer player)
        {
            var on = !IsOn(player);
            if (on)
                Enabled[player] = true;
            else
                Enabled.TryRemove(player, out _);
            return on;
        }

        public static void Reset(ExtPlayer player)
        {
            if (player != null)
                Enabled.TryRemove(player, out _);
        }
    }
}
