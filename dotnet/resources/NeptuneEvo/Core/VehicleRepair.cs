using System;
using System.Collections.Generic;
using GTANetworkAPI;
using Localization;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Handles;
using NeptuneEvo.Players;
using NeptuneEvo.VehicleData.LocalData;
using NeptuneEvo.VehicleData.LocalData.Models;
using NeptuneEvo.Character;
using NeptuneEvo.VehicleModel;
using Redage.SDK;

namespace NeptuneEvo.Core
{
    /// <summary>
    /// Ремонт машины через G → «Машина» → «Починить машину» (капот должен быть открыт).
    ///  - есть ключ (Wrench, в руке или в инвентаре) — 15 секунд анимации, ключ расходуется;
    ///  - ключа нет — мини-игра HotWire «соедините провода» (CEF PlayerHotWire, клиент src_client/vehicle/hotwire.js,
    ///    источник: github.com/NikaKondr/hotwire, MIT). Прошёл — машина починена.
    /// </summary>
    public class VehicleRepair : Script
    {
        private static readonly nLog Log = new nLog("Core.VehicleRepair");

        private const float MaxDistance = 3.5f;
        private const int KitRepairMs = 15000;
        private const int MinGameSeconds = 3;        // быстрее не пройти — защита от подделки события
        private const int GameCooldownSeconds = 30;  // между попытками мини-игры

        private class HotWireSession
        {
            public ExtVehicle Vehicle;
            public DateTime StartedAt;
        }

        private static readonly Dictionary<ExtPlayer, HotWireSession> Sessions = new Dictionary<ExtPlayer, HotWireSession>();
        private static readonly Dictionary<ExtPlayer, DateTime> LastGame = new Dictionary<ExtPlayer, DateTime>();

        public static void Start(ExtPlayer player, ExtVehicle vehicle)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null || vehicle == null || vehicle.GetVehicleLocalData() == null)
                    return;

                if (player.Position.DistanceTo(vehicle.Position) > MaxDistance)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.CarTooFar), 3000);
                    return;
                }

                if (VehicleStreaming.GetDoorState(vehicle, DoorId.DoorHood) != DoorState.DoorOpen)
                {
                    Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "Сначала откройте капот: G → Машина → Открыть/Закрыть капот", 4000);
                    return;
                }

                if (Sessions.ContainsKey(player))
                    return;

                var wrench = Chars.Repository.isItem(player, "inventory", ItemId.Wrench);
                if (wrench != null)
                    StartKitRepair(player, vehicle);
                else
                    StartHotWire(player, vehicle);
            }
            catch (Exception e)
            {
                Log.Write($"Start Exception: {e}");
            }
        }

        #region Ключ
        private static void StartKitRepair(ExtPlayer player, ExtVehicle vehicle)
        {
            var sessionData = player.GetSessionData();

            player.Rotation = new Vector3(player.Rotation.X, player.Rotation.Y, player.Rotation.Z - 180);
            Trigger.ClientEvent(player, "blockMove", true);
            Main.OnAntiAnim(player);
            Trigger.PlayAnimation(player, "mini@repair", "fixing_a_ped", 39);
            Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.StartRepairing, vehicle.NumberPlate), 5000);

            NAPI.Task.Run(() =>
            {
                try
                {
                    if (!player.IsCharacterData()) return;
                    Main.OffAntiAnim(player);
                    Trigger.ClientEvent(player, "blockMove", false);
                    Trigger.StopAnimation(player);

                    if (vehicle == null || !vehicle.Exists || player.Position.DistanceTo(vehicle.Position) > MaxDistance)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.CarTooFar), 3000);
                        return;
                    }

                    var wrench = Chars.Repository.isItem(player, "inventory", ItemId.Wrench);
                    if (wrench == null)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.MustWrench), 3000);
                        return;
                    }

                    // Если ключ был в руке — сначала убираем его из рук
                    if (wrench.Location == "fastSlots" && sessionData.ActiveWeap.Index == wrench.Index)
                        sessionData.ActiveWeap = new ItemStruct("", -1, null);

                    Chars.Repository.RemoveIndex(player, wrench.Location, wrench.Index);
                    FinishRepair(player, vehicle);
                }
                catch (Exception e)
                {
                    Log.Write($"StartKitRepair Task Exception: {e}");
                }
            }, KitRepairMs);
        }
        #endregion

        #region HotWire
        private static void StartHotWire(ExtPlayer player, ExtVehicle vehicle)
        {
            if (LastGame.TryGetValue(player, out var last) && (DateTime.Now - last).TotalSeconds < GameCooldownSeconds)
            {
                var wait = GameCooldownSeconds - (int)(DateTime.Now - last).TotalSeconds;
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Попробовать снова можно через {wait} сек. Или купите ключ в 24/7", 3000);
                return;
            }

            Sessions[player] = new HotWireSession { Vehicle = vehicle, StartedAt = DateTime.Now };
            LastGame[player] = DateTime.Now;

            Main.OnAntiAnim(player);
            Trigger.ClientEvent(player, "blockMove", true);
            Trigger.PlayAnimation(player, "mini@repair", "fixing_a_ped", 39);
            Trigger.ClientEvent(player, "client.hotwire.open");
            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, "Ключа нет — попробуйте починить проводку вручную", 4000);
        }

        private static void StopHotWire(ExtPlayer player)
        {
            Sessions.Remove(player);
            if (!player.IsCharacterData()) return;
            Main.OffAntiAnim(player);
            Trigger.ClientEvent(player, "blockMove", false);
            Trigger.StopAnimation(player);
        }

        [RemoteEvent("server.hotwire.exit")]
        public static void OnHotWireExit(ExtPlayer player)
        {
            if (!Sessions.ContainsKey(player)) return;
            StopHotWire(player);
            Notify.Send(player, NotifyType.Warning, NotifyPosition.BottomCenter, "Ремонт прерван", 2500);
        }

        [RemoteEvent("server.hotwire.finished")]
        public static void OnHotWireFinished(ExtPlayer player)
        {
            try
            {
                if (!Sessions.TryGetValue(player, out var session)) return;
                StopHotWire(player);

                if ((DateTime.Now - session.StartedAt).TotalSeconds < MinGameSeconds)
                    return;

                var vehicle = session.Vehicle;
                if (vehicle == null || !vehicle.Exists || player.Position.DistanceTo(vehicle.Position) > MaxDistance)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.CarTooFar), 3000);
                    return;
                }

                FinishRepair(player, vehicle);
            }
            catch (Exception e)
            {
                Log.Write($"OnHotWireFinished Exception: {e}");
            }
        }

        [ServerEvent(Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(ExtPlayer player, DisconnectionType type, string reason)
        {
            Sessions.Remove(player);
            LastGame.Remove(player);
        }
        #endregion

        private static void FinishRepair(ExtPlayer player, ExtVehicle vehicle)
        {
            VehicleManager.RepairCar(vehicle);
            NAPI.Entity.SetEntityPosition(vehicle, vehicle.Position + new Vector3(0, 0, 0.5f));
            NAPI.Entity.SetEntityRotation(vehicle, new Vector3(0, 0, vehicle.Rotation.Z));
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.SucRepair), 3000);
            BattlePass.Repository.UpdateReward(player, 12);
        }
    }
}
