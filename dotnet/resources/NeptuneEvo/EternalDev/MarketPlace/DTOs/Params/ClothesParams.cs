using NeptuneEvo.Chars.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.DTOs.Params
{
    public class ClothesParams : ParamsBase
    {
        public ClothesParams(string data) : base(data)
        {
            var split = data.Split("@@");

            ItemId = (ItemId)Convert.ToInt32(split[0]);
            Count = Convert.ToInt32(split[1]);
            ItemData = split[2];

            var clothesInfo = ItemData.Split("_");
            Drawable = Convert.ToInt32(clothesInfo[0]);
            Texture = Convert.ToInt32(clothesInfo[1]);
            Gender = clothesInfo[2];
        }

        [JsonProperty("itemId")]
        public ItemId ItemId { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("itemData")]
        public string ItemData { get; set; }

        [JsonProperty("drawable")]
        public int Drawable { get; set; }

        [JsonProperty("texture")]
        public int Texture { get; set; }

        [JsonProperty("gender")]
        public string Gender { get; set; }
    }
}
