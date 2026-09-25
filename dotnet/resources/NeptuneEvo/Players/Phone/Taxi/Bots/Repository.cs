using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.Accounts;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Handles;
using NeptuneEvo.Jobs.Models;
using NeptuneEvo.MoneySystem;
using NeptuneEvo.Players.Phone.Taxi.Bots.Models;
using NeptuneEvo.VehicleData.LocalData;
using Redage.SDK;

namespace NeptuneEvo.Players.Phone.Taxi.Bots
{
    /// <summary>
    /// Заказы такси от NPC-пассажиров.
    /// Сервер сам создаёт заказ, когда у таксиста нет заказов от игроков,
    /// выбирает точку посадки и точку назначения и проверяет каждый этап поездки.
    /// Клиент отвечает только за визуал (спавн педа, посадка/высадка).
    /// </summary>
    public class Repository : Script
    {
        private static readonly nLog Log = new nLog("Phone.Taxi.Bots");

        // Настройки генерации заказов
        private const int TickIntervalMs = 5000;
        private const int OrderCooldownSeconds = 20;        // пауза между заказами одному таксисту
        private const int OfferLifetimeSeconds = 60;        // сколько заказ висит в списке
        private const int AcceptedLifetimeSeconds = 420;    // сколько можно ехать к пассажиру
        private const int TripLifetimeSeconds = 900;        // сколько можно везти пассажира
        private const float PickupMinDistance = 150f;       // точка посадки от таксиста
        private const float PickupMaxDistance = 1200f;
        private const float DestinationMinDistance = 600f;  // точка назначения от точки посадки
        private const float DestinationMaxDistance = 3000f;
        private const float BoardRadius = 60f;              // допуск сервера на посадку (клиент начинает посадку с 15 м)
        private const float FinishRadius = 35f;             // допуск сервера на прибытие
        private const float MaxAverageSpeed = 45f;          // м/с (~160 км/ч) — защита от телепорта

        // Оплата: дистанция/3, в пределах [MinReward; MaxReward], × VIP × MoneyMultiplier
        public const int MinReward = 150;
        public const int MaxReward = 1700;

        // Ограничения точек: без воды, гор и удалённых районов
        private const float MinPointZ = 2f;
        private const float MaxPointZ = 100f;
        private const float CityMinX = -3000f, CityMaxX = 1600f;
        private const float CityMinY = -3200f, CityMaxY = 900f;

        private static readonly string[] PedModels =
        {
            "a_m_m_business_01",
            "a_m_m_bevhills_01",
            "a_m_m_eastsa_01",
            "a_m_y_business_02",
            "a_m_y_hipster_01",
            "a_f_y_business_02",
            "a_f_y_tourist_01",
            "a_f_m_bevhills_01",
        };

        private static readonly string[] PedNames =
        {
            "Пассажир", "Клиент диспетчера", "Турист", "Бизнесмен", "Студент"
        };

        private static readonly ConcurrentDictionary<int, BotOrder> Orders = new ConcurrentDictionary<int, BotOrder>(); // key = driver UUID
        private static readonly ConcurrentDictionary<int, DateTime> NextOrderTime = new ConcurrentDictionary<int, DateTime>();
        private static readonly Random Rnd = new Random();
        private static int _orderSeq = 0;
        private static List<Vector3> _points = null;

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            Timers.Start("taxiBotOrders", TickIntervalMs, Tick, true);
        }

        #region Точки посадки/назначения

        /// <summary>
        /// Пул точек — места, куда гарантированно подъезжает машина:
        ///  - точки заезда в гаражи домов (там же появляется машина при выезде);
        ///  - точки разгрузки бизнесов (туда заезжают грузовики дальнобойщиков).
        /// Входы в дома не используем: у квартир и мотелей они бывают на этажах и крышах.
        /// Дополнительно отсекаем воду/горы по высоте и выезды за пределы города.
        /// </summary>
        private static List<Vector3> GetPoints()
        {
            if (_points != null && _points.Count > 0)
                return _points;

            var points = new List<Vector3>();

            foreach (var garage in NeptuneEvo.Houses.GarageManager.Garages.Values)
            {
                if (garage?.Position == null)
                    continue;
                points.Add(garage.Position);
            }

            foreach (var biz in BusinessManager.BizList.Values)
            {
                if (biz?.UnloadPoint == null || (biz.UnloadPoint.X == 0 && biz.UnloadPoint.Y == 0))
                    continue;
                points.Add(biz.UnloadPoint);
            }

            _points = points
                .Where(p => p.Z >= MinPointZ && p.Z <= MaxPointZ)
                .Where(p => p.X >= CityMinX && p.X <= CityMaxX && p.Y >= CityMinY && p.Y <= CityMaxY)
                .ToList();

            Log.Write($"Taxi bot points: {_points.Count}");
            return _points;
        }

