using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using GTANetworkAPI;
using Localization;
using NeptuneEvo.Accounts;
using NeptuneEvo.Character;
using NeptuneEvo.Functions;
using NeptuneEvo.GUI;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using NeptuneEvo.Players.Models;
using NeptuneEvo.Players.Popup.List.Models;
using NeptuneEvo.Quests;
using NeptuneEvo.VehicleData.LocalData;
using NeptuneEvo.VehicleData.LocalData.Models;
using Redage.SDK;

namespace NeptuneEvo.Core
{
    class DrivingSchool : Script
    {
        private static readonly nLog Log = new nLog("Core.DrivingSchool");

        private static readonly Vector3 EnterSchool = new Vector3(435.8382, -984.11847, 30.689484);

        private static readonly Vector3[] StartCourseCoord =
        {
            new Vector3(424.9018, -1026.2645, 28.815866),
            new Vector3(421.59225, -1026.5282, 28.834274),
            new Vector3(417.99475, -1027.1974, 28.91486),
        };

        private static readonly Vector3[] StartCourseRot =
        {
            new Vector3(0, 0, 2.5),
            new Vector3(0, 0, 2.5),
            new Vector3(0, 0, 2.5),
        };

        // Старый район движения (Mission Row), чтобы маршрут оставался компактным и безопасным.
        private static readonly List<Vector3> DrivingCoords = new List<Vector3>
        {
            new Vector3(374.2735, -1038.3071, 29.276443),
            new Vector3(255.90155, -1037.9536, 29.271255),
            new Vector3(279.44644, -882.29755, 29.221624),
            new Vector3(201.3517, -827.9175, 30.957222),
            new Vector3(111.67548, -970.1769, 29.450388),
            new Vector3(55.705387, -1241.544, 29.375198),
            new Vector3(129.47919, -1381.1102, 29.371664),
            new Vector3(218.17566, -1150.559, 29.36021),
            new Vector3(401.44742, -1112.798, 29.427078),
            new Vector3(406.66547, -1029.0198, 29.380238),
        };

        // Вопросы теории. Ответы на все есть во вкладке «Теория» окна автошколы
        // (src_cef/src/views/player/drivingschool/theory.js) — держите их согласованными.
        private static readonly List<TheoryQuestion> TheoryQuestions = new List<TheoryQuestion>
        {
            new TheoryQuestion("Какой сигнал светофора запрещает движение?", new [] { "Красный", "Зелёный", "Мигающий жёлтый" }, 0),
            new TheoryQuestion("Что означает мигающий жёлтый сигнал?", new [] { "Движение запрещено", "Перекрёсток нерегулируемый, проезжайте с осторожностью", "Нужно развернуться" }, 1),
            new TheoryQuestion("Кому вы обязаны уступить дорогу?", new [] { "Транспорту с включёнными мигалками и сиреной", "Пешеходам на переходе", "И тем, и другим" }, 2),
            new TheoryQuestion("Можно ли ехать по встречной полосе, чтобы объехать пробку?", new [] { "Да, если никого нет", "Нет", "Только ночью" }, 1),
            new TheoryQuestion("Что нужно сделать перед началом движения?", new [] { "Убедиться, что никому не мешаете, и включить поворотник", "Сразу нажать газ", "Посигналить и ехать" }, 0),
            new TheoryQuestion("Можно ли управлять транспортом в нетрезвом виде?", new [] { "Можно на малой скорости", "Нет, запрещено", "Можно за городом" }, 1),
            new TheoryQuestion("Максимальная скорость на экзамене (легковой, мотоцикл)?", new [] { "60 км/ч", "80 км/ч", "120 км/ч" }, 1),
            new TheoryQuestion("Максимальная скорость на экзамене на грузовике (категория C)?", new [] { "70 км/ч", "90 км/ч", "Без ограничений" }, 0),
            new TheoryQuestion("Сколько ошибок на практике приводят к провалу экзамена?", new [] { "1", "3", "10" }, 1),
            new TheoryQuestion("Что считается ошибкой на практике?", new [] { "Превышение скорости и столкновения", "Включённые фары", "Езда по своей полосе" }, 0),
            new TheoryQuestion("Вы вышли из учебной машины во время практики. Сколько секунд есть, чтобы вернуться?", new [] { "5", "15", "60" }, 1),
            new TheoryQuestion("Куда ехать во время практики?", new [] { "Куда угодно, главное — вернуться", "По контрольным точкам маршрута, отмеченным на карте", "На трассу за городом" }, 1),
            new TheoryQuestion("Что делать при ДТП?", new [] { "Скрыться с места", "Остановиться и действовать по ситуации", "Уехать и написать позже" }, 1),
            new TheoryQuestion("Можно ли резко тормозить без причины в потоке?", new [] { "Да", "Нет, это опасно для едущих сзади", "Можно, если торопишься" }, 1),
            new TheoryQuestion("Нужно ли пристёгиваться ремнём безопасности?", new [] { "Да, всегда", "Нет", "Только на трассе" }, 0),
            new TheoryQuestion("Как правильно поворачивать на перекрёстке?", new [] { "Снизить скорость, включить поворотник и пропустить пешеходов", "Не сбавлять скорость", "Повернуть с крайней противоположной полосы" }, 0),
            new TheoryQuestion("Как проехать нерегулируемый пешеходный переход, если на нём люди?", new [] { "Посигналить и проехать", "Остановиться и пропустить пешеходов", "Объехать их" }, 1),
            new TheoryQuestion("Разрешено ли парковаться на тротуаре?", new [] { "Да", "Нет", "Только у магазина" }, 1),
            new TheoryQuestion("Что будет, если персонаж погибнет во время практики?", new [] { "Экзамен продолжится", "Экзамен провален", "Выдадут лицензию" }, 1),
            new TheoryQuestion("Что делать, если сзади едет машина экстренной службы с сиреной?", new [] { "Ускориться", "Прижаться вправо и пропустить", "Остановиться посреди дороги" }, 1),
        };

