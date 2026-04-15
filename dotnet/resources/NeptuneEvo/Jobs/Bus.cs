using GTANetworkAPI;
using NeptuneEvo.Handles;
using System;
using System.Collections.Generic;
using NeptuneEvo.Core;
using Redage.SDK;
using System.Linq;
using Localization;
using NeptuneEvo.Functions;
using NeptuneEvo.Accounts;
using NeptuneEvo.Players.Models;
using NeptuneEvo.Players;
using NeptuneEvo.Character.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Jobs.Models;
using NeptuneEvo.Quests;
using NeptuneEvo.VehicleData.LocalData;

namespace NeptuneEvo.Jobs
{
    class Bus : Script
    {
        private static readonly nLog Log = new nLog("Jobs.Bus");

        private static List<string> BusWaysNames = new List<string>
        {
            "[Городской 1] EMS - Old City Hall - Market - Driving School - NEWS - LSPD",
            "[Городской 2] EMS - Old City Hall - Market - Driving School - NEWS - LSPD",
            "[Городской 3] EMS - Old City Hall - Market - Driving School - NEWS - LSPD",
        };
        private static List<List<BusCheck>> BusWays = new List<List<BusCheck>>()
        {
            new List<BusCheck>() // busway1
            {
                new BusCheck(new Vector3(463.78735, -674.06415, 27.818863)),
                new BusCheck(new Vector3(353.81128, -673.3719, 29.834314)),
                new BusCheck(new Vector3(308.50708, -764.00635, 29.754377), true),
                new BusCheck(new Vector3(298.45306, -812.08545, 29.841228)),
                new BusCheck(new Vector3(241.48691, -832.1899, 30.405945)),
                new BusCheck(new Vector3(214.81517, -733.6068, 34.60165)),
                new BusCheck(new Vector3(274.30054, -586.10626, 43.644268), true),
                new BusCheck(new Vector3(313.15225, -485.85574, 43.750698)),
                new BusCheck(new Vector3(451.86758, -351.58432, 47.82203)),
                new BusCheck(new Vector3(522.57794, -205.16173, 52.396324)),
                new BusCheck(new Vector3(575.04987, -89.98442, 69.04095)),
                new BusCheck(new Vector3(694.20636, -2.577138, 84.63317)),
                new BusCheck(new Vector3(769.78326, 106.87052, 79.26235)),
                new BusCheck(new Vector3(761.2202, 176.63538, 82.423584), true),
                new BusCheck(new Vector3(614.37085, 232.57607, 102.10711)),
                new BusCheck(new Vector3(436.92944, 293.80518, 103.45449)),
                new BusCheck(new Vector3(267.4122, 341.5739, 105.97788)),
                new BusCheck(new Vector3(89.18705, 336.86612, 112.95764)),
                new BusCheck(new Vector3(19.09471, 233.38876, 109.88524)),
                new BusCheck(new Vector3(-35.34923, 83.759674, 75.335236)),
                new BusCheck(new Vector3(-70.66857, -54.56637, 61.304157)),
                new BusCheck(new Vector3(-115.73929, -84.75978, 57.15646)),
                new BusCheck(new Vector3(-251.53116, -56.768105, 50.025047)),
                new BusCheck(new Vector3(-319.9071, -177.89638, 39.713936)),
                new BusCheck(new Vector3(-517.8612, -269.98218, 35.961834), true),
                new BusCheck(new Vector3(-637.0907, -340.11807, 35.28972)),
                new BusCheck(new Vector3(-745.4498, -326.02478, 36.69483)),
                new BusCheck(new Vector3(-968.3309, -216.46997, 38.240185)),
                new BusCheck(new Vector3(-1287.9657, -53.025146, 47.223648), true),
                new BusCheck(new Vector3(-1477.9679, -99.27132, 51.35809)),
                new BusCheck(new Vector3(-1607.1597, -212.67621, 55.260513)),
                new BusCheck(new Vector3(-1679.347, -341.61438, 49.420418)),
                new BusCheck(new Vector3(-1770.7251, -441.78616, 42.275208)),
                new BusCheck(new Vector3(-1882.2374, -380.65005, 48.945312), true),
                new BusCheck(new Vector3(-1929.1748, -315.1801, 45.75602)),
                new BusCheck(new Vector3(-1919.2212, -213.98195, 36.42688)),
                new BusCheck(new Vector3(-2077.2607, -181.1639, 23.561241)),
                new BusCheck(new Vector3(-2165.0835, -313.60608, 13.513528)),
                new BusCheck(new Vector3(-2116.118, -377.2931, 13.309739)),
                new BusCheck(new Vector3(-1886.9792, -559.1868, 12.175919)),
                new BusCheck(new Vector3(-1731.1263, -700.6112, 10.700001), true),
                new BusCheck(new Vector3(-1636.2358, -763.39386, 10.722627)),
                new BusCheck(new Vector3(-1447.6786, -841.2925, 16.146486)),
                new BusCheck(new Vector3(-1290.3981, -900.5671, 11.873873)),
                new BusCheck(new Vector3(-1154.48, -832.8742, 14.824876)),
                new BusCheck(new Vector3(-1033.4685, -806.7502, 17.812508)),
                new BusCheck(new Vector3(-857.21796, -971.69055, 15.455125)),
                new BusCheck(new Vector3(-754.7794, -1160.3079, 11.13419)),
                new BusCheck(new Vector3(-688.2619, -1249.8187, 11.083057), true),
                new BusCheck(new Vector3(-648.5486, -1293.1781, 11.160974)),
                new BusCheck(new Vector3(-544.2863, -1165.2975, 19.551844)),
                new BusCheck(new Vector3(-521.17126, -937.24036, 24.423046), true),
                new BusCheck(new Vector3(-457.6673, -845.75977, 31.06772)),
                new BusCheck(new Vector3(-252.32465, -881.33875, 31.207403), true),
                new BusCheck(new Vector3(-126.848404, -919.5824, 29.803246)),
                new BusCheck(new Vector3(1.3415923, -967.2384, 29.886044)),
                new BusCheck(new Vector3(354.1879, -1060.4279, 29.867931), true),
                new BusCheck(new Vector3(402.01562, -998.7102, 29.867247)),
                new BusCheck(new Vector3(454.75784, -958.1483, 28.884958)),
                new BusCheck(new Vector3(503.65225, -890.3943, 26.0824)),
                new BusCheck(new Vector3(507.85663, -750.52423, 25.299847)),
            },
            new List<BusCheck>() // busway2
            {
                new BusCheck(new Vector3(463.78735, -674.06415, 27.818863)),
                new BusCheck(new Vector3(353.81128, -673.3719, 29.834314)),
                new BusCheck(new Vector3(308.50708, -764.00635, 29.754377), true),
                new BusCheck(new Vector3(298.45306, -812.08545, 29.841228)),
                new BusCheck(new Vector3(241.48691, -832.1899, 30.405945)),
                new BusCheck(new Vector3(214.81517, -733.6068, 34.60165)),
                new BusCheck(new Vector3(274.30054, -586.10626, 43.644268), true),
                new BusCheck(new Vector3(313.15225, -485.85574, 43.750698)),
                new BusCheck(new Vector3(451.86758, -351.58432, 47.82203)),
                new BusCheck(new Vector3(522.57794, -205.16173, 52.396324)),
                new BusCheck(new Vector3(575.04987, -89.98442, 69.04095)),
                new BusCheck(new Vector3(694.20636, -2.577138, 84.63317)),
                new BusCheck(new Vector3(769.78326, 106.87052, 79.26235)),
                new BusCheck(new Vector3(761.2202, 176.63538, 82.423584), true),
                new BusCheck(new Vector3(614.37085, 232.57607, 102.10711)),
                new BusCheck(new Vector3(436.92944, 293.80518, 103.45449)),
                new BusCheck(new Vector3(267.4122, 341.5739, 105.97788)),
                new BusCheck(new Vector3(89.18705, 336.86612, 112.95764)),
                new BusCheck(new Vector3(19.09471, 233.38876, 109.88524)),
                new BusCheck(new Vector3(-35.34923, 83.759674, 75.335236)),
                new BusCheck(new Vector3(-70.66857, -54.56637, 61.304157)),
                new BusCheck(new Vector3(-115.73929, -84.75978, 57.15646)),
                new BusCheck(new Vector3(-251.53116, -56.768105, 50.025047)),
                new BusCheck(new Vector3(-319.9071, -177.89638, 39.713936)),
                new BusCheck(new Vector3(-517.8612, -269.98218, 35.961834), true),
                new BusCheck(new Vector3(-637.0907, -340.11807, 35.28972)),
                new BusCheck(new Vector3(-745.4498, -326.02478, 36.69483)),
                new BusCheck(new Vector3(-968.3309, -216.46997, 38.240185)),
                new BusCheck(new Vector3(-1287.9657, -53.025146, 47.223648), true),
                new BusCheck(new Vector3(-1477.9679, -99.27132, 51.35809)),
                new BusCheck(new Vector3(-1607.1597, -212.67621, 55.260513)),
                new BusCheck(new Vector3(-1679.347, -341.61438, 49.420418)),
                new BusCheck(new Vector3(-1770.7251, -441.78616, 42.275208)),
                new BusCheck(new Vector3(-1882.2374, -380.65005, 48.945312), true),
                new BusCheck(new Vector3(-1929.1748, -315.1801, 45.75602)),
                new BusCheck(new Vector3(-1919.2212, -213.98195, 36.42688)),
                new BusCheck(new Vector3(-2077.2607, -181.1639, 23.561241)),
                new BusCheck(new Vector3(-2165.0835, -313.60608, 13.513528)),
                new BusCheck(new Vector3(-2116.118, -377.2931, 13.309739)),
                new BusCheck(new Vector3(-1886.9792, -559.1868, 12.175919)),
                new BusCheck(new Vector3(-1731.1263, -700.6112, 10.700001), true),
                new BusCheck(new Vector3(-1636.2358, -763.39386, 10.722627)),
                new BusCheck(new Vector3(-1447.6786, -841.2925, 16.146486)),
                new BusCheck(new Vector3(-1290.3981, -900.5671, 11.873873)),
                new BusCheck(new Vector3(-1154.48, -832.8742, 14.824876)),
                new BusCheck(new Vector3(-1033.4685, -806.7502, 17.812508)),
                new BusCheck(new Vector3(-857.21796, -971.69055, 15.455125)),
                new BusCheck(new Vector3(-754.7794, -1160.3079, 11.13419)),
                new BusCheck(new Vector3(-688.2619, -1249.8187, 11.083057), true),
                new BusCheck(new Vector3(-648.5486, -1293.1781, 11.160974)),
                new BusCheck(new Vector3(-544.2863, -1165.2975, 19.551844)),
                new BusCheck(new Vector3(-521.17126, -937.24036, 24.423046), true),
                new BusCheck(new Vector3(-457.6673, -845.75977, 31.06772)),
                new BusCheck(new Vector3(-252.32465, -881.33875, 31.207403), true),
                new BusCheck(new Vector3(-126.848404, -919.5824, 29.803246)),
                new BusCheck(new Vector3(1.3415923, -967.2384, 29.886044)),
                new BusCheck(new Vector3(354.1879, -1060.4279, 29.867931), true),
                new BusCheck(new Vector3(402.01562, -998.7102, 29.867247)),
                new BusCheck(new Vector3(454.75784, -958.1483, 28.884958)),
                new BusCheck(new Vector3(503.65225, -890.3943, 26.0824)),
                new BusCheck(new Vector3(507.85663, -750.52423, 25.299847)),
            },
            new List<BusCheck>() // busway3
            {
                new BusCheck(new Vector3(463.78735, -674.06415, 27.818863)),
                new BusCheck(new Vector3(353.81128, -673.3719, 29.834314)),
                new BusCheck(new Vector3(308.50708, -764.00635, 29.754377), true),
                new BusCheck(new Vector3(298.45306, -812.08545, 29.841228)),
                new BusCheck(new Vector3(241.48691, -832.1899, 30.405945)),
                new BusCheck(new Vector3(214.81517, -733.6068, 34.60165)),
                new BusCheck(new Vector3(274.30054, -586.10626, 43.644268), true),
                new BusCheck(new Vector3(313.15225, -485.85574, 43.750698)),
                new BusCheck(new Vector3(451.86758, -351.58432, 47.82203)),
                new BusCheck(new Vector3(522.57794, -205.16173, 52.396324)),
                new BusCheck(new Vector3(575.04987, -89.98442, 69.04095)),
                new BusCheck(new Vector3(694.20636, -2.577138, 84.63317)),
                new BusCheck(new Vector3(769.78326, 106.87052, 79.26235)),
                new BusCheck(new Vector3(761.2202, 176.63538, 82.423584), true),
                new BusCheck(new Vector3(614.37085, 232.57607, 102.10711)),
                new BusCheck(new Vector3(436.92944, 293.80518, 103.45449)),
                new BusCheck(new Vector3(267.4122, 341.5739, 105.97788)),
                new BusCheck(new Vector3(89.18705, 336.86612, 112.95764)),
                new BusCheck(new Vector3(19.09471, 233.38876, 109.88524)),
                new BusCheck(new Vector3(-35.34923, 83.759674, 75.335236)),
                new BusCheck(new Vector3(-70.66857, -54.56637, 61.304157)),
                new BusCheck(new Vector3(-115.73929, -84.75978, 57.15646)),
                new BusCheck(new Vector3(-251.53116, -56.768105, 50.025047)),
                new BusCheck(new Vector3(-319.9071, -177.89638, 39.713936)),
                new BusCheck(new Vector3(-517.8612, -269.98218, 35.961834), true),
                new BusCheck(new Vector3(-637.0907, -340.11807, 35.28972)),
                new BusCheck(new Vector3(-745.4498, -326.02478, 36.69483)),
                new BusCheck(new Vector3(-968.3309, -216.46997, 38.240185)),
                new BusCheck(new Vector3(-1287.9657, -53.025146, 47.223648), true),
                new BusCheck(new Vector3(-1477.9679, -99.27132, 51.35809)),
                new BusCheck(new Vector3(-1607.1597, -212.67621, 55.260513)),
                new BusCheck(new Vector3(-1679.347, -341.61438, 49.420418)),
                new BusCheck(new Vector3(-1770.7251, -441.78616, 42.275208)),
                new BusCheck(new Vector3(-1882.2374, -380.65005, 48.945312), true),
                new BusCheck(new Vector3(-1929.1748, -315.1801, 45.75602)),
                new BusCheck(new Vector3(-1919.2212, -213.98195, 36.42688)),
                new BusCheck(new Vector3(-2077.2607, -181.1639, 23.561241)),
                new BusCheck(new Vector3(-2165.0835, -313.60608, 13.513528)),
                new BusCheck(new Vector3(-2116.118, -377.2931, 13.309739)),
                new BusCheck(new Vector3(-1886.9792, -559.1868, 12.175919)),
                new BusCheck(new Vector3(-1731.1263, -700.6112, 10.700001), true),
                new BusCheck(new Vector3(-1636.2358, -763.39386, 10.722627)),
                new BusCheck(new Vector3(-1447.6786, -841.2925, 16.146486)),
                new BusCheck(new Vector3(-1290.3981, -900.5671, 11.873873)),
                new BusCheck(new Vector3(-1154.48, -832.8742, 14.824876)),
                new BusCheck(new Vector3(-1033.4685, -806.7502, 17.812508)),
                new BusCheck(new Vector3(-857.21796, -971.69055, 15.455125)),
                new BusCheck(new Vector3(-754.7794, -1160.3079, 11.13419)),
                new BusCheck(new Vector3(-688.2619, -1249.8187, 11.083057), true),
                new BusCheck(new Vector3(-648.5486, -1293.1781, 11.160974)),
                new BusCheck(new Vector3(-544.2863, -1165.2975, 19.551844)),
                new BusCheck(new Vector3(-521.17126, -937.24036, 24.423046), true),
                new BusCheck(new Vector3(-457.6673, -845.75977, 31.06772)),
                new BusCheck(new Vector3(-252.32465, -881.33875, 31.207403), true),
                new BusCheck(new Vector3(-126.848404, -919.5824, 29.803246)),
                new BusCheck(new Vector3(1.3415923, -967.2384, 29.886044)),
                new BusCheck(new Vector3(354.1879, -1060.4279, 29.867931), true),
                new BusCheck(new Vector3(402.01562, -998.7102, 29.867247)),
                new BusCheck(new Vector3(454.75784, -958.1483, 28.884958)),
                new BusCheck(new Vector3(503.65225, -890.3943, 26.0824)),
                new BusCheck(new Vector3(507.85663, -750.52423, 25.299847)),
            },
        };

