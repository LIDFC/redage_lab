using System;
using GTANetworkAPI;
using NeptuneEvo.Handles;
using NeptuneEvo.Character;
using NeptuneEvo.Players;
using Redage.SDK;
using Newtonsoft.Json.Linq;
using PhoneBusiness = NeptuneEvo.Players.Phone.Property.Businesses.Repository;

namespace NeptuneEvo.Businesses.Tablet
{
    public class Events : Script
    {
        [RemoteEvent("server.tablet.business.load")]
        public void OnLoad(ExtPlayer player, int bizId, int days)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null)
                return;
            // Без бизнеса приложение показывает заглушку
            if (characterData.BizIDs.Count == 0)
            {
                Trigger.ClientEvent(player, "client.tablet.business.data", "null");
                return;
            }
            // -1 — последний выбранный бизнес, иначе первый
            if (bizId == -1)
            {
                var selected = player.GetSessionData()?.SelectData.SelectedBiz ?? -1;
                bizId = characterData.BizIDs.Contains(selected) ? selected : characterData.BizIDs[0];
            }
            var biz = Repository.GetOwned(player, bizId);
            if (biz == null)
                return;
            Repository.SendData(player, biz);
            Repository.SendHistory(player, biz.ID, days);
        }

        [RemoteEvent("server.tablet.business.history")]
        public void OnHistory(ExtPlayer player, int days)
        {
            var biz = Repository.GetOwned(player);
            if (biz != null)
                Repository.SendHistory(player, biz.ID, days);
        }

        /// <summary>
        /// Действия владельца. Заказы/цены/продажа — существующие методы телефона (работают по SelectedBiz,
        /// который выставил GetOwned), после действия планшет получает свежие данные.
        /// </summary>
        [RemoteEvent("server.tablet.business.action")]
        public void OnAction(ExtPlayer player, string action, string json)
        {
            try
            {
                var biz = Repository.GetOwned(player);
                if (biz == null)
                    return;

                JObject args;
                try { args = string.IsNullOrEmpty(json) ? new JObject() : JObject.Parse(json); }
                catch { return; }

                int Int(string key) { try { return (int)Math.Clamp(args[key]?.Value<double>() ?? 0, int.MinValue, int.MaxValue); } catch { return 0; } }
                string Str(string key) => args[key]?.Value<string>() ?? "";

                switch (action)
                {
                    case "withdraw":
                        Repository.Withdraw(player, biz);
                        break;
                    case "payTax":
                        Repository.PayTax(player, biz, Int("amount"));
                        break;
                    case "extraCharge":
                        PhoneBusiness.ExtraCharge(player, Str("name"), Int("value"));
                        break;
                    case "addOrder":
                        PhoneBusiness.AddOrder(player, Str("name"), Int("value"));
                        break;
                    case "cancelOrder":
                        PhoneBusiness.CancelOrder(player, Int("uid"));
                        break;
                    case "maxProducts":
                        PhoneBusiness.MaxProducts(player);
                        return;
                    case "sell":
                        PhoneBusiness.OnSell(player);
                        return;
                    case "gps":
                        Trigger.ClientEvent(player, "createWaypoint", biz.EnterPoint.X, biz.EnterPoint.Y);
                        return;
                    default:
                        return;
                }
                Repository.SendData(player, biz);
            }
            catch (Exception e)
            {
                new Redage.SDK.nLog("Businesses.Tablet").Write($"OnAction({action}) Exception: {e}");
            }
        }
    }
}
