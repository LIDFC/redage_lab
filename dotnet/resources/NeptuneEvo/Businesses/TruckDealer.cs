using System;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.Core;
using Redage.SDK;

namespace NeptuneEvo.Businesses
{
    /// <summary>
    /// Грузовой автосалон (бизнес типа 17): обычный бизнес, который можно купить. Продаёт коммерческий транспорт
    /// (BusinessManager.TruckModels) — его можно купить себе или для организации («Купить (ОРГ)»), и такие машины
    /// подходят для грузов подрядов. Если на сервере нет ни одного такого салона — создаётся в промзоне порта.
    /// </summary>
    public static class TruckDealer
    {
        private static readonly nLog Log = new nLog("TruckDealer");

        /// <summary>Промзона порта Elysian Island, рядом с металлобазой (позиция «в полный рост»).</summary>
        private static readonly Vector3 DefaultPoint = new Vector3(1236.264, -3217.617, 5.800362);

        public static void Seed()
        {
            try
            {
                if (BusinessManager.BizList.Values.Any(b => b.Type == BusinessManager.TruckDealerType))
                    return;

                var lowRooms = BusinessManager.BizList.Values.Where(b => b.Type == 4 && b.SellPrice > 0).ToList();
                var price = lowRooms.Count > 0 ? (int)lowRooms.Average(b => b.SellPrice) : 1_500_000;

                BusinessManager.CreateStateBusiness(BusinessManager.TruckDealerType, DefaultPoint - new Vector3(0, 0, 1.12), DefaultPoint, price, biz =>
                    Log.Write($"Создан грузовой автосалон (бизнес #{biz.ID}, цена {price})", nLog.Type.Success));
            }
            catch (Exception e)
            {
                Log.Write($"Seed Exception: {e}");
            }
        }
    }
}
