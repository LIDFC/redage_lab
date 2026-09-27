using MySqlConnector;
using Newtonsoft.Json;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace NeptuneEvo.BlackMarket.Audit
{
    /// <summary>Источник оплаты/средств в аудите.</summary>
    public static class Source
    {
        public const string Personal = "personal";
        public const string Fraction = "fraction";
        public const string Cash = "cash";
        public const string System = "system";
        public const string Admin = "admin";
    }

    /// <summary>
    /// Полный технический журнал для администрации: кто, кому, что, сколько, откуда, комиссия, результат.
    /// Игрокам не показывается.
    /// </summary>
    public static class AuditLog
    {
        public static void Write(string action, int actorUuid = 0, int targetUuid = 0, int itemId = 0, int count = 0,
            long amount = 0, string source = "", long fee = 0, string result = "ok", object details = null)
        {
            BlackMarketRepository.Enqueue(
                "INSERT INTO `blackmarket_audit` (`created`, `action`, `actor_uuid`, `target_uuid`, `item_id`, `count`, `amount`, `source`, `fee`, `result`, `details`) " +
                "VALUES (@created, @action, @actor, @target, @item, @count, @amount, @source, @fee, @result, @details)",
                ("@created", DateTime.Now), ("@action", action), ("@actor", actorUuid), ("@target", targetUuid),
                ("@item", itemId), ("@count", count), ("@amount", amount), ("@source", source ?? ""),
                ("@fee", fee), ("@result", result ?? "ok"),
                ("@details", details == null ? null : details as string ?? JsonConvert.SerializeObject(details)));
        }

        /// <summary>Последние записи журнала (для /bm logs): все или по участнику.</summary>
        public static async Task<List<string>> Read(int uuid, int limit)
        {
            var sql = uuid > 0
                ? "SELECT * FROM `blackmarket_audit` WHERE `actor_uuid`=@uuid OR `target_uuid`=@uuid ORDER BY `id` DESC LIMIT @limit"
                : "SELECT * FROM `blackmarket_audit` ORDER BY `id` DESC LIMIT @limit";
            using var command = new MySqlCommand(sql);
            command.Parameters.AddWithValue("@uuid", uuid);
            command.Parameters.AddWithValue("@limit", limit);
            using var data = await MySQL.QueryReadAsync(command);

            var lines = new List<string>();
            if (data == null)
                return lines;
            foreach (DataRow row in data.Rows)
            {
                var line = $"#{row["id"]} {Convert.ToDateTime(row["created"]):dd.MM HH:mm} {row["action"]} a:{row["actor_uuid"]} t:{row["target_uuid"]}";
                if (Convert.ToInt32(row["item_id"]) != 0) line += $" item:{row["item_id"]}x{row["count"]}";
                if (Convert.ToInt64(row["amount"]) != 0) line += $" {row["amount"]}";
                if (row["source"].ToString().Length > 0) line += $" [{row["source"]}]";
                if (Convert.ToInt64(row["fee"]) != 0) line += $" fee:{row["fee"]}";
                if (row["result"].ToString() != "ok") line += $" {row["result"]}";
                lines.Add(line);
            }
            return lines;
        }
    }
}
