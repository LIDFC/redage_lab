using GTANetworkAPI;
using NeptuneEvo.BlackMarket.Audit;
using NeptuneEvo.BlackMarket.Crypto;
using NeptuneEvo.BlackMarket.History;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using Redage.SDK;
using System;

namespace NeptuneEvo.BlackMarket.Admin
{
    /// <summary>
    /// Администрирование крипты. Просмотр — уровень из CommandsAccess (5+), изменение балансов — 8+.
    ///  /crypto get|give|take &lt;id|uNNN&gt; [сумма] — личный кошелёк (id — игрок онлайн, uNNN — UUID, работает и офлайн)
    ///  /fcrypto get|give|take &lt;fractionId&gt; [сумма] — кошелёк криминальной фракции
    /// </summary>
    class CryptoCommands : Script
    {
        public const int ManageLevel = 8;

        [Command(AdminCommands.crypto, GreedyArg = true)]
        public static void CMD_Crypto(ExtPlayer player, string args)
        {
            if (!CommandsAccess.CanUseCmd(player, AdminCommands.crypto))
                return;

            var parts = (args ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                Chat(player, "/crypto get|give|take <id|uUUID> [сумма]");
                return;
            }

            var uuid = ResolveUuid(parts[1]);
            if (uuid <= 0 || !Main.PlayerNames.TryGetValue(uuid, out var name))
            {
                Chat(player, "Игрок не найден (id онлайн или u<UUID>)");
                return;
            }

            var action = parts[0].ToLower();
            if (action == "get")
            {
                var wallet = CryptoWallets.Get(uuid);
                Chat(player, $"Крипта {name} (UUID {uuid}): {BlackMarketCore.Btc(wallet.Balance)}, в P2P-заявках {BlackMarketCore.Btc(wallet.Reserved)}");
                return;
            }

            if (action != "give" && action != "take")
            {
                Chat(player, "/crypto get|give|take <id|uUUID> [сумма]");
                return;
            }
            if (!CanManage(player) || !ParseAmount(player, parts, 2, out var amount))
                return;

            var delta = action == "give" ? amount : -amount;
            if (!CryptoWallets.ChangePersonal(uuid, delta))
            {
                Chat(player, $"Недостаточно свободных BTC у {name}");
                return;
            }

            HistoryLog.Add(uuid, "admin", action == "give" ? "Зачисление" : "Списание", delta);
            AuditLog.Write(action == "give" ? "admin_give" : "admin_take", player.GetUUID(), uuid, amount: delta, source: Source.Admin);
            GameLog.Admin(player.Name, $"crypto {action} {amount}", name);
            Chat(player, $"{name}: {(delta > 0 ? "+" : "")}{BlackMarketCore.Btc(delta)}, баланс {BlackMarketCore.Btc(CryptoWallets.Get(uuid).Balance)}");
        }

        [Command(AdminCommands.fcrypto, GreedyArg = true)]
        public static void CMD_FractionCrypto(ExtPlayer player, string args)
        {
            if (!CommandsAccess.CanUseCmd(player, AdminCommands.fcrypto))
                return;

            var parts = (args ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var fractionId))
            {
                Chat(player, "/fcrypto get|give|take <fractionId> [сумма]");
                return;
            }
            if (!BlackMarketCore.IsCriminalFraction(fractionId))
            {
                Chat(player, "Крипто-кошелёк есть только у банд, мафий и байкеров");
                return;
            }

            var fraction = BlackMarketCore.FractionName(fractionId);
            var action = parts[0].ToLower();
            if (action == "get")
            {
                Chat(player, $"Крипта {fraction}: {BlackMarketCore.Btc(CryptoWallets.GetFraction(fractionId))}");
                return;
            }
            if (action != "give" && action != "take")
            {
                Chat(player, "/fcrypto get|give|take <fractionId> [сумма]");
                return;
            }
            if (!CanManage(player) || !ParseAmount(player, parts, 2, out var amount))
                return;

            var delta = action == "give" ? amount : -amount;
            if (!CryptoWallets.ChangeFraction(fractionId, delta))
            {
                Chat(player, $"Недостаточно BTC у {fraction}");
                return;
            }

            HistoryLog.Add(0, "admin", action == "give" ? "Зачисление" : "Списание", delta, fractionId: fractionId);
            AuditLog.Write(action == "give" ? "admin_fgive" : "admin_ftake", player.GetUUID(), amount: delta, source: Source.Admin,
                details: new { fractionId });
            GameLog.Admin(player.Name, $"fcrypto {action} {amount} frac {fractionId}", fraction);
            Chat(player, $"{fraction}: {(delta > 0 ? "+" : "")}{BlackMarketCore.Btc(delta)}, баланс {BlackMarketCore.Btc(CryptoWallets.GetFraction(fractionId))}");
        }

        public static bool CanManage(ExtPlayer player)
        {
            var characterData = player.GetCharacterData();
            if (characterData != null && characterData.AdminLVL >= ManageLevel)
                return true;
            Chat(player, $"Изменение доступно с {ManageLevel} уровня администратора");
            return false;
        }

        /// <summary>«12» — игрок онлайн с этим id, «u1234» — UUID персонажа (онлайн или офлайн).</summary>
        public static int ResolveUuid(string target)
        {
            if (string.IsNullOrEmpty(target))
                return 0;
            if ((target[0] == 'u' || target[0] == 'U') && int.TryParse(target.Substring(1), out var uuid))
                return uuid;
            if (int.TryParse(target, out var id))
            {
                var target_ = Main.GetPlayerByID(id);
                return target_ != null && target_.IsCharacterData() ? target_.GetUUID() : 0;
            }
            return 0;
        }

        private static bool ParseAmount(ExtPlayer player, string[] parts, int index, out long amount)
        {
            amount = 0;
            if (parts.Length <= index || !long.TryParse(parts[index], out amount) || amount <= 0 || amount > 1_000_000_000_000)
            {
                Chat(player, "Укажите сумму больше нуля");
                return false;
            }
            return true;
        }

        public static void Chat(ExtPlayer player, string text) =>
            Trigger.SendChatMessage(player, $"!{{#fc0}}[ЧР] !{{#fff}}{text}");
    }
}
