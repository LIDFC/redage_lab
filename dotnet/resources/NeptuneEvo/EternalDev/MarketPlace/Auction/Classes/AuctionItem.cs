using NeptuneEvo.EternalDev.MarketPlace.Enums;
using NeptuneEvo.Handles;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Auction.Classes
{
    public class AuctionItem
    {
        public int Id { get; set; }
        public LotType Type { get; set; }
        public string Data { get; set; }
        public List<BetData> Bets { get; set; } = new List<BetData>();
        public int BetStep { get; set; }
        public int Cost { get; set; }
        public DateTime EndDate { get; set; }
        public List<int> Views { get; set; } = new List<int>();
        public List<int> Favourites { get; set; } = new List<int>();

        public bool IsSave { get; set; } = false;

        public int Participants 
        { 
            get
            {
                return Bets.Select(x => x.Author).Distinct().Count();
            }
        }

        public BetData CurrentBet
        {
            get
            {
                return Bets.Count == 0 ? null : Bets.Last();
            }
        }

        public void Save()
        {
            IsSave = true;
        }

        public void SetBet(ExtPlayer player, int multiplayer)
        {
            var lastBet = CurrentBet;
            if (lastBet != null && lastBet.Author == player.GetUUID())
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Последняя ставка была сделана вами!", 3000);
                return;
            }

            var cost = (CurrentBet is null ? Cost : CurrentBet.NewBet) + (BetStep * multiplayer);
            if (!MoneySystem.Bank.Change(player.CharacterData.Bank, -cost))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Недостаточно средств!", 3000);
                return;
            }

            if (lastBet != null)
            {
                Manager.AddMoney(lastBet.Author, lastBet.NewBet, $"Вашу ставку на аукционе #{Id} перебили!");
            }
            else
            {
                EndDate = DateTime.Now.AddHours(Manager.Config.App.AuctionTime);
            }

            var betData = new BetData()
            {
                Author = player.GetUUID(),
                Bet = BetStep * multiplayer,
                NewBet = cost,
                Date = DateTime.Now,
            };

            if (DateTime.Now.AddSeconds(Manager.Config.App.TimeRuleToAddByBet) >= EndDate)
            {
                EndDate = EndDate.AddSeconds(Manager.Config.App.TimeRuleToAddByBet);
                betData.UpTime = Manager.Config.App.TimeRuleToAddByBet;
            }

            Bets.Add(betData);
            IsSave = true;

            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы успешно поставили {MoneySystem.Wallet.Format(cost)}$ на аукционе #{Id}", 3000);
            Manager.UpdateItem("auction", Type, Id);

            var players = Manager.GetPlayersInLot("auction", Id);
            players.ForEach(x => MarketPlace.Methods.Requests.On(x, "auction", Id.ToString()));
        }

        public void TryFinish()
        {
            if (EndDate > DateTime.Now)
                return;

            var lastBet = CurrentBet;
            if (lastBet is null)
                return;

            MarketPlace.Methods.BuyLot.SetToEstate(lastBet.Author, Type, Data, 1, true);
            AuctionManager.DeleteAuctionItem(this);

            var player = Main.GetPlayerByUUID(lastBet.Author);
            if (player != null)
            {
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы победили в аукционе #{Id}", 3000);
            }
        }
    }
}
