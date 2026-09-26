using NeptuneEvo.Handles;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Auction.Classes
{
    public class BetData
    {
        public int Author { get; set; }
        public int Bet { get; set; }
        public int NewBet { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public int UpTime { get; set; } = 0;

        [JsonIgnore]
        public ExtPlayer Player
        {
            get
            {
                return Main.GetPlayerByUUID(Author);
            }
        }
    }
}
