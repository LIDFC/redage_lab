using System;
using NeptuneEvo.Players.Phone.Messages.Models;
using Redage.SDK;

namespace NeptuneEvo.MoneySystem
{
    /// <summary>
    /// Напоминание об оплате налога: после ежечасного списания считаем, на сколько часов хватит счёта
    /// недвижимости, и на порогах 24 / 12 / 3 / 1 ч шлём SMS от банка (и уведомление, если игрок в игре).
    /// Раньше игроки узнавали о долге, только когда имущество уже забрали.
    /// </summary>
    public static class TaxReminder
    {
        private static readonly long[] Thresholds = { 24, 12, 3, 1 };

        public static void Check(string owner, string what, long balance, int tax)
        {
            try
            {
                if (tax <= 0 || balance < 0 || string.IsNullOrEmpty(owner) || !Main.PlayerUUIDs.TryGetValue(owner, out var uuid))
                    return;
                var hours = balance / tax;
                if (Array.IndexOf(Thresholds, hours) == -1)
                    return;
                var text = $"Налог на {what} оплачен ещё на {hours} ч. Если счёт закончится, недвижимость заберут. " +
                           "Пополнить: Fleeca в телефоне → «Налоги» или любой банкомат.";
                Players.Phone.Messages.Repository.AddSystemMessageToUuid(uuid, (int)DefaultNumber.Bank, text, DateTime.Now);
                var player = Main.GetPlayerByUUID(uuid);
                if (player != null)
                    Notify.Send(player, hours <= 3 ? NotifyType.Error : NotifyType.Warning, NotifyPosition.BottomCenter,
                        $"Налог на {what}: осталось {hours} ч. Пополните счёт в Fleeca или банкомате", 8000);
            }
            catch (Exception e)
            {
                Debugs.Repository.Exception(e);
            }
        }
    }
}
