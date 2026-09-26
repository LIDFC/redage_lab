using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.DTOs.Params
{
    public abstract class ParamsBase
    {
        [JsonIgnore]
        public string Data { get; set; }

        public ParamsBase(string data)
        {
            Data = data;
        }
    }                                
}
