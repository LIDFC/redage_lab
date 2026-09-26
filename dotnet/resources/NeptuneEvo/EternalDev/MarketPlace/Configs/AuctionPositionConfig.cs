using GTANetworkAPI;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Configs
{
    public class AuctionPositionConfig
    {
        /// <summary>
        /// Позиция
        /// </summary>
        [JsonProperty("position")]
        public Vector3 Position { get; set; }

        /// <summary>
        /// Радиус
        /// </summary>
        [JsonProperty("range")]
        public float Range { get; set; }

        /// <summary>
        /// Измерение
        /// </summary>
        [JsonProperty("dimension")]
        public uint Dimension { get; set; }

        public AuctionPositionConfig(Vector3 position, float range, uint dimension)
        {
            Position = position;
            Range = range;
            Dimension = dimension;
        }

        public void GTAElements()
        {
            CustomColShape.CreateSphereColShape(Position, Range, Dimension, ColShapeEnums.MarketPlaceAuction);
            NAPI.Marker.CreateMarker(MarkerType.VerticalCylinder, Position - new Vector3(0, 0, 1), new Vector3(), new Vector3(), 1f, new Color(52, 152, 219, 100), false, Dimension);
            NAPI.TextLabel.CreateTextLabel("~b~Торговая площадка\n~w~Нажмите «Взаимодействие»", Position + new Vector3(0, 0, 0.6), 6f, 0.4f, 0, new Color(255, 255, 255), true, Dimension);
            // Точка на улице (без интерьера) — ставим метку на карте
            if (Dimension == 0)
                NAPI.Blip.CreateBlip(525, Position, 0.65f, 4, "Торговая площадка", 255, 0, true, 0, 0);
        }

        [Interaction(ColShapeEnums.MarketPlaceAuction)]
        public static void OnInteraction(ExtPlayer player)
        {
            Manager.Subscribe(player);
        }
    }
}
