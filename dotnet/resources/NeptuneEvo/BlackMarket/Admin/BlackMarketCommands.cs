using GTANetworkAPI;
using NeptuneEvo.BlackMarket.Audit;
using NeptuneEvo.BlackMarket.Config;
using NeptuneEvo.BlackMarket.Crypto;
using NeptuneEvo.BlackMarket.Deliveries;
using NeptuneEvo.BlackMarket.Methods;
using NeptuneEvo.BlackMarket.P2P;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Core;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using System;
using System.Globalization;
using System.Linq;

namespace NeptuneEvo.BlackMarket.Admin
{
    /// <summary>
    /// /bm — управление Чёрным рынком (просмотр с 5 уровня, изменения с 8):
    ///  lots [itemId] | lot id | dellot id | p2p | delp2p id | drops | system
    ///  wl [add|remove itemId] | cfg [ключ значение] | logs [uuid] | droppoint add|count
    /// Администраторы видят UUID сторон; игрокам эти данные не показываются.
    /// </summary>
    class BlackMarketCommands : Script
    {
        [Command(AdminCommands.blackmarket, GreedyArg = true)]
        public static void CMD_BlackMarket(ExtPlayer player, string args)
        {
            if (!CommandsAccess.CanUseCmd(player, AdminCommands.blackmarket))
                return;

            var parts = (args ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var sub = parts.Length > 0 ? parts[0].ToLower() : "";
            int Arg(int i) => parts.Length > i && int.TryParse(parts[i], out var v) ? v : 0;

            switch (sub)
            {
                case "lots":
                    {
                        var itemFilter = Arg(1);
                        var lots = Lots.Snapshot().Where(l => itemFilter == 0 || (int)l.ItemId == itemFilter).OrderBy(l => l.Id).ToList();
                        CryptoCommands.Chat(player, $"Лотов: {lots.Count}");
                        foreach (var l in lots.Take(20))
                            CryptoCommands.Chat(player, $"#{l.Id} {Lots.ItemName(l.ItemId)} x{l.Count} по {l.PriceUnit} BTC, владелец UUID {l.OwnerUuid}, до {l.Ends:dd.MM HH:mm}");
                    }
                    return;
                case "lot":
                    {
                        var l = Lots.Get(Arg(1));
                        if (l == null) { CryptoCommands.Chat(player, "Лот не найден"); return; }
                        Main.PlayerNames.TryGetValue(l.OwnerUuid, out var name);
                        CryptoCommands.Chat(player, $"#{l.Id} {Lots.ItemName(l.ItemId)} (id {(int)l.ItemId}) x{l.Count}, {l.PriceUnit} BTC/шт, " +
                            $"владелец {name} (UUID {l.OwnerUuid}), создан {l.Created:dd.MM HH:mm}, до {l.Ends:dd.MM HH:mm}, в эскроу {Escrow.Count(Escrow.LotContainer(l.Id), l.ItemId)}");
                    }
                    return;
                case "dellot":
                    if (!CryptoCommands.CanManage(player)) return;
                    if (Lots.AdminDelete(Arg(1), player.GetUUID()))
                    {
                        GameLog.Admin(player.Name, $"bm dellot {Arg(1)}", "");
                        CryptoCommands.Chat(player, "Лот снят, товар возвращён владельцу");
                    }
                    else CryptoCommands.Chat(player, "Лот не найден");
                    return;
                case "p2p":
                    foreach (var o in P2PManager.Snapshot().OrderBy(o => o.Id).Take(20))
                        CryptoCommands.Chat(player, $"P2P #{o.Id}: {o.AmountLeft} BTC по {o.PricePerBtc:0.####}$, UUID {o.OwnerUuid}, до {o.Ends:dd.MM HH:mm}");
                    return;
                case "delp2p":
                    if (!CryptoCommands.CanManage(player)) return;
                    if (P2PManager.AdminDelete(Arg(1), player.GetUUID()))
                    {
                        GameLog.Admin(player.Name, $"bm delp2p {Arg(1)}", "");
                        CryptoCommands.Chat(player, "Заявка снята, BTC разблокированы");
                    }
                    else CryptoCommands.Chat(player, "Заявка не найдена");
                    return;
                case "drops":
                    foreach (var d in DropManager.Snapshot().OrderBy(d => d.Id).Take(20))
                        CryptoCommands.Chat(player, $"Закладка #{d.Id}: {Lots.ItemName(d.ItemId)} x{d.Count}, покупатель UUID {d.BuyerUuid}, до {d.Expires:HH:mm}, " +
                            $"{d.Position.X:0},{d.Position.Y:0},{d.Position.Z:0}");
                    return;
                case "system":
                    CryptoCommands.Chat(player, $"Системный кошелёк (комиссии): {BlackMarketCore.Btc(CryptoWallets.SystemBalance)}");
                    return;
                case "wl":
                    Whitelist(player, parts);
                    return;
                case "cfg":
                    Cfg(player, parts);
                    return;
                case "logs":
                    Logs(player, Arg(1));
                    return;
                case "droppoint":
                    DropPoint(player, parts);
                    return;
            }

            CryptoCommands.Chat(player, "/bm lots [itemId] | lot id | dellot id | p2p | delp2p id | drops | system | wl [add|remove id] | cfg [ключ значение] | logs [uuid] | droppoint add|count");
        }

        private static void Whitelist(ExtPlayer player, string[] parts)
        {
            var config = BlackMarketConfig.Current;
            if (parts.Length < 3)
            {
                CryptoCommands.Chat(player, $"Whitelist ({config.Whitelist.Count}): " +
                    string.Join(", ", config.Whitelist.Take(60).Select(i => $"{i}:{Lots.ItemName((ItemId)i)}")));
                return;
            }
            if (!CryptoCommands.CanManage(player) || !int.TryParse(parts[2], out var itemId) || !Chars.Repository.ItemsInfo.ContainsKey((ItemId)itemId))
            {
                CryptoCommands.Chat(player, "Неизвестный itemId");
                return;
            }

            if (parts[1] == "add" && !config.Whitelist.Contains(itemId))
                config.Whitelist.Add(itemId);
            else if (parts[1] == "remove")
                config.Whitelist.Remove(itemId);
            else
            {
                CryptoCommands.Chat(player, "/bm wl add|remove itemId");
                return;
            }
            BlackMarketConfig.Save();
            AuditLog.Write("admin_whitelist", player.GetUUID(), itemId: itemId, result: parts[1], source: Source.Admin);
            GameLog.Admin(player.Name, $"bm wl {parts[1]} {itemId}", "");
            CryptoCommands.Chat(player, $"{Lots.ItemName((ItemId)itemId)}: {(config.Whitelist.Contains(itemId) ? "разрешён" : "запрещён")}. Уже выставленные лоты не трогаются.");
        }

        private static void Cfg(ExtPlayer player, string[] parts)
        {
            var c = BlackMarketConfig.Current;
            if (parts.Length < 3)
            {
                CryptoCommands.Chat(player, $"rank={c.FractionPaymentRank} ffee={c.FractionFeePercent}% p2pfee={c.P2PFeePercent}% hours={c.LotMinHours}-{c.LotMaxHours} " +
                    $"drop={c.DropMinutes}/{c.DropOfflineMinutes}мин pickup={c.PickupSeconds}с rate={c.ExchangeUsdPerBtc}$ maxprice={c.MaxPricePerUnit}");
                return;
            }
            if (!CryptoCommands.CanManage(player))
                return;
            if (!decimal.TryParse(parts[2].Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) || v < 0)
            {
                CryptoCommands.Chat(player, "Значение должно быть числом ≥ 0");
                return;
            }

            switch (parts[1].ToLower())
            {
                case "rank": c.FractionPaymentRank = (int)v; break;
                case "ffee": c.FractionFeePercent = Math.Min(100, v); break;
                case "p2pfee": c.P2PFeePercent = Math.Min(100, v); break;
                case "minhours": c.LotMinHours = Math.Max(1, (int)v); break;
                case "maxhours": c.LotMaxHours = Math.Max(c.LotMinHours, (int)v); break;
                case "drop": c.DropMinutes = Math.Max(1, (int)v); break;
                case "dropoffline": c.DropOfflineMinutes = Math.Max(1, (int)v); break;
                case "pickup": c.PickupSeconds = Math.Max(1, (int)v); break;
                case "rate": c.ExchangeUsdPerBtc = v; break;
                case "maxprice": c.MaxPricePerUnit = Math.Max(1, (long)v); break;
                default:
                    CryptoCommands.Chat(player, "Ключи: rank ffee p2pfee minhours maxhours drop dropoffline pickup rate maxprice");
                    return;
            }
            BlackMarketConfig.Save();
            AuditLog.Write("admin_config", player.GetUUID(), source: Source.Admin, details: new { key = parts[1], value = v });
            GameLog.Admin(player.Name, $"bm cfg {parts[1]} {v}", "");
            CryptoCommands.Chat(player, $"{parts[1]} = {v}");
        }

        private static async void Logs(ExtPlayer player, int uuid)
        {
            try
            {
                var lines = await AuditLog.Read(uuid, 15);
                NAPI.Task.Run(() =>
                {
                    CryptoCommands.Chat(player, uuid > 0 ? $"Журнал по UUID {uuid}:" : "Журнал Чёрного рынка:");
                    foreach (var line in lines)
                        CryptoCommands.Chat(player, line);
                });
            }
            catch (Exception e)
            {
                BlackMarketCore.Log.Write($"Logs Exception: {e}");
            }
        }

        /// <summary>droppoint add — добавить точку закладки там, где стоит админ (на улице, на земле).</summary>
        private static void DropPoint(ExtPlayer player, string[] parts)
        {
            var config = BlackMarketConfig.Current;
            if (parts.Length > 1 && parts[1] == "add")
            {
                if (!CryptoCommands.CanManage(player)) return;
                config.DropPoints.Add(player.Position);
                BlackMarketConfig.Save();
                GameLog.Admin(player.Name, "bm droppoint add", "");
                CryptoCommands.Chat(player, $"Точка добавлена, всего {config.DropPoints.Count}");
                return;
            }
            CryptoCommands.Chat(player, $"Точек закладок: {config.DropPoints.Count}. /bm droppoint add — добавить текущую позицию");
        }
    }
}
