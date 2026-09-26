using NeptuneEvo.BlackMarket.Config;
using NeptuneEvo.BlackMarket.Crypto;
using NeptuneEvo.BlackMarket.Deliveries;
using NeptuneEvo.BlackMarket.Methods;
using NeptuneEvo.BlackMarket.P2P;
using Redage.SDK;
using System;

namespace NeptuneEvo.BlackMarket
{
    /// <summary>
    /// Точка входа модуля Чёрного рынка: инициализация (из Main после загрузки инвентаря и фракций)
    /// и сохранение при рестарте. Остальные подсистемы подключаются сюда по фазам.
    /// </summary>
    public static class BlackMarketManager
    {
        public static bool Ready { get; private set; }

        public static void Init()
        {
            try
            {
                BlackMarketRepository.Init();
                BlackMarketConfig.Load();
                CryptoWallets.Load();
                Lots.Load();
                DropManager.Load();
                P2PManager.Load();
                Ready = true;

                // В главном потоке: объекты мира и инвентарь трогаем только оттуда
                Timers.Start("blackmarket.tick", 1000, Tick, true);
            }
            catch (Exception e)
            {
                BlackMarketCore.Log.Write($"Init Exception: {e}");
            }
        }

        private static void Tick()
        {
            if (!Ready)
                return;
            try
            {
                Lots.Tick();
                DropManager.Tick();
                P2PManager.Tick();
            }
            catch (Exception e)
            {
                BlackMarketCore.Log.Write($"Tick Exception: {e}");
            }
        }

        /// <summary>Вызывается при сохранении/остановке сервера: дописать очередь БД.</summary>
        public static void Save()
        {
            try
            {
                BlackMarketRepository.Flush();
            }
            catch (Exception e)
            {
                BlackMarketCore.Log.Write($"Save Exception: {e}");
            }
        }
    }
}
