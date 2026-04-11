using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NeptuneEvo.Fractions.Models;
using NeptuneEvo.Handles;
using Redage.SDK;

namespace NeptuneEvo.Fractions.Table.Logs
{
    public class Repository
    {
        private static readonly nLog Log = new nLog("Fractions.Table.Logs.Repository");
        private const int MaxLogsPerFraction = 200;

        private static readonly ConcurrentDictionary<int, List<string>> LogsByFraction = new ConcurrentDictionary<int, List<string>>();

        public static void AddLogs(ExtPlayer player, FractionLogsType type, string text)
        {
            try
            {
                if (player == null || !player.IsCharacterData())
                    return;

                var fractionId = player.GetFractionId();
                if (fractionId <= 0)
                    return;

                var uuid = player.GetUUID();
                AddInternal(fractionId, $"[{DateTime.Now:dd.MM.yyyy HH:mm}] ({type}) {player.Name} ({uuid}): {text}");
            }
            catch (Exception e)
            {
                Log.Write($"AddLogs Exception: {e}");
            }
        }

        public static void AddOffLogs(int fractionId, string targetName, int targetUuid, FractionLogsType type, string text)
        {
            try
            {
                if (fractionId <= 0)
                    return;

                AddInternal(fractionId, $"[{DateTime.Now:dd.MM.yyyy HH:mm}] ({type}) {targetName} ({targetUuid}): {text}");
            }
            catch (Exception e)
            {
                Log.Write($"AddOffLogs Exception: {e}");
            }
        }

        public static string GetLogs(ExtPlayer player, int memberUuid)
        {
            try
            {
                if (player == null || !player.IsCharacterData())
                    return "[]";

                var fractionId = player.GetFractionId();
                if (fractionId <= 0)
                    return "[]";

                if (!LogsByFraction.TryGetValue(fractionId, out var logs) || logs.Count == 0)
                    return "[]";

                return Newtonsoft.Json.JsonConvert.SerializeObject(logs);
            }
            catch (Exception e)
            {
                Log.Write($"GetLogs Exception: {e}");
                return "[]";
            }
        }

        private static void AddInternal(int fractionId, string entry)
        {
            var list = LogsByFraction.GetOrAdd(fractionId, _ => new List<string>());

            lock (list)
            {
                list.Add(entry);
                if (list.Count > MaxLogsPerFraction)
                    list.RemoveRange(0, list.Count - MaxLogsPerFraction);
            }
        }
    }
}
