using NeptuneEvo.EternalDev.MarketPlace.DTOs.Params;
using NeptuneEvo.EternalDev.MarketPlace.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace NeptuneEvo.EternalDev.MarketPlace.DTOs
{
    public class MarketLotDTO : BaseItemDTO
    {
        public MarketLotDTO(int id, LotType lotType) : base(id, lotType)
        {
            var marketLot = Manager.GetMarketItem(id);
            if (marketLot is null)
                return;

            Favourites = marketLot.Favourites.Count;
            Views = marketLot.Views.Count;
            EndDate = Methods.Formatter.DateTimeToMilliseconds(marketLot.EndDate);
            CreateDate = Methods.Formatter.DateTimeToMilliseconds(marketLot.CreateDate);

            var sim = Main.SimCards.FirstOrDefault(u => u.Value == marketLot.Owner).Key;
            Author = new AuthorData()
            {
                Id = marketLot.Owner,
                Name = Main.PlayerNames[marketLot.Owner],
                PhoneNumber = Main.SimCards.ContainsKey(sim) ? sim : -1,
            };
            Photos = marketLot.Photos;
            Comment = marketLot.Comment;
            Cost = marketLot.Cost;

            if (marketLot.Type == LotType.Vehicle)
                Params = new VehicleParams(marketLot.Data);

            if (marketLot.Type == LotType.Business)
                Params = new BusinessParams(marketLot.Data);

            if (marketLot.Type == LotType.Service)
                Params = new ServiceParams(marketLot.Data);

            if (marketLot.Type == LotType.Item)
                Params = new ItemParams(marketLot.Data);

            if (marketLot.Type == LotType.Clothes)
                Params = new ClothesParams(marketLot.Data);
        }

        [JsonProperty("cost")]
        public int Cost { get; set; }

        [JsonProperty("favourites")]
        public int Favourites { get; set; }

        [JsonProperty("views")]
        public int Views { get; set; }

        [JsonProperty("endDate")]
        public long EndDate { get; set; }
        
        [JsonProperty("createDate")]
        public long CreateDate { get; set; }

        [JsonProperty("author")]
        public AuthorData Author { get; set; }

        [JsonProperty("photos")]
        public List<string> Photos { get; set; } = new List<string>();

        [JsonProperty("comment")]
        public string Comment { get; set; }
    }
}
