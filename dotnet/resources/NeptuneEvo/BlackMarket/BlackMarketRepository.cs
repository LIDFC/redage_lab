using MySqlConnector;
using Redage.SDK;
using System;
using System.Collections.Concurrent;
using System.Data;
using System.Threading;

namespace NeptuneEvo.BlackMarket
{
    /// <summary>
    /// БД Чёрного рынка. Состояние живёт в памяти и меняется только под <see cref="BlackMarketCore.Sync"/>,
    /// а записи в БД уходят в одну очередь и выполняются одним фоновым потоком строго по порядку:
    /// главный поток не ждёт MySQL, а более поздняя запись не может обогнать раннюю.
    /// </summary>
    public static class BlackMarketRepository
    {
        private static readonly nLog Log = new nLog("BlackMarket.DB");
        private static readonly BlockingCollection<MySqlCommand> Queue = new BlockingCollection<MySqlCommand>();
        private static int _pending = 0;
        private static Thread _writer;

        public static void Init()
        {
            EnsureTables();

            if (_writer != null)
                return;
            _writer = new Thread(WriterLoop) { IsBackground = true, Name = "BlackMarketDB" };
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
                    Log.Write($"Writer Exception: {e}");
                }
                finally
                {
                    command.Dispose();
                    Interlocked.Decrement(ref _pending);
                }
            }
        }

        /// <summary>Поставить команду в очередь записи (параметры уже заданы).</summary>
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

        /// <summary>Дождаться записи всех изменений (перед рестартом/сохранением сервера).</summary>
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

        private static void EnsureTables()
        {
            var tables = new[]
            {
                @"CREATE TABLE IF NOT EXISTS `crypto_wallets` (
                    `uuid` int(11) NOT NULL,
                    `balance` bigint(20) NOT NULL DEFAULT 0,
                    `reserved` bigint(20) NOT NULL DEFAULT 0,
                    PRIMARY KEY (`uuid`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                @"CREATE TABLE IF NOT EXISTS `crypto_fraction_wallets` (
                    `fraction_id` int(11) NOT NULL,
                    `balance` bigint(20) NOT NULL DEFAULT 0,
                    PRIMARY KEY (`fraction_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                @"CREATE TABLE IF NOT EXISTS `crypto_system` (
                    `id` int(11) NOT NULL,
                    `balance` bigint(20) NOT NULL DEFAULT 0,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                @"CREATE TABLE IF NOT EXISTS `crypto_history` (
                    `id` bigint(20) NOT NULL AUTO_INCREMENT,
                    `uuid` int(11) NOT NULL DEFAULT 0,
                    `fraction_id` int(11) NOT NULL DEFAULT 0,
                    `kind` varchar(32) NOT NULL,
                    `title` varchar(128) NOT NULL,
                    `amount` bigint(20) NOT NULL,
                    `currency` varchar(8) NOT NULL DEFAULT 'BTC',
                    `created` datetime NOT NULL,
                    PRIMARY KEY (`id`),
                    KEY `uuid` (`uuid`),
                    KEY `fraction_id` (`fraction_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                @"CREATE TABLE IF NOT EXISTS `blackmarket_audit` (
                    `id` bigint(20) NOT NULL AUTO_INCREMENT,
                    `created` datetime NOT NULL,
                    `action` varchar(32) NOT NULL,
                    `actor_uuid` int(11) NOT NULL DEFAULT 0,
                    `target_uuid` int(11) NOT NULL DEFAULT 0,
                    `item_id` int(11) NOT NULL DEFAULT 0,
                    `count` int(11) NOT NULL DEFAULT 0,
                    `amount` bigint(20) NOT NULL DEFAULT 0,
                    `source` varchar(32) NOT NULL DEFAULT '',
                    `fee` bigint(20) NOT NULL DEFAULT 0,
                    `result` varchar(32) NOT NULL DEFAULT 'ok',
                    `details` text NULL,
                    PRIMARY KEY (`id`),
                    KEY `actor_uuid` (`actor_uuid`),
                    KEY `target_uuid` (`target_uuid`),
                    KEY `action` (`action`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                @"CREATE TABLE IF NOT EXISTS `blackmarket_lots` (
                    `id` int(11) NOT NULL,
                    `owner_uuid` int(11) NOT NULL,
                    `item_id` int(11) NOT NULL,
                    `count` int(11) NOT NULL,
                    `price_unit` bigint(20) NOT NULL,
                    `created` datetime NOT NULL,
                    `ends` datetime NOT NULL,
                    PRIMARY KEY (`id`),
                    KEY `owner_uuid` (`owner_uuid`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                @"CREATE TABLE IF NOT EXISTS `blackmarket_drops` (
                    `id` int(11) NOT NULL,
                    `buyer_uuid` int(11) NOT NULL,
                    `lot_id` int(11) NOT NULL,
                    `item_id` int(11) NOT NULL,
                    `count` int(11) NOT NULL,
                    `pos_x` float NOT NULL,
                    `pos_y` float NOT NULL,
                    `pos_z` float NOT NULL,
                    `created` datetime NOT NULL,
                    `expires` datetime NOT NULL,
                    PRIMARY KEY (`id`),
                    KEY `buyer_uuid` (`buyer_uuid`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                @"CREATE TABLE IF NOT EXISTS `crypto_p2p` (
                    `id` int(11) NOT NULL,
                    `owner_uuid` int(11) NOT NULL,
                    `amount_left` bigint(20) NOT NULL,
                    `price_per_btc` decimal(18,4) NOT NULL,
                    `created` datetime NOT NULL,
                    `ends` datetime NOT NULL,
                    PRIMARY KEY (`id`),
                    KEY `owner_uuid` (`owner_uuid`)
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
