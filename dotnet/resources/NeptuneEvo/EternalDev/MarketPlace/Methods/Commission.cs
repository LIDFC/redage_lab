using NeptuneEvo.EternalDev.MarketPlace.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Methods
{
    public class Commission
    {
        public static int Get(LotType lotType, int price, int hours, int count)
        {
            if (lotType != LotType.Item && lotType != LotType.Clothes)
                return Convert.ToInt32(Math.Round((hours * price * Manager.Config.App.ServicePercent) / (lotType == LotType.Service ? 1 : 100)));

            var pricePerItem = price;
            var bodyCost = (1 + .25m * (count - 1)) * pricePerItem;

            if (bodyCost < Manager.Config.App.PriceLowLimit)
            {
                bodyCost = Manager.Config.App.PriceLowLimit;
            }
            else if (bodyCost > Manager.Config.App.PriceHighLimit)
            {
                bodyCost = Manager.Config.App.PriceHighLimit + .2m * (bodyCost - Manager.Config.App.PriceHighLimit);
            }

            return Convert.ToInt32(Math.Round(bodyCost * Manager.Config.App.MarketPercent * hours));
        }
    }
}
