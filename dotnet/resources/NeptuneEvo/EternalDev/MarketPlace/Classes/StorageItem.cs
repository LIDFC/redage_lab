using NeptuneEvo.EternalDev.MarketPlace.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Classes
{
    public class StorageItem
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("type")]
        public LotType Type { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }

        [JsonProperty("endDate")]
        public DateTime EndDate { get; set; }
    }
}
