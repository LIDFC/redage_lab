using GTANetworkAPI;
using MySqlX.XDevAPI.Relational;
using NeptuneEvo.Character.Models;
using NeptuneEvo.Core;
using NeptuneEvo.EternalDev.MarketPlace.Configs;
using NeptuneEvo.Handles;
using NeptuneEvo.VehicleData.LocalData.Models;
using Redage.SDK;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;

namespace NeptuneEvo.EternalDev.MarketPlace.Methods
{
    public class Testdrive : Script
    {
        [RemoteEvent("server.marketPlace.actions.testdrive")]
        public static void On(ExtPlayer player, int id)
        {
            var marketItem = Manager.GetMarketItem(id);
            if (marketItem is null || marketItem.Type != Enums.LotType.Vehicle)
                return;

            var vehicleData = VehicleManager.GetVehicleToAutoId(Convert.ToInt32(marketItem.Data));
            if (vehicleData is null)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Не удалось получить данные о транспорте {marketItem.Id} (#{marketItem.Data})", 3000);
                return;
            }

            if (Manager.Config.NeedNearestAutoshopForTestdrive && !IsNearAutoshop(player.Position, 10f))
            {
                Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"Вы должны стоять около автосалона", 3000); 
                return;
            }

            var testdrive = GetNearestTestdrivePosition(player.Position);
            if (testdrive is null)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, $"Ошибка получения данных о позициях тестдрайва", 3000);
                return;
            }

            var playerPosition = player.Position;
            if (player.HasData(InteriorPositionConfig.Shared.IN_INTERIOR))
                playerPosition = player.GetData<InteriorPositionConfig>(InteriorPositionConfig.Shared.IN_INTERIOR).ExteriorPosition;

            player.CharacterData.ExteriorPos = playerPosition;

            uint dimension = Dimensions.RequestPrivateDimension(player.Value);
            Trigger.Dimension(player, dimension);
            player.Position = testdrive.Position;

            var vehicle = VehicleStreaming.CreateVehicle(NAPI.Util.GetHashKey(vehicleData.Model), testdrive.Position, testdrive.Heading, 0, 0, "TESTDRIVE", acc: VehicleAccess.AutoRoom, workdriv: player.GetUUID(), petrol: 9999, dimension: dimension, engine: true);
            VehicleManager.SetCustomization(vehicle, vehicleData.Components);

            player.SessionData.TestDriveVehicle = vehicle;

            Trigger.ClientEvent(player, "startTestDrive", vehicle);
            player.SessionData.TimersData.TestDriveTimer = Timers.StartOnce(Manager.Config.TestdriveTime * 1000, () => CarRoom.timer_exitVehicle(player, false), true);
           
            Manager.UnSubscribe(player);
            Notify.Send(player, NotifyType.Info, NotifyPosition.BottomCenter, $"У вас есть {Math.Truncate((decimal)Manager.Config.TestdriveTime / 60)} минут", 3000);
        }

        public static TestdrivePosition GetNearestTestdrivePosition(Vector3 position)
        {
            var result = Manager.Config.TestdrivePositions.OrderBy(data 
                => position.DistanceTo(data.Position))
                .FirstOrDefault();

            return result;
        }

        public static bool IsNearAutoshop(Vector3 position, float range)
        {
            var positions = new List<Vector3>();
            positions.AddRange(BusinessManager.BizList.Values.Where(x => new int[] { 2, 3, 4, 5, 15 }.Contains(x.Type)).Select(x => x.EnterPoint));
            positions.AddRange(Manager.Config.AuctionPositions.Select(x => x.Position));

            var result = positions.OrderBy(pos
                => position.DistanceTo(pos))
                .FirstOrDefault();

            return result.DistanceTo(position) <= range;
        }
    }                                                   
}
