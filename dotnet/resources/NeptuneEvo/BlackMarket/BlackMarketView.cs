using NeptuneEvo.BlackMarket.Config;
using NeptuneEvo.BlackMarket.Crypto;
using NeptuneEvo.BlackMarket.Deliveries;
using NeptuneEvo.BlackMarket.Methods;
using NeptuneEvo.BlackMarket.P2P;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Handles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeptuneEvo.BlackMarket
{
    /// <summary>
    /// Данные для CEF-приложения. Продавцы и покупатели анонимны: вместо UUID только признак «моё».
    /// </summary>
    public static class BlackMarketView
    {
        public static object Build(ExtPlayer player)
        {
            var uuid = player.GetUUID();
            var config = BlackMarketConfig.Current;
            var wallet = CryptoWallets.Get(uuid);

            object fraction = null;
            var fractionId = player.GetFractionId();
            if (BlackMarketCore.IsCriminalFraction(fractionId))
            {
                var rank = player.GetFractionMemberData()?.Rank ?? 0;
                fraction = new
                {
                    name = BlackMarketCore.FractionName(fractionId),
                    balance = CryptoWallets.GetFraction(fractionId),
                    rank = (int)rank,
                    needRank = config.FractionPaymentRank,
                    canPay = rank >= config.FractionPaymentRank,
                };
            }

            var now = DateTime.Now;
            return new
            {
                wallet = new { balance = wallet.Balance, reserved = wallet.Reserved },
                fraction,
                config = new
                {
                    fractionFee = config.FractionFeePercent,
                    p2pFee = config.P2PFeePercent,
                    minHours = config.LotMinHours,
                    maxHours = config.LotMaxHours,
                    rate = config.ExchangeUsdPerBtc,
                    dropMinutes = config.DropMinutes,
                    maxPrice = config.MaxPricePerUnit,
                },
                lots = Lots.Snapshot().Where(l => l.Count > 0).Select(l => new
                {
                    id = l.Id,
                    itemId = (int)l.ItemId,
                    name = Lots.ItemName(l.ItemId),
                    category = Category(l.ItemId),
                    count = l.Count,
                    price = l.PriceUnit,
                    minutesLeft = (int)Math.Max(0, (l.Ends - now).TotalMinutes),
                    mine = l.OwnerUuid == uuid,
                }).ToList(),
                inventory = Escrow.InventorySummary(player, config.IsAllowed).Select(i => new
                {
                    itemId = (int)i.Key,
                    name = Lots.ItemName(i.Key),
                    category = Category(i.Key),
                    count = i.Value,
                }).ToList(),
                p2p = P2PManager.Snapshot().Select(o => new
                {
                    id = o.Id,
                    amount = o.AmountLeft,
                    price = o.PricePerBtc,
                    minutesLeft = (int)Math.Max(0, (o.Ends - now).TotalMinutes),
                    mine = o.OwnerUuid == uuid,
                }).ToList(),
                drops = DropManager.ForBuyer(uuid).Select(d => new
                {
                    id = d.Id,
                    itemId = (int)d.ItemId,
                    name = Lots.ItemName(d.ItemId),
                    count = d.Count,
                    minutesLeft = (int)Math.Max(0, (d.Expires - now).TotalMinutes),
                }).ToList(),
            };
        }

        /// <summary>Категория для фильтров каталога — по метаданным предмета.</summary>
        public static string Category(ItemId itemId)
        {
            if (itemId == ItemId.Drugs || itemId == ItemId.Cocaine)
                return "drugs";
            if (!Chars.Repository.ItemsInfo.TryGetValue(itemId, out var info))
                return "other";
            switch (info.functionType)
            {
                case newItemType.Weapons: return "weapons";
                case newItemType.MeleeWeapons: return "melee";
                case newItemType.Ammo: return "ammo";
                case newItemType.Modification: return "mods";
            }
            return itemId == ItemId.BodyArmor ? "armor" : "tools";
        }
    }
}
