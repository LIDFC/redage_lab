using Newtonsoft.Json;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using MySqlConnector;

namespace NeptuneEvo.BlackMarket.History
{
    public class HistoryEntry
    {
        [JsonProperty("date")] public DateTime Date { get; set; }
        [JsonProperty("kind")] public string Kind { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("amount")] public long Amount { get; set; }
        [JsonProperty("currency")] public string Currency { get; set; }
    }

    /// <summary>
    /// История операций для игроков: дата, что, сумма. Без контрагентов, UUID и фракций — это только в аудите.
    /// Запись с fraction_id показывается в истории кошелька банды.
    /// </summary>
    public static class HistoryLog
    {
        public const string Btc = "BTC";
        public const string Usd = "$";

        public static void Add(int uuid, string kind, string title, long amount, string currency = Btc, int fractionId = 0)
        {
            BlackMarketRepository.Enqueue(
                "INSERT INTO `crypto_history` (`uuid`, `fraction_id`, `kind`, `title`, `amount`, `currency`, `created`) " +
                "VALUES (@uuid, @fraction, @kind, @title, @amount, @currency, @created)",
                ("@uuid", uuid), ("@fraction", fractionId), ("@kind", kind), ("@title", Cut(title, 128)),
                ("@amount", amount), ("@currency", currency), ("@created", DateTime.Now));
        }

        public static async Task<List<HistoryEntry>> GetPlayer(int uuid, int limit = 100) =>
            await Read("SELECT * FROM `crypto_history` WHERE `uuid`=@id AND `fraction_id`=0 ORDER BY `id` DESC LIMIT @limit", uuid, limit);

        public static async Task<List<HistoryEntry>> GetFraction(int fractionId, int limit = 100) =>
            await Read("SELECT * FROM `crypto_history` WHERE `fraction_id`=@id ORDER BY `id` DESC LIMIT @limit", fractionId, limit);

        private static async Task<List<HistoryEntry>> Read(string sql, int id, int limit)
        {
            var list = new List<HistoryEntry>();
            using var command = new MySqlCommand(sql);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@limit", limit);
            using var data = await MySQL.QueryReadAsync(command);
            if (data == null)
                return list;
            foreach (DataRow row in data.Rows)
                list.Add(new HistoryEntry
                {
                    Date = Convert.ToDateTime(row["created"]),
                    Kind = row["kind"].ToString(),
                    Title = row["title"].ToString(),
                    Amount = Convert.ToInt64(row["amount"]),
                    Currency = row["currency"].ToString(),
                });
            return list;
        }

        private static string Cut(string value, int max) =>
            string.IsNullOrEmpty(value) ? "" : value.Length <= max ? value : value.Substring(0, max);
    }
}