        #region BusStations
        public static Dictionary<string, Vector3> BusStations = new Dictionary<string, Vector3>()
        {
            { "LSPD", new Vector3(394.8946, -990.8792, 30.60689) },
            { "Main Square", new Vector3(-528.8386, -328.6082, 36.34783) },
            { "FIB", new Vector3(-1621.519, -532.9644, 35.70459) },
            { "West Side", new Vector3(19.75618, -1533.853, 30.54906) },
            { "Airport", new Vector3(-1032.82, -2723.92, 14.99705) },
            { "Airport Hotel", new Vector3(-888.1733, -2186.11, 9.900888) },
            { "Driving School", new Vector3(-663.498, -1244.046, 11.90458) },
            { "Lawn Mower", new Vector3(-1354.111, -43.5153, 52.53339) },
            { "Power Station", new Vector3(740.4898, 100.5469, 81.29053) },
            { "Taxi", new Vector3(918.1192, -188.8451, 74.84467) },
            { "Truck Station", new Vector3(602.8403, -3018.18, 6.131153) },
            { "East Side", new Vector3(837.6479, -1807.675, 29.10327) },
            { "Collector", new Vector3(807.4769, -1195.802, 27.39124) },
            { "Mechanic", new Vector3(449.7962, -1249.931, 30.22602) },
            { "Chumash", new Vector3(-3104.435, 1097.097, 20.59407) },
            { "Paleto Bay", new Vector3(-151.3444, 6211.825, 31.31864) },
            { "Sandy Shores", new Vector3(1856.94, 3669.111, 34.11074) },
        };
        #endregion

