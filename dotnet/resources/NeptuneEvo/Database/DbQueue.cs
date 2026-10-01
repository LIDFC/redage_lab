using System;
using System.Collections.Concurrent;
using System.Data;
using System.Threading;
using MySqlConnector;
using Redage.SDK;

namespace NeptuneEvo.Database
{
    /// <summary>
    /// Общая очередь записи в БД для новых систем (армия, качалка, подработки, история денег, /cfg, трава).
    /// Одна фоновая нить пишет команды строго по порядку: главный поток не ждёт MySQL, поздняя запись не обгонит раннюю.
    /// Нить запускается сама при первой записи — не зависит от того, стартовал ли какой-то модуль.
    /// Перед рестартом/остановкой — Flush() (вызывается рядом с сохранением сервера, Core/Admin.cs).
    /// </summary>
    public static class DbQueue
    {
        private static readonly nLog Log = new nLog("Database.DbQueue");
        private static readonly BlockingCollection<MySqlCommand> Queue = new BlockingCollection<MySqlCommand>();
        private static readonly object StartLock = new object();
        private static int _pending;
        private static Thread _writer;

        private static void EnsureWriter()
        {
            if (_writer != null)
                return;
            lock (StartLock)
            {
                if (_writer != null)
                    return;
                _writer = new Thread(WriterLoop) { IsBackground = true, Name = "DbQueue" };
                _writer.Start();
            }
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

        public static void Enqueue(MySqlCommand command)
        {
            EnsureWriter();
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

        /// <summary>Синхронное чтение (как раньше, из главного потока).</summary>
        public static DataTable Read(string sql, params (string name, object value)[] parameters)
        {
            using var command = new MySqlCommand(sql);
            foreach (var (name, value) in parameters)
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            return MySQL.QueryRead(command);
        }

        /// <summary>
        /// Чтение без фриза: запрос выполняется в фоне, onMain вызывается в игровом потоке с результатом
        /// (null при ошибке). Использовать вместо Read во всём, что срабатывает во время игры.
        /// </summary>
        public static void ReadThen(string sql, Action<DataTable> onMain, params (string name, object value)[] parameters)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                DataTable table = null;
                try
                {
                    table = Read(sql, parameters);
                }
                catch (Exception e)
                {
                    Log.Write($"ReadThen Exception: {e.Message}");
                }
                GTANetworkAPI.NAPI.Task.Run(() =>
                {
                    try
                    {
                        onMain(table);
                    }
                    catch (Exception e)
                    {
                        Log.Write($"ReadThen callback Exception: {e}");
                    }
                    finally
                    {
                        table?.Dispose();
                    }
                });
            });
        }

        /// <summary>Дождаться записи всей очереди (перед рестартом/сохранением сервера).</summary>
        public static void Flush(int timeoutMs = 15000)
        {
            var until = DateTime.Now.AddMilliseconds(timeoutMs);
            while (Volatile.Read(ref _pending) > 0 && DateTime.Now < until)
                Thread.Sleep(20);
        }
    }
}
