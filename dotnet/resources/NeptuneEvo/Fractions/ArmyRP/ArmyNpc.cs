using System;
using GTANetworkAPI;
using NeptuneEvo.Character;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using NeptuneEvo.Quests;
using NeptuneEvo.Quests.Models;
using Redage.SDK;

namespace NeptuneEvo.Fractions.ArmyRP
{
    /// <summary>
    /// Кэтрин Келлерман на стойке штаба армии — стандартный диалог квестов (CEF json/quests/fraction/npc_army.json).
    /// Стартовый узел зависит от игрока: 0 — гражданский, 1 — рядовой состав (устав, нормы поведения), 2 — офицеры (командование).
    /// </summary>
    class ArmyNpc : Script
    {
        private static readonly nLog Log = new nLog("ArmyNpc");
        public const string QuestName = "npc_army";

        public static void Create()
        {
            PedSystem.Repository.CreateQuest("s_f_y_ranger_01", new Vector3(-2348.598, 3210.923, 29.224812), 146.52292f,
                questName: QuestName, colShapeEnums: ColShapeEnums.ArmyNpc, title: "~y~NPC~w~ Кэтрин Келлерман\nДежурная штаба");
        }

        [Interaction(ColShapeEnums.ArmyNpc)]
        public static void Open(ExtPlayer player, int index)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null)
                    return;
                if (sessionData.CuffedData.Cuffed || sessionData.DeathData.InDeath)
                    return;

                var node = !ArmyUtil.IsArmy(player) ? 0 : ArmyUtil.IsOfficer(player) ? 2 : 1;
                player.SelectQuest(new PlayerQuestModel(QuestName, 0, 0, false, DateTime.Now));
                Trigger.ClientEvent(player, "client.quest.open", index, QuestName, node, 0, 0);
            }
            catch (Exception e)
            {
                Log.Write($"Open Exception: {e}");
            }
        }
    }
}
