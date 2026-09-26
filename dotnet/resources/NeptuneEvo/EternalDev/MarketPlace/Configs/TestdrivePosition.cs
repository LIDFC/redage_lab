using GTANetworkAPI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Configs
{
    public class TestdrivePosition
    {
        /// <summary>
        /// Позиция
        /// </summary>
        [JsonProperty("position")]
        public Vector3 Position { get; set; }

        /// <summary>
        /// Поворот
        /// </summary>
        [JsonProperty("heading")]
        public float Heading { get; set; }

        public TestdrivePosition(Vector3 position, float heading)
        {
            Position = position;
            Heading = heading;
        }
    }
}
