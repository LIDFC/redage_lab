using System;
using System.Collections.Generic;
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
            new Vector3(431.21985, -1032.8828, 28.9801),
            new Vector3(410.47412, -1030.1249, 28.97287),
            new Vector3(390.51257, -1026.577, 29.244324),
            new Vector3(365.74847, -1020.3939, 29.338669),
            new Vector3(339.89932, -1015.8041, 29.335278),
            new Vector3(324.61993, -997.4377, 29.24564),
            new Vector3(344.75146, -990.05334, 29.254818),
            new Vector3(372.65784, -996.766, 29.25668),
            new Vector3(402.62216, -1002.49744, 29.267765),
            new Vector3(428.49573, -1008.6071, 28.998224),
        };

        private static readonly List<TheoryQuestion> TheoryQuestions = new List<TheoryQuestion>
        {
            new TheoryQuestion("Какой сигнал светофора запрещает движение?", new [] { "Красный", "Зелёный", "Жёлтый" }, 0),
            new TheoryQuestion("Можно ли ехать по встречной полосе без причины?", new [] { "Да", "Нет", "Можно ночью" }, 1),
            new TheoryQuestion("Что нужно сделать перед началом движения?", new [] { "Проверить обстановку и включить поворотник", "Сразу нажать газ", "Посигналить" }, 0),
            new TheoryQuestion("Разрешено ли управлять транспортом в нетрезвом виде?", new [] { "Разрешено при малой скорости", "Нет, запрещено", "Разрешено за городом" }, 1),
            new TheoryQuestion("Что означает мигающий жёлтый сигнал?", new [] { "Движение запрещено", "Светофор неисправен/нерегулируемый перекрёсток", "Обязательная остановка" }, 1),
            new TheoryQuestion("Нужно ли пристёгиваться ремнём безопасности?", new [] { "Да", "Нет", "Только на трассе" }, 0),
            new TheoryQuestion("Кому уступают на пешеходном переходе?", new [] { "Авто с мигалкой", "Пешеходам", "Никому" }, 1),
            new TheoryQuestion("Можно ли резко тормозить без причины в потоке?", new [] { "Да", "Нет", "Можно, если торопишься" }, 1),
            new TheoryQuestion("Что делать при ДТП?", new [] { "Уехать", "Остановиться и действовать по ситуации", "Продолжить путь и написать позже" }, 1),
            new TheoryQuestion("Минимум правильных ответов для сдачи теста: 30% от 10 вопросов. Сколько это?", new [] { "3", "5", "7" }, 0),
        };

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
                if (!FunctionsAccess.IsWorking("startDrivingCourse"))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.FunctionOffByAdmins), 3000);
                    return;
                }

                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                if (sessionData.DSchoolData.IsDriving || sessionData.WorkData.OnWork)
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
                    StartTheory(player, index);
                    return;
                }

                if (Chars.UpdateData.CanIChange(player, Main.LicPrices[index], true) != 255) return;

                MoneySystem.Wallet.Change(player, -Main.LicPrices[index]);
                GrantLicense(player, index);
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, GetSuccessMessage(index), 3000);
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
            if (dSchoolData.IsDriving || dSchoolData.IsTheory)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Вы уже проходите экзамен.", 3000);
                return;
            }

            if (Chars.UpdateData.CanIChange(player, Main.LicPrices[licenseIndex], true) != 255) return;

            MoneySystem.Wallet.Change(player, -Main.LicPrices[licenseIndex]);

            dSchoolData.IsTheory = true;
            dSchoolData.TheoryQuestionIndex = 0;
            dSchoolData.TheoryCorrectAnswers = 0;
            dSchoolData.License = (byte)licenseIndex;

            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Теоретический экзамен начат (10 вопросов). Для сдачи нужно минимум 3 правильных ответа.", 6500);
            OpenTheoryQuestion(player);
        }

        private static void OpenTheoryQuestion(ExtPlayer player)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null) return;

            var dSchoolData = sessionData.DSchoolData;
            if (!dSchoolData.IsTheory) return;

            if (dSchoolData.TheoryQuestionIndex >= TheoryQuestions.Count)
            {
                FinishTheory(player);
                return;
            }

            var question = TheoryQuestions[dSchoolData.TheoryQuestionIndex];
            var frameList = new FrameListData();
            frameList.Header = $"Теория {dSchoolData.TheoryQuestionIndex + 1}/{TheoryQuestions.Count}\n{question.Question}";
            frameList.Callback = CallbackTheory;

            for (int i = 0; i < question.Answers.Length; i++)
                frameList.List.Add(new ListData(question.Answers[i], i));

            Players.Popup.List.Repository.Open(player, frameList);
        }

        private static void CallbackTheory(ExtPlayer player, object listItem)
        {
            try
            {
                if (!(listItem is int answerIndex)) return;

                var sessionData = player.GetSessionData();
                if (sessionData == null) return;

                var dSchoolData = sessionData.DSchoolData;
                if (!dSchoolData.IsTheory) return;
                if (dSchoolData.TheoryQuestionIndex >= TheoryQuestions.Count) return;

                var question = TheoryQuestions[dSchoolData.TheoryQuestionIndex];
                if (answerIndex == question.CorrectAnswer)
                    dSchoolData.TheoryCorrectAnswers++;

                dSchoolData.TheoryQuestionIndex++;
                OpenTheoryQuestion(player);
            }
            catch (Exception e)
            {
                Log.Write($"CallbackTheory Exception: {e}");
            }
        }

        private static void FinishTheory(ExtPlayer player)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null) return;
            var dSchoolData = sessionData.DSchoolData;

            bool isPass = dSchoolData.TheoryCorrectAnswers >= 3;
            byte license = dSchoolData.License;

            CleanupTheory(sessionData);

            if (!isPass)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Теория провалена. Оплата не возвращается, можно пересдавать сразу.", 5000);
                return;
            }

            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, "Теория сдана. Начинаем практический экзамен.", 5000);
            StartPractice(player, license);
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
                    vehicleHash = VehicleHash.Bagger;
                    platePrefix = "LICA";
                    break;
                case 1:
                    vehicleHash = VehicleHash.Dilettante;
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

            var vehicle = VehicleStreaming.CreateVehicle(vehicleHash, spawnPosition, spawnRotation, 30, 30, $"{platePrefix}{player.Value}", acc: "SCHOOL", workdriv: player, petrol: 9999);

            var dSchoolData = sessionData.DSchoolData;
            dSchoolData.Vehicle = vehicle;
            dSchoolData.IsDriving = true;
            dSchoolData.License = (byte)licenseIndex;
            dSchoolData.Check = 0;

            Trigger.ClientEvent(player, "setIntoVehicle", vehicle, VehicleSeat.Driver - 1);
            CreatePracticeCheckpoint(player, 0);
            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Практика началась. Следуйте по чекпоинтам.", 4000);
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

            dSchoolData.IsDriving = false;
            dSchoolData.Check = -1;

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
        [Interaction(ColShapeEnums.DriveSchool)]
        public static void OnDriveSchool(ExtPlayer player)
        {
            try
            {
                if (!player.IsCharacterData()) return;

                var frameList = new FrameListData();
                frameList.Header = "Лицензии";
                frameList.Callback = CallbackDriveSchool;

                frameList.List.Add(new ListData(LangFunc.GetText(LangType.Ru, DataName.MotoLic, Main.LicPrices[0]), 0));
                frameList.List.Add(new ListData(LangFunc.GetText(LangType.Ru, DataName.LegLic, Main.LicPrices[1]), 1));
                frameList.List.Add(new ListData(LangFunc.GetText(LangType.Ru, DataName.GruzLic, Main.LicPrices[2]), 2));
                frameList.List.Add(new ListData(LangFunc.GetText(LangType.Ru, DataName.VodLic, Main.LicPrices[3]), 3));
                frameList.List.Add(new ListData(LangFunc.GetText(LangType.Ru, DataName.VertLic, Main.LicPrices[4]), 4));
                frameList.List.Add(new ListData(LangFunc.GetText(LangType.Ru, DataName.SamLic, Main.LicPrices[5]), 5));

                Players.Popup.List.Repository.Open(player, frameList);
                BattlePass.Repository.UpdateReward(player, 149);
            }
            catch (Exception e)
            {
                Log.Write($"OnDriveSchool Exception: {e}");
            }
        }

        private static void CallbackDriveSchool(ExtPlayer player, object listItem)
        {
            if (!(listItem is int))
                return;

            if (!player.IsCharacterData())
                return;

            StartDrivingCourse(player, Convert.ToInt32(listItem));
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
