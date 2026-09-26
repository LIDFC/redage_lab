using GTANetworkAPI;
using NeptuneEvo.Houses;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.DTOs.Params
{
    public class HouseParams : ParamsBase
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        public HouseParams(string data) : base(data)
        {
            Id = Convert.ToInt32(data);

            var house = HouseManager.Houses.Find(x => x.ID == Id);
            if (house is null)
                return;

            Cost = house.Price;
            Area = "";

            var garage = GarageManager.Garages[house.GarageID];
            if (garage != null)
                Garages = GarageManager.GarageTypes[garage.Type].MaxCars;
            else
                Garages = 1;

            People = HouseManager.MaxRoommates[house.Type];
            Position = house.Position;
        }

        [JsonProperty("cost")]
        public int Cost { get; set; }

        [JsonProperty("area")]
        public string Area { get; set; }

        [JsonProperty("garages")]
        public int Garages { get; set; }

        [JsonProperty("people")]
        public int People { get; set; }

        [JsonProperty("position")]
        public Vector3 Position { get; set; }
    }
}
