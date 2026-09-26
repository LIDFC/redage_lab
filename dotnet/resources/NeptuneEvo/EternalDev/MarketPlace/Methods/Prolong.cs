using GTANetworkAPI;
using NeptuneEvo.EternalDev.MarketPlace.Enums;
using NeptuneEvo.Handles;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Methods
{
    public class Prolong : Script
    {
        [RemoteEvent("server.marketPlace.prolong")]
        public static void On(ExtPlayer player, int id, int hours, string paymentType)
        {
            var marketItem = Manager.GetMarketItem(id);
            if (marketItem is null || marketItem.Owner != player.GetUUID())
                return;

            var commission = Commission.Get(marketItem.Type, marketItem.Cost, hours, 1);
            if (paymentType == "Wallet" ? !MoneySystem.Wallet.Change(player, -commission)
                : paymentType == "Card" ? !MoneySystem.Bank.Change(player.CharacterData.Bank, -commission) : true)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"У вас недостаточно средств!", 3000);
                return;
            }

            marketItem.EndDate.AddHours(hours);
            marketItem.Save();

            Manager.UpdateItem("main", marketItem.Type, marketItem.Id);
        }
    }
}
