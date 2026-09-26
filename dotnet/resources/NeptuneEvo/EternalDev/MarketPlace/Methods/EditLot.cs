using GTANetworkAPI;
using NeptuneEvo.EternalDev.MarketPlace.Extensions;
using NeptuneEvo.Handles;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Methods
{
    public class EditLot : Script
    {
        [RemoteEvent("server.marketPlace.edit_lot")]
        public static void On(ExtPlayer player, int lotId, string comment, string titile, int price)
        {
            if (!player.GetMarketPlaceSession(out var appSession))
                return;

            var marketItem = Manager.GetMarketItem(lotId);
            if (marketItem is null)
                return;

            if (marketItem.Owner != player.GetUUID())
                return;

            marketItem.Cost = price;
            marketItem.Comment = comment;

            if (marketItem.Type == Enums.LotType.Service)
                marketItem.Data = titile;

            marketItem.Save();
            Manager.UpdateItem("main", marketItem.Type, marketItem.Id);

            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы успешно изменили свое объявление!", 3000);
        }
    }
}
