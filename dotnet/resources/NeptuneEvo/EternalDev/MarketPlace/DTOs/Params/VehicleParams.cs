using NeptuneEvo.Core;
using NeptuneEvo.VehicleModel;
using Newtonsoft.Json;
using GTANetworkAPI;
using System;

namespace NeptuneEvo.EternalDev.MarketPlace.DTOs.Params
{
    public class VehicleParams : ParamsBase
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        public VehicleParams(string data) : base(data)
        {
            Id = Convert.ToInt32(data);

            var vehicleData = VehicleManager.GetVehicleToAutoId(Id);
            if (vehicleData == null)
                return;

            Model = vehicleData.Model;

            ModelName = vMain.GetName(NAPI.Util.GetHashKey(Model));
            if (ModelName is null)
                ModelName = Model;

            NumberPlate = vehicleData.Number;

            var productData = BusinessManager.GetBusProductData(Model);
            Cost = productData is null ? 0 : productData.Price;
            MotorShow = GetMotorShowName(Model);
            Mileage = 0;
            Capacity = vMain.GetMaxSlots(NAPI.Util.GetHashKey(Model));
            Gas = "Regular"; // в проекте один тип топлива
            Tuning = new TuningData()
            {
                Engine = vehicleData.Components.Engine + 1,
                Brakes = vehicleData.Components.Brakes + 1,
                Transmission = vehicleData.Components.Transmission + 1,
                Turbo = vehicleData.Components.Turbo + 1,
            };
        }

        // В каком автосалоне продаётся модель (в оригинале — vMain.GetVehicleMotorShow, которого нет в этой сборке)
        private static readonly string[] MotorShowNames = { "Премиум", "Люкс", "Эконом", "Мотосалон", "Элитный" };
        private static string GetMotorShowName(string model)
        {
            for (int i = 0; i < BusinessManager.CarsNames.Length && i < MotorShowNames.Length; i++)
            {
                if (BusinessManager.CarsNames[i] != null && System.Linq.Enumerable.Any(BusinessManager.CarsNames[i], m => m.Equals(model, StringComparison.OrdinalIgnoreCase)))
                    return MotorShowNames[i];
            }
            var product = BusinessManager.GetBusProductData(model);
            if (product != null && product.Type == BusinessManager.BusProductToType.Donate) return "Донат-салон";
            if (product != null && product.Type == BusinessManager.BusProductToType.Elite) return "Элитный";
            return "—";
        }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("modelName")]
        public string ModelName { get; set; }

        [JsonProperty("numberPlate")]
        public string NumberPlate { get; set; }

        [JsonProperty("cost")]
        public int Cost { get; set; }

        [JsonProperty("motorShow")]
        public string MotorShow { get; set; }

        [JsonProperty("mileage")]
        public int Mileage { get; set; }

        [JsonProperty("capacity")]
        public int Capacity { get; set; }

        [JsonProperty("gas")]
        public string Gas { get; set; }

        [JsonProperty("tuning")]
        public TuningData Tuning { get; set; }

        public class TuningData
        {
            [JsonProperty("engine")]
            public int Engine { get; set; }

            [JsonProperty("brakes")]
            public int Brakes { get; set; }

            [JsonProperty("transmission")]
            public int Transmission { get; set; }

            [JsonProperty("turbo")]
            public int Turbo { get; set; }
        }
    }
}
