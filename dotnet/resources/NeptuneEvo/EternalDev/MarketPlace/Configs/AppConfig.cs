using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Configs
{
    public class AppConfig
    {
        /// <summary>
        /// Процент, для вычисления комиссии
        /// </summary>
        [JsonProperty("marketPercent")]
        public decimal MarketPercent { get; set; } = 0.0001m;
        
        /// <summary>
        /// Максимальное количество часов выставления лота
        /// </summary>
        [JsonProperty("maxHours")]
        public int MaxHours { get; set; } = 120;
        
        /// <summary>
        /// Минимальное количество часов
        /// </summary>
        [JsonProperty("minHours")]
        public int MinHours { get; set; } = 5;
        
        /// <summary>
        /// Цена для вычисления минимальной цены выставления от госки например
        /// </summary>
        [JsonProperty("minPriceMultiplier")]
        public decimal MinPriceMultiplier { get; set; } = 0.5m;

        /// <summary>
        /// Максимальная цена для вычисления комисиии
        /// </summary>
        [JsonProperty("priceHighLimit")]
        public int PriceHighLimit { get; set; } = 2500000;
        
        /// <summary>
        /// Минимальная цена для вычисления комисиии
        /// </summary>
        [JsonProperty("priceLowLimit")]
        public int PriceLowLimit { get; set; } = 40000;
        
        /// <summary>
        /// Цена удаления лота с маркетплейса
        /// </summary>
        [JsonProperty("removeLotPrice")]
        public int RemoveLotPrice { get; set; } = 1000;
        
        /// <summary>
        /// Процент взымаемый за размещение услуги
        /// </summary>
        [JsonProperty("servicePercent")]
        public decimal ServicePercent { get; set; } = .01m;

        /// <summary>
        /// Время в секундах, когда при ставках время будет увеличиваться
        /// </summary>
        [JsonProperty("timeRuleToAddByBet")]
        public int TimeRuleToAddByBet { get; set; } = 60;

        /// <summary>
        /// Время в секундах, когда при ставке увлечивается конец на текущее значение
        /// </summary>
        [JsonProperty("timeAddAuctionByBet")]
        public int TimeAddAuctionByBet { get; set; } = 30;

        /// <summary>
        /// Время в часах, когда создается аукцион
        /// </summary>
        [JsonProperty("auctionTime")]
        public int AuctionTime { get; set; } = 12;

        /// <summary>
        /// Шаг ставки на аукционе
        /// </summary>
        [JsonProperty("betStep")]
        public int BetStep { get; set; } = 100_000;
    }
}
