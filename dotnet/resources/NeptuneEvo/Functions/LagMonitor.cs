using System;
using System.Threading;
using GTANetworkAPI;
using Redage.SDK;

namespace NeptuneEvo.Functions
{
    /// <summary>
    /// Сторож игрового потока: раз в секунду ставит в очередь главного потока пустую задачу и меряет,
    /// через сколько она выполнилась. Задержка больше LagMs = фриз сервера (у всех игроков) — пишем в лог.
    /// Вместе с «Долгий таймер» (Timers) и «Медленный QueryRead» (MySQL) по времени в логе видно, кто тормозит.
    /// Также запоминает id главного потока для MySQL (метка «[ИГРОВОЙ ПОТОК — фриз]»).
    /// </summary>
    class LagMonitor : Script
    {
        private static readonly nLog Log = new nLog("LagMonitor");
        private const int LagMs = 150;
        private static long _posted;
        private static int _pending;
        private static DateTime _lastReport = DateTime.MinValue;
        private static long _worstSinceReport;

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            MySQL.MainThreadId = Thread.CurrentThread.ManagedThreadId;
            var thread = new Thread(Loop) { IsBackground = true, Name = "LagMonitor" };
            thread.Start();
        }

        private static void Loop()
        {
            // Дать серверу запуститься: загрузка ресурсов сама по себе долгая
            Thread.Sleep(60000);
            while (true)
            {
                try
                {
                    if (Interlocked.CompareExchange(ref _pending, 1, 0) == 0)
                    {
                        Interlocked.Exchange(ref _posted, DateTime.UtcNow.Ticks);
                        NAPI.Task.Run(() =>
                        {
                            var lag = (DateTime.UtcNow.Ticks - Interlocked.Read(ref _posted)) / TimeSpan.TicksPerMillisecond;
                            Interlocked.Exchange(ref _pending, 0);
                            Check(lag);
                        });
                    }
                    else
                    {
                        // Предыдущая метка всё ещё не выполнена — поток стоит прямо сейчас
                        var lag = (DateTime.UtcNow.Ticks - Interlocked.Read(ref _posted)) / TimeSpan.TicksPerMillisecond;
                        if (lag > 3000)
                            Check(lag, true);
                    }
                }
                catch (Exception e)
                {
                    Log.Write($"Loop Exception: {e.Message}");
                }
                Thread.Sleep(1000);
            }
        }

        private static void Check(long lag, bool stuck = false)
        {
            if (lag < LagMs)
                return;
            if (lag > _worstSinceReport)
                _worstSinceReport = lag;
            // Не чаще раза в 10 секунд: в сообщении худшая задержка за это время
            if ((DateTime.Now - _lastReport).TotalSeconds < 10)
                return;
            _lastReport = DateTime.Now;
            Log.Write(stuck
                ? $"Игровой поток не отвечает уже {lag} мс — сервер завис (смотрите строки выше: долгий таймер / медленный запрос)"
                : $"Фриз игрового потока: {_worstSinceReport} мс", nLog.Type.Warn);
            _worstSinceReport = 0;
        }
    }
}
