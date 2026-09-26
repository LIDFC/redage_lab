using NeptuneEvo.EternalDev.MarketPlace.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.DTOs
{
    public class FavouriteDataDTO
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("subData")]
        public string SubData { get; set; }
    }
}
