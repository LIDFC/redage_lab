using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.DTOs.Params
{
    public class ServiceParams : ParamsBase
    {
        public ServiceParams(string data): base(data)
        {
            Name = data;
        }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
