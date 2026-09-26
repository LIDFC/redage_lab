using NeptuneEvo.EternalDev.MarketPlace.DTOs;
using NeptuneEvo.EternalDev.MarketPlace.DTOs.Params;
using NeptuneEvo.EternalDev.MarketPlace.Enums;
using NeptuneEvo.EternalDev.MarketPlace.Methods;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Auction.DTOs
{
    public class AuctionItemDTO : BaseItemDTO
    {
        public AuctionItemDTO(int id, LotType lotType) : base(id, lotType)
        {
            var auctionItem = AuctionManager.GetAuctionItem(id);
            if (auctionItem is null)
                return;

            CurrentBet = auctionItem.CurrentBet is null ? auctionItem.Cost : auctionItem.CurrentBet.NewBet;
            BetStep = auctionItem.BetStep;
            Favourites = auctionItem.Favourites.Count;
            Views = auctionItem.Views.Count;
            EndDate = Formatter.DateTimeToMilliseconds(auctionItem.EndDate);
            Participants = auctionItem.Participants;

            switch(lotType)
            {
                case LotType.Vehicle:
                    Params = new VehicleParams(auctionItem.Data);
                    break;
                case LotType.House:
                    Params = new HouseParams(auctionItem.Data);
                    break;
                case LotType.Business:
                    Params = new BusinessParams(auctionItem.Data);
                    break;
            }
        }

        [JsonProperty("currentBet")]
        public int CurrentBet { get; set; }

        [JsonProperty("betStep")]
        public int BetStep { get; set; }

        [JsonProperty("favourites")]
        public int Favourites { get; set; }

        [JsonProperty("views")]
        public int Views { get; set; }

        [JsonProperty("endDate")]
        public long EndDate { get; set; }

        [JsonProperty("createDate")]
        public long CreateDate { get; set; }

        [JsonProperty("participants")]
        public int Participants { get; set; }
    }
}
