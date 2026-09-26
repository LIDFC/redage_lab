using GTANetworkAPI;
using NeptuneEvo.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.DTOs.Params
{
    public class BusinessParams : ParamsBase
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        public BusinessParams(string data): base(data)
        {
            Id = Convert.ToInt32(data);

            if (!BusinessManager.BizList.TryGetValue(Id, out var business))
                return;

            Name = BusinessManager.BusinessTypeNames[business.Type];
            Cost = business.SellPrice;
            Area = "";
            Type = business.Type;
            Position = business.EnterPoint;
        }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("cost")]
        public int Cost { get; set; }

        [JsonProperty("area")]
        public string Area { get; set; }

        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("position")]
        public Vector3 Position { get; set; }
    }
}
