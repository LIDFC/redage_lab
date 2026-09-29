using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Chars
{
    /// <summary>
    /// Названия для кастомной одежды: у неё нет GXT-меток, и магазины показывают только номер.
    /// settings/clothesNames.json: { "Male_Tops": { "454": "Футболка U.S. Army" } } — ключ как у json/clothes_{ключ}.json.
    /// Название попадает в поле Name сгенерированного JSON, клиент показывает «Название [id]».
    /// </summary>
    partial class ClothesComponents
    {
        private const string NamesPath = "settings/clothesNames.json";
        private static Dictionary<string, Dictionary<int, string>> _clothesNames = new Dictionary<string, Dictionary<int, string>>();

        private static Dictionary<string, Dictionary<int, string>> DefaultClothesNames() => new Dictionary<string, Dictionary<int, string>>
        {
            {
                "Male_Tops", new Dictionary<int, string>
                {
                    { 453, "Куртка Highway Patrol" },
                    { 454, "Футболка U.S. Army" },
                }
            },
        };

        private static void LoadClothesNames()
        {
            try
            {
                if (File.Exists(NamesPath))
                    _clothesNames = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<int, string>>>(File.ReadAllText(NamesPath))
                                    ?? new Dictionary<string, Dictionary<int, string>>();
                else
                {
                    _clothesNames = DefaultClothesNames();
                    File.WriteAllText(NamesPath, JsonConvert.SerializeObject(_clothesNames, Formatting.Indented));
                }
            }
            catch (Exception e)
            {
                Log.Write($"LoadClothesNames Exception: {e.Message}");
            }
        }

        private static string ClothesName(string jsonName, int id) =>
            _clothesNames.TryGetValue(jsonName, out var names) && names.TryGetValue(id, out var name) ? name : null;
    }
}
