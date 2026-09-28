using System;
using System.Collections.Generic;
using GTANetworkAPI;
using NeptuneEvo.Character;
using NeptuneEvo.Character.Models;
using NeptuneEvo.Core;
using NeptuneEvo.Fractions;
using NeptuneEvo.Fractions.Models;
using NeptuneEvo.Fractions.Player;
using NeptuneEvo.Handles;
using NeptuneEvo.Organizations.Player;
using Redage.SDK;

namespace NeptuneEvo.Crime
{
    /// <summary>
    /// Общее для криминальных механик (ограбление домов, трава, угон): кто считается криминалом,
    /// «ночь», маска, вызов полиции с меткой и звёзды розыска.
    /// </summary>
    public static class CrimeCore
    {
        public static readonly object Sync = new object();
        public static readonly nLog Log = new nLog("Crime");
        public static readonly Random Rnd = new Random();

        /// <summary>Банды, байкеры, мафия или организация с криминальными возможностями.</summary>
        public static bool IsCriminal(ExtPlayer player) =>
            IsCriminalFraction(player) || (player.GetOrganizationData()?.CrimeOptions ?? false);

        /// <summary>Член государственной криминальной фракции (банда, байкеры, мафия).</summary>
        public static bool IsCriminalFraction(ExtPlayer player)
        {
            var fraction = player.GetFractionMemberData();
            return fraction != null && Fractions.Manager.FractionTypes.TryGetValue(fraction.Id, out var type)
                && (type == FractionsType.Gangs || type == FractionsType.Bikers || type == FractionsType.Mafia);
        }

        /// <summary>
        /// Денежный коэффициент криминальных заработков (скупка, покупатели травы, наличные при ограблении).
        /// Фракционные банды получают обычную цену — для игрока это объясняется долей «в общак банды»
        /// (реально никуда не переводится); криминальные организации работают «без общака» и получают больше.
        /// </summary>
        public const double OrgPayoutFactor = 1.25;
        public const int GangCommonFundPercent = 20;

        public static double PayoutFactor(ExtPlayer player) =>
            IsCriminalFraction(player) ? 1.0 : IsCriminal(player) ? OrgPayoutFactor : 1.0;

        public static int Payout(ExtPlayer player, long amount) => (int)Math.Round(amount * PayoutFactor(player));

        /// <summary>Хвост к уведомлению о выплате: « (20% — в общак банды)» для фракционных банд.</summary>
        public static string FundSuffix(ExtPlayer player) =>
            IsCriminalFraction(player) ? $" ({GangCommonFundPercent}% ушло в общак банды)" : "";

        /// <summary>Пояснение к цене для игрока.</summary>
        public static string PayoutNote(ExtPlayer player) =>
            IsCriminalFraction(player)
                ? $"{GangCommonFundPercent}% с каждой сделки уходит в общак банды"
                : IsCriminal(player) ? $"Организация работает без общака банды — выплаты на {(int)Math.Round((OrgPayoutFactor - 1) * 100)}% выше" : "";

        /// <summary>Игровой час: время сервера или замороженное администратором.</summary>
        public static int Hour => Admin.TimeChanged ? Admin.SetTime[0] : DateTime.Now.Hour;

        public static bool IsNight => Hour >= 22 || Hour < 6;

        public static bool HasMask(ExtPlayer player) =>
            player.HasSharedData("IS_MASK") && player.GetSharedData<bool>("IS_MASK");

        public static bool IsPolice(ExtPlayer player)
        {
            var fraction = player.GetFractionMemberData();
            return fraction != null && Configs.IsFractionPolic(fraction.Id);
        }

        /// <summary>Добавить звёзды розыска (не больше 6).</summary>
        public static void AddWanted(ExtPlayer player, int stars, string reason)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null || stars <= 0)
                return;
            var level = Math.Min(6, (characterData.WantedLVL?.Level ?? 0) + stars);
            Police.setPlayerWantedLevel(player, new WantedLevel(level, "Полиция", DateTime.Now, reason));
        }

        private static readonly Dictionary<string, ExtBlip> Blips = new Dictionary<string, ExtBlip>();

        /// <summary>
        /// Вызов полиции: сообщение [F] полиции, шерифам и FIB, метка на карте для силовиков на 5 минут,
        /// и всегда звёзды розыска виновнику.
        /// </summary>
        public static void CallPolice(ExtPlayer player, Vector3 position, string key, string text, int stars, string reason)
        {
            try
            {
                var message = "!{#F08080}[F] " + text;
                Fractions.Manager.sendFractionMessage((int)Fractions.Models.Fractions.POLICE, message, true);
                Fractions.Manager.sendFractionMessage((int)Fractions.Models.Fractions.SHERIFF, message, true);
                Fractions.Manager.sendFractionMessage((int)Fractions.Models.Fractions.FIB, message, true);

                if (Blips.TryGetValue(key, out var old) && old != null && old.Exists)
                    old.Delete();
                var blip = (ExtBlip)NAPI.Blip.CreateBlip(161, position, 1.2f, 1, reason, 0, 0, true, 0, 0);
                blip.Transparency = 0;
                Blips[key] = blip;
                foreach (var foreachPlayer in Character.Repository.GetPlayers())
                {
                    if (IsPolice(foreachPlayer))
                        Trigger.ClientEvent(foreachPlayer, "changeBlipAlpha", blip, 255);
                }
                NAPI.Task.Run(() =>
                {
                    if (Blips.TryGetValue(key, out var current) && current == blip)
                    {
                        if (blip.Exists) blip.Delete();
                        Blips.Remove(key);
                    }
                }, 5 * 60 * 1000);

                if (player != null && player.IsCharacterData())
                {
                    AddWanted(player, stars, reason);
                    Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "Кто-то вызвал полицию!", 4000);
                }
            }
            catch (Exception e)
            {
                Log.Write($"CallPolice Exception: {e}");
            }
        }

        public static bool Roll(int percent) => Rnd.Next(100) < percent;
    }
}
