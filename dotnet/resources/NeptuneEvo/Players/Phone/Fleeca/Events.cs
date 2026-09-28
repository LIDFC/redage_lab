using System;
using GTANetworkAPI;
using NeptuneEvo.Handles;
using NeptuneEvo.Organizations.Contracts;

namespace NeptuneEvo.Players.Phone.Fleeca
{
    public class Events : Script
    {
        [RemoteEvent("server.phone.bank.load")]
        public void OnLoad(ExtPlayer player)
        {
            try
            {
                Repository.Load(player);
            }
            catch (Exception e)
            {
                Debugs.Repository.Exception(e);
            }
        }

        [RemoteEvent("server.phone.bank.action")]
        public void OnAction(ExtPlayer player, string action, int arg1, int arg2)
        {
            try
            {
                if (!ContractsCore.AntiSpam(player, 800))
                    return;
                Repository.Action(player, action, arg1, arg2);
            }
            catch (Exception e)
            {
                Debugs.Repository.Exception(e);
            }
        }

        [RemoteEvent("server.phone.bank.history")]
        public void OnHistory(ExtPlayer player)
        {
            try
            {
                if (!ContractsCore.AntiSpam(player, 800))
                    return;
                Repository.History(player);
            }
            catch (Exception e)
            {
                Debugs.Repository.Exception(e);
            }
        }
    }
}
