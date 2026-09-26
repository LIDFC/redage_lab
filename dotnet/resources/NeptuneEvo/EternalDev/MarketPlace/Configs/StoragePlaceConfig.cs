using GTANetworkAPI;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Configs
{
    public class StoragePlaceConfig
    {
        /// <summary>
        /// Позиция
        /// </summary>
        [JsonProperty("position")]
        public Vector3 Position { get; set; }

        /// <summary>
        /// Радиус зоны
        /// </summary>
        [JsonProperty("range")]
        public float Range { get; set; }

        /// <summary>
        /// Позиция текста
        /// </summary>
        [JsonProperty("label_position")]
        public Vector3 LabelPosition { get; set; }  

        public StoragePlaceConfig(Vector3 position, float range, Vector3 labelPosition)
        {
            Position = position;
            Range = range;
            LabelPosition = labelPosition;
        }

        public void GTAElements()
        {
            NAPI.TextLabel.CreateTextLabel("Склад хранения", LabelPosition, 50f, 5f, 0, new Color(255, 255, 255, 200), false, 0);
            CustomColShape.CreateSphereColShape(Position, Range, 0, ColShapeEnums.MarketPlaceStorage);
        }

        [Interaction(ColShapeEnums.MarketPlaceStorage)]
        public static void OnInteraction(ExtPlayer player)
        {
            Chars.Repository.LoadOtherItemsData(player, "marketStorage", player.GetUUID().ToString(), 13, Manager.Config.MaxSlotsInStorage, "", false);
        }
    }
}
