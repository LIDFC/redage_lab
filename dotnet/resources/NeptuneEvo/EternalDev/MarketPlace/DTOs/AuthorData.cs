using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.DTOs
{
    public class AuthorData
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("phoneNumber")]
        public int PhoneNumber { get; set; }
    }
}
