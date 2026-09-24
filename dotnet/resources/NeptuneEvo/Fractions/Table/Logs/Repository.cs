using System;
using System.Collections.Generic;
using System.Linq;
using Database;
using LinqToDB;
using NeptuneEvo.Fractions.Models;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Handles;
using NeptuneEvo.Table.Models;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Fractions.Table.Logs
{
    /// <summary>
    /// Журнал действий фракции (таблица fractionlogs) для планшета: вкладки «Личный», «Общий», «Склад».
    /// </summary>
    public class Repository
    {
        private static readonly nLog Log = new nLog("Fractions.Table.Logs");

        /// <summary>
        /// Размер страницы — совпадает с logsMax в src_client/table/frac.js
        /// </summary>
        private const int PageSize = 20;
        private const int MaxSearchLength = 64;

        /// <summary>
        /// Типы, которые показываются во вкладке «Склад»
        /// </summary>
        private static readonly sbyte[] StockTypes =
        {
            (sbyte)FractionLogsType.OpenStock,
            (sbyte)FractionLogsType.CloseStock,
            (sbyte)FractionLogsType.TakeMats,
            (sbyte)FractionLogsType.TakeDrugs,
            (sbyte)FractionLogsType.TakeMoney,
            (sbyte)FractionLogsType.TakeMedkits,
            (sbyte)FractionLogsType.TakeStock,
            (sbyte)FractionLogsType.TakeSpecial,
            (sbyte)FractionLogsType.TakeOre,
        };

        /// <summary>
        /// Записать действие игрока, состоящего во фракции.
        /// </summary>
        public static void AddLogs(ExtPlayer player, FractionLogsType type, string text)
        {
            var memberFractionData = player.GetFractionMemberData();
            if (memberFractionData == null)
                return;

            Insert(memberFractionData.Id, memberFractionData.Name, memberFractionData.UUID, memberFractionData.Rank, type, text);
        }

        /// <summary>
        /// Записать действие для игрока не в сети (например, варн с увольнением).
        /// </summary>
        public static void AddOffLogs(int fractionId, string name, int uuid, FractionLogsType type, string text)
        {
            var memberFractionData = Manager.GetFractionMemberData(uuid);
            var rank = memberFractionData != null ? memberFractionData.Rank : (byte)0;

            Insert(fractionId, name, uuid, rank, type, text);
        }

        private static void Insert(int fractionId, string name, int uuid, byte rank, FractionLogsType type, string text)
        {
            if (fractionId <= 0)
                return;

            var time = DateTime.Now;

            Trigger.SetTask(async () =>
            {
                try
                {
                    await using var db = new ServerBD("MainDB");

                    await db.InsertAsync(new Fractionlogs
                    {
                        Fraction = (sbyte)fractionId,
                        Name = name ?? "",
                        Uuid = uuid,
                        Rank = (sbyte)rank,
                        Text = text ?? "",
                        Type = (sbyte)type,
                        Time = time,
                    });
                }
                catch (Exception e)
                {
                    Log.Write($"Insert Exception: {e}");
                }
            });
        }

        /// <summary>
        /// Первая страница личных логов при открытии планшета.
        /// </summary>
        public static void GetLogs(ExtPlayer player, int uuid) =>
            GetLog(player, uuid, false, "", 0);

        /// <summary>
        /// Страница логов. uuid = -1 — все участники; isStock — только действия со складом.
        /// Чужие и общие логи доступны только с правом RankToAccess.Logs.
        /// </summary>
        public static void GetLog(ExtPlayer player, int uuid, bool isStock, string text, int pageId)
        {
            try
            {
                var memberFractionData = player.GetFractionMemberData();
                if (memberFractionData == null)
                    return;

                var isOwn = uuid == memberFractionData.UUID && !isStock;
                if (!isOwn && !player.IsFractionAccess(RankToAccess.Logs))
                    return;

                if (pageId < 0)
                    pageId = 0;

                text = (text ?? "").Trim();
                if (text.Length > MaxSearchLength)
                    text = text.Substring(0, MaxSearchLength);

                var fractionId = (sbyte)memberFractionData.Id;

                Trigger.SetTask(async () =>
                {
                    try
                    {
                        await using var db = new ServerBD("MainDB");

                        var query = db.Fractionlogs.Where(l => l.Fraction == fractionId);

                        if (uuid != -1)
                            query = query.Where(l => l.Uuid == uuid);

                        if (isStock)
                            query = query.Where(l => StockTypes.Contains(l.Type));

                        if (text.Length > 0)
                            query = query.Where(l => l.Text.Contains(text) || l.Name.Contains(text));

                        var logs = await query
                            .OrderByDescending(l => l.AutoId)
                            .Skip(pageId * PageSize)
                            .Take(PageSize)
                            .ToListAsync();

                        var result = logs
                            .Select(l => new List<object> { l.Text, l.Time, l.Uuid, l.Name, l.Rank })
                            .ToList();

                        Trigger.ClientEvent(player, "client.frac.main.logs", JsonConvert.SerializeObject(result));
                    }
                    catch (Exception e)
                    {
                        Log.Write($"GetLog Task Exception: {e}");
                    }
                });
            }
            catch (Exception e)
            {
                Log.Write($"GetLog Exception: {e}");
            }
        }
    }
}
