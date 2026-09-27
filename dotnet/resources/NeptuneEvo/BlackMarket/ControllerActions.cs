using GTANetworkAPI;
using NeptuneEvo.BlackMarket.Crypto;
using NeptuneEvo.BlackMarket.Deliveries;
using NeptuneEvo.BlackMarket.History;
using NeptuneEvo.BlackMarket.Methods;
using NeptuneEvo.BlackMarket.Models;
using NeptuneEvo.BlackMarket.P2P;
using NeptuneEvo.BlackMarket.Vpn;
using NeptuneEvo.Character;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Handles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Redage.SDK;
using System;
using System.Linq;

namespace NeptuneEvo.BlackMarket
{
    public partial class Controller
    {
        /// <summary>Переключатель VPN в настройках телефона. Состояние не сохраняется между сессиями.</summary>
        [RemoteEvent("server.blackmarket.vpn")]
        public static void OnVpn(ExtPlayer player)
        {
            if (!player.IsCharacterData() || !BlackMarketCore.AntiSpam(player, 800))
                return;
            var on = VpnState.Toggle(player);
            Trigger.ClientEvent(player, "client.blackmarket.vpn", on);
            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, on ? "VPN включён" : "VPN выключен", 2500);
        }

        [RemoteEvent("server.blackmarket.open")]
        public static void OnOpen(ExtPlayer player)
        {
            if (!player.IsCharacterData() || !BlackMarketManager.Ready)
                return;
            if (!VpnState.IsOn(player))
            {
                Trigger.ClientEvent(player, "client.blackmarket.vpn", false);
                return;
            }
            Trigger.ClientEvent(player, "client.blackmarket.open", JsonConvert.SerializeObject(BlackMarketView.Build(player)));
        }

        /// <summary>
        /// Все действия приложения. Из CEF приходят только идентификаторы и введённые числа —
        /// владелец, цена лота, баланс, фракция, ранг, комиссия и координаты определяются здесь.
        /// </summary>
        [RemoteEvent("server.blackmarket.action")]
        public static void OnAction(ExtPlayer player, string action, string json)
        {
            if (!player.IsCharacterData() || !BlackMarketManager.Ready)
                return;
            if (!VpnState.IsOn(player))
            {
                Trigger.ClientEvent(player, "client.blackmarket.vpn", false);
                return;
            }
            if (!BlackMarketCore.AntiSpam(player))
                return;

            JObject args;
            try
            {
                args = string.IsNullOrEmpty(json) ? new JObject() : JObject.Parse(json);
            }
            catch
            {
                return;
            }

            try
            {
                OpResult result;
                switch (action)
                {
                    case "refresh":
                        result = null;
                        break;
                    case "createLot":
                        result = Lots.Create(player, Int(args, "itemId"), Int(args, "count"), Long(args, "price"), Int(args, "hours"));
                        break;
                    case "editLot":
                        result = Lots.EditPrice(player, Int(args, "id"), Long(args, "price"));
                        break;
                    case "cancelLot":
                        result = Lots.Cancel(player, Int(args, "id"));
                        break;
                    case "buy":
                        result = Lots.Buy(player, Int(args, "id"), Int(args, "count"), (string)args["source"] == "fraction", out _);
                        break;
                    case "p2pCreate":
                        result = P2PManager.Create(player, Long(args, "amount"), Dec(args, "price"), Int(args, "hours"));
                        break;
                    case "p2pCancel":
                        result = P2PManager.Cancel(player, Int(args, "id"));
                        break;
                    case "p2pBuy":
                        result = P2PManager.Buy(player, Int(args, "id"), Long(args, "amount"));
                        break;
                    case "transfer":
                        result = CryptoOps.Transfer(player, Int(args, "phone"), Long(args, "amount"));
                        break;
                    case "fDeposit":
                        result = CryptoOps.FractionDeposit(player, Long(args, "amount"));
                        break;
                    case "fWithdraw":
                        result = CryptoOps.FractionWithdraw(player, Long(args, "amount"));
                        break;
                    case "exchange":
                        result = CryptoOps.Exchange(player, Long(args, "btc"));
                        break;
                    case "launder":
                        result = CashOut.Launder(player);
                        break;
                    case "cashout":
                        result = CashOut.Cashout(player, Long(args, "btc"));
                        break;
                    case "cashoutGps":
                        {
                            var point = Config.BlackMarketConfig.Current.CashoutPoint;
                            Trigger.ClientEvent(player, "createWaypoint", point.X, point.Y);
                            result = OpResult.Success("Мавр отмечен на карте");
                        }
                        break;
                    case "gps":
                        {
                            var drop = DropManager.ForBuyer(player.GetUUID()).FirstOrDefault(d => d.Id == Int(args, "id"));
                            if (drop != null)
                                DropManager.ShowGps(player, drop);
                            result = drop != null ? OpResult.Success("Метка поставлена") : OpResult.Fail("Заказ не найден");
                        }
                        break;
                    case "history":
                        SendHistory(player, (string)args["scope"] == "fraction");
                        return;
                    default:
                        return;
                }

                if (result != null)
                    Notify.Send(player, result.Ok ? NotifyType.Success : NotifyType.Error, NotifyPosition.BottomCenter, result.Message, 4000);
                Trigger.ClientEvent(player, "client.blackmarket.result", action, result?.Ok ?? true, result?.Message ?? "",
                    JsonConvert.SerializeObject(BlackMarketView.Build(player)));
            }
            catch (Exception e)
            {
                BlackMarketCore.Log.Write($"OnAction({action}) Exception: {e}");
            }
        }

        private static async void SendHistory(ExtPlayer player, bool fraction)
        {
            try
            {
                var fractionId = player.GetFractionId();
                var list = fraction
                    ? (BlackMarketCore.IsCriminalFraction(fractionId) ? await HistoryLog.GetFraction(fractionId) : new System.Collections.Generic.List<HistoryEntry>())
                    : await HistoryLog.GetPlayer(player.GetUUID());
                var data = JsonConvert.SerializeObject(list);
                NAPI.Task.Run(() =>
                {
                    if (player.IsCharacterData())
                        Trigger.ClientEvent(player, "client.blackmarket.history", fraction ? "fraction" : "me", data);
                });
            }
            catch (Exception e)
            {
                BlackMarketCore.Log.Write($"SendHistory Exception: {e}");
            }
        }

        private static int Int(JObject args, string key)
        {
            var token = args[key];
            if (token == null) return 0;
            try { return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, token.Value<double>())); } catch { return 0; }
        }

        private static long Long(JObject args, string key)
        {
            var token = args[key];
            if (token == null) return 0;
            try { return (long)Math.Max(long.MinValue, Math.Min(long.MaxValue, token.Value<double>())); } catch { return 0; }
        }

        private static decimal Dec(JObject args, string key)
        {
            var token = args[key];
            if (token == null) return 0;
            try { return token.Value<decimal>(); } catch { return 0; }
        }
    }
}
