using GTANetworkAPI;
using GTANetworkMethods;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeptuneEvo.EternalDev.MarketPlace.Configs
{
    public class InteriorPositionConfig
    {
        public class Shared
        {
            public static string IN_INTERIOR => "MARKETPLACE::IN:INTERIOR";
        }

        /// <summary>
        /// Название блипа
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Спрайт блипа
        /// </summary>
        [JsonProperty("blip_sprite")]
        public uint BlipSprite { get; set; }

        /// <summary>
        /// Цвет блипа
        /// </summary>
        [JsonProperty("blip_color")]
        public byte BlipColor { get; set; }

        /// <summary>
        /// Размер блипа
        /// </summary>
        [JsonProperty("blip_scale")]
        public float BlipScale { get; set; }

        /// <summary>
        /// Позиция снаружи
        /// </summary>
        [JsonProperty("exterior_position")]
        public Vector3 ExteriorPosition { get; set; }

        /// <summary>
        /// Позиция интерьера
        /// </summary>
        [JsonProperty("interior_position")]
        public Vector3 InteriorPosition { get; set; }

        /// <summary>
        /// Цвет маркера
        /// </summary>
        [JsonProperty("marker_color")]
        public Color MarkerColor { get; set; }

        /// <summary>
        /// Размер маркера
        /// </summary>
        [JsonProperty("marker_scale")]
        public float MarkerScale { get; set; }

        /// <summary>
        /// Измерение интерьера
        /// </summary>
        [JsonProperty("dimension")]
        public uint Dimension { get; set; }

        public InteriorPositionConfig(string name, uint blipSprite, byte blipColor, float blipScale, Vector3 exteriorPosition, Vector3 interiorPosition, Color markerColor, float markerScale, uint dimension)
        {
            Name = name;
            BlipSprite = blipSprite;
            BlipColor = blipColor;
            BlipScale = blipScale;
            ExteriorPosition = exteriorPosition;
            InteriorPosition = interiorPosition;
            MarkerColor = markerColor;
            MarkerScale = markerScale;
            Dimension = dimension;
        }

        public void GTAElements()
        {
            NAPI.Blip.CreateBlip(BlipSprite, ExteriorPosition, BlipScale, BlipColor, Name, 255, 0, true, 0, 0);

            void CreateColshape(Vector3 position, bool isExterior)
            {
                var dimension = isExterior ? 0 : Dimension;
                var colShape = CustomColShape.CreateCylinderColShape(position, 2, 2, dimension, ColShapeEnums.MarketPlaceInterior, isExterior ? 1 : 0);
                colShape.OnEntityEnterColShape += (s, e) => e.SetData(nameof(InteriorPositionConfig), this);
                colShape.OnEntityExitColShape += (s, e) => e.SetData(nameof(InteriorPositionConfig), this);

                NAPI.Marker.CreateMarker(MarkerType.VerticalCylinder, position - new Vector3(0, 0, 1), new Vector3(), new Vector3(), MarkerScale, MarkerColor, false, dimension);
            }

            CreateColshape(ExteriorPosition, true);
            CreateColshape(InteriorPosition, false);
        }

        public void Interaction(ExtPlayer player, bool isExterior)
        {
            var position = InteriorPosition;
            var dimension = Dimension;

            if (!isExterior)
            {
                position = ExteriorPosition;
                dimension = 0;
                player.ResetData(Shared.IN_INTERIOR);

                // Вышел на улицу — точка возврата больше не нужна
                player.CharacterData.ExteriorPos = new Vector3();
            }
            else
            {
                player.SetData(Shared.IN_INTERIOR, this);
                // Если выйдет из игры внутри — появится у входа (у автора было наоборот)
                player.CharacterData.ExteriorPos = ExteriorPosition;
            }

            player.Position = position;
            player.Dimension = dimension;
        }

        [Interaction(ColShapeEnums.MarketPlaceInterior)]
        public static void OnInteraction(ExtPlayer player, int type)
        {
            if (!player.HasData(nameof(InteriorPositionConfig)))
                return;

            player.GetData<InteriorPositionConfig>(nameof(InteriorPositionConfig))?
                .Interaction(player, type == 1);
        }
    }
}
