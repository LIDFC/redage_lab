using NeptuneEvo.BlackMarket.Audit;
using NeptuneEvo.BlackMarket.Config;
using NeptuneEvo.BlackMarket.Crypto;
using NeptuneEvo.BlackMarket.Deliveries;
using NeptuneEvo.BlackMarket.History;
using NeptuneEvo.BlackMarket.Models;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Handles;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace NeptuneEvo.BlackMarket.Methods
{
    /// <summary>
    /// Лоты Чёрного рынка: выставление, смена цены, снятие, покупка (в том числе частичная), истечение срока.
    /// Каждая операция целиком выполняется под <see cref="BlackMarketCore.Sync"/>: актуальные данные лота
    /// перечитываются внутри блокировки, все проверки идут до первого списания.
    /// </summary>
    public static class Lots
    {
        private static readonly Dictionary<int, Lot> All = new Dictionary<int, Lot>();
        private static int _lastId = 0;

        public static List<Lot> Snapshot()
        {
            lock (BlackMarketCore.Sync)
                return All.Values.Select(Copy).ToList();
        }

        public static Lot Get(int id)
        {
            lock (BlackMarketCore.Sync)
                return All.TryGetValue(id, out var lot) ? Copy(lot) : null;
        }

        private static Lot Copy(Lot l) => new Lot
        {
            Id = l.Id, OwnerUuid = l.OwnerUuid, ItemId = l.ItemId, Count = l.Count,
            PriceUnit = l.PriceUnit, Created = l.Created, Ends = l.Ends,
        };

        public static string ItemName(ItemId itemId) =>
            Chars.Repository.ItemsInfo.TryGetValue(itemId, out var info) ? info.Name : $"#{(int)itemId}";

        #region Загрузка
        public static void Load()
        {
            lock (BlackMarketCore.Sync)
            {
                All.Clear();
                _lastId = Math.Max(_lastId, MaxContainerId("bmlot_"));

                using var data = BlackMarketRepository.Read("SELECT * FROM `blackmarket_lots`");
                if (data != null)
                {
                    foreach (DataRow row in data.Rows)
                    {
                        var lot = new Lot
                        {
                            Id = Convert.ToInt32(row["id"]),
                            OwnerUuid = Convert.ToInt32(row["owner_uuid"]),
                            ItemId = (ItemId)Convert.ToInt32(row["item_id"]),
                            Count = Convert.ToInt32(row["count"]),
                            PriceUnit = Convert.ToInt64(row["price_unit"]),
                            Created = Convert.ToDateTime(row["created"]),
                            Ends = Convert.ToDateTime(row["ends"]),
                        };
                        _lastId = Math.Max(_lastId, lot.Id);

                        // Источник истины о количестве — сами предметы в эскроу
                        var stored = Escrow.Count(Escrow.LotContainer(lot.Id), lot.ItemId);
                        if (stored != lot.Count)
                        {
                            BlackMarketCore.Log.Write($"Лот {lot.Id}: в БД {lot.Count}, в эскроу {stored} — берём эскроу");
                            lot.Count = stored;
                            SaveLot(lot);
                        }

                        All[lot.Id] = lot;
                    }
                }

                // Просроченные за время простоя — вернуть владельцам (а не удалить, как делает Маркетплейс)
                foreach (var lot in All.Values.Where(l => l.Ends <= DateTime.Now || l.Count <= 0).ToList())
                    Close(lot, "lot_expire", 0);

                BlackMarketCore.Log.Write($"Лотов Чёрного рынка: {All.Count}");
            }
        }

        public static int MaxContainerId(string prefix)
        {
            var max = 0;
            foreach (var key in Chars.Repository.ItemsData.Keys)
                if (key.StartsWith(prefix) && int.TryParse(key.Substring(prefix.Length), out var id))
                    max = Math.Max(max, id);
            return max;
        }
        #endregion

        #region Выставление / цена / снятие
        public static OpResult Create(ExtPlayer player, int itemIdRaw, int count, long priceUnit, int hours)
        {
            var config = BlackMarketConfig.Current;
            var itemId = (ItemId)itemIdRaw;
            if (!config.IsAllowed(itemId))
                return OpResult.Fail("Этот предмет нельзя продавать на Чёрном рынке");
            if (count < 1)
                return OpResult.Fail("Укажите количество");
            if (priceUnit < 1 || priceUnit > config.MaxPricePerUnit)
                return OpResult.Fail($"Цена за штуку: от 1 до {BlackMarketCore.Btc(config.MaxPricePerUnit)}");
            if (hours < config.LotMinHours || hours > config.LotMaxHours)
                return OpResult.Fail($"Срок объявления: от {config.LotMinHours} до {config.LotMaxHours} ч");

            var uuid = player.GetUUID();
            lock (BlackMarketCore.Sync)
            {
                if (Escrow.CountInInventory(player, itemId) < count)
                    return OpResult.Fail($"В инвентаре нет {count} шт. «{ItemName(itemId)}»");

                var lot = new Lot
                {
                    Id = ++_lastId,
                    OwnerUuid = uuid,
                    ItemId = itemId,
                    Count = count,
                    PriceUnit = priceUnit,
                    Created = DateTime.Now,
                    Ends = DateTime.Now.AddHours(hours),
                };
                var container = Escrow.LotContainer(lot.Id);
                var moved = Escrow.TakeFromPlayer(player, itemId, count, container);
                if (moved != count)
                {
                    // Инвентарь поменялся на ходу — всё вернуть, лот не создавать
                    Escrow.ReturnToOwner(uuid, container);
                    AuditLog.Write("lot_create", uuid, itemId: (int)itemId, count: count, amount: priceUnit, result: "escrow_failed");
                    return OpResult.Fail("Не удалось забрать предметы из инвентаря, попробуйте ещё раз");
                }

                All[lot.Id] = lot;
                BlackMarketRepository.Enqueue(
                    "INSERT INTO `blackmarket_lots` (`id`, `owner_uuid`, `item_id`, `count`, `price_unit`, `created`, `ends`) " +
                    "VALUES (@id, @owner, @item, @count, @price, @created, @ends)",
                    ("@id", lot.Id), ("@owner", lot.OwnerUuid), ("@item", (int)lot.ItemId), ("@count", lot.Count),
                    ("@price", lot.PriceUnit), ("@created", lot.Created), ("@ends", lot.Ends));
                AuditLog.Write("lot_create", uuid, itemId: (int)itemId, count: count, amount: priceUnit, details: new { lot = lot.Id, hours });
                return OpResult.Success($"Лот выставлен: {ItemName(itemId)} × {count} по {BlackMarketCore.Btc(priceUnit)}");
            }
        }

        public static OpResult EditPrice(ExtPlayer player, int lotId, long priceUnit)
        {
            var config = BlackMarketConfig.Current;
            if (priceUnit < 1 || priceUnit > config.MaxPricePerUnit)
                return OpResult.Fail($"Цена за штуку: от 1 до {BlackMarketCore.Btc(config.MaxPricePerUnit)}");

            lock (BlackMarketCore.Sync)
            {
                if (!All.TryGetValue(lotId, out var lot) || lot.OwnerUuid != player.GetUUID())
                    return OpResult.Fail("Лот не найден");
                var old = lot.PriceUnit;
                lot.PriceUnit = priceUnit;
                SaveLot(lot);
                AuditLog.Write("lot_edit", lot.OwnerUuid, itemId: (int)lot.ItemId, count: lot.Count, amount: priceUnit, details: new { lot = lot.Id, old });
                return OpResult.Success($"Новая цена: {BlackMarketCore.Btc(priceUnit)} за штуку");
            }
        }

        public static OpResult Cancel(ExtPlayer player, int lotId)
        {
            lock (BlackMarketCore.Sync)
            {
                if (!All.TryGetValue(lotId, out var lot) || lot.OwnerUuid != player.GetUUID())
                    return OpResult.Fail("Лот не найден");
                Close(lot, "lot_cancel", lot.OwnerUuid);
                return OpResult.Success("Лот снят, товар возвращён (что не влезло — на личном складе)");
            }
        }

        /// <summary>Админское удаление: товар возвращается владельцу.</summary>
        public static bool AdminDelete(int lotId, int adminUuid)
        {
            lock (BlackMarketCore.Sync)
            {
                if (!All.TryGetValue(lotId, out var lot))
                    return false;
                Close(lot, "lot_admin_delete", adminUuid);
                return true;
            }
        }

        /// <summary>Закрыть лот и вернуть остаток владельцу (снятие, срок, админ).</summary>
        private static void Close(Lot lot, string action, int actorUuid)
        {
            All.Remove(lot.Id);
            var left = Escrow.ReturnToOwner(lot.OwnerUuid, Escrow.LotContainer(lot.Id));
            BlackMarketRepository.Enqueue("DELETE FROM `blackmarket_lots` WHERE `id`=@id", ("@id", lot.Id));
            AuditLog.Write(action, actorUuid, lot.OwnerUuid, (int)lot.ItemId, lot.Count, result: left > 0 ? "return_partial" : "ok",
                details: new { lot = lot.Id, notReturned = left });

            if (action == "lot_expire")
            {
                var owner = Main.GetPlayerByUUID(lot.OwnerUuid);
                if (owner != null && owner.IsCharacterData())
                    Notify.Send(owner, NotifyType.Info, NotifyPosition.BottomCenter,
                        $"Срок объявления «{ItemName(lot.ItemId)}» истёк, товар возвращён", 5000);
            }
        }

        private static void SaveLot(Lot lot) =>
            BlackMarketRepository.Enqueue("UPDATE `blackmarket_lots` SET `count`=@count, `price_unit`=@price WHERE `id`=@id",
                ("@count", lot.Count), ("@price", lot.PriceUnit), ("@id", lot.Id));
        #endregion

        #region Покупка
        /// <summary>
        /// Покупка count штук из лота. Порядок: актуальный лот → право покупки → количество → источник и баланс →
        /// точка закладки → (только теперь) списание, зачисление продавцу, комиссия, перенос предметов в закладку.
        /// </summary>
        public static OpResult Buy(ExtPlayer player, int lotId, int count, bool fromFraction, out Drop drop)
        {
            drop = null;
            var config = BlackMarketConfig.Current;
            var buyerUuid = player.GetUUID();

            lock (BlackMarketCore.Sync)
            {
                if (!All.TryGetValue(lotId, out var lot))
                    return OpResult.Fail("Объявление уже снято или раскуплено");
                if (lot.OwnerUuid == buyerUuid)
                    return OpResult.Fail("Нельзя купить свой лот");
                if (lot.Ends <= DateTime.Now)
                    return OpResult.Fail("Срок объявления истёк");
                if (count < 1 || count > lot.Count)
                    return OpResult.Fail(lot.Count > 0 ? $"Доступно {lot.Count} шт." : "Товар закончился");

                var container = Escrow.LotContainer(lot.Id);
                if (Escrow.Count(container, lot.ItemId) < count)
                    return OpResult.Fail("Товар закончился");

                long total;
                try
                {
                    total = checked(lot.PriceUnit * count);
                }
                catch (OverflowException)
                {
                    return OpResult.Fail("Слишком большая сумма");
                }

                long fee = 0;
                var fractionId = 0;
                if (fromFraction)
                {
                    fractionId = player.GetFractionId();
                    if (!BlackMarketCore.IsCriminalFraction(fractionId))
                        return OpResult.Fail("У вашей фракции нет крипто-кошелька");
                    var member = player.GetFractionMemberData();
                    if (member == null || member.Rank < config.FractionPaymentRank)
                        return OpResult.Fail($"Платить с кошелька банды можно с {config.FractionPaymentRank} ранга");
                    fee = (long)Math.Ceiling(total * config.FractionFeePercent / 100m);
                    if (CryptoWallets.GetFraction(fractionId) < total + fee)
                        return OpResult.Fail($"В кошельке банды не хватает: нужно {BlackMarketCore.Btc(total + fee)} (включая комиссию {BlackMarketCore.Btc(fee)})");
                }
                else if (CryptoWallets.Available(buyerUuid) < total)
                    return OpResult.Fail($"Не хватает BTC: нужно {BlackMarketCore.Btc(total)}");

                var point = DropManager.PickPoint();
                if (point == null)
                    return OpResult.Fail("Сейчас нет свободных мест для закладки, попробуйте позже");

                // --- Списание и зачисление ---
                var paid = fromFraction
                    ? CryptoWallets.ChangeFraction(fractionId, -(total + fee))
                    : CryptoWallets.ChangePersonal(buyerUuid, -total);
                if (!paid)
                    return OpResult.Fail("Не хватает BTC");

                CryptoWallets.ChangePersonal(lot.OwnerUuid, total);
                CryptoWallets.ChangeSystem(fee);

                lot.Count -= count;
                drop = DropManager.Create(buyerUuid, lot, count, point);

                if (lot.Count <= 0)
                {
                    All.Remove(lot.Id);
                    BlackMarketRepository.Enqueue("DELETE FROM `blackmarket_lots` WHERE `id`=@id", ("@id", lot.Id));
                }
                else
                    SaveLot(lot);

                // --- История без имён и полный аудит ---
                var title = $"{ItemName(lot.ItemId)} × {count}";
                if (fromFraction)
                    HistoryLog.Add(0, "purchase", title, -(total + fee), fractionId: fractionId);
                else
                    HistoryLog.Add(buyerUuid, "purchase", title, -total);
                HistoryLog.Add(lot.OwnerUuid, "sale", title, total);
                AuditLog.Write("lot_buy", buyerUuid, lot.OwnerUuid, (int)lot.ItemId, count, total,
                    fromFraction ? Source.Fraction : Source.Personal, fee,
                    details: new { lot = lot.Id, drop = drop.Id, fractionId, priceUnit = lot.PriceUnit });

                var seller = Main.GetPlayerByUUID(lot.OwnerUuid);
                if (seller != null && seller.IsCharacterData())
                    Notify.Send(seller, NotifyType.Success, NotifyPosition.BottomCenter,
                        $"Чёрный рынок: продано {title}, +{BlackMarketCore.Btc(total)}", 5000);

                return OpResult.Success($"Товар подготовлен. Точка отмечена на карте — {config.DropMinutes} мин.");
            }
        }
        #endregion

        /// <summary>Раз в секунду: закрыть просроченные лоты.</summary>
        public static void Tick()
        {
            lock (BlackMarketCore.Sync)
            {
                var now = DateTime.Now;
                foreach (var lot in All.Values.Where(l => l.Ends <= now).ToList())
                    Close(lot, "lot_expire", 0);
            }
        }
    }
}
