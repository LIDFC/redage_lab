using NeptuneEvo.Handles;
using Newtonsoft.Json;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Classes
{
    public class UserProfile
    {
        public ExtPlayer Player
        {
            get
            {
                return Main.GetPlayerByUUID(UUID);
            }
        }

        public int UUID { get; set; }
        public List<StorageItem> Storage { get; set; } = new List<StorageItem>();

        public void AddItem(StorageItem storageItem)
        {
            storageItem.Id = Storage.Count == 0 ? 1 : Storage.Max(x => x.Id) + 1;
            Storage.Add(storageItem);
            
            if (Player != null)
                Manager.UpdateStorage(Player);

            Save();
        }

        public void RemoveItem(StorageItem storageItem)
        {
            if (Storage.Contains(storageItem))
                Storage.Remove(storageItem);

            if (Player != null)
                Manager.UpdateStorage(Player);

            Save();
        }

        public void Save()
        {
            MySQL.Query($"UPDATE `e-dev_marketplace-profiles` SET `storage`='{JsonConvert.SerializeObject(Storage)}' WHERE `uuid`={UUID}");
        }
    }
}
