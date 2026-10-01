using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GTANetworkAPI;

namespace Redage.SDK
{
    /// <summary>
    /// 
    /// </summary>
    public static class Timers
    {
        /// <summary>
        /// 
        /// </summary>
        public static ConcurrentDictionary<string, nTimer> TimersData = new ConcurrentDictionary<string, nTimer>();
        /// <summary>
        /// 
        /// </summary>
        public static nLog Log = new nLog("nTimer");
        private static Thread thread;

        /// <summary>Обработчик таймера дольше этого (мс) — пишем в лог «долгий таймер» с именем.</summary>
        public static int SlowMs = 50;
        private static readonly ConcurrentDictionary<string, DateTime> SlowReported = new ConcurrentDictionary<string, DateTime>();

        /// <summary>Понятное имя таймера для лога: свой ID или класс.метод обработчика.</summary>
        internal static string NameOf(nTimer timer)
        {
            var method = timer.action?.Method;
            var where = method == null ? "?" : $"{method.DeclaringType?.FullName?.Replace("NeptuneEvo.", "")}.{method.Name}";
            return Guid.TryParse(timer.ID, out _) ? where : $"{timer.ID} ({where})";
        }

        /// <summary>Не чаще раза в минуту на один таймер, чтобы лог не забивался.</summary>
        internal static void ReportSlow(nTimer timer, long ms, string thread)
        {
            var name = NameOf(timer);
            var now = DateTime.Now;
            if (SlowReported.TryGetValue(name, out var last) && (now - last).TotalSeconds < 60)
                return;
            SlowReported[name] = now;
            Log.Write($"Долгий таймер {ms} мс [{thread}]: {name}", nLog.Type.Warn);
        }

        private static readonly Random Jitter = new Random();
        /// <summary>
        /// Разнос повторяющихся таймеров: первый запуск сдвигается на случайные 0–10% интервала (не больше 3 с),
        /// чтобы таймеры с одинаковым интервалом, созданные при старте, не срабатывали в один момент.
        /// </summary>
        internal static int StartJitter(int ms)
        {
            if (ms < 1000)
                return 0;
            lock (Jitter)
                return Jitter.Next(0, Math.Min(3000, ms / 10) + 1);
        }

