using System;
using GTANetworkAPI;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Fractions.Models;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Handles;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Crime
{
    /// <summary>
    /// Вкладка «Криминал» в меню фракции/организации (CEF fractions/elements/crime): как и где брать задания,
    /// что нужно с собой, куда сбывать, плюс метки GPS (Carter Scott, Мавр, ближайшая поляна, покупатель травы).
    /// </summary>
    public class CrimeGuide : Script
    {
        [RemoteEvent("server.crime.guide.load")]
        public static void OnLoad(ExtPlayer player)
        {
            try
            {
                if (!player.IsCharacterData())
                    return;
                var allowed = CrimeCore.IsCriminal(player);
                object data;
                if (!allowed)
                    data = new { allowed = false };
                else
                {
                    var fracId = player.GetFractionId();
                    var isGang = Fractions.Manager.FractionTypes.TryGetValue(fracId, out var type) && type == FractionsType.Gangs;
                    data = new
                    {
                        allowed = true,
                        isGang,
                        isFraction = CrimeCore.IsCriminalFraction(player),
                        payoutNote = CrimeCore.PayoutNote(player),
                        fundPercent = CrimeCore.GangCommonFundPercent,
                        orgBonus = (int)Math.Round((CrimeCore.OrgPayoutFactor - 1) * 100),
                        burglary = new
                        {
                            picks = LockBreak.CountPicks(player),
                            mask = CrimeCore.HasMask(player),
                            cooldown = Burglary.BurglaryManager.CooldownMinutes(player.GetUUID()),
                            lockpickPrice = Main.BlackMarketLockPick,
                        },
                        theft = new { cooldown = CarTheft.CarTheftManager.CooldownLeft(player) },
                        weed = Weed.WeedManager.GuideInfo(player),
                        fence = BlackMarket.Fence.FenceManager.View(player),
                    };
                    Weed.WeedManager.SendSpots(player);
                }
                Trigger.ClientEvent(player, "client.crime.guide.data", JsonConvert.SerializeObject(data));
            }
            catch (Exception e)
            {
                CrimeCore.Log.Write($"CrimeGuide OnLoad Exception: {e}");
            }
        }

        [RemoteEvent("server.crime.guide.gps")]
        public static void OnGps(ExtPlayer player, string target)
        {
            try
            {
                if (!player.IsCharacterData() || !CrimeCore.IsCriminal(player))
                    return;
                Vector3 point = null;
                string text = null;
                switch (target)
                {
                    case "mavr":
                        point = BlackMarket.Config.BlackMarketConfig.Current.CashoutPoint;
                        text = "Мавр отмечен на карте";
                        break;
                    case "carter":
                        point = Fractions.CarDelivery.GangStartDelivery;
                        text = "Carter Scott (задания банды) отмечен на карте";
                        break;
                    case "glade":
                        point = Weed.WeedManager.NearestFreeSpot(player.Position);
                        text = point == null ? "Все поляны сейчас заняты" : "Ближайшая свободная поляна отмечена на карте";
                        break;
                    case "buyer":
                        point = Weed.WeedManager.NearestBuyer(player.Position);
                        text = point == null ? "Покупателей нет" : "Ближайший покупатель травы отмечен на карте";
                        break;
                    case "chop":
                        point = BlackMarket.Config.BlackMarketConfig.Current.ChopPoint ?? BlackMarket.Config.BlackMarketConfig.Current.CashoutPoint;
                        text = "Разборка отмечена на карте";
                        break;
                    default:
                        return;
                }
                if (point != null)
                    Trigger.ClientEvent(player, "createWaypoint", point.X, point.Y);
                Notify.Send(player, point != null ? NotifyType.Success : NotifyType.Error, NotifyPosition.BottomCenter, text, 3000);
            }
            catch (Exception e)
            {
                CrimeCore.Log.Write($"CrimeGuide OnGps Exception: {e}");
            }
        }
    }
}