        private static Vector3 PickPoint(Vector3 from, float minDist, float maxDist)
        {
            var candidates = GetPoints()
                .Where(p =>
                {
                    var d = p.DistanceTo2D(from);
                    return d >= minDist && d <= maxDist;
                })
                .ToList();

            if (candidates.Count == 0)
                return null;

            return candidates[Rnd.Next(candidates.Count)];
        }

        #endregion

        #region Проверки

        private static bool IsTaxiDriverOnShift(ExtPlayer player, bool mustBeInVehicle)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null)
                return false;

            var characterData = player.GetCharacterData();
            if (characterData == null)
                return false;

            if (characterData.WorkID != (int)JobsId.Taxi || !sessionData.WorkData.OnWork)
                return false;

            if (!mustBeInVehicle)
                return true;

            if (!player.IsInVehicle || player.VehicleSeat != (int)VehicleSeat.Driver)
                return false;

            var vehicle = (ExtVehicle)player.Vehicle;
            var vehicleLocalData = vehicle?.GetVehicleLocalData();
            return vehicleLocalData != null && vehicleLocalData.WorkId == JobsId.Taxi && vehicleLocalData.WorkDriver == characterData.UUID;
        }

        private static bool IsBusyWithPlayerOrder(ExtPlayer player)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null)
                return true;

            if (sessionData.TaxiData.Passager != null)
                return true;

            return Phone.Taxi.Repository.OrdersList.Any(o => o.Driver == player);
        }

        #endregion

        #region Генерация заказов

        private static void Tick()
        {
            try
            {
                var now = DateTime.Now;

                // Чистим просроченные заказы
                foreach (var order in Orders.Values.ToList())
                {
                    var driver = Main.GetPlayerByUUID(order.DriverUUID);
                    if (!driver.IsCharacterData() || !IsTaxiDriverOnShift(driver, false))
                    {
                        Remove(order.DriverUUID, driver, false);
                        continue;
                    }

                    var expired =
                        (order.Stage == BotOrderStage.Offered && now > order.CreatedAt.AddSeconds(OfferLifetimeSeconds)) ||
                        (order.Stage == BotOrderStage.Accepted && now > order.AcceptedAt.AddSeconds(AcceptedLifetimeSeconds)) ||
                        (order.Stage == BotOrderStage.Boarded && now > order.BoardedAt.AddSeconds(TripLifetimeSeconds));

                    if (expired)
                    {
                        if (order.Stage != BotOrderStage.Offered)
                            Notify.Send(driver, NotifyType.Warning, NotifyPosition.BottomCenter, "Пассажир не дождался и отменил заказ.", 5000);
                        Remove(order.DriverUUID, driver, true);
                    }
                }

                // Если есть свободные заказы от живых игроков — NPC-заказы не создаём
                if (Phone.Taxi.Repository.OrdersList.Any(o => o.Driver == null && o.Player.IsCharacterData()))
                    return;

                foreach (var driver in Phone.Taxi.Repository.GetListTaxi())
                {
                    var characterData = driver.GetCharacterData();
                    if (characterData == null)
                        continue;

                    if (Orders.ContainsKey(characterData.UUID))
                        continue;

                    if (NextOrderTime.TryGetValue(characterData.UUID, out var nextTime) && now < nextTime)
                        continue;

                    if (!IsTaxiDriverOnShift(driver, false) || IsBusyWithPlayerOrder(driver))
                        continue;

                    CreateOrder(driver, characterData.UUID);
                }
            }
            catch (Exception e)
            {
                Log.Write($"Tick Exception: {e}");
            }
        }

        private static void CreateOrder(ExtPlayer driver, int driverUUID)
        {
            var pickup = PickPoint(driver.Position, PickupMinDistance, PickupMaxDistance);
            if (pickup == null)
            {
                NextOrderTime[driverUUID] = DateTime.Now.AddSeconds(OrderCooldownSeconds);
                return;
            }

            var destination = PickPoint(pickup, DestinationMinDistance, DestinationMaxDistance);
            if (destination == null)
            {
                NextOrderTime[driverUUID] = DateTime.Now.AddSeconds(OrderCooldownSeconds);
                return;
            }

            var order = new BotOrder
            {
                Id = -100000 - System.Threading.Interlocked.Increment(ref _orderSeq),
                DriverUUID = driverUUID,
                Name = $"{PedNames[Rnd.Next(PedNames.Length)]} (NPC)",
                Model = PedModels[Rnd.Next(PedModels.Length)],
                PickupPos = pickup,
                PickupHeading = (float)Rnd.Next(0, 360),
                DestinationPos = destination,
            };

            Orders[driverUUID] = order;

            Trigger.ClientEvent(driver, "client.phone.taxijob.botAdd", order.Id, order.Name, pickup.X, pickup.Y, pickup.Z);
        }

        private static void Remove(int driverUUID, ExtPlayer driver, bool withCooldown)
        {
            if (!Orders.TryRemove(driverUUID, out var order))
                return;

            if (withCooldown)
                NextOrderTime[driverUUID] = DateTime.Now.AddSeconds(OrderCooldownSeconds);

            if (driver.IsCharacterData())
                Trigger.ClientEvent(driver, "client.phone.taxijob.botReset", order.Id);
        }

        #endregion

        #region Этапы поездки (вызываются с клиента)

        public static bool IsBotOrderId(int id) => id <= -100000;

        public static void OnTake(ExtPlayer player, int id)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null)
                return;

            if (!Orders.TryGetValue(characterData.UUID, out var order) || order.Id != id || order.Stage != BotOrderStage.Offered)
            {
                Trigger.ClientEvent(player, "client.phone.taxijob.dell", id);
                return;
            }

            if (!IsTaxiDriverOnShift(player, true))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Чтобы принять заказ, сядьте за руль рабочего такси.", 3000);
                return;
            }

            if (IsBusyWithPlayerOrder(player))
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Сначала завершите текущий заказ.", 3000);
                return;
            }

            order.Stage = BotOrderStage.Accepted;
            order.AcceptedAt = DateTime.Now;

            Trigger.ClientEvent(player, "client.phone.taxijob.botAccepted", order.Id, order.Name, order.Model,
                order.PickupPos.X, order.PickupPos.Y, order.PickupPos.Z, order.PickupHeading);
        }

        public static void OnBoarded(ExtPlayer player, int id)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null)
                return;

            if (!Orders.TryGetValue(characterData.UUID, out var order) || order.Id != id || order.Stage != BotOrderStage.Accepted)
                return;

            if (!IsTaxiDriverOnShift(player, true) || player.Position.DistanceTo2D(order.PickupPos) > BoardRadius)
                return;

            order.Stage = BotOrderStage.Boarded;
            order.BoardedAt = DateTime.Now;

            Trigger.ClientEvent(player, "client.phone.taxijob.botDestination", order.Id,
                order.DestinationPos.X, order.DestinationPos.Y, order.DestinationPos.Z);
        }

        public static void OnFinish(ExtPlayer player, int id)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null)
                return;

            var accountData = player.GetAccountData();
            if (accountData == null)
                return;

            var characterData = player.GetCharacterData();
            if (characterData == null)
                return;

            if (!Orders.TryGetValue(characterData.UUID, out var order) || order.Id != id || order.Stage != BotOrderStage.Boarded)
                return;

            if (!IsTaxiDriverOnShift(player, true))
                return;

            if (player.Position.DistanceTo2D(order.DestinationPos) > FinishRadius)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, "Подъезжайте ближе к точке назначения.", 3000);
                return;
            }

            var distance = order.PickupPos.DistanceTo2D(order.DestinationPos);
            var minSeconds = distance / MaxAverageSpeed;
            if ((DateTime.Now - order.BoardedAt).TotalSeconds < minSeconds)
            {
                Trigger.SendToAdmins(1, $"[TAXI-NPC] {player.Name} ({player.Value}) доставил NPC-пассажира слишком быстро ({distance:0} м).");
                Remove(characterData.UUID, player, true);
                return;
            }

            Orders.TryRemove(characterData.UUID, out _);
            NextOrderTime[characterData.UUID] = DateTime.Now.AddSeconds(OrderCooldownSeconds);

            var baseReward = Math.Clamp((int)(distance / 3f), MinReward, MaxReward);
            var reward = Convert.ToInt32(baseReward * Group.GroupPayAdd[accountData.VipLvl] * Main.ServerSettings.MoneyMultiplier);

            Wallet.Change(player, reward);
            GameLog.Money("server", $"player({characterData.UUID})", reward, "taxiBotRide");

            if (characterData.JobSkills.ContainsKey((int)JobsId.Taxi))
            {
                if (characterData.JobSkills[(int)JobsId.Taxi] < 1000)
                    characterData.JobSkills[(int)JobsId.Taxi] += 1;
            }
            else characterData.JobSkills.Add((int)JobsId.Taxi, 1);

            BattlePass.Repository.UpdateReward(player, 59);
            BattlePass.Repository.UpdateReward(player, 2);
            BattlePass.Repository.UpdateReward(player, 159);

            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Поездка выполнена. Вы заработали ${Wallet.Format(reward)}", 4000);
            Trigger.ClientEvent(player, "client.phone.taxijob.botFinished", order.Id);
        }

        public static void OnCancel(ExtPlayer player, int id)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null)
                return;

            if (!Orders.TryGetValue(characterData.UUID, out var order) || order.Id != id)
            {
                Trigger.ClientEvent(player, "client.phone.taxijob.botReset", id);
                return;
            }

            Remove(characterData.UUID, player, true);
        }

        /// <summary>
        /// Конец смены / выход с сервера.
        /// </summary>
        public static void OnEndWork(ExtPlayer player)
        {
            var characterData = player.GetCharacterData();
            if (characterData == null)
                return;

            Remove(characterData.UUID, player, false);
            NextOrderTime.TryRemove(characterData.UUID, out _);
        }

        #endregion
    }
}
