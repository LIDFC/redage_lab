using GTANetworkAPI;
using NeptuneEvo.BlackMarket.Audit;
using NeptuneEvo.BlackMarket.Config;
using NeptuneEvo.BlackMarket.Methods;
using NeptuneEvo.BlackMarket.Models;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace NeptuneEvo.BlackMarket.Deliveries
{
    /// <summary>
    /// Закладки: после оплаты товар переезжает из лота в контейнер закладки и появляется объект в случайной точке
    /// из конфига. Метку на карте видит только покупатель (клиентский блип), а сам объект — все: забрать может любой,
    /// кто успеет первым (E + анимация). Срок 90 мин, после выхода покупателя из игры — не больше 30 мин.
    /// Незабранная закладка исчезает вместе с товаром. После рестарта закладки восстанавливаются с исходным сроком.
    /// </summary>
    public class DropManager : Script
    {
        private static readonly Dictionary<int, Drop> All = new Dictionary<int, Drop>();
        private static int _lastId = 0;
        private static readonly Random Random = new Random();

        private const string BlipPrefix = "bmdrop_";
        private const int BlipSprite = 1;
        private const int BlipColor = 1;

        #region Загрузка
        public static void Load()
        {
            lock (BlackMarketCore.Sync)
            {
                All.Clear();
                _lastId = Math.Max(_lastId, Lots.MaxContainerId("bmdrop_"));

                using var data = BlackMarketRepository.Read("SELECT * FROM `blackmarket_drops`");
                if (data != null)
                {
                    foreach (DataRow row in data.Rows)
                    {
                        var drop = new Drop
                        {
                            Id = Convert.ToInt32(row["id"]),
                            BuyerUuid = Convert.ToInt32(row["buyer_uuid"]),
                            LotId = Convert.ToInt32(row["lot_id"]),
                            ItemId = (ItemId)Convert.ToInt32(row["item_id"]),
                            Count = Convert.ToInt32(row["count"]),
                            Position = new Vector3(Convert.ToSingle(row["pos_x"]), Convert.ToSingle(row["pos_y"]), Convert.ToSingle(row["pos_z"])),
                            Created = Convert.ToDateTime(row["created"]),
                            Expires = Convert.ToDateTime(row["expires"]),
                        };
                        _lastId = Math.Max(_lastId, drop.Id);
                        drop.Count = Escrow.Count(Escrow.DropContainer(drop.Id), drop.ItemId);

                        // Срок считается и во время простоя сервера
                        if (drop.Expires <= DateTime.Now || drop.Count <= 0)
                        {
                            Expire(drop, drop.Count <= 0 ? "drop_empty" : "drop_expire");
                            continue;
                        }

                        All[drop.Id] = drop;
                        Spawn(drop);
                    }
                }
                BlackMarketCore.Log.Write($"Закладок восстановлено: {All.Count}");
            }
        }
        #endregion

        #region Создание
        /// <summary>Свободная точка: не ближе 10 м к другой активной закладке.</summary>
        public static Vector3 PickPoint()
        {
            var points = BlackMarketConfig.Current.DropPoints;
            if (points == null || points.Count == 0)
                return null;
            lock (BlackMarketCore.Sync)
            {
                var free = points.Where(p => All.Values.All(d => d.Position.DistanceTo(p) > 10f)).ToList();
                return free.Count > 0 ? free[Random.Next(free.Count)] : null;
            }
        }

        /// <summary>Создать закладку из лота (вызывается из Lots.Buy под блокировкой, после оплаты).</summary>
        public static Drop Create(int buyerUuid, Lot lot, int count, Vector3 point)
        {
            lock (BlackMarketCore.Sync)
            {
                var drop = new Drop
                {
                    Id = ++_lastId,
                    BuyerUuid = buyerUuid,
                    LotId = lot.Id,
                    ItemId = lot.ItemId,
                    Position = point,
                    Created = DateTime.Now,
                    Expires = DateTime.Now.AddMinutes(BlackMarketConfig.Current.DropMinutes),
                };
                drop.Count = Escrow.Move(Escrow.LotContainer(lot.Id), Escrow.DropContainer(drop.Id), lot.ItemId, count);
                All[drop.Id] = drop;

                BlackMarketRepository.Enqueue(
                    "INSERT INTO `blackmarket_drops` (`id`, `buyer_uuid`, `lot_id`, `item_id`, `count`, `pos_x`, `pos_y`, `pos_z`, `created`, `expires`) " +
                    "VALUES (@id, @buyer, @lot, @item, @count, @x, @y, @z, @created, @expires)",
                    ("@id", drop.Id), ("@buyer", buyerUuid), ("@lot", lot.Id), ("@item", (int)drop.ItemId), ("@count", drop.Count),
                    ("@x", point.X), ("@y", point.Y), ("@z", point.Z), ("@created", drop.Created), ("@expires", drop.Expires));

                Spawn(drop);
                var buyer = Main.GetPlayerByUUID(buyerUuid);
                if (buyer != null && buyer.IsCharacterData())
                    ShowGps(buyer, drop);
                return drop;
            }
        }

        private static void Spawn(Drop drop)
        {
            var ground = drop.Position - new Vector3(0, 0, 0.98);
            drop.Object = NAPI.Object.CreateObject(NAPI.Util.GetHashKey(BlackMarketConfig.Current.DropProp), ground, new Vector3(0, 0, Random.Next(0, 360)), 255, 0);
            drop.Shape = CustomColShape.CreateCylinderColShape(drop.Position - new Vector3(0, 0, 1.2), 1.4f, 2.6f, 0, ColShapeEnums.BlackMarketDrop, drop.Id);
        }

        private static void Despawn(Drop drop)
        {
            if (drop.Object != null && drop.Object.Exists)
                drop.Object.Delete();
            drop.Object = null;
            CustomColShape.DeleteColShape(drop.Shape);
            drop.Shape = null;
        }
        #endregion

        #region GPS покупателя
        public static void ShowGps(ExtPlayer player, Drop drop)
        {
            Trigger.ClientEvent(player, "createBlip", BlipPrefix + drop.Id, "Закладка", BlipSprite, drop.Position, 1f, BlipColor);
            Trigger.ClientEvent(player, "createWaypoint", drop.Position.X, drop.Position.Y);
        }

        private static void HideGps(int uuid, int dropId)
        {
            var player = Main.GetPlayerByUUID(uuid);
            if (player != null && player.IsCharacterData())
                Trigger.ClientEvent(player, "deleteBlip", BlipPrefix + dropId);
        }

        /// <summary>Вход в игру: вернуть метки на живые закладки покупателя.</summary>
        public static void OnCharacterLoaded(ExtPlayer player)
        {
            var uuid = player.GetUUID();
            lock (BlackMarketCore.Sync)
            {
                foreach (var drop in All.Values.Where(d => d.BuyerUuid == uuid))
                    ShowGps(player, drop);
            }
        }

        /// <summary>Выход покупателя: закладке остаётся не больше dropOfflineMinutes.</summary>
        public static void OnPlayerDisconnected(int uuid)
        {
            if (uuid <= 0)
                return;
            lock (BlackMarketCore.Sync)
            {
                var limit = DateTime.Now.AddMinutes(BlackMarketConfig.Current.DropOfflineMinutes);
                foreach (var drop in All.Values.Where(d => d.BuyerUuid == uuid && d.Expires > limit))
                {
                    drop.Expires = limit;
                    BlackMarketRepository.Enqueue("UPDATE `blackmarket_drops` SET `expires`=@expires WHERE `id`=@id",
                        ("@expires", drop.Expires), ("@id", drop.Id));
                }
                foreach (var drop in All.Values.Where(d => d.PickingUuid == uuid))
                    drop.PickingUuid = 0;
            }
        }

        /// <summary>Закладки игрока для раздела «Мои заказы» (только свои, только активные).</summary>
        public static List<Drop> ForBuyer(int uuid)
        {
            lock (BlackMarketCore.Sync)
                return All.Values.Where(d => d.BuyerUuid == uuid).ToList();
        }
        #endregion

        #region Забрать
        [Interaction(ColShapeEnums.BlackMarketDrop)]
        public static void OnInteraction(ExtPlayer player, int index)
        {
            var sessionData = player.GetSessionData();
            var characterData = player.GetCharacterData();
            if (sessionData == null || characterData == null || player.IsInVehicle || sessionData.DeathData.IsDying)
                return;

            var pickupMs = Math.Max(1, BlackMarketConfig.Current.PickupSeconds) * 1000;
            lock (BlackMarketCore.Sync)
            {
                if (!All.TryGetValue(index, out var drop))
                    return;
                if (drop.PickingUuid != 0)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Кто-то уже роется здесь", 3000);
                    return;
                }
                drop.PickingUuid = characterData.UUID;
            }

            Trigger.ClientEvent(player, "blockMove", true);
            Main.OnAntiAnim(player);
            Trigger.PlayAnimation(player, "amb@medic@standing@kneel@base", "base", 39);
            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Достаёте закладку...", pickupMs);

            NAPI.Task.Run(() => FinishPickup(player, index), pickupMs);
        }

        private static void FinishPickup(ExtPlayer player, int dropId)
        {
            try
            {
                if (player != null && player.IsCharacterData())
                {
                    Main.OffAntiAnim(player);
                    Trigger.ClientEvent(player, "blockMove", false);
                    Trigger.StopAnimation(player);
                }

                lock (BlackMarketCore.Sync)
                {
                    // Закладку могли забрать/она могла истечь, пока шла анимация — всё перепроверяем
                    if (!All.TryGetValue(dropId, out var drop))
                        return;
                    var characterData = player?.GetCharacterData();
                    if (characterData == null || drop.PickingUuid != characterData.UUID)
                        return;
                    drop.PickingUuid = 0;

                    if (player.GetSessionData()?.DeathData.IsDying != false || player.IsInVehicle || player.Position.DistanceTo(drop.Position) > 3f)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Не удалось забрать закладку", 3000);
                        return;
                    }

                    var container = Escrow.DropContainer(drop.Id);
                    var given = Escrow.GiveToPlayer(player, container);
                    if (given <= 0)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Нет места в инвентаре — закладка осталась на месте", 4000);
                        return;
                    }

                    var left = Escrow.Count(container, drop.ItemId);
                    var stolen = characterData.UUID != drop.BuyerUuid;
                    AuditLog.Write(stolen ? "drop_stolen" : "drop_pickup", characterData.UUID, drop.BuyerUuid, (int)drop.ItemId, given,
                        details: new { drop = drop.Id, lot = drop.LotId, left });

                    if (left > 0)
                    {
                        drop.Count = left;
                        BlackMarketRepository.Enqueue("UPDATE `blackmarket_drops` SET `count`=@count WHERE `id`=@id", ("@count", left), ("@id", drop.Id));
                        Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter,
                            $"Забрано {given} шт. «{Lots.ItemName(drop.ItemId)}», остальное не влезло", 4000);
                        return;
                    }

                    Remove(drop);
                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter,
                        $"Вы забрали закладку: {Lots.ItemName(drop.ItemId)} × {given}", 4000);
                }
            }
            catch (Exception e)
            {
                BlackMarketCore.Log.Write($"FinishPickup Exception: {e}");
            }
        }
        #endregion

        #region Истечение
        /// <summary>Раз в секунду: убрать просроченные закладки (товар потерян).</summary>
        public static void Tick()
        {
            lock (BlackMarketCore.Sync)
            {
                var now = DateTime.Now;
                foreach (var drop in All.Values.Where(d => d.Expires <= now && d.PickingUuid == 0).ToList())
                    Expire(drop, "drop_expire");
            }
        }

        private static void Expire(Drop drop, string action)
        {
            Escrow.Destroy(Escrow.DropContainer(drop.Id));
            AuditLog.Write(action, 0, drop.BuyerUuid, (int)drop.ItemId, drop.Count, details: new { drop = drop.Id, lot = drop.LotId });
            Remove(drop);
        }

        private static void Remove(Drop drop)
        {
            All.Remove(drop.Id);
            Despawn(drop);
            HideGps(drop.BuyerUuid, drop.Id);
            BlackMarketRepository.Enqueue("DELETE FROM `blackmarket_drops` WHERE `id`=@id", ("@id", drop.Id));
        }

        /// <summary>Админ: все активные закладки.</summary>
        public static List<Drop> Snapshot()
        {
            lock (BlackMarketCore.Sync)
                return All.Values.ToList();
        }
        #endregion
    }
}
