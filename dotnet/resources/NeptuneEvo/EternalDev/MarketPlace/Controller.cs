using GTANetworkAPI;
using NeptuneEvo.Handles;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace
{
    public class Controller : Script
    {
        [ServerEvent(Event.ResourceStart)]
        public static void OnStart()
            => Manager.Initialize();

        [RemoteEvent("server.marketPlace.openApp")]
        public static void OpenApp(ExtPlayer player)
            => Manager.Subscribe(player);

        [RemoteEvent("server.marketPlace.closeApp")]
        public static void CloseApp(ExtPlayer player)
            => Manager.UnSubscribe(player);

        [RemoteEvent("server.marketPlace.setPage")]
        public static void SetPage(ExtPlayer player, string pageName)
            => Manager.SetPage(player, pageName);
    }
}