        [ServerEvent(Event.ResourceStart)]
        public void onResourceStartHandler()
        {
            try
            {
                for (int a = 0; a < BusWays.Count; a++)
                {
                    for (int x = 0; x < BusWays[a].Count; x++)
                    {
                        CustomColShape.CreateCylinderColShape(BusWays[a][x].Pos, 4, 3, 0, ColShapeEnums.BusWays, a, x);
                    }
                }

                foreach (var station in BusStations)
                    NAPI.TextLabel.CreateTextLabel($"~w~Автобусная остановка\n~o~{station.Key}", station.Value, 30f, 0.4f, 0, new Color(255, 255, 255), true, 0);
            }
            catch (Exception e)
            {
                Log.Write($"onResourceStartHandler Exception: {e.ToString()}");
            }
        }

        #region BusWays
        [Interaction(ColShapeEnums.BusWays, In: true)]
        public static void InBusWays(ExtPlayer player, int Index, int ListId)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                var accountData = player.GetAccountData();
                if (accountData == null) return;
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                if (!NAPI.Player.IsPlayerInAnyVehicle(player)) return;
                var vehicle = (ExtVehicle)player.Vehicle;
                var vehicleLocalData = vehicle.GetVehicleLocalData();
                if (vehicleLocalData != null)
                {
                    if (vehicleLocalData.WorkId != JobsId.Bus) return;
                    if (characterData.WorkID != (int)JobsId.Bus || !sessionData.WorkData.OnWork || sessionData.WorkData.WorkWay != Index) return;
                    int way = sessionData.WorkData.WorkWay;
                    if (ListId != sessionData.WorkData.WorkCheck) return;
                    int check = sessionData.WorkData.WorkCheck;

                    if (sessionData.WorkData.BusOnStop) return;
                    if (!BusWays[way][check].IsStop)
                    {
                        if (sessionData.WorkData.WorkCheck != check) return;

                        // ── КОНЕЦ МАРШРУТА ──────────────────────────────────────────────────────
                        // Если это последний чекпоинт — заморозить и спросить игрока
                        if (check + 1 == BusWays[way].Count)
                        {
                            // Выплатить за последний чекпоинт
                            int lastPayment = Convert.ToInt32(Main.BuswaysPayments[way] * Group.GroupPayAdd[accountData.VipLvl] * Main.ServerSettings.MoneyMultiplier);
                            (byte, float) levelInfo = characterData.JobSkills.ContainsKey((int)JobsId.Bus)
                                ? Main.GetPlayerJobLevelBonus((int)JobsId.Bus, characterData.JobSkills[(int)JobsId.Bus])
                                : (0, 1);
                            if (levelInfo.Item1 >= 1) lastPayment = Convert.ToInt32(lastPayment * levelInfo.Item2);
                            MoneySystem.Wallet.Change(player, lastPayment);
                            GameLog.Money($"server", $"player({characterData.UUID})", lastPayment, $"busCheck");

                            // Заморозить автобус, убрать маршрут
                            Trigger.ClientEvent(player, "deleteCheckpoint", 3, 0);
                            Trigger.ClientEvent(player, "deleteWorkBlip");
                            Trigger.ClientEvent(player, "freeze", true);
                            sessionData.WorkData.BusOnStop = true;

                            //Показать диалог: продолжить маршрут или закончить?
                            Trigger.ClientEvent(player, "openDialog", "busRouteEnd",
                                LangFunc.GetText(LangType.Ru, DataName.BusRouteEndQuestion));

                            return;
                        }
                        // ────────────────────────────────────────────────────────────────────────

                        check++;

                        Vector3 direction = (check + 1 != BusWays[way].Count)
                            ? BusWays[way][check + 1].Pos - new Vector3(0, 0, 0.12)
                            : BusWays[way][0].Pos - new Vector3(0, 0, 1.12);
                        Color color = (BusWays[way][check].IsStop) ? new Color(255, 255, 255) : new Color(255, 0, 0);
                        Trigger.ClientEvent(player, "createCheckpoint", 3, 1, BusWays[way][check].Pos - new Vector3(0, 0, 1.12), 4, 0, color.Red, color.Green, color.Blue, direction);
                        Trigger.ClientEvent(player, "createWaypoint", BusWays[way][check].Pos.X, BusWays[way][check].Pos.Y);
                        Trigger.ClientEvent(player, "createWorkBlip", BusWays[way][check].Pos);
                        sessionData.WorkData.WorkCheck = check;

                        int payment = Convert.ToInt32(Main.BuswaysPayments[way] * Group.GroupPayAdd[accountData.VipLvl] * Main.ServerSettings.MoneyMultiplier);
                        (byte, float) jobLevelInfo = characterData.JobSkills.ContainsKey((int)JobsId.Bus)
                            ? Main.GetPlayerJobLevelBonus((int)JobsId.Bus, characterData.JobSkills[(int)JobsId.Bus])
                            : (0, 1);
                        if (jobLevelInfo.Item1 >= 1) payment = Convert.ToInt32(payment * jobLevelInfo.Item2);

                        MoneySystem.Wallet.Change(player, payment);
                        GameLog.Money($"server", $"player({characterData.UUID})", payment, $"busCheck");
                        BattlePass.Repository.UpdateReward(player, 64);
                        BattlePass.Repository.UpdateReward(player, 160);

                        UpdateBusJobSkill(player, characterData, sessionData, way, payment);
                    }
                    else
                    {
                        if (sessionData.WorkData.WorkCheck != check) return;
                        Trigger.ClientEvent(player, "deleteCheckpoint", 3, 0);
                        Trigger.ClientEvent(player, "deleteWorkBlip");
                        Trigger.ClientEvent(player, "freeze", true);
                        sessionData.WorkData.BusOnStop = true;
                        Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.BusStop), 6000);
                        var mypos = player.Position;
                        sessionData.TimersData.BusTimer = Timers.StartOnce(5000, () => timer_busStop(player, way, check, mypos), true);
                        foreach (ExtPlayer foreachPlayer in Main.GetPlayersInRadiusOfPosition(mypos, 30))
                        {
                            if (!foreachPlayer.IsCharacterData()) continue;
                            Trigger.SendChatMessage(foreachPlayer, LangFunc.GetText(LangType.Ru, DataName.NextBus, BusWaysNames[way]));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write($"busCheckpointEnterWay Exception: {e.ToString()}");
            }
        }

