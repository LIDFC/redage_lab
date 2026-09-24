using GTANetworkAPI;
using NeptuneEvo.Handles;

namespace NeptuneEvo.Fractions.Table.Logs
{
    public class Events : Script
    {
        [RemoteEvent("server.frac.main.getLog")]
        public void GetLog(ExtPlayer player, int uuid, bool isStock, string text, int pageId) =>
            Repository.GetLog(player, uuid, isStock, text, pageId);
    }
}
