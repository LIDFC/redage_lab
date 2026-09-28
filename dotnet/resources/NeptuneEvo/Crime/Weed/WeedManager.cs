using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using GTANetworkAPI;
using MySqlConnector;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Houses;
using NeptuneEvo.PedSystem;
using NeptuneEvo.Players;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Crime.Weed
{
    /// <summary>
    /// Трава «полный цикл»: семена (Мавр / P2P) → посадка в скрытой точке или дома → полив бутылкой воды →
    /// урожай «Свежая конопля» → сушка (время) и фасовка в «Наркотики» → продажа Мавру (скупка) или NPC-покупателям.
    /// Садить, собирать и продавать может только криминал; полиция уничтожает кусты за награду.
    /// Рост считается по абсолютному времени и хранится в `weed_plants`.
    /// </summary>
    public class WeedManager : Script
    {
        private const int ActionSeconds = 4;
        private const float SpotRadius = 4f;
        private const float MinPlantDistance = 1.2f;
        private const int StarsHarvest = 1;
        private const int StarsSell = 1;

        private static readonly string[] Models =
        {
            "bkr_prop_weed_01_small_01a",
            "bkr_prop_weed_med_01a",
            "bkr_prop_weed_lrg_01a",
        };

        private class Plant
        {
            public int Id;
            public int OwnerUuid;
            public string OwnerName;
            public int HouseId = -1;
            public int SpotIndex = -1;
            public Vector3 Position;
            public uint Dimension;
            public DateTime Planted;
            public DateTime LastWater;
            public bool Busy;

            public ExtObject Object;
            public ExtTextLabel Label;
            public ExtColShape Shape;
            public int ShownStage = -1;
            public string ShownText;
        }

        private class BuyerRuntime
        {
            public WeedBuyer Config;
            public ExtPed Ped;
            public DateTime Day = DateTime.Today;
            public int SoldToday;
        }

        private static readonly Dictionary<int, Plant> Plants = new Dictionary<int, Plant>();
        private static readonly List<BuyerRuntime> Buyers = new List<BuyerRuntime>();
        private static readonly Dictionary<int, int> BuyerByPed = new Dictionary<int, int>();
        /// <summary>uuid → индекс покупателя, у которого открыт ввод количества.</summary>
        private static readonly Dictionary<int, int> SellTarget = new Dictionary<int, int>();
        private static readonly HashSet<int> BusyPlayers = new HashSet<int>();
        private static int _nextId = 1;
        private static bool _loaded;

        private static WeedConfig Cfg => WeedConfig.Current;
        private static long Unix(DateTime time) => new DateTimeOffset(time).ToUnixTimeSeconds();
        private static DateTime FromUnix(long value) => DateTimeOffset.FromUnixTimeSeconds(value).LocalDateTime;

        // ------------------------------------------------------------------ старт

        public static void Init()
        {
            try
            {
                WeedConfig.Load();
                using (var create = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS `weed_plants` (
                    `id` INT NOT NULL,
                    `owner` INT NOT NULL,
                    `owner_name` VARCHAR(64) NOT NULL DEFAULT '',
                    `house` INT NOT NULL DEFAULT -1,
                    `spot` INT NOT NULL DEFAULT -1,
                    `x` FLOAT NOT NULL, `y` FLOAT NOT NULL, `z` FLOAT NOT NULL,
                    `dim` INT NOT NULL DEFAULT 0,
                    `planted` BIGINT NOT NULL,
                    `watered` BIGINT NOT NULL,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"))
                    MySQL.Query(create);

                var table = BlackMarket.BlackMarketRepository.Read("SELECT * FROM `weed_plants`");
                // Рестарт — не вина игрока: у растущих кустов «полив» сдвигается, чтобы не засохли за время простоя
                var now = DateTime.Now;
                if (table != null)
                {
                    foreach (DataRow row in table.Rows)
                    {
                        var plant = new Plant
                        {
                            Id = Convert.ToInt32(row["id"]),
                            OwnerUuid = Convert.ToInt32(row["owner"]),
                            OwnerName = row["owner_name"].ToString(),
                            HouseId = Convert.ToInt32(row["house"]),
                            SpotIndex = Convert.ToInt32(row["spot"]),
                            Position = new Vector3(Convert.ToSingle(row["x"]), Convert.ToSingle(row["y"]), Convert.ToSingle(row["z"])),
                            Dimension = (uint)Convert.ToInt32(row["dim"]),
                            Planted = FromUnix(Convert.ToInt64(row["planted"])),
                            LastWater = FromUnix(Convert.ToInt64(row["watered"])),
                        };
                        var grace = now.AddMinutes(-Cfg.WaterMinutes / 2.0);
                        if (plant.LastWater < grace && now < ReadyAt(plant))
                            plant.LastWater = grace;
                        Plants[plant.Id] = plant;
                        _nextId = Math.Max(_nextId, plant.Id + 1);
                    }
                }
                foreach (var plant in Plants.Values.ToList())
                {
                    if (!CheckAlive(plant, false))
                        continue;
                    Spawn(plant);
                }

                SpawnBuyers();
                Fractions.Manager.FractionDataMats[502] = new Fractions.Manager.FracMatsData(502, "Семена конопли", Chars.Repository.ItemsInfo[ItemId.WeedSeed].Icon, $"{Cfg.SeedPrice}$");
                Fractions.Manager.FractionDataMats[503] = new Fractions.Manager.FracMatsData(503, "Бутылка воды", Chars.Repository.ItemsInfo[ItemId.WaterBottle].Icon, $"{Cfg.WaterPrice}$");
                _loaded = true;
                Timers.Start("crime.weed", 15000, Tick, true);
                CrimeCore.Log.Write($"Weed: кустов {Plants.Count}, точек {Cfg.Spots.Count}, покупателей {Buyers.Count}", nLog.Type.Success);
            }
            catch (Exception e)
            {
                CrimeCore.Log.Write($"Weed Init Exception: {e}");
            }
        }

        public static void Save() => WeedConfig.Save();

        // ------------------------------------------------------------------ состояние куста

        private static DateTime ReadyAt(Plant plant) => plant.Planted.AddMinutes(Cfg.GrowMinutes);
        private static bool IsReady(Plant plant) => DateTime.Now >= ReadyAt(plant);

        private static int Stage(Plant plant)
        {
            var progress = (DateTime.Now - plant.Planted).TotalMinutes / Math.Max(1, Cfg.GrowMinutes);
            return progress >= 1 ? 2 : progress >= 0.35 ? 1 : 0;
        }

        /// <summary>Жив ли куст. Засохший (не поливали) или сгнивший (не собрали) удаляется.</summary>
        private static bool CheckAlive(Plant plant, bool notify = true)
        {
            var now = DateTime.Now;
            var readyAt = ReadyAt(plant);
            var waterUntil = (now < readyAt ? now : readyAt) - plant.LastWater;
            string reason = null;
            if (waterUntil.TotalMinutes > Cfg.WaterMinutes)
                reason = "засох без полива";
            else if (now > readyAt.AddMinutes(Cfg.RotMinutes))
                reason = "сгнил — урожай не собрали вовремя";
            if (reason == null)
                return true;

            Remove(plant);
            if (notify)
            {
                var owner = Main.GetPlayerByUUID(plant.OwnerUuid);
                if (owner != null)
                {
                    Notify.Send(owner, NotifyType.Warning, NotifyPosition.BottomCenter, $"Ваш куст конопли {reason}", 5000);
                    SendBlips(owner);
                }
            }
            return false;
        }

        private static string StatusText(Plant plant)
        {
            var now = DateTime.Now;
            if (IsReady(plant))
                return "~g~Конопля созрела~w~\n[E] Собрать урожай";
            var percent = (int)((now - plant.Planted).TotalMinutes * 100 / Math.Max(1, Cfg.GrowMinutes));
            var left = Cfg.WaterMinutes - (int)(now - plant.LastWater).TotalMinutes;
            var water = left <= 5 ? $"~r~Нужен полив ({Math.Max(0, left)} мин)" : $"~b~Полито~w~, полив через {left - 5} мин";
            return $"~y~Конопля {percent}%~w~\n{water}";
        }

        // ------------------------------------------------------------------ мир

        private static void Spawn(Plant plant)
        {
            Despawn(plant);
            var stage = Stage(plant);
            plant.ShownStage = stage;
            plant.Object = (ExtObject)NAPI.Object.CreateObject(NAPI.Util.GetHashKey(Models[stage]), plant.Position, new Vector3(0, 0, (plant.Id * 53) % 360), 255, plant.Dimension);
            // Клиент кладёт куст на землю (src_client/player/crime.js)
            plant.Object.SetSharedData("weedPlant", plant.Id);
            plant.ShownText = StatusText(plant);
            plant.Label = (ExtTextLabel)NAPI.TextLabel.CreateTextLabel(Main.StringToU16(plant.ShownText), plant.Position + new Vector3(0, 0, 1.3), 4f, 0.35f, 4, new Color(255, 255, 255), true, plant.Dimension);
            plant.Shape = CustomColShape.CreateCylinderColShape(plant.Position - new Vector3(0, 0, 1.5), 1.4f, 4f, plant.Dimension, ColShapeEnums.WeedPlant, plant.Id);
        }

        private static void Despawn(Plant plant)
        {
            if (plant.Object != null && plant.Object.Exists)
                plant.Object.Delete();
            if (plant.Label != null && plant.Label.Exists)
                plant.Label.Delete();
            if (plant.Shape != null)
                CustomColShape.DeleteColShape(plant.Shape);
            plant.Object = null;
            plant.Label = null;
            plant.Shape = null;
        }

        private static void Remove(Plant plant)
        {
            Despawn(plant);
            Plants.Remove(plant.Id);
            BlackMarket.BlackMarketRepository.Enqueue("DELETE FROM `weed_plants` WHERE `id`=@id", ("@id", plant.Id));
        }

        private static void Refresh(Plant plant)
        {
            var stage = Stage(plant);
            if (stage != plant.ShownStage || plant.Object == null || !plant.Object.Exists)
            {
                Spawn(plant);
                return;
            }
            var text = StatusText(plant);
            if (text != plant.ShownText && plant.Label != null && plant.Label.Exists)
            {
                plant.ShownText = text;
                plant.Label.Text = Main.StringToU16(text);
            }
        }

        private static void Tick()
        {
            if (!_loaded)
                return;
            foreach (var plant in Plants.Values.ToList())
            {
                if (plant.Busy || !CheckAlive(plant))
                    continue;
                Refresh(plant);
            }
        }

        /// <summary>Метки своих уличных кустов (при входе в игру и после изменений).</summary>
        public static void SendBlips(ExtPlayer player)
        {
            try
            {
                if (player == null || !player.IsCharacterData())
                    return;
                var uuid = player.GetUUID();
                var list = Plants.Values
                    .Where(p => p.OwnerUuid == uuid && p.HouseId == -1)
                    .Select(p => new { x = p.Position.X, y = p.Position.Y, z = p.Position.Z })
                    .ToList();
                Trigger.ClientEvent(player, "client.weed.blips", JsonConvert.SerializeObject(list));
            }
            catch (Exception e)
            {
                CrimeCore.Log.Write($"Weed SendBlips Exception: {e}");
            }
        }

        public static void OnCharacterLoaded(ExtPlayer player) => SendBlips(player);

        // ------------------------------------------------------------------ предметы

        /// <summary>Хук из ItemsUse: семена — посадка, свежая конопля — фасовка.</summary>
        public static void OnUseItem(ExtPlayer player, ItemId itemId)
        {
            if (itemId == ItemId.WeedSeed)
                TryPlant(player);
            else if (itemId == ItemId.WeedRaw)
                TryPack(player);
        }

        private static bool CanAct(ExtPlayer player)
        {
            if (player.IsInVehicle)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Выйдите из транспорта", 3000);
                return false;
            }
            if (BusyPlayers.Contains(player.GetUUID()))
                return false;
            return true;
        }

        private static void TryPlant(ExtPlayer player)
        {
            var characterData = player.GetCharacterData();
            var sessionData = player.GetSessionData();
            if (characterData == null || sessionData == null || !CanAct(player))
                return;
            if (!CrimeCore.IsCriminal(player))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Выращивать коноплю может только криминал", 3000);
                return;
            }
            var uuid = characterData.UUID;
            if (Plants.Values.Count(p => p.OwnerUuid == uuid) >= Cfg.PlayerMaxPlants)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"У вас уже {Cfg.PlayerMaxPlants} кустов", 3000);
                return;
            }

            var position = player.Position - new Vector3(0, 0, 1);
            var houseId = -1;
            var spotIndex = -1;
            if (characterData.InsideHouseID != -1)
            {
                var house = HouseManager.Houses.FirstOrDefault(h => h.ID == characterData.InsideHouseID);
                if (house == null || (house.Owner != sessionData.Name && !house.Roommates.ContainsKey(sessionData.Name)))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Садить можно только в своём доме", 3000);
                    return;
                }
                if (Plants.Values.Count(p => p.HouseId == house.ID) >= Cfg.HomeMaxPlants)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"В доме уже {Cfg.HomeMaxPlants} горшка", 3000);
                    return;
                }
                houseId = house.ID;
            }
            else
            {
                if (player.Dimension != 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Здесь садить нельзя", 3000);
                    return;
                }
                var best = -1;
                var bestDistance = float.MaxValue;
                for (var i = 0; i < Cfg.Spots.Count; i++)
                {
                    var spot = Cfg.Spots[i];
                    var distance = new Vector3(spot.X, spot.Y, 0).DistanceTo(new Vector3(position.X, position.Y, 0));
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = i;
                    }
                }
                if (best == -1 || bestDistance > SpotRadius)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Здесь не вырастет. Ищите скрытые поляны в лесах (или садите дома)", 4000);
                    return;
                }
                if (Plants.Values.Any(p => p.SpotIndex == best))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "На этой поляне уже что-то растёт", 3000);
                    return;
                }
                spotIndex = best;
            }
            if (Plants.Values.Any(p => p.Dimension == player.Dimension && p.Position.DistanceTo(position) < MinPlantDistance))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Слишком близко к другому кусту", 3000);
                return;
            }

            var dimension = player.Dimension;
            RunAction(player, "amb@world_human_gardener_plant@male@base", "base", () =>
            {
                if (Chars.Repository.getCountItem($"char_{uuid}", ItemId.WeedSeed, false) <= 0 || player.Position.DistanceTo(position + new Vector3(0, 0, 1)) > 2f)
                    return;
                Chars.Repository.Remove(player, $"char_{uuid}", "inventory", ItemId.WeedSeed, 1);
                var now = DateTime.Now;
                var plant = new Plant
                {
                    Id = _nextId++,
                    OwnerUuid = uuid,
                    OwnerName = sessionData.Name,
                    HouseId = houseId,
                    SpotIndex = spotIndex,
                    Position = position,
                    Dimension = dimension,
                    Planted = now,
                    LastWater = now,
                };
                Plants[plant.Id] = plant;
                Spawn(plant);
                BlackMarket.BlackMarketRepository.Enqueue(
                    "INSERT INTO `weed_plants` (`id`,`owner`,`owner_name`,`house`,`spot`,`x`,`y`,`z`,`dim`,`planted`,`watered`) VALUES (@id,@owner,@name,@house,@spot,@x,@y,@z,@dim,@planted,@watered)",
                    ("@id", plant.Id), ("@owner", uuid), ("@name", plant.OwnerName), ("@house", houseId), ("@spot", spotIndex),
                    ("@x", position.X), ("@y", position.Y), ("@z", position.Z), ("@dim", (int)dimension),
                    ("@planted", Unix(now)), ("@watered", Unix(now)));
                SendBlips(player);
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter,
                    $"Посажено. Созреет через {Cfg.GrowMinutes} мин, поливайте бутылкой воды не реже чем раз в {Cfg.WaterMinutes} мин", 6000);
                BlackMarket.Audit.AuditLog.Write("weed_plant", uuid, details: new { plant = plant.Id, house = houseId, spot = spotIndex });
            });
        }

        /// <summary>Фасовка: все высохшие пачки «Свежей конопли» превращаются в «Наркотики» 1:1.</summary>
        private static void TryPack(ExtPlayer player)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null || !CanAct(player))
                return;
            var location = $"char_{characterData.UUID}";
            if (!Chars.Repository.ItemsData.TryGetValue(location, out var locations) || !locations.TryGetValue("inventory", out var inventory))
                return;

            var dryBefore = Unix(DateTime.Now.AddMinutes(-Cfg.DryMinutes));
            var dry = new Dictionary<string, int>();
            long wetest = 0;
            foreach (var item in inventory.Values)
            {
                if (item.ItemId != ItemId.WeedRaw)
                    continue;
                long.TryParse(item.Data, out var harvested);
                var count = item.Count < 1 ? 1 : item.Count;
                if (harvested <= dryBefore)
                    dry[item.Data ?? ""] = (dry.TryGetValue(item.Data ?? "", out var c) ? c : 0) + count;
                else
                    wetest = Math.Max(wetest, harvested);
            }
            var total = dry.Values.Sum();
            if (total == 0)
            {
                var minutes = wetest == 0 ? Cfg.DryMinutes : (int)Math.Ceiling((wetest - dryBefore) / 60.0);
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"Конопля ещё сохнет, осталось ~{Math.Max(1, minutes)} мин", 4000);
                return;
            }
            if (Chars.Repository.isFreeSlots(player, ItemId.Drugs, total) != 0)
                return;

            RunAction(player, "amb@prop_human_parking_meter@male@base", "base", () =>
            {
                var packed = 0;
                foreach (var pair in dry)
                {
                    var have = inventory.Values.Where(i => i.ItemId == ItemId.WeedRaw && (i.Data ?? "") == pair.Key).Sum(i => i.Count < 1 ? 1 : i.Count);
                    var take = Math.Min(have, pair.Value);
                    if (take <= 0)
                        continue;
                    Chars.Repository.Remove(player, location, "inventory", ItemId.WeedRaw, take, pair.Key);
                    packed += take;
                }
                if (packed <= 0)
                    return;
                if (Chars.Repository.AddNewItem(player, location, "inventory", ItemId.Drugs, packed) == -1)
                {
                    // Места не хватило — возвращаем сырьё, чтобы ничего не пропало
                    Chars.Repository.AddNewItem(player, location, "inventory", ItemId.WeedRaw, packed, "0");
                    return;
                }
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Расфасовано: {packed} г травы", 4000);
            }, 5);
        }

        /// <summary>Покупка у Мавра: семена (только криминал) и вода.</summary>
        public static void BuyFromMavr(ExtPlayer player, ItemId itemId)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null)
                return;
            var seed = itemId == ItemId.WeedSeed;
            if (seed && !CrimeCore.IsCriminal(player))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "— Семена только для своих.", 3000);
                return;
            }
            var price = seed ? Cfg.SeedPrice : Cfg.WaterPrice;
            if (characterData.Money < price)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Недостаточно денег", 3000);
                return;
            }
            if (Chars.Repository.isFreeSlots(player, itemId) != 0)
                return;
            if (Chars.Repository.AddNewItem(player, $"char_{characterData.UUID}", "inventory", itemId, 1) == -1)
                return;
            MoneySystem.Wallet.Change(player, -price);
            GameLog.Money($"player({characterData.UUID})", "server", price, seed ? "buyMavr(weedSeed)" : "buyMavr(water)");
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter,
                seed ? "Семена куплены. Садите на лесных полянах (Грейпсид, Палето, Занкудо, северный берег) или дома в горшке" : "Бутылка воды куплена", 5000);
        }

        // ------------------------------------------------------------------ куст [E]

        [Interaction(ColShapeEnums.WeedPlant)]
        public static void OnPlant(ExtPlayer player, int plantId)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null || !Plants.TryGetValue(plantId, out var plant) || plant.Busy || !CanAct(player))
                    return;
                if (!CheckAlive(plant))
                    return;

                if (CrimeCore.IsPolice(player))
                {
                    plant.Busy = true;
                    RunAction(player, "amb@world_human_gardener_plant@male@base", "base", () =>
                    {
                        plant.Busy = false;
                        if (!Plants.ContainsKey(plant.Id))
                            return;
                        Remove(plant);
                        var reward = Cfg.PoliceReward;
                        if (reward > 0)
                        {
                            MoneySystem.Wallet.Change(player, reward);
                            GameLog.Money("server", $"player({characterData.UUID})", reward, $"weedDestroy({plant.Id})");
                        }
                        Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Куст уничтожен. Награда ${reward}", 4000);
                        var owner = Main.GetPlayerByUUID(plant.OwnerUuid);
                        if (owner != null)
                            SendBlips(owner);
                        BlackMarket.Audit.AuditLog.Write("weed_destroy", characterData.UUID, details: new { plant = plant.Id, owner = plant.OwnerUuid });
                    }, onCancel: () => plant.Busy = false);
                    return;
                }

                if (!CrimeCore.IsCriminal(player))
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Какой-то куст. Лучше не трогать", 3000);
                    return;
                }

                if (IsReady(plant))
                {
                    Harvest(player, plant);
                    return;
                }

                if (plant.OwnerUuid != characterData.UUID && plant.HouseId == -1)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Чужой куст ещё не созрел", 3000);
                    return;
                }
                Water(player, plant);
            }
            catch (Exception e)
            {
                CrimeCore.Log.Write($"Weed OnPlant Exception: {e}");
            }
        }

        private static void Water(ExtPlayer player, Plant plant)
        {
            var uuid = player.GetUUID();
            if ((DateTime.Now - plant.LastWater).TotalMinutes < 5)
            {
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Земля ещё влажная, полив не нужен", 3000);
                return;
            }
            if (Chars.Repository.getCountItem($"char_{uuid}", ItemId.WaterBottle, false) <= 0)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Нужна бутылка воды (магазин 24/7 или Мавр)", 3500);
                return;
            }
            plant.Busy = true;
            RunAction(player, "amb@world_human_gardener_plant@male@base", "base", () =>
            {
                plant.Busy = false;
                if (!Plants.ContainsKey(plant.Id) || Chars.Repository.getCountItem($"char_{uuid}", ItemId.WaterBottle, false) <= 0)
                    return;
                Chars.Repository.Remove(player, $"char_{uuid}", "inventory", ItemId.WaterBottle, 1);
                plant.LastWater = DateTime.Now;
                BlackMarket.BlackMarketRepository.Enqueue("UPDATE `weed_plants` SET `watered`=@w WHERE `id`=@id", ("@w", Unix(plant.LastWater)), ("@id", plant.Id));
                Refresh(plant);
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Полито. Следующий полив — в течение {Cfg.WaterMinutes} мин", 3500);
            }, onCancel: () => plant.Busy = false);
        }

        private static void Harvest(ExtPlayer player, Plant plant)
        {
            var characterData = player.GetCharacterData();
            var amount = CrimeCore.Rnd.Next(Cfg.YieldMin, Cfg.YieldMax + 1);
            if (Chars.Repository.isFreeSlots(player, ItemId.WeedRaw, amount) != 0)
                return;
            plant.Busy = true;
            RunAction(player, "amb@world_human_gardener_plant@male@base", "base", () =>
            {
                plant.Busy = false;
                if (!Plants.ContainsKey(plant.Id))
                    return;
                var location = $"char_{characterData.UUID}";
                if (Chars.Repository.AddNewItem(player, location, "inventory", ItemId.WeedRaw, amount, Unix(DateTime.Now).ToString(), stack: false) == -1)
                    return;
                Remove(plant);
                var owner = Main.GetPlayerByUUID(plant.OwnerUuid);
                if (owner != null)
                {
                    SendBlips(owner);
                    if (owner != player)
                        Notify.Send(owner, NotifyType.Warning, NotifyPosition.BottomCenter, "Кто-то собрал урожай с вашего куста!", 5000);
                }
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter,
                    $"Собрано {amount} свежей конопли. Через {Cfg.DryMinutes} мин высохнет — «Использовать» для фасовки", 6000);
                BlackMarket.Audit.AuditLog.Write("weed_harvest", characterData.UUID, details: new { plant = plant.Id, owner = plant.OwnerUuid, amount });
                if (plant.HouseId == -1 && CrimeCore.Roll(Cfg.HarvestPoliceChance))
                    CrimeCore.CallPolice(player, plant.Position, $"weed_{plant.Id}", "Грибники заметили подозрительный сбор урожая в лесу", StarsHarvest, "Выращивание наркотиков");
            }, onCancel: () => plant.Busy = false);
        }

        /// <summary>Действие с анимацией: игрок стоит на месте N секунд, потом onDone (если не умер/не вышел).</summary>
        private static void RunAction(ExtPlayer player, string dict, string anim, Action onDone, int seconds = ActionSeconds, Action onCancel = null)
        {
            var uuid = player.GetUUID();
            if (!BusyPlayers.Add(uuid))
            {
                onCancel?.Invoke();
                return;
            }
            var start = player.Position;
            Trigger.PlayAnimation(player, dict, anim, 1);
            Trigger.ClientEvent(player, "blockMove", true);
            Main.OnAntiAnim(player);
            NAPI.Task.Run(() =>
            {
                BusyPlayers.Remove(uuid);
                try
                {
                    if (player == null || !player.IsCharacterData() || player.GetUUID() != uuid)
                    {
                        onCancel?.Invoke();
                        return;
                    }
                    Trigger.StopAnimation(player);
                    Trigger.ClientEvent(player, "blockMove", false);
                    Main.OffAntiAnim(player);
                    if (player.Position.DistanceTo(start) > 2f || player.IsInVehicle)
                    {
                        onCancel?.Invoke();
                        return;
                    }
                    onDone();
                }
                catch (Exception e)
                {
                    onCancel?.Invoke();
                    CrimeCore.Log.Write($"Weed RunAction Exception: {e}");
                }
            }, seconds * 1000);
        }

        // ------------------------------------------------------------------ покупатели

        private static void SpawnBuyers()
        {
            if (Cfg.Buyers.Count == 0)
            {
                Cfg.Buyers = AutoBuyers();
                WeedConfig.Save();
            }
            for (var i = 0; i < Cfg.Buyers.Count; i++)
            {
                var buyer = Cfg.Buyers[i];
                if (buyer?.Position == null)
                    continue;
                var ped = PedSystem.Repository.CreateQuest(buyer.Model ?? "g_m_y_famca_01", buyer.Position, buyer.Heading, 0, null, ColShapeEnums.WeedBuyer, "~g~Покупатель", false);
                Buyers.Add(new BuyerRuntime { Config = buyer, Ped = ped });
                BuyerByPed[ped.Value] = Buyers.Count - 1;
            }
        }

        /// <summary>
        /// Шесть покупателей у задних дворов (точки разгрузки) магазинов 24/7 — в разных районах:
        /// берём самые удалённые друг от друга точки (farthest-point sampling).
        /// </summary>
        private static List<WeedBuyer> AutoBuyers()
        {
            var points = BusinessManager.BizList.Values
                .Where(b => b.Type == 0 && b.UnloadPoint != null && b.UnloadPoint.DistanceTo(new Vector3()) > 10)
                .Select(b => (pos: b.UnloadPoint, name: b.Address))
                .ToList();
            var result = new List<WeedBuyer>();
            if (points.Count == 0)
                return result;
            var chosen = new List<(Vector3 pos, string name)> { points.OrderBy(p => p.pos.Y).First() };
            while (chosen.Count < Math.Min(6, points.Count))
            {
                var next = points.Where(p => !chosen.Contains(p))
                    .OrderByDescending(p => chosen.Min(c => c.pos.DistanceTo(p.pos)))
                    .First();
                chosen.Add(next);
            }
            var models = new[] { "g_m_y_famca_01", "g_m_y_ballasout_01", "g_m_y_mexgoon_02", "a_m_y_hippy_01", "a_m_m_hillbilly_01", "g_m_y_salvagoon_01" };
            for (var i = 0; i < chosen.Count; i++)
            {
                result.Add(new WeedBuyer
                {
                    Name = string.IsNullOrEmpty(chosen[i].name) ? $"Покупатель {i + 1}" : chosen[i].name,
                    Model = models[i % models.Length],
                    Position = chosen[i].pos,
                    Heading = 0,
                    Price = 150 + CrimeCore.Rnd.Next(0, 5) * 10,
                    DailyCap = 60,
                });
            }
            return result;
        }

        private static int Remaining(BuyerRuntime buyer)
        {
            if (buyer.Day != DateTime.Today)
            {
                buyer.Day = DateTime.Today;
                buyer.SoldToday = 0;
            }
            return Math.Max(0, buyer.Config.DailyCap - buyer.SoldToday);
        }

        [Interaction(ColShapeEnums.WeedBuyer)]
        public static void OnBuyer(ExtPlayer player, int pedValue)
        {
            try
            {
                if (!player.IsCharacterData() || !BuyerByPed.TryGetValue(pedValue, out var index))
                    return;
                if (!CrimeCore.IsCriminal(player))
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "— Чего надо? Проваливай.", 3000);
                    return;
                }
                var buyer = Buyers[index];
                var have = Chars.Repository.getCountItem($"char_{player.GetUUID()}", ItemId.Drugs, false);
                if (have <= 0)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"— Есть трава? Беру по ${buyer.Config.Price} за грамм.", 4000);
                    return;
                }
                var left = Remaining(buyer);
                if (left <= 0)
                {
                    Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "— На сегодня хватит, приходи завтра.", 3500);
                    return;
                }
                SellTarget[player.GetUUID()] = index;
                Trigger.ClientEvent(player, "openInput", $"Продать траву по ${buyer.Config.Price}",
                    $"У вас {have} г, покупатель возьмёт до {left} г", 4, "weed_sell");
            }
            catch (Exception e)
            {
                CrimeCore.Log.Write($"Weed OnBuyer Exception: {e}");
            }
        }

        /// <summary>Ответ на ввод количества (Main.inputCallback "weed_sell").</summary>
        public static void OnSellInput(ExtPlayer player, string text)
        {
            try
            {
                var characterData = player.GetCharacterData();
                if (characterData == null || !SellTarget.TryGetValue(characterData.UUID, out var index))
                    return;
                SellTarget.Remove(characterData.UUID);
                var buyer = Buyers[index];
                if (buyer.Ped == null || player.Position.DistanceTo(buyer.Config.Position) > 4f || !CrimeCore.IsCriminal(player))
                    return;
                if (!int.TryParse(text?.Trim(), out var amount) || amount <= 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Введите количество", 3000);
                    return;
                }
                var location = $"char_{characterData.UUID}";
                amount = Math.Min(amount, Remaining(buyer));
                amount = Math.Min(amount, Chars.Repository.getCountItem(location, ItemId.Drugs, false));
                if (amount <= 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Нечего продать", 3000);
                    return;
                }
                Chars.Repository.Remove(player, location, "inventory", ItemId.Drugs, amount);
                buyer.SoldToday += amount;
                var money = amount * buyer.Config.Price;
                MoneySystem.Wallet.Change(player, money);
                GameLog.Money("server", $"player({characterData.UUID})", money, $"weedSell({index},{amount})");
                BlackMarket.Audit.AuditLog.Write("weed_sell_npc", characterData.UUID, details: new { buyer = index, amount, money });
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Продано {amount} г за ${money}", 4000);
                if (CrimeCore.Roll(Cfg.SellPoliceChance))
                    CrimeCore.CallPolice(player, player.Position, $"weedsell_{index}", "Поступил звонок: на улице торгуют наркотиками", StarsSell, "Сбыт наркотиков");
            }
            catch (Exception e)
            {
                CrimeCore.Log.Write($"Weed OnSellInput Exception: {e}");
            }
        }

        [ServerEvent(Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(ExtPlayer player, DisconnectionType type, string reason)
        {
            if (player == null)
                return;
            SellTarget.Remove(player.GetUUID());
        }
    }
}
