using NeptuneEvo.EternalDev.MarketPlace.Auction.Classes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Auction.DTOs
{
    public class BetDTO
    {
        [JsonProperty("author")]
        public int Author { get; set; }

        [JsonProperty("authorName")]
        public string AuthorName { get; set; }

        [JsonProperty("bet")]
        public int Bet { get; set; }

        [JsonProperty("newBet")]
        public int NewBet { get; set; }

        [JsonProperty("date")]
        public long Date { get; set; }

        [JsonProperty("upTime")]
        public int UpTime { get; set; }

        public BetDTO(BetData betData)
        {
            Author = betData.Author;
            AuthorName = Main.PlayerNames[Author];
            Bet = betData.Bet;
            NewBet = betData.NewBet;
            UpTime = betData.UpTime;
            Date = MarketPlace.Methods.Formatter.DateTimeToMilliseconds(betData.Date);
        }
    }
}
