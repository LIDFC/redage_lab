using NeptuneEvo.BlackMarket.Crypto;
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
                CryptoWallets.Load();
                Ready = true;
            }
            catch (Exception e)
            {
                BlackMarketCore.Log.Write($"Init Exception: {e}");
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
