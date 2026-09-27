using GTANetworkAPI;
using NeptuneEvo.BlackMarket.Deliveries;
using NeptuneEvo.Handles;
using System;

namespace NeptuneEvo.BlackMarket
{
    /// <summary>
    /// Серверные события Чёрного рынка. Каждый RemoteEvent проверяет VPN и антиспам на сервере,
    /// сам определяет игрока/UUID/фракцию/ранг/баланс и не доверяет цифрам из CEF.
    /// </summary>
    public partial class Controller : Script
    {
        [ServerEvent(Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(ExtPlayer player, DisconnectionType type, string reason)
        {
            try
            {
                var uuid = player.GetUUID();
                DropManager.OnPlayerDisconnected(uuid);
                Vpn.VpnState.Reset(player);
            }
            catch (Exception e)
            {
                BlackMarketCore.Log.Write($"OnPlayerDisconnected Exception: {e}");
            }
        }
    }
}
