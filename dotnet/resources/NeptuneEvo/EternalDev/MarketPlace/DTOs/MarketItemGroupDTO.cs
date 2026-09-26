using NeptuneEvo.EternalDev.MarketPlace.DTOs.Params;
using NeptuneEvo.EternalDev.MarketPlace.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.DTOs
{
    public class MarketItemGroupDTO
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("minPrice")]
        public int MinPrice { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("params")]
        public ParamsBase Params { get; set; }
    }
}
