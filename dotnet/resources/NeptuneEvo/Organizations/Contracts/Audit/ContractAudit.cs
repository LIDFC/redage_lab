using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using MySqlConnector;
using NeptuneEvo.Organizations.Models;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Organizations.Contracts.Audit
{
    /// <summary>
    /// Логи подрядов: понятная игрокам запись в существующие логи организации (orglogs)
    /// и полный технический журнал для администрации (org_contract_audit).
    /// </summary>
    public static class ContractAudit
    {
        /// <summary>Технический аудит (только для админов, /orgc logs).</summary>
        public static void Write(string action, int contractId = 0, int orgId = 0, int actorUuid = 0, long amount = 0,
            string result = "ok", object details = null)
        {
            ContractsRepository.Enqueue(
                "INSERT INTO `org_contract_audit` (`created`, `action`, `contract_id`, `org_id`, `actor_uuid`, `amount`, `result`, `details`) " +
                "VALUES (@created, @action, @contract, @org, @actor, @amount, @result, @details)",
                ("@created", DateTime.Now), ("@action", action), ("@contract", contractId), ("@org", orgId),
                ("@actor", actorUuid), ("@amount", amount), ("@result", result ?? "ok"),
                ("@details", details == null ? null : details as string ?? JsonConvert.SerializeObject(details)));
        }

        /// <summary>Запись во вкладку «Логи» организации. uuid = 0 — событие системы (дедлайн, админ).</summary>
        public static void OrgLog(int orgId, int uuid, string name, OrganizationLogsType type, string text)
        {
            try
            {
                Table.Logs.Repository.AddOffLogs(orgId, string.IsNullOrEmpty(name) ? "Система" : name, uuid, type, text);
            }
            catch (Exception e)
            {
                ContractsCore.Log.Write($"OrgLog Exception: {e}");
            }
        }

        public static async Task<List<string>> Read(int contractId, int orgId, int limit)
        {
            var sql = "SELECT * FROM `org_contract_audit`";
            if (contractId > 0) sql += " WHERE `contract_id`=@contract";
            else if (orgId > 0) sql += " WHERE `org_id`=@org";
            sql += " ORDER BY `id` DESC LIMIT @limit";

            using var command = new MySqlCommand(sql);
            command.Parameters.AddWithValue("@contract", contractId);
            command.Parameters.AddWithValue("@org", orgId);
            command.Parameters.AddWithValue("@limit", limit);
            using var data = await MySQL.QueryReadAsync(command);

            var lines = new List<string>();
            if (data == null)
                return lines;
            foreach (DataRow row in data.Rows)
            {
                var line = $"#{row["id"]} {Convert.ToDateTime(row["created"]):dd.MM HH:mm} {row["action"]} c:{row["contract_id"]} o:{row["org_id"]} a:{row["actor_uuid"]}";
                if (Convert.ToInt64(row["amount"]) != 0) line += $" {row["amount"]}";
                if (row["result"].ToString() != "ok") line += $" {row["result"]}";
                if (row["details"] != DBNull.Value && row["details"].ToString().Length > 0) line += $" {row["details"]}";
                lines.Add(line);
            }
            return lines;
        }
    }
}
