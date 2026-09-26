using GTANetworkAPI;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.EternalDev.MarketPlace.Enums;
using NeptuneEvo.Handles;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Classes
{
    public class MarketItem
    {
        public int Id { get; set; }
        public LotType Type { get; set; }
        public int Cost { get; set; }

        public string Data { get; set; }
        public int Owner { get; set; }

        public DateTime CreateDate { get; set; }
        public DateTime EndDate { get; set; }

        public List<int> Views { get; set; } = new List<int>();
        public List<int> Favourites { get; set; } = new List<int>();

        public List<string> Photos { get; set; }
        public string Comment { get; set; }

        public bool IsSave { get; set; } = false;


        public void Save()
        {
            IsSave = true;
        }

        public void Delete()
        {
            if (Type == Enums.LotType.Item || Type == Enums.LotType.Clothes)
            {
                var split = Data.Split("@@");
                Chars.Repository.AddNewItem(null, $"marketStorage_{Owner}", "marketStorage", (ItemId)Convert.ToInt32(split[0]), Convert.ToInt32(split[1]), split[2], MaxSlots: Manager.Config.MaxSlotsInStorage);
            }

            var player = Main.GetPlayerByUUID(Owner);
            NAPI.Task.Run(() =>
            {
                if (player is null)
                    return;

                Manager.UpdateStorage(player);
            }, 1000);

            Manager.DeleteLot(this);
            Manager.UpdatePage(Type.ToString().ToLower());
        }
    }
}
