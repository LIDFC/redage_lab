using NeptuneEvo.Chars.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.DTOs.Params
{
    public class ItemParams : ParamsBase
    {
        public ItemParams(string data) : base(data)
        {
            var split = data.Split("@@");

            ItemId = (ItemId)Convert.ToInt32(split[0]);
            Count = Convert.ToInt32(split[1]);
            ItemData = split[2];
        }

        [JsonProperty("itemId")]
        public ItemId ItemId { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("itemData")]
        public string ItemData { get; set; }
    }
}
