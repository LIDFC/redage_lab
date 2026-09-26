using System;
using System.Collections.Generic;
using GTANetworkAPI;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Jobs.Models;
using NeptuneEvo.Players;
using NeptuneEvo.Quests.Models;
using Redage.SDK;

namespace NeptuneEvo.Jobs
{
    /// <summary>
    /// NPC-работодатели на рабочих базах. Их диалоги лежат в src_cef/src/json/quests/work/npc_*.json:
    ///  - «Устроиться на работу» (perform) — WorkManager.JobJoin с обычными проверками уровня и прав;
    ///  - второе действие (action) — меню аренды рабочего транспорта этой базы,
    ///    у электрика — начало/конец смены (транспорт ему не нужен).
    /// На базах с арендой NPC создаёт Rentcar (colshape RentCar, аренда берёт точку спавна оттуда),
    /// электрика создаём здесь.
    /// </summary>
    public class JobEmployers : Script
    {
        private static readonly nLog Log = new nLog("Jobs.JobEmployers");

        public class EmployerData
        {
            public string Actor;
            public JobsId Job;
            public string Name;
            public string Skin;

            public EmployerData(string actor, JobsId job, string name, string skin)
            {
                Actor = actor;
                Job = job;
                Name = name;
                Skin = skin;
            }
        }

        // Имена совпадают с actorData в src_cef/src/json/quests/quests.js
        public static readonly IReadOnlyDictionary<RentCarId, EmployerData> ByRentZone = new Dictionary<RentCarId, EmployerData>
        {
            { RentCarId.JobTaxi,      new EmployerData("npc_taxi",         JobsId.Taxi,          "Директор таксопарка",     "a_m_m_business_01") },
            { RentCarId.JobBus,       new EmployerData("npc_bus",          JobsId.Bus,           "Начальник автоколонны",   "s_m_m_gentransport") },
            { RentCarId.JobLawnmower, new EmployerData("npc_lawnmower",    JobsId.Lawnmower,     "Главный газонокосильщик", "s_m_m_gardener_01") },
            { RentCarId.JobTrucker,   new EmployerData("npc_truckers",     JobsId.Trucker,       "Водитель-дальнобойщик",   "s_m_m_trucker_01") },
            { RentCarId.JobCollector, new EmployerData("npc_collector",    JobsId.CashCollector, "Банковский HR",           "s_m_m_armoured_01") },
            { RentCarId.JobMechanic,  new EmployerData("npc_automechanic", JobsId.CarMechanic,   "Главный механик",         "s_m_y_xmech_01") },
            { RentCarId.JobPostman,   new EmployerData("npc_gopostal",     JobsId.Postman,       "Старший почтальон",       "s_m_m_postal_01") },
        };

        public static readonly EmployerData Electrician = new EmployerData("npc_electrician", JobsId.Electrician, "Прораб", "s_m_y_construct_01");

        private static readonly Dictionary<string, EmployerData> ByActor = new Dictionary<string, EmployerData>();

        static JobEmployers()
        {
            foreach (var employer in ByRentZone.Values)
                ByActor[employer.Actor] = employer;
            ByActor[Electrician.Actor] = Electrician;
        }

        public static string GetTitle(EmployerData employer) =>
            $"~y~NPC~w~ {employer.Name}\nРабота: {WorkManager.JobList[(int)employer.Job]}";

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            try
            {
                // Позиция прораба — settings/electrician.json (/elecforeman)
                PedSystem.Repository.CreateQuest(Electrician.Skin, Jobs.Electrician.ForemanPosition, Jobs.Electrician.ForemanHeading,
                    questName: Electrician.Actor, title: GetTitle(Electrician), colShapeEnums: ColShapeEnums.JobEmployer, isBlipVisible: false);
            }
            catch (Exception e)
            {
                Log.Write($"OnResourceStart Exception: {e}");
            }
        }

        [Interaction(ColShapeEnums.JobEmployer)]
        public static void OnElectricianEmployer(ExtPlayer player, int index)
        {
            OpenDialog(player, index, Electrician.Actor);
        }

        /// <summary>
        /// Открыть диалог работодателя. pedIndex — ped.Value (Index колшейпа NPC).
        /// </summary>
        public static void OpenDialog(ExtPlayer player, int pedIndex, string actor)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null || !player.IsCharacterData())
                    return;

                if (sessionData.CuffedData.Cuffed || sessionData.DeathData.InDeath)
                    return;

                player.SelectQuest(new PlayerQuestModel(actor, 0, 0, false, DateTime.Now));
                Trigger.ClientEvent(player, "client.quest.open", pedIndex, actor, 0, 0, 0);
            }
            catch (Exception e)
            {
                Log.Write($"OpenDialog Exception: {e}");
            }
        }

        /// <summary>
        /// «Устроиться на работу». Возвращает true, если actor — работодатель.
        /// </summary>
        public static bool TryPerform(ExtPlayer player, string actor)
        {
            if (actor == null || !ByActor.TryGetValue(actor, out var employer))
                return false;

            var characterData = player.GetCharacterData();
            if (characterData == null)
                return true;

            if (characterData.WorkID == (int)employer.Job)
            {
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter,
                    $"Вы уже работаете здесь: {WorkManager.JobList[(int)employer.Job]}", 3000);
                return true;
            }

            WorkManager.JobJoin(player, (int)employer.Job);
            return true;
        }

        /// <summary>
        /// Второе действие диалога: аренда рабочего транспорта (или смена электрика).
        /// </summary>
        public static bool TryAction(ExtPlayer player, string actor)
        {
            if (actor == null || !ByActor.TryGetValue(actor, out var employer))
                return false;

            if (employer.Job == JobsId.Electrician)
                Jobs.Electrician.OnElectrician(player);
            else
                Rentcar.OpenRentMenuInZone(player);

            return true;
        }
    }
}
