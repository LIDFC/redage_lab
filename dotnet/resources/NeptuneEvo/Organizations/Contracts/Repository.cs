using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading;
using GTANetworkAPI;
using MySqlConnector;
using NeptuneEvo.Organizations.Contracts.Models;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Organizations.Contracts
{
    /// <summary>
    /// БД подрядов. Состояние живёт в памяти и меняется только под <see cref="ContractsCore.Sync"/>,
    /// записи уходят в одну очередь и выполняются одним фоновым потоком строго по порядку.
    /// Связанные изменения (статус контракта + деньги и репутация организации) пишутся одной транзакцией.
    /// </summary>
    public static class ContractsRepository
    {
        private static readonly BlockingCollection<MySqlCommand> Queue = new BlockingCollection<MySqlCommand>();
        private static int _pending = 0;
        private static Thread _writer;

        public static void Init()
        {
            EnsureTables();

            if (_writer != null)
                return;
            _writer = new Thread(WriterLoop) { IsBackground = true, Name = "OrgContractsDB" };
            _writer.Start();
        }

        private static void WriterLoop()
        {
            foreach (var command in Queue.GetConsumingEnumerable())
            {
                try
                {
                    MySQL.Query(command);
                }
                catch (Exception e)
                {
                    ContractsCore.Log.Write($"Writer Exception: {e}");
                }
                finally
                {
                    command.Dispose();
                    Interlocked.Decrement(ref _pending);
                }
            }
        }

        public static void Enqueue(MySqlCommand command)
        {
            Interlocked.Increment(ref _pending);
            Queue.Add(command);
        }

        public static void Enqueue(string sql, params (string name, object value)[] parameters)
        {
            var command = new MySqlCommand(sql);
            foreach (var (name, value) in parameters)
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            Enqueue(command);
        }

        /// <summary>
        /// Несколько запросов одной транзакцией (один MySqlCommand: при ошибке COMMIT не выполняется,
        /// соединение закрывается — MariaDB откатывает всё).
        /// </summary>
        public static void EnqueueTransaction(params MySqlCommand[] commands)
        {
            var batch = new MySqlCommand();
            var sql = "START TRANSACTION;";
            var index = 0;
            foreach (var command in commands)
            {
                var text = command.CommandText;
                foreach (MySqlParameter parameter in command.Parameters)
                {
                    var name = $"{parameter.ParameterName}_{index}";
                    text = Regex.Replace(text, Regex.Escape(parameter.ParameterName) + @"\b", name);
                    batch.Parameters.AddWithValue(name, parameter.Value ?? DBNull.Value);
                }
                sql += text.TrimEnd(';') + ";";
                index++;
                command.Dispose();
            }
            batch.CommandText = sql + "COMMIT;";
            Enqueue(batch);
        }

        public static void Flush(int timeoutMs = 15000)
        {
            var until = DateTime.Now.AddMilliseconds(timeoutMs);
            while (Volatile.Read(ref _pending) > 0 && DateTime.Now < until)
                Thread.Sleep(20);
        }

        public static DataTable Read(string sql, params (string name, object value)[] parameters)
        {
            using var command = new MySqlCommand(sql);
            foreach (var (name, value) in parameters)
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            return MySQL.QueryRead(command);
        }

        public static MySqlCommand Command(string sql, params (string name, object value)[] parameters)
        {
            var command = new MySqlCommand(sql);
            foreach (var (name, value) in parameters)
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            return command;
        }

        // ------------------------------------------------------------------ контракты

        private const string UpsertSql =
            "INSERT INTO `org_contracts` (`id`,`template_id`,`type`,`title`,`description`,`status`,`org_id`,`accepted_uuid`,`accepted_name`," +
            "`reward`,`penalty`,`required_rep`,`rep_reward`,`rep_penalty`,`deadline_minutes`,`deadline_at`,`point_name`,`pos_x`,`pos_y`,`pos_z`," +
            "`materials`,`source`,`gen_slot`,`created_at`,`accepted_at`,`finished_at`) VALUES " +
            "(@id,@tpl,@type,@title,@descr,@status,@org,@uuid,@uname,@reward,@penalty,@reqrep,@reprew,@reppen,@dlmin,@dlat,@pname,@px,@py,@pz," +
            "@materials,@source,@slot,@created,@accepted,@finished) " +
            "ON DUPLICATE KEY UPDATE `status`=@status,`org_id`=@org,`accepted_uuid`=@uuid,`accepted_name`=@uname,`reward`=@reward,`penalty`=@penalty," +
            "`required_rep`=@reqrep,`rep_reward`=@reprew,`rep_penalty`=@reppen,`deadline_minutes`=@dlmin,`deadline_at`=@dlat,`point_name`=@pname," +
            "`pos_x`=@px,`pos_y`=@py,`pos_z`=@pz,`materials`=@materials,`accepted_at`=@accepted,`finished_at`=@finished";

        /// <summary>Полный снимок контракта (вставка или обновление) — команда для очереди/транзакции.</summary>
        public static MySqlCommand SaveCommand(Contract contract) =>
            Command(UpsertSql,
                ("@id", contract.Id), ("@tpl", contract.TemplateId), ("@type", contract.Type), ("@title", contract.Title),
                ("@descr", contract.Description), ("@status", (byte)contract.Status), ("@org", contract.OrganizationId),
                ("@uuid", contract.AcceptedByUuid), ("@uname", contract.AcceptedByName ?? ""),
                ("@reward", contract.Reward), ("@penalty", contract.Penalty), ("@reqrep", contract.RequiredReputation),
                ("@reprew", contract.ReputationReward), ("@reppen", contract.ReputationPenalty),
                ("@dlmin", contract.DeadlineMinutes), ("@dlat", contract.DeadlineAt),
                ("@pname", contract.PointName ?? ""), ("@px", contract.DeliveryPosition.X), ("@py", contract.DeliveryPosition.Y), ("@pz", contract.DeliveryPosition.Z),
                ("@materials", JsonConvert.SerializeObject(contract.Materials)), ("@source", (byte)contract.Source),
                ("@slot", contract.GenSlot ?? ""), ("@created", contract.CreatedAt), ("@accepted", contract.AcceptedAt), ("@finished", contract.FinishedAt));

        public static void Save(Contract contract) =>
            Enqueue(SaveCommand(contract));

        /// <summary>Деньги и репутация организации — пишутся сразу (а не только в часовом SaveOrganizations).</summary>
        public static MySqlCommand OrganizationCommand(int organizationId, int money, int reputation) =>
            Command("UPDATE `organizations` SET `Money`=@money, `reputation`=@rep WHERE `Organization`=@org",
                ("@money", money), ("@rep", reputation), ("@org", organizationId));

        public static List<Contract> LoadContracts()
        {
            var list = new List<Contract>();
            using var data = Read("SELECT * FROM `org_contracts` WHERE `status` IN (0, 1)");
            if (data == null)
                return list;

            foreach (DataRow row in data.Rows)
            {
                try
                {
                    list.Add(new Contract
                    {
                        Id = Convert.ToInt32(row["id"]),
                        TemplateId = row["template_id"].ToString(),
                        Type = row["type"].ToString(),
                        Title = row["title"].ToString(),
                        Description = row["description"].ToString(),
                        Status = (ContractStatus)Convert.ToByte(row["status"]),
                        OrganizationId = Convert.ToInt32(row["org_id"]),
                        AcceptedByUuid = Convert.ToInt32(row["accepted_uuid"]),
                        AcceptedByName = row["accepted_name"].ToString(),
                        Reward = Convert.ToInt32(row["reward"]),
                        Penalty = Convert.ToInt32(row["penalty"]),
                        RequiredReputation = Convert.ToInt32(row["required_rep"]),
                        ReputationReward = Convert.ToInt32(row["rep_reward"]),
                        ReputationPenalty = Convert.ToInt32(row["rep_penalty"]),
                        DeadlineMinutes = Convert.ToInt32(row["deadline_minutes"]),
                        DeadlineAt = row["deadline_at"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["deadline_at"]),
                        PointName = row["point_name"].ToString(),
                        DeliveryPosition = new Vector3(Convert.ToSingle(row["pos_x"]), Convert.ToSingle(row["pos_y"]), Convert.ToSingle(row["pos_z"])),
                        Materials = JsonConvert.DeserializeObject<List<ContractMaterial>>(row["materials"].ToString()) ?? new List<ContractMaterial>(),
                        Source = (ContractSource)Convert.ToByte(row["source"]),
                        GenSlot = row["gen_slot"].ToString(),
                        CreatedAt = Convert.ToDateTime(row["created_at"]),
                        AcceptedAt = row["accepted_at"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["accepted_at"]),
                        FinishedAt = row["finished_at"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["finished_at"]),
                    });
                }
                catch (Exception e)
                {
                    ContractsCore.Log.Write($"LoadContracts row Exception: {e}");
                }
            }
            return list;
        }

        public static int LoadLastId()
        {
            using var data = Read("SELECT IFNULL(MAX(`id`), 0) AS `id` FROM `org_contracts`");
            return data == null || data.Rows.Count == 0 ? 0 : Convert.ToInt32(data.Rows[0]["id"]);
        }

        /// <summary>Последний выполненный слот генерации — чтобы рестарт не создавал набор повторно.</summary>
        public static string LoadLastSlot(bool scheduledOnly)
        {
            using var data = Read("SELECT `slot` FROM `org_contract_gen`" + (scheduledOnly ? " WHERE `slot` NOT LIKE 'force%'" : "") + " ORDER BY `created_at` DESC, `slot` DESC LIMIT 1");
            return data == null || data.Rows.Count == 0 ? null : data.Rows[0]["slot"].ToString();
        }

        public static void SaveSlot(string slot) =>
            Enqueue("INSERT IGNORE INTO `org_contract_gen` (`slot`, `created_at`) VALUES (@slot, @created)",
                ("@slot", slot), ("@created", DateTime.Now));

        public static Dictionary<int, int> LoadReputations()
        {
            var result = new Dictionary<int, int>();
            using var data = Read("SELECT `Organization`, `reputation` FROM `organizations`");
            if (data == null)
                return result;
            foreach (DataRow row in data.Rows)
                result[Convert.ToInt32(row["Organization"])] = Convert.ToInt32(row["reputation"]);
            return result;
        }

        private static void EnsureTables()
        {
            var tables = new[]
            {
                "ALTER TABLE `organizations` ADD COLUMN IF NOT EXISTS `reputation` INT NOT NULL DEFAULT 0",
                @"CREATE TABLE IF NOT EXISTS `org_contracts` (
                    `id` int(11) NOT NULL,
                    `template_id` varchar(64) NOT NULL,
                    `type` varchar(64) NOT NULL DEFAULT '',
                    `title` varchar(128) NOT NULL DEFAULT '',
                    `description` varchar(512) NOT NULL DEFAULT '',
                    `status` tinyint(4) NOT NULL DEFAULT 0,
                    `org_id` int(11) NOT NULL DEFAULT 0,
                    `accepted_uuid` int(11) NOT NULL DEFAULT 0,
                    `accepted_name` varchar(64) NOT NULL DEFAULT '',
                    `reward` int(11) NOT NULL DEFAULT 0,
                    `penalty` int(11) NOT NULL DEFAULT 0,
                    `required_rep` int(11) NOT NULL DEFAULT 0,
                    `rep_reward` int(11) NOT NULL DEFAULT 0,
                    `rep_penalty` int(11) NOT NULL DEFAULT 0,
                    `deadline_minutes` int(11) NOT NULL DEFAULT 0,
                    `deadline_at` datetime NULL DEFAULT NULL,
                    `point_name` varchar(128) NOT NULL DEFAULT '',
                    `pos_x` float NOT NULL DEFAULT 0,
                    `pos_y` float NOT NULL DEFAULT 0,
                    `pos_z` float NOT NULL DEFAULT 0,
                    `materials` text NOT NULL,
                    `source` tinyint(4) NOT NULL DEFAULT 0,
                    `gen_slot` varchar(32) NOT NULL DEFAULT '',
                    `created_at` datetime NOT NULL,
                    `accepted_at` datetime NULL DEFAULT NULL,
                    `finished_at` datetime NULL DEFAULT NULL,
                    PRIMARY KEY (`id`),
                    KEY `status` (`status`),
                    KEY `org_id` (`org_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                @"CREATE TABLE IF NOT EXISTS `org_contract_gen` (
                    `slot` varchar(32) NOT NULL,
                    `created_at` datetime NOT NULL,
                    PRIMARY KEY (`slot`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                @"CREATE TABLE IF NOT EXISTS `org_contract_npc` (
                    `org_id` int(11) NOT NULL,
                    `day` date NOT NULL,
                    `uuid` int(11) NOT NULL DEFAULT 0,
                    `result` varchar(32) NOT NULL DEFAULT '',
                    PRIMARY KEY (`org_id`, `day`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                @"CREATE TABLE IF NOT EXISTS `org_cargo` (
                    `id` int(11) NOT NULL,
                    `owner_type` varchar(16) NOT NULL DEFAULT 'org',
                    `owner_id` int(11) NOT NULL DEFAULT 0,
                    `contract_id` int(11) NOT NULL DEFAULT 0,
                    `cargo_type` varchar(32) NOT NULL,
                    `quantity` int(11) NOT NULL DEFAULT 0,
                    `state` tinyint(4) NOT NULL DEFAULT 0,
                    `vehicle_number` varchar(16) NOT NULL DEFAULT '',
                    `carrier_uuid` int(11) NOT NULL DEFAULT 0,
                    `pos_x` float NOT NULL DEFAULT 0,
                    `pos_y` float NOT NULL DEFAULT 0,
                    `pos_z` float NOT NULL DEFAULT 0,
                    `dimension` int(11) NOT NULL DEFAULT 0,
                    `created_at` datetime NOT NULL,
                    PRIMARY KEY (`id`),
                    KEY `owner` (`owner_type`, `owner_id`),
                    KEY `vehicle_number` (`vehicle_number`),
                    KEY `state` (`state`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                @"CREATE TABLE IF NOT EXISTS `org_contract_audit` (
                    `id` bigint(20) NOT NULL AUTO_INCREMENT,
                    `created` datetime NOT NULL,
                    `action` varchar(32) NOT NULL,
                    `contract_id` int(11) NOT NULL DEFAULT 0,
                    `org_id` int(11) NOT NULL DEFAULT 0,
                    `actor_uuid` int(11) NOT NULL DEFAULT 0,
                    `amount` bigint(20) NOT NULL DEFAULT 0,
                    `result` varchar(32) NOT NULL DEFAULT 'ok',
                    `details` text NULL,
                    PRIMARY KEY (`id`),
                    KEY `contract_id` (`contract_id`),
                    KEY `org_id` (`org_id`),
                    KEY `action` (`action`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            };

            foreach (var sql in tables)
            {
                using var command = new MySqlCommand(sql);
                MySQL.Query(command);
            }
        }
    }
}
