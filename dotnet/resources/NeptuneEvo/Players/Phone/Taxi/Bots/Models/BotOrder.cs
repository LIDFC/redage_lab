using System;
using GTANetworkAPI;

namespace NeptuneEvo.Players.Phone.Taxi.Bots.Models
{
    public enum BotOrderStage
    {
        Offered = 0,  // заказ висит в списке у таксиста
        Accepted,     // таксист принял заказ и едет к пассажиру
        Boarded       // пассажир в машине, едем к точке назначения
    }

    public class BotOrder
    {
        public int Id;
        public int DriverUUID;
        public string Name;
        public string Model;
        public Vector3 PickupPos;
        public float PickupHeading;
        public Vector3 DestinationPos;
        public BotOrderStage Stage = BotOrderStage.Offered;
        public DateTime CreatedAt = DateTime.Now;
        public DateTime AcceptedAt;
        public DateTime BoardedAt;
    }
}