        /// <summary>
        /// Вызывается из dialogCallback когда игрок ответил на вопрос "продолжить маршрут?"
        /// state = true  → продолжить (начать с check 0)
        /// state = false → закончить работу
        /// </summary>
        [RemoteEvent("dialogCallback")]
        public static void OnDialogCallback(ExtPlayer player, string callback, bool state)
        {
            try
            {
                if (callback != "busRouteEnd") return;

                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                var accountData = player.GetAccountData();
                if (accountData == null) return;
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                // Разморозить в любом случае
                Trigger.ClientEvent(player, "freeze", false);
                sessionData.WorkData.BusOnStop = false;

                if (state)
                {
                    // ── ПРОДОЛЖИТЬ: сбросить маршрут на начало ──────────────────────────
                    int way = sessionData.WorkData.WorkWay;
                    sessionData.WorkData.WorkCheck = 0;

                    Vector3 direction = BusWays[way].Count > 1
                        ? BusWays[way][1].Pos - new Vector3(0, 0, 0.12)
                        : BusWays[way][0].Pos - new Vector3(0, 0, 1.12);
                    Color color = BusWays[way][0].IsStop ? new Color(255, 255, 255) : new Color(255, 0, 0);

                    Trigger.ClientEvent(player, "createCheckpoint", 3, 1,
                        BusWays[way][0].Pos - new Vector3(0, 0, 1.12), 4, 0,
                        color.Red, color.Green, color.Blue, direction);
                    Trigger.ClientEvent(player, "createWaypoint", BusWays[way][0].Pos.X, BusWays[way][0].Pos.Y);
                    Trigger.ClientEvent(player, "createWorkBlip", BusWays[way][0].Pos);

                    Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter,
                        LangFunc.GetText(LangType.Ru, DataName.YouRentBus, BusWaysNames[way]), 5000);
                }
                else
                {
                    // ── ЗАКОНЧИТЬ ────────────────────────────────────────────────────────
                    EndWork(player);
                }
            }
            catch (Exception e)
            {
                Log.Write($"OnDialogCallback (busRouteEnd) Exception: {e.ToString()}");
            }
        }

        private static void timer_busStop(ExtPlayer player, int way, int check, Vector3 mypos)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                var accountData = player.GetAccountData();
                if (accountData == null) return;
                var characterData = player.GetCharacterData();
                if (characterData == null) return;

                if (sessionData.TimersData.BusTimer != null)
                {
                    Timers.Stop(sessionData.TimersData.BusTimer);
                    sessionData.TimersData.BusTimer = null;
                }

                sessionData.WorkData.BusOnStop = false;
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.YouCanGoNext), 3000);

                int payment = Convert.ToInt32(Main.BuswaysPayments[way] * Group.GroupPayAdd[accountData.VipLvl] * Main.ServerSettings.MoneyMultiplier);
                (byte, float) jobLevelInfo = characterData.JobSkills.ContainsKey((int)JobsId.Bus)
                    ? Main.GetPlayerJobLevelBonus((int)JobsId.Bus, characterData.JobSkills[(int)JobsId.Bus])
                    : (0, 1);
                if (jobLevelInfo.Item1 >= 1) payment = Convert.ToInt32(payment * jobLevelInfo.Item2);

                MoneySystem.Wallet.Change(player, payment);
                GameLog.Money($"server", $"player({characterData.UUID})", payment, $"busCheck");

                if (check + 1 != BusWays[way].Count) check++;
                else check = 0;

                Vector3 direction = (check + 1 < BusWays[way].Count)
                    ? BusWays[way][check + 1].Pos - new Vector3(0, 0, 0.12)
                    : BusWays[way][0].Pos - new Vector3(0, 0, 1.12);
                Color color = BusWays[way][check].IsStop ? new Color(255, 255, 255) : new Color(255, 0, 0);

                Trigger.ClientEvent(player, "createCheckpoint", 3, 1, BusWays[way][check].Pos - new Vector3(0, 0, 1.12), 4, 0, color.Red, color.Green, color.Blue, direction);
                Trigger.ClientEvent(player, "createWaypoint", BusWays[way][check].Pos.X, BusWays[way][check].Pos.Y);
                Trigger.ClientEvent(player, "createWorkBlip", BusWays[way][check].Pos);
                Trigger.ClientEvent(player, "freeze", false);
                sessionData.WorkData.WorkCheck = check;

                UpdateBusJobSkill(player, characterData, sessionData, way, payment);

                foreach (ExtPlayer foreachPlayer in Main.GetPlayersInRadiusOfPosition(mypos, 30))
                {
                    if (!foreachPlayer.IsCharacterData()) continue;
                    Notify.Send(foreachPlayer, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.BusStartsWay, BusWaysNames[way]), 1000);
                }
            }
            catch (Exception e)
            {
                Log.Write($"timer_busStop Exception: {e.ToString()}");
            }
        }
        #endregion

        /// <summary>Общая логика прокачки навыка и квеста автобусника</summary>
        private static void UpdateBusJobSkill(ExtPlayer player, CharacterData characterData, SessionData sessionData, int way, int payment)
        {
            if (characterData.JobSkills.ContainsKey((int)JobsId.Bus))
            {
                if (characterData.JobSkills[(int)JobsId.Bus] < 70000)
                    characterData.JobSkills[(int)JobsId.Bus] += 1;
            }
            else
                characterData.JobSkills.Add((int)JobsId.Bus, 1);

            if (qMain.GetQuestsLine(player, Zdobich.QuestName) == (int)zdobich_quests.Stage11)
            {
                sessionData.WorkData.PointsCount += payment;
                if (sessionData.WorkData.PointsCount < qMain.GetQuestsData(player, Zdobich.QuestName, (int)zdobich_quests.Stage11))
                    sessionData.WorkData.PointsCount = qMain.GetQuestsData(player, Zdobich.QuestName, (int)zdobich_quests.Stage11) + payment;

                if (sessionData.WorkData.PointsCount >= 500)
                {
                    qMain.UpdateQuestsStage(player, Zdobich.QuestName, (int)zdobich_quests.Stage11, 1, isUpdateHud: true);
                    qMain.UpdateQuestsComplete(player, Zdobich.QuestName, (int)zdobich_quests.Stage11, true);
                    Trigger.SendChatMessage(player, "!{#fc0}" + LangFunc.GetText(LangType.Ru, DataName.QuestPartComplete));
                }
                else
                {
                    qMain.UpdateQuestsData(player, Zdobich.QuestName, (int)zdobich_quests.Stage11, sessionData.WorkData.PointsCount.ToString());
                    Trigger.SendChatMessage(player, LangFunc.GetText(LangType.Ru, DataName.YouEarnedJob, sessionData.WorkData.PointsCount, 500 - sessionData.WorkData.PointsCount));
                }
            }
        }

        public static bool EndWork(ExtPlayer player)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null) return false;

            if (sessionData.WorkData.OnWork)
            {
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.EndWorkDay), 3000);
                sessionData.WorkData.OnWork = false;
                Trigger.ClientEvent(player, "deleteCheckpoint", 3, 0);
                Trigger.ClientEvent(player, "deleteWorkBlip");
                return true;
            }
            return false;
        }

        public static void StartWork(ExtPlayer player)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null) return;

            var characterData = player.GetCharacterData();
            if (characterData == null) return;

            var ways = new Dictionary<int, int> { { 0, 0 }, { 1, 0 }, { 2, 0 } };
            foreach (ExtPlayer foreachPlayer in Character.Repository.GetPlayers())
            {
                var foreachSessionData = foreachPlayer.GetSessionData();
                if (foreachSessionData == null) continue;
                var foreachCharacterData = foreachPlayer.GetCharacterData();
                if (foreachCharacterData == null) continue;
                if (foreachCharacterData.WorkID != (int)JobsId.Bus || !foreachSessionData.WorkData.OnWork) continue;
                ways[foreachSessionData.WorkData.WorkWay]++;
            }

            int way = -1;
            for (int i = 0; i < ways.Count; i++)
                if (ways[i] == 0) { way = i; break; }
            if (way == -1)
                for (int i = 0; i < ways.Count; i++)
                    if (ways[i] == 1) { way = i; break; }
            if (way == -1) way = 0;

            sessionData.WorkData.OnWork = true;
            sessionData.WorkData.WorkWay = way;
            sessionData.WorkData.WorkCheck = 0;

            Trigger.ClientEvent(player, "createCheckpoint", 3, 1,
                BusWays[way][0].Pos - new Vector3(0, 0, 1.12), 4, 0,
                255, 0, 0, BusWays[way][1].Pos - new Vector3(0, 0, 1.12));
            Trigger.ClientEvent(player, "createWaypoint", BusWays[way][0].Pos.X, BusWays[way][0].Pos.Y);
            Trigger.ClientEvent(player, "createWorkBlip", BusWays[way][0].Pos);
            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter,
                LangFunc.GetText(LangType.Ru, DataName.YouRentBus, BusWaysNames[way]), 10000);
        }

        [ServerEvent(Event.PlayerEnterVehicle)]
        public static void OnPlayerEnterVehicle(ExtPlayer player, ExtVehicle vehicle, sbyte seatId)
        {
            if (seatId == (int)VehicleSeat.Driver) return;

            var sessionData = player.GetSessionData();
            if (sessionData == null) return;

            var characterData = player.GetCharacterData();
            if (characterData == null) return;

            var vehicleLocalData = vehicle.GetVehicleLocalData();
            if (vehicleLocalData == null || vehicleLocalData.WorkId != JobsId.Bus) return;

            if (characterData.Money >= Main.BusPay)
            {
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter,
                    LangFunc.GetText(LangType.Ru, DataName.BusPayed, Main.BusPay), 3000);
                BattlePass.Repository.UpdateReward(player, 63);
                MoneySystem.Wallet.Change(player, -Main.BusPay);

                var fractionData = Fractions.Manager.GetFractionData((int)Fractions.Models.Fractions.CITY);
                if (fractionData != null)
                    fractionData.Money += Main.BusPay;

                GameLog.Money($"player({characterData.UUID})", $"frac(6)", Main.BusPay, $"busPay");
            }
            else
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter,
                    LangFunc.GetText(LangType.Ru, DataName.NoMoney), 3000);
                VehicleManager.WarpPlayerOutOfVehicle(player);
            }
        }

        internal class BusCheck
        {
            public Vector3 Pos { get; }
            public bool IsStop { get; }

            public BusCheck(Vector3 pos, bool isStop = false)
            {
                Pos = pos;
                IsStop = isStop;
            }
        }
    }
}
