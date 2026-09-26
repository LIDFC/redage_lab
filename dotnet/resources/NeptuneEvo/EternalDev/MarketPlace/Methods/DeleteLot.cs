using GTANetworkAPI;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Handles;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Methods
{
    public class DeleteLot : Script
    {
        [RemoteEvent("server.marketPlace.delete_lot")]
        public static void On(ExtPlayer player, int id)
        {
            var marketItem = Manager.GetMarketItem(id);
            if (marketItem is null || marketItem.Owner != player.GetUUID())
                return;   

            if (marketItem.Type == Enums.LotType.Item)
            {
                if (!MoneySystem.Wallet.Change(player, -Manager.Config.App.RemoveLotPrice))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"У вас недостаточно средств", 3000);
                    return;
                }
            }

            marketItem.Delete();
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы успешно удаилил ваше объявление", 3000);
        }
    }
}
