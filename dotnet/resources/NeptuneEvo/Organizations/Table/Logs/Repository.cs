using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NeptuneEvo.Character;
using NeptuneEvo.Handles;
using NeptuneEvo.Organizations.Player;
using NeptuneEvo.Players;
using NeptuneEvo.Organizations.Models;
using Redage.SDK;

namespace NeptuneEvo.Organizations.Table.Logs
{
    public class Repository
    {
        private static readonly nLog Log = new nLog("Organizations.Table.Logs.Repository");
        private const int MaxLogsPerOrganization = 200;

        private static readonly ConcurrentDictionary<int, List<string>> LogsByOrganization = new ConcurrentDictionary<int, List<string>>();

        public static void AddLogs(ExtPlayer player, OrganizationLogsType type, string text)
        {
            try
            {
                if (player == null || !player.IsCharacterData())
                    return;

                var organizationData = player.GetOrganizationData();
                if (organizationData == null)
                    return;

                var uuid = player.GetUUID();
                AddInternal(organizationData.Id, $"[{DateTime.Now:dd.MM.yyyy HH:mm}] ({type}) {player.Name} ({uuid}): {text}");
            }
            catch (Exception e)
            {
                Log.Write($"AddLogs Exception: {e}");
            }
        }

        public static string GetLogs(ExtPlayer player, int memberUuid)
        {
            try
            {
                if (player == null || !player.IsCharacterData())
                    return "[]";

                var organizationData = player.GetOrganizationData();
                if (organizationData == null)
                    return "[]";

                if (!LogsByOrganization.TryGetValue(organizationData.Id, out var logs) || logs.Count == 0)
                    return "[]";

                return Newtonsoft.Json.JsonConvert.SerializeObject(logs);
            }
            catch (Exception e)
            {
                Log.Write($"GetLogs Exception: {e}");
                return "[]";
            }
        }

        private static void AddInternal(int organizationId, string entry)
        {
            var list = LogsByOrganization.GetOrAdd(organizationId, _ => new List<string>());

            lock (list)
            {
                list.Add(entry);
                if (list.Count > MaxLogsPerOrganization)
                    list.RemoveRange(0, list.Count - MaxLogsPerOrganization);
            }
        }
    }
}
