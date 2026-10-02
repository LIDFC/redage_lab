using GTANetworkAPI;
using NeptuneEvo.Handles;
using NeptuneEvo.Character;
using NeptuneEvo.Players;
using Redage.SDK;

namespace NeptuneEvo.Core
{
    class DebugCommands : Script
    {
        /// <summary>Проверка, видит ли игра колесо и боковые кнопки мыши и срабатывают ли бинды на них — src_client/player/bind.js.</summary>
        [Command("mousetest")]
        public static void CMD_MouseTest(ExtPlayer player)
        {
            if (player.IsCharacterData())
                Trigger.ClientEvent(player, "client.mousetest");
        }
    }
}
