using System;
using System.Collections.Generic;
using System.Linq;
using Database;
using LinqToDB;
using NeptuneEvo.Organizations.Models;
using NeptuneEvo.Organizations.Player;
using NeptuneEvo.Handles;
using NeptuneEvo.Table.Models;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Organizations.Table.Logs
{
    /// <summary>
    /// Журнал действий организации/семьи (таблица orglogs) для планшета: вкладки «Личный», «Общий», «Склад».
    /// </summary>
    public class Repository
    {
        private static readonly nLog Log = new nLog("Organizations.Table.Logs");

        /// <summary>
        /// Размер страницы — совпадает с logsMax в src_client/table/org.js
        /// </summary>
        private const int PageSize = 20;
        private const int MaxSearchLength = 64;

        /// <summary>
        /// Типы, которые показываются во вкладке «Склад»
        /// </summary>
        private static readonly sbyte[] StockTypes =
        {
            (sbyte)OrganizationLogsType.OpenStock,
            (sbyte)OrganizationLogsType.CloseStock,
            (sbyte)OrganizationLogsType.TakeMats,
            (sbyte)OrganizationLogsType.TakeDrugs,
            (sbyte)OrganizationLogsType.TakeMoney,
            (sbyte)OrganizationLogsType.TakeMedkits,
            (sbyte)OrganizationLogsType.TakeStock,
        };

        /// <summary>
        /// Записать действие игрока, состоящего в организации.
        /// </summary>
        public static void AddLogs(ExtPlayer player, OrganizationLogsType type, string text)
        {
            var memberOrganizationData = player.GetOrganizationMemberData();
            if (memberOrganizationData == null)
                return;

            Insert(memberOrganizationData.Id, memberOrganizationData.Name, memberOrganizationData.UUID, memberOrganizationData.Rank, type, text);
        }

        /// <summary>
        /// Записать действие для игрока не в сети (например, варн с увольнением).
        /// </summary>
        public static void AddOffLogs(int organizationId, string name, int uuid, OrganizationLogsType type, string text)
        {
            var memberOrganizationData = Manager.GetOrganizationMemberData(uuid);
            var rank = memberOrganizationData != null ? memberOrganizationData.Rank : (byte)0;

            Insert(organizationId, name, uuid, rank, type, text);
        }

        private static void Insert(int organizationId, string name, int uuid, byte rank, OrganizationLogsType type, string text)
        {
            if (organizationId <= 0)
                return;

            var time = DateTime.Now;

            Trigger.SetTask(async () =>
            {
                try
                {
                    await using var db = new ServerBD("MainDB");

                    await db.InsertAsync(new Orglogs
                    {
                        Organization = (short)organizationId,
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
                var memberOrganizationData = player.GetOrganizationMemberData();
                if (memberOrganizationData == null)
                    return;

                var isOwn = uuid == memberOrganizationData.UUID && !isStock;
                if (!isOwn && !player.IsOrganizationAccess(RankToAccess.Logs))
                    return;

                if (pageId < 0)
                    pageId = 0;

                text = (text ?? "").Trim();
                if (text.Length > MaxSearchLength)
                    text = text.Substring(0, MaxSearchLength);

                var organizationId = (short)memberOrganizationData.Id;

                Trigger.SetTask(async () =>
                {
                    try
                    {
                        await using var db = new ServerBD("MainDB");

                        var query = db.Orglogs.Where(l => l.Organization == organizationId);

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

                        Trigger.ClientEvent(player, "client.org.main.logs", JsonConvert.SerializeObject(result));
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
