using NeptuneEvo.EternalDev.MarketPlace.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.DTOs
{
    public class StorageItemDTO : BaseItemDTO
    {
        public StorageItemDTO(int id, LotType lotType) : base(id, lotType) { }

        [JsonProperty("onEstate")]
        public bool OnEstate { get; set; }

        [JsonProperty("endDate")]
        public long EndDate { get; set; }
    }
}
