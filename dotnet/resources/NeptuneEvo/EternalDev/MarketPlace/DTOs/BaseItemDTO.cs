using NeptuneEvo.EternalDev.MarketPlace.DTOs.Params;
using NeptuneEvo.EternalDev.MarketPlace.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.DTOs
{
    public class BaseItemDTO
    {
        public BaseItemDTO(int id, LotType lotType)
        {
            Id = id;
            LotType = lotType;
        }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("type")]
        public string Type
        {
            get
            {
                return LotType.ToString().ToLower();
            }
        }

        [JsonProperty("lotType")]
        public LotType LotType { get; set; }

        [JsonProperty("params")]
        public ParamsBase Params { get; set; }
    }
}
