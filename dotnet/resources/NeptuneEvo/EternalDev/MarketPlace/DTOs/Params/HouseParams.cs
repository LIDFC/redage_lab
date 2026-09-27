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

            // Квартиры и дома без гаража не должны ронять весь список лотов
            Garages = GarageManager.Garages.TryGetValue(house.GarageID, out var garage) && garage != null
                      && GarageManager.GarageTypes.TryGetValue(garage.Type, out var garageType)
                ? garageType.MaxCars
                : 1;

            People = house.Type >= 0 && house.Type < HouseManager.MaxRoommates.Count ? HouseManager.MaxRoommates[house.Type] : 1;
            Position = house.Position;
            Type = house.ApartmentId >= 0 ? "apartament" : "house";
        }

        [JsonProperty("cost")]
        public int Cost { get; set; }

        [JsonProperty("area")]
        public string Area { get; set; }

        [JsonProperty("garages")]
        public int Garages { get; set; }

        [JsonProperty("people")]
        public int People { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } = "house";

        [JsonProperty("position")]
        public Vector3 Position { get; set; }
    }
}