        /// <summary>
        /// 
        /// </summary>
        public static void Init()
        {
            thread = new Thread(Logic);
            thread.IsBackground = true;
            thread.Name = "nTimer";
            thread.Start();
        }
        private static void Logic()
        {
            try
            {
                while (true)
                {
                    try
                    {
                        foreach (var pair in TimersData)
                        {
                            try
                            {
                                nTimer timer = pair.Value;
                                if (timer != null && !timer.isFinished) timer.Elapsed();
                                else if (timer != null && timer.isFinished) TimersData.TryRemove(pair.Key, out _);
                            }
                            catch (Exception e)
                            {
                                Log.Write($"Logic Foreach Exception: {e.ToString()}");
                            }
                        }
                        Thread.Sleep(100);
                    }
                    catch (Exception e)
                    {
                        Log.Write($"Logic While Exception: {e.ToString()}");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"Logic Exception: {e.ToString()}");
            }
        }

        /// <summary>
        /// Находит и возвращает объект таймера
        /// </summary>
        /// <param name="id">Уникальный идентификатор таймера</param>
        /// <returns>Объект таймера</returns>
        public static nTimer Get(string id)
        {
            try
            {
                if (TimersData.ContainsKey(id)) return TimersData[id];
                return null;
            }
            catch (Exception e)
            {
                Log.Write($"Get Exception: {e.ToString()}");
                return null;
            }
        }

        /// <summary>
        /// Start() запускает таймер и возвращает случайный ID
        /// </summary>
        /// <param name="interval">Интервал срабатывания действия</param>
        /// <param name="action">Лямбда-выражение с действием</param>
        /// <param name="isnapitask">Нужно ли выполнить это в главном потоке</param>
        /// <returns>Уникальный ID таймера</returns>
        public static string Start(int interval, Action action, bool isnapitask = false)
        {
            try
            {
                string id = Guid.NewGuid().ToString();
                nTimer newtimer = new nTimer(action, id, interval, isnapitask_: isnapitask);
                lock (TimersData)
                {
                    TimersData.TryAdd(id, newtimer);
                }
                return id;
            }
            catch (Exception e)
            {
                Log.Write($"Start Exception: {e.ToString()}");
                return null;
            }
        }
        /// <summary>
        /// Start() запускает таймер с уникальным ID
        /// </summary>
        /// <exception>
        /// Exception возникает при передаче уже существующего ID или значения null
        /// </exception>
        /// <param name="id">Уникальный идентификатор таймера</param>
        /// <param name="interval">Интервал срабатывания действия</param>
        /// <param name="action">Лямбда-выражение с действием</param>
        /// <param name="isnapitask">Нужно ли выполнить это в главном потоке</param>
        /// <returns>Уникальный ID таймера</returns>
        public static string Start(string id, int interval, Action action, bool isnapitask = false)
        {
            try
            {
                if (id is null) throw new Exception("Id cannot be null");
                if (TimersData.ContainsKey(id)) throw new Exception("This id is already in use!");
                nTimer newtimer = new nTimer(action, id, interval, isnapitask_: isnapitask);
                lock (TimersData)
                {
                    TimersData.TryAdd(id, newtimer);
                }
                return id;
            }
            catch (Exception e)
            {
                Log.Write($"Start({id}) Exception: {e.ToString()}");
                return null;
            }
        }
        /// <summary>
        /// StartOnce() запускает таймер один раз и возвращает случайный ID
        /// </summary>
        /// <param name="interval">Интервал срабатывания действия</param>
        /// <param name="action">Лямбда-выражение с действием</param>
        /// <param name="isnapitask">Нужно ли выполнить это в главном потоке</param>
        /// <returns>Уникальный ID таймера</returns>
        public static string StartOnce(int interval, Action action, bool isnapitask = false)
        {
            try
            {
                string id = Guid.NewGuid().ToString();
                nTimer newtimer = new nTimer(action, id, interval, true, isnapitask_: isnapitask);
                lock (TimersData)
                {
                    TimersData.TryAdd(id, newtimer);
                }
                return id;
            }
            catch (Exception e)
            {
                Log.Write($"StartOnce Exception: {e.ToString()}");
                return null;
            }
        }
        /// <summary>
        /// StartOnce() запускает таймер один раз и возвращает ID
        /// </summary>
        /// <exception>
        /// Exception возникает при передаче уже существующего ID или значения null
        /// </exception>
        /// <param name="id">Уникальный идентификатор таймера</param>
        /// <param name="interval">Интервал срабатывания действия</param>
        /// <param name="action">Лямбда-выражение с действием</param>
        /// <param name="isnapitask">Нужно ли выполнить это в главном потоке</param>
        /// <returns>Уникальный ID таймера</returns>
        public static string StartOnce(string id, int interval, Action action, bool isnapitask = false)
        {
            try
            {
                if (id is null) throw new Exception("Id cannot be null");
                if (TimersData.ContainsKey(id)) throw new Exception("This id is already in use!");
                nTimer newtimer = new nTimer(action, id, interval, true, isnapitask_: isnapitask);
                lock (TimersData)
                {
                    TimersData.TryAdd(id, newtimer);
                }
                return id;
            }
            catch (Exception e)
            {
                Log.Write($"StartOnce({id}) Exception: {e.ToString()}");
                return null;
            }
        }
        /// <summary>
        /// StartTask() запускает таймер отдельной задачей и возвращает случайный ID
        /// </summary>
        /// <param name="interval">Интервал срабатывания действия</param>
        /// <param name="action">Лямбда-выражение с действием</param>
        /// <returns>Уникальный ID таймера</returns>
        public static string StartTask(int interval, Action action)
        {
            try
            {
                string id = Guid.NewGuid().ToString();
                nTimer newtimer = new nTimer(action, id, interval, false, true);
                lock (TimersData)
                {
                    TimersData.TryAdd(id, newtimer);
                }
                return id;
            }
            catch (Exception e)
            {
                Log.Write($"StartTask Exception: {e.ToString()}");
                return null;
            }
        }
        /// <summary>
        /// StartTask() запускает таймер отдельной задачей и возвращает ID
        /// </summary>
        /// <exception>
        /// Exception возникает при передаче уже существующего ID или значения null
        /// </exception>
        /// <param name="id">Уникальный идентификатор таймера</param>
        /// <param name="interval">Интервал срабатывания действия</param>
        /// <param name="action">Лямбда-выражение с действием</param>
        /// <returns>Уникальный ID таймера</returns>
        public static string StartTask(string id, int interval, Action action)
        {
            try
            {
                if (id is null) throw new Exception("Id cannot be null");
                if (TimersData.ContainsKey(id)) throw new Exception("This id is already in use!");

                nTimer newtimer = new nTimer(action, id, interval, false, true);
                lock (TimersData)
                {
                    TimersData.TryAdd(id, newtimer);
                }
                return id;
            }
            catch (Exception e)
            {
                Log.Write($"StartTask({id}) Exception: {e.ToString()}");
                return null;
            }
        }
        /// <summary>
        /// StartOnceTask() запускает таймер один раз отдельной задачей и возвращает случайный ID
        /// </summary>
        /// <param name="interval">Интервал срабатывания действия</param>
        /// <param name="action">Лямбда-выражение с действием</param>
        /// <returns>Уникальный ID таймера</returns>
        public static string StartOnceTask(int interval, Action action)
        {
            try
            {
                string id = Guid.NewGuid().ToString();
                nTimer newtimer = new nTimer(action, id, interval, true, true);
                lock (TimersData)
                {
                    TimersData.TryAdd(id, newtimer);
                }
                return id;
            }
            catch (Exception e)
            {
                Log.Write($"StartOnceTask Exception: {e.ToString()}");
                return null;
            }
        }
        /// <summary>
        /// StartOnceTask() запускает таймер один раз отдельной задачей и возвращает ID
        /// </summary>
        /// <exception>
        /// Exception возникает при передаче уже существующего ID или значения null
        /// </exception>
        /// <param name="id">Уникальный идентификатор таймера</param>
        /// <param name="interval">Интервал срабатывания действия</param>
        /// <param name="action">Лямбда-выражение с действием</param>
        /// <returns>Уникальный ID таймера</returns>
        public static string StartOnceTask(string id, int interval, Action action)
        {
            try
            {
                if (id is null) throw new Exception("Id cannot be null");
                if (TimersData.ContainsKey(id)) throw new Exception("This id is already in use!");

                nTimer newtimer = new nTimer(action, id, interval, true, true);
                lock (TimersData)
                {
                    TimersData.TryAdd(id, newtimer);
                }
                return id;
            }
            catch (Exception e)
            {
                Log.Write($"StartOnceTask({id}) Exception: {e.ToString()}");
                return null;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        public static void Stop(string id)
        {
            try
            {
                if (id is null) throw new Exception("Trying to stop timer with NULL ID");
                if (TimersData.ContainsKey(id))
                {
                    TimersData[id].isFinished = true;
                    TimersData.TryRemove(id, out _);
                }
            }
            catch (Exception e)
            {
                Log.Write($"Stop Exception: {e.ToString()}");
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public static void Stats()
        {
            string timers_ = "";
            foreach (nTimer t in TimersData.Values)
            {
                string state = (t.isFinished) ? "stopped" : "active";
                timers_ += $"{t.ID}:{state} ";
            }

            Log.Write(
                $"\nThread State = {thread.ThreadState.ToString()}" +
                $"\nTimers Count = {TimersData.Count}" +
                $"\nTimers = {timers_}" +
                $"\n");
        }
    }
    /// <summary>
    /// 
    /// </summary>
    public class nTimer
    {
        /// <summary>Выполнить обработчик с замером времени (долгие — в лог).</summary>
        private void Run(string thread)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            action.Invoke();
            sw.Stop();
            if (sw.ElapsedMilliseconds >= Timers.SlowMs)
                Timers.ReportSlow(this, sw.ElapsedMilliseconds, thread);
        }

        /// <summary>
        /// 
        /// </summary>
        public string ID { get; }
        /// <summary>
        /// 
        /// </summary>
        public int MS { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public DateTime Next { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public Action action { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool isOnce { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool isTask { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool isNapiTask { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool isFinished { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="action_"></param>
        /// <param name="id_"></param>
        /// <param name="ms_"></param>
        /// <param name="isonce_"></param>
        /// <param name="istask_"></param>
        /// <param name="isnapitask_"></param>
        public nTimer(Action action_, string id_, int ms_, bool isonce_ = false, bool istask_ = false, bool isnapitask_ = false)
        {
            action = action_;

            ID = id_;
            MS = ms_;
            Next = DateTime.Now.AddMilliseconds(MS + (isonce_ ? 0 : Timers.StartJitter(ms_)));

            isOnce = isonce_;
            isTask = istask_;
            isNapiTask = isnapitask_;
            isFinished = false;
        }
        /// <summary>
        /// 
        /// </summary>
        public void Elapsed()
        {
            try
            {
                // Раньше здесь был перебор всех таймеров (Values.Contains) — на каждом таймере каждые 100 мс
                if (!Timers.TimersData.TryGetValue(ID, out var current) || !ReferenceEquals(current, this)) return;
                if (isFinished) return;
                if (Next <= DateTime.Now)
                {
                    if (isOnce) isFinished = true;
                    Next = DateTime.Now.AddMilliseconds(MS);
                    Timers.Log.Debug($"Timer.{ID}.Invoke");

                    if (isTask) 
                    {
                        Task.Factory.StartNew(() => 
                        {
                            try
                            {
                                Run("task");
                            }
                            catch (Exception e)
                            {
                                Timers.Log.Write($"Elapsed({ID},MS:{MS},ONCE:{isOnce},From:{action.Target?.ToString()}) Task #1 Exception: {e.ToString()}");
                            }
                        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                    }
                    else if (isNapiTask)
                    {
                        NAPI.Task.Run(() =>
                        {
                            try
                            {
                                Run("main");
                            }
                            catch (Exception e)
                            {
                                Timers.Log.Write($"Elapsed({ID},MS:{MS},ONCE:{isOnce},From:{action.Target?.ToString()}) Task #2 Exception: {e.ToString()}");
                            }
                        });
                    }
                    else Run("timer");

                    Timers.Log.Debug($"Timer.{ID}.Completed", nLog.Type.Success);
                }
            }
            catch (Exception e)
            {
                Timers.Log.Write($"Elapsed({ID},MS:{MS},ONCE:{isOnce},From:{action.Target?.ToString()}) Exception: {e.ToString()}");
            }
        }
    }
}