        private const int TheoryQuestionsCount = 10;
        private const int TheoryPassScore = 8;
        private const int MaxPenalties = 3;

        private static int Step = 0;

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            try
            {
                Main.CreateBlip(new Main.BlipData(498, "Автошкола", EnterSchool, 2, true));
                PedSystem.Repository.CreateQuest("mp_m_securoguard_01", EnterSchool, -60.65183f, title: "~y~NPC~w~ Офицер Бенсон\nВыдача лицензий", colShapeEnums: ColShapeEnums.DriveSchool);

                for (int i = 0; i < DrivingCoords.Count; i++)
                    CustomColShape.CreateCylinderColShape(DrivingCoords[i], 7, 5, 0, ColShapeEnums.DriveSchoolCoord, i);
            }
            catch (Exception e)
            {
                Log.Write($"OnResourceStart Exception: {e}");
            }
        }

        [ServerEvent(Event.PlayerEnterVehicle)]
        public void OnPlayerEnterVehicleHandler(ExtPlayer player, ExtVehicle vehicle, sbyte seatid)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;

                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                var vehicleLocalData = vehicle.GetVehicleLocalData();
                if (vehicleLocalData == null || vehicleLocalData.Access != VehicleAccess.School) return;

                if (vehicleLocalData.WorkDriver != characterData.UUID)
                {
                    VehicleManager.WarpPlayerOutOfVehicle(player);
                    Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "Эта учебная машина не предназначается для Вас.", 3000);
                    return;
                }

                if (sessionData.TimersData.SchoolTimer != null)
                {
                    Timers.Stop(sessionData.TimersData.SchoolTimer);
                    sessionData.TimersData.SchoolTimer = null;
                }
            }
            catch (Exception e)
            {
                Log.Write($"OnPlayerEnterVehicleHandler Exception: {e}");
            }
        }

        [ServerEvent(Event.PlayerExitVehicle)]
        public void OnPlayerExitVehicle(ExtPlayer player, ExtVehicle vehicle)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                if (!player.IsCharacterData()) return;

                if (sessionData.DSchoolData.Vehicle != vehicle) return;

                if (sessionData.TimersData.SchoolTimer == null)
                    sessionData.TimersData.SchoolTimer = Timers.StartOnce(15000, () => FailPractice(player, LangFunc.GetText(LangType.Ru, DataName.FailExam)), true);

                Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "Вернитесь в учебное авто в течение 15 секунд, иначе экзамен будет провален.", 6000);
            }
            catch (Exception e)
            {
                Log.Write($"OnPlayerExitVehicle Exception: {e}");
            }
        }

        public static void Event_PlayerDeath(ExtPlayer player, ExtPlayer killer, uint reason)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                if (!sessionData.DSchoolData.IsDriving) return;

                FailPractice(player, "Экзамен провален из-за смерти персонажа.");
            }
            catch (Exception e)
            {
                Log.Write($"Event_PlayerDeath Exception: {e}");
            }
        }

        public static void OnPlayerDisconnected(ExtPlayer player, DisconnectionType type, string reason)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;

                CleanupPractice(player, sessionData, false);
                CleanupTheory(sessionData);
            }
            catch (Exception e)
            {
                Log.Write($"OnPlayerDisconnected Exception: {e}");
            }
        }

        [Interaction(ColShapeEnums.DriveSchoolCoord, In: true)]
        public static void InDriveSchoolCoord(ExtPlayer player, int index)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;

                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                var dSchoolData = sessionData.DSchoolData;
                if (!dSchoolData.IsDriving || dSchoolData.Vehicle == null) return;
                if (!player.IsInVehicle || player.Vehicle != dSchoolData.Vehicle) return;
                if (dSchoolData.Check != index) return;

                dSchoolData.Check++;

                if (dSchoolData.Check >= DrivingCoords.Count)
                {
                    CleanupPractice(player, sessionData, true);
                    GrantLicense(player, dSchoolData.License);
                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, "Практический экзамен сдан, лицензия выдана.", 5000);
                    return;
                }

                CreatePracticeCheckpoint(player, dSchoolData.Check);
                SendPracticeProgress(player);
            }
            catch (Exception e)
            {
                Log.Write($"InDriveSchoolCoord Exception: {e}");
            }
        }

        private static void StartDrivingCourse(ExtPlayer player, int index)
        {
            try
            {
                if (index < 0 || index >= Main.LicPrices.Length)
                    return;

                if (!FunctionsAccess.IsWorking("startDrivingCourse"))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.FunctionOffByAdmins), 3000);
                    return;
                }

                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                if (sessionData.DSchoolData.IsDriving || sessionData.DSchoolData.IsTheory || sessionData.WorkData.OnWork)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.CantDoThisNow), 3000);
                    return;
                }

                if (characterData.Licenses[index])
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.YouAlreadyHaveLic), 3000);
                    return;
                }

                if (index == 4 || index == 5)
                {
                    if (characterData.LVL < 20)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.LicMustBe20), 3000);
                        return;
                    }
                }

                if (index <= 2)
                {
                    // Теория уже сдана по этой категории — сразу на практику, без повторной оплаты
                    if (sessionData.DSchoolData.TheoryPassed == index)
                    {
                        Trigger.ClientEvent(player, "client.drivingschool.close");
                        StartPractice(player, index);
                        return;
                    }
                    StartTheory(player, index);
                    return;
                }

                if (Chars.UpdateData.CanIChange(player, Main.LicPrices[index], true) != 255) return;

                MoneySystem.Wallet.Change(player, -Main.LicPrices[index]);
                GrantLicense(player, index);
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, GetSuccessMessage(index), 3000);
                OpenMenu(player); // обновить карточки лицензий
            }
            catch (Exception e)
            {
                Log.Write($"StartDrivingCourse Exception: {e}");
            }
        }

        private static void StartTheory(ExtPlayer player, int licenseIndex)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null) return;

            var dSchoolData = sessionData.DSchoolData;

            if (Chars.UpdateData.CanIChange(player, Main.LicPrices[licenseIndex], true) != 255) return;

            MoneySystem.Wallet.Change(player, -Main.LicPrices[licenseIndex]);

            dSchoolData.IsTheory = true;
            dSchoolData.TheoryQuestionIndex = 0;
            dSchoolData.TheoryCorrectAnswers = 0;
            dSchoolData.License = (byte)licenseIndex;
            dSchoolData.TheoryOrder = Enumerable.Range(0, TheoryQuestions.Count)
                .OrderBy(_ => SafeMain.SafeRNG.Next())
                .Take(TheoryQuestionsCount)
                .ToList();

            SendTheoryQuestion(player);
        }

        private static void SendTheoryQuestion(ExtPlayer player)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null) return;

            var dSchoolData = sessionData.DSchoolData;
            if (!dSchoolData.IsTheory) return;

            if (dSchoolData.TheoryQuestionIndex >= dSchoolData.TheoryOrder.Count)
            {
                FinishTheory(player);
                return;
            }

            var question = TheoryQuestions[dSchoolData.TheoryOrder[dSchoolData.TheoryQuestionIndex]];
            Trigger.ClientEvent(player, "client.drivingschool.question", JsonConvert.SerializeObject(new
            {
                number = dSchoolData.TheoryQuestionIndex + 1,
                total = dSchoolData.TheoryOrder.Count,
                correct = dSchoolData.TheoryCorrectAnswers,
                question = question.Question,
                answers = question.Answers,
            }));
        }

        [RemoteEvent("server.drivingschool.answer")]
        public static void OnTheoryAnswer(ExtPlayer player, int answerIndex)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;

                var dSchoolData = sessionData.DSchoolData;
                if (!dSchoolData.IsTheory) return;
                if (dSchoolData.TheoryQuestionIndex >= dSchoolData.TheoryOrder.Count) return;

                var question = TheoryQuestions[dSchoolData.TheoryOrder[dSchoolData.TheoryQuestionIndex]];
                if (answerIndex == question.CorrectAnswer)
                    dSchoolData.TheoryCorrectAnswers++;

                dSchoolData.TheoryQuestionIndex++;
                SendTheoryQuestion(player);
            }
            catch (Exception e)
            {
                Log.Write($"OnTheoryAnswer Exception: {e}");
            }
        }

        private static void FinishTheory(ExtPlayer player)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null) return;
            var dSchoolData = sessionData.DSchoolData;

            var correct = dSchoolData.TheoryCorrectAnswers;
            var total = dSchoolData.TheoryOrder.Count;
            bool isPass = correct >= TheoryPassScore;
            byte license = dSchoolData.License;

            CleanupTheory(sessionData);
            if (isPass)
                dSchoolData.TheoryPassed = license;

            Trigger.ClientEvent(player, "client.drivingschool.result", JsonConvert.SerializeObject(new
            {
                passed = isPass,
                correct,
                total,
                need = TheoryPassScore,
                license = (int)license,
            }));
        }

        [RemoteEvent("server.drivingschool.start")]
        public static void OnStart(ExtPlayer player, int index)
        {
            if (!IsNearSchool(player)) return;
            StartDrivingCourse(player, index);
        }

        [RemoteEvent("server.drivingschool.practice")]
        public static void OnStartPractice(ExtPlayer player)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null || !IsNearSchool(player)) return;

            var license = sessionData.DSchoolData.TheoryPassed;
            if (license > 2) return;

            Trigger.ClientEvent(player, "client.drivingschool.close");
            StartPractice(player, license);
        }

        [RemoteEvent("server.drivingschool.close")]
        public static void OnClose(ExtPlayer player)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null) return;

            if (sessionData.DSchoolData.IsTheory)
            {
                CleanupTheory(sessionData);
                Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "Теория прервана. Оплата не возвращается.", 4000);
            }
        }

        [RemoteEvent("server.drivingschool.penalty")]
        public static void OnPenalty(ExtPlayer player, string reason)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;

                var dSchoolData = sessionData.DSchoolData;
                if (!dSchoolData.IsDriving || dSchoolData.Vehicle == null || player.Vehicle != dSchoolData.Vehicle) return;

                var text = reason == "speed" ? "Превышение скорости" : reason == "crash" ? "Столкновение" : "Нарушение";
                dSchoolData.Penalties++;

                if (dSchoolData.Penalties >= MaxPenalties)
                {
                    FailPractice(player, $"Экзамен провален: {MaxPenalties} ошибки. Последняя — {text.ToLower()}.");
                    return;
                }

                Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, $"Ошибка: {text} ({dSchoolData.Penalties}/{MaxPenalties})", 3000);
                SendPracticeProgress(player);
            }
            catch (Exception e)
            {
                Log.Write($"OnPenalty Exception: {e}");
            }
        }

        private static bool IsNearSchool(ExtPlayer player)
        {
            return player.IsCharacterData() && player.Dimension == 0 && player.Position.DistanceTo(EnterSchool) < 6f;
        }

        private static int GetSpeedLimit(int licenseIndex) => licenseIndex == 2 ? 70 : 80;

        private static void SendPracticeProgress(ExtPlayer player)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null) return;
            var dSchoolData = sessionData.DSchoolData;
            Trigger.ClientEvent(player, "client.drivingschool.practice.progress", Math.Max(0, (int)dSchoolData.Check), DrivingCoords.Count, dSchoolData.Penalties);
        }

        private static void StartPractice(ExtPlayer player, int licenseIndex)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null) return;

            var characterData = player.GetCharacterData();
            if (characterData == null) return;

            VehicleHash vehicleHash;
            string platePrefix;
            switch (licenseIndex)
            {
                case 0:
                    vehicleHash = VehicleHash.Pcj;
                    platePrefix = "LICA";
                    break;
                case 1:
                    vehicleHash = VehicleHash.Primo;
                    platePrefix = "LICB";
                    break;
                case 2:
                    vehicleHash = VehicleHash.Flatbed;
                    platePrefix = "LICC";
                    break;
                default:
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Для этой лицензии практический экзамен не предусмотрен.", 3000);
                    return;
            }

            var spawnPosition = StartCourseCoord[Step];
            var spawnRotation = StartCourseRot[Step];
            if (++Step >= StartCourseCoord.Length) Step = 0;

            var vehicle = VehicleStreaming.CreateVehicle((uint)vehicleHash, spawnPosition, spawnRotation.Z, 30, 30, $"{platePrefix}{player.Value}", acc: VehicleAccess.School, workdriv: characterData.UUID, petrol: 9999);

            var dSchoolData = sessionData.DSchoolData;
            dSchoolData.Vehicle = vehicle;
            dSchoolData.IsDriving = true;
            dSchoolData.License = (byte)licenseIndex;
            dSchoolData.Check = 0;
            dSchoolData.Penalties = 0;
            dSchoolData.TheoryPassed = 255;

            Trigger.ClientEvent(player, "setIntoVehicle", vehicle, VehicleSeat.Driver - 1);
            CreatePracticeCheckpoint(player, 0);
            Trigger.ClientEvent(player, "client.drivingschool.practice.start", LicenseNames[licenseIndex], DrivingCoords.Count, GetSpeedLimit(licenseIndex), MaxPenalties);
            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Практика началась. Следуйте по контрольным точкам.", 4000);
        }

        private static void CreatePracticeCheckpoint(ExtPlayer player, int index)
        {
            if (index < 0 || index >= DrivingCoords.Count) return;

            var current = DrivingCoords[index] - new Vector3(0, 0, 2);
            Vector3 direction = index + 1 < DrivingCoords.Count ? DrivingCoords[index + 1] - new Vector3(0, 0, 2) : current;

            Trigger.ClientEvent(player, "createCheckpoint", 12, 1, current, 4, 0, 255, 0, 0, direction);
            Trigger.ClientEvent(player, "createWaypoint", DrivingCoords[index].X, DrivingCoords[index].Y);
            Trigger.ClientEvent(player, "createWorkBlip", DrivingCoords[index]);
        }

        private static void FailPractice(ExtPlayer player, string reason)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null) return;

            CleanupPractice(player, sessionData, false);
            Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, reason, 5000);
        }

        private static void CleanupPractice(ExtPlayer player, SessionData sessionData, bool deleteCheckpoint)
        {
            var dSchoolData = sessionData.DSchoolData;

            if (dSchoolData.Vehicle != null)
            {
                VehicleStreaming.DeleteVehicle(dSchoolData.Vehicle);
                dSchoolData.Vehicle = null;
            }

            if (deleteCheckpoint || dSchoolData.IsDriving)
            {
                Trigger.ClientEvent(player, "deleteCheckpoint", 12, 0);
                Trigger.ClientEvent(player, "deleteWorkBlip");
            }

            if (dSchoolData.IsDriving)
                Trigger.ClientEvent(player, "client.drivingschool.practice.end");

            dSchoolData.IsDriving = false;
            dSchoolData.Check = -1;
            dSchoolData.Penalties = 0;

            if (sessionData.TimersData.SchoolTimer != null)
            {
                Timers.Stop(sessionData.TimersData.SchoolTimer);
                sessionData.TimersData.SchoolTimer = null;
            }
        }

        private static void CleanupTheory(SessionData sessionData)
        {
            var dSchoolData = sessionData.DSchoolData;
            dSchoolData.IsTheory = false;
            dSchoolData.TheoryQuestionIndex = 0;
            dSchoolData.TheoryCorrectAnswers = 0;
            dSchoolData.TheoryOrder = new List<int>();
            dSchoolData.License = 255;
        }

        private static string GetSuccessMessage(int index)
        {
            switch (index)
            {
                case 0: return LangFunc.GetText(LangType.Ru, DataName.SucBuyMotoLic);
                case 1: return LangFunc.GetText(LangType.Ru, DataName.SucBuyVehLic);
                case 2: return LangFunc.GetText(LangType.Ru, DataName.SucBuyGruzLic);
                case 3: return LangFunc.GetText(LangType.Ru, DataName.SucBuySeaLic);
                case 4: return LangFunc.GetText(LangType.Ru, DataName.SucBuyHeliLic);
                case 5: return LangFunc.GetText(LangType.Ru, DataName.SucBuyPlaneLic);
                default: return "Лицензия выдана.";
            }
        }

        private static void GrantLicense(ExtPlayer player, int index)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null) return;

            characterData.Licenses[index] = true;

            if (index == 1)
            {
                qMain.UpdateQuestsStage(player, Zdobich.QuestName, (int)zdobich_quests.Stage7, 2, isUpdateHud: true);
                qMain.UpdateQuestsComplete(player, Zdobich.QuestName, (int)zdobich_quests.Stage7, true);
            }
        }

        #region menu
        private static readonly string[] LicenseNames = { "Мотоцикл (A)", "Легковой (B)", "Грузовой (C)", "Водный транспорт", "Вертолёт", "Самолёт" };

        [Interaction(ColShapeEnums.DriveSchool)]
        public static void OnDriveSchool(ExtPlayer player)
        {
            try
            {
                if (!player.IsCharacterData()) return;
                OpenMenu(player);
                BattlePass.Repository.UpdateReward(player, 149);
            }
            catch (Exception e)
            {
                Log.Write($"OnDriveSchool Exception: {e}");
            }
        }

        /// <summary>
        /// Окно автошколы (CEF PlayerDrivingSchool): лицензии, теория для подготовки, экзамен.
        /// </summary>
        private static void OpenMenu(ExtPlayer player)
        {
            var sessionData = player.GetSessionData();
            var characterData = player.GetCharacterData();
            if (sessionData == null || characterData == null) return;

            var licenses = new List<object>();
            for (var i = 0; i < LicenseNames.Length && i < Main.LicPrices.Length; i++)
            {
                licenses.Add(new
                {
                    index = i,
                    name = LicenseNames[i],
                    price = Main.LicPrices[i],
                    has = characterData.Licenses[i],
                    exam = i <= 2,
                    needLvl = i == 4 || i == 5 ? 20 : 0,
                    theoryPassed = sessionData.DSchoolData.TheoryPassed == i,
                });
            }

            Trigger.ClientEvent(player, "client.drivingschool.open", JsonConvert.SerializeObject(new
            {
                lvl = characterData.LVL,
                questions = TheoryQuestionsCount,
                passScore = TheoryPassScore,
                maxPenalties = MaxPenalties,
                checkpoints = DrivingCoords.Count,
                licenses,
            }));
        }
        #endregion

        private class TheoryQuestion
        {
            public string Question { get; }
            public string[] Answers { get; }
            public int CorrectAnswer { get; }

            public TheoryQuestion(string question, string[] answers, int correctAnswer)
            {
                Question = question;
                Answers = answers;
                CorrectAnswer = correctAnswer;
            }
        }
    }
}
