using System;
using System.Collections.Generic;
using GTANetworkAPI;
using NeptuneEvo.Handles;
using Newtonsoft.Json;
using NeptuneEvo.Core;
using Redage.SDK;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Database;
using LinqToDB;
using Localization;
using MySqlConnector;
using NeptuneEvo.Accounts;
using NeptuneEvo.Players.Models;
using NeptuneEvo.Players;
using NeptuneEvo.Character.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Functions;
using NeptuneEvo.Quests.Models;

namespace NeptuneEvo.Houses
{
    public class HouseFurniture
    {
        public string Name { get; }
        public string Model { get; }
        public int Id { get; }
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public bool IsSet { get; set; }

        [JsonIgnore]
        public GTANetworkAPI.Object obj { get; private set; }

        public HouseFurniture(int id, string name, string model)
        {
            Name = name;
            Model = model;
            Id = id;
            IsSet = false;
        }

        public GTANetworkAPI.Object Create(uint Dimension)
        {
            try
            {
                obj = NAPI.Object.CreateObject(NAPI.Util.GetHashKey(Model), Position, Rotation, 255, Dimension);
                Selecting.Objects.TryAdd(obj.Id, new Selecting.ObjData
                {
                    Type = (Name.Equals("Оружейный сейф") ? "WeaponSafe" : 
                            Name.Equals("Шкаф с одеждой") ? "ClothesSafe" : 
                            Name.Equals("Взломостойкий сейф") ? "BurglarProofSafe" :
                            Name.Equals("Шкаф с предметами") ? "SubjectSafe" :
                            "InteriorItem"),
                    entity = obj,

                });
                return obj;
            }
            catch (Exception e)
            {
                FurnitureManager.Log.Write($"Create Exception: {e.ToString()}");
                return null;
            }
        }
    }

    public class ShopFurnitureBuy
    {
        public string Prop { get; }
        public string Type { get; }
        public int Price;
        public Dictionary<ItemId, int> Items { get; }

        public ShopFurnitureBuy(string prop, string type, int price, Dictionary<ItemId, int> items)
        {
            Prop = prop;
            Type = type;
            Price = price;
            Items = items;
        }
    }
    class FurnitureManager : Script
    {
        public static readonly nLog Log = new nLog("Houses.HouseFurniture");
        public static Dictionary<int, Dictionary<int, HouseFurniture>> HouseFurnitures = new Dictionary<int, Dictionary<int, HouseFurniture>>();
        public static string QuestName = "npc_furniture";
        public static Vector3 FurnitureBuyPos = new Vector3(-591.12317, -285.2158, 35.45478);
        public static void Init()
        {
            try
            {
                using MySqlCommand cmd = new MySqlCommand
                {
                    CommandText = "SELECT * FROM `furniture`"
                };

                using DataTable result = MySQL.QueryRead(cmd);
                if (result == null || result.Rows.Count == 0)
                {
                    Log.Write("DB return null result.", nLog.Type.Warn);
                    return;
                }
                int id = 0;
                string furniture;
                foreach (DataRow Row in result.Rows)
                {
                    try
                    {
                        id = Convert.ToInt32(Row["uuid"].ToString());
                        furniture = Row["furniture"].ToString();
                        Dictionary<int, HouseFurniture> furnitures;
                        if (string.IsNullOrEmpty(furniture)) furnitures = new Dictionary<int, HouseFurniture>();
                        else furnitures = JsonConvert.DeserializeObject<Dictionary<int, HouseFurniture>>(furniture);
                        HouseFurnitures[id] = furnitures;
                    }
                    catch (Exception e)
                    {
                        Log.Write($"FurnitureManager Foreach Exception: {e.ToString()}");
                    }
                }
                Log.Write($"Loaded {HouseFurnitures.Count} players furnitures.", nLog.Type.Success);
                
                Main.CreateBlip(new Main.BlipData(566, "Мебельный магазин",FurnitureBuyPos, 30, true));
                PedSystem.Repository.CreateQuest("s_m_y_airworker", FurnitureBuyPos, -64.57715f, questName: QuestName, title: "~y~NPC~w~ Иван\nПродавец мебели", colShapeEnums: ColShapeEnums.FurnitureBuy);
            }
            catch (Exception e)
            {
                Log.Write($"FurnitureManager Exception: {e.ToString()}");
            }
        }
        [Interaction(ColShapeEnums.FurnitureBuy)]
        private static void Open(ExtPlayer player, int index)
        {
            var sessionData = player.GetSessionData();
            if (sessionData == null) return;
            if (!player.IsCharacterData()) return;
            if (sessionData.CuffedData.Cuffed)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.IsCuffed), 3000);
                return;
            }
            if (sessionData.DeathData.InDeath)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.IsDying), 3000);
                return;
            }
            if (Main.IHaveDemorgan(player, true)) return;

            player.SelectQuest(new PlayerQuestModel(QuestName, 0, 0, false, DateTime.Now));
            Trigger.ClientEvent(player, "client.quest.open", index, QuestName, 0, 0, 0);
        }
        public static Dictionary<string, ShopFurnitureBuy> NameModels = new Dictionary<string, ShopFurnitureBuy>()
		{
			{ "Оружейный сейф", new ShopFurnitureBuy("prop_ld_int_safe_01", "Хранилища", 0, new Dictionary<ItemId, int>()
				{
					{ ItemId.Iron, 200 },
					{ ItemId.Ruby, 5 },
					{ ItemId.Gold, 5 },
				}
			) },
			{ "Шкаф с одеждой", new ShopFurnitureBuy("bkr_prop_biker_garage_locker_01", "Хранилища", 1, new Dictionary<ItemId, int>()
			{
				{ ItemId.WoodOak, 44 },
				{ ItemId.WoodMaple, 20 },
				{ ItemId.WoodPine, 10 },
			}) },
			{ "Шкаф с предметами", new ShopFurnitureBuy("hei_heist_bed_chestdrawer_04", "Хранилища", 2, new Dictionary<ItemId, int>()
			{
				{ ItemId.WoodOak, 44 },
				{ ItemId.WoodMaple, 20 },
				{ ItemId.WoodPine, 10 },
			}) },
			{ "Взломостойкий сейф", new ShopFurnitureBuy("p_secret_weapon_02", "Хранилища", 3, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 1000 },
				{ ItemId.Gold, 200 },
				{ ItemId.Ruby, 70 },
			}) },

			{ "Пинг-понг", new ShopFurnitureBuy("ch_prop_vault_painting_01a", "Картины", 5, new Dictionary<ItemId, int>()
			{
				{ ItemId.WoodOak, 80 },
			}) },
			{ "Стримснайперы", new ShopFurnitureBuy("ch_prop_vault_painting_01b", "Картины", 6, new Dictionary<ItemId, int>()
			{
				{ ItemId.WoodMaple, 40 },
			}) },
			{ "Завод", new ShopFurnitureBuy("ch_prop_vault_painting_01f", "Картины", 7, new Dictionary<ItemId, int>()
			{
				{ ItemId.WoodPine, 25 },
			}) },
			{ "Переговоры", new ShopFurnitureBuy("ch_prop_vault_painting_01h", "Картины", 8, new Dictionary<ItemId, int>()
			{
				{ ItemId.WoodOak, 80 },
			}) },
			{ "Девчонки", new ShopFurnitureBuy("ch_prop_vault_painting_01j", "Картины", 9, new Dictionary<ItemId, int>()
			{
				{ ItemId.WoodMaple, 40 },
			}) },

			{ "DAB", new ShopFurnitureBuy("vw_prop_casino_art_statue_01a", "Статуи", 10, new Dictionary<ItemId, int>()
			{
				{ ItemId.WoodPine, 250 },
			}) },
			{ "Twerk", new ShopFurnitureBuy("vw_prop_casino_art_statue_02a", "Статуи", 11, new Dictionary<ItemId, int>()
			{
				{ ItemId.Ruby, 150 },
			}) },
			{ "Монахиня", new ShopFurnitureBuy("vw_prop_casino_art_statue_04a", "Статуи", 12, new Dictionary<ItemId, int>()
			{
				{ ItemId.Gold, 1000 },
			}) },

			{ "Paul Ridor", new ShopFurnitureBuy("hei_prop_drug_statue_01", "Фигурки", 13, new Dictionary<ItemId, int>()
			{
				{ ItemId.Ruby, 7 },
			}) },
			{ "Оскар", new ShopFurnitureBuy("ex_prop_exec_award_gold", "Фигурки", 14, new Dictionary<ItemId, int>()
			{
				{ ItemId.Emerald, 13 },
			}) },
			{ "Monkey King", new ShopFurnitureBuy("vw_prop_vw_pogo_gold_01a", "Фигурки", 15, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 100 },
			}) },
			{ "Авария", new ShopFurnitureBuy("xs_prop_trophy_goldbag_01a", "Фигурки", 16, new Dictionary<ItemId, int>()
			{
				{ ItemId.Ruby, 7 },
			}) },
			{ "Кубок FIFA", new ShopFurnitureBuy("sum_prop_ac_wifaaward_01a", "Фигурки", 17, new Dictionary<ItemId, int>()
			{
				{ ItemId.Emerald, 13 },
			}) },
			{ "Шампанское", new ShopFurnitureBuy("xs_prop_trophy_champ_01a", "Фигурки", 18, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 100 },
			}) },

			{ "Пальма", new ShopFurnitureBuy("prop_fbibombplant", "Растения", 19, new Dictionary<ItemId, int>()
			{
				{ ItemId.WoodMaple, 27 },
			}) },
			{ "Маленькое дерево", new ShopFurnitureBuy("prop_plant_int_01a", "Растения", 20, new Dictionary<ItemId, int>()
			{
				{ ItemId.WoodPine, 20 },
			}) },
			{ "Круглое дерево", new ShopFurnitureBuy("prop_plant_int_02b", "Растения", 21, new Dictionary<ItemId, int>()
			{
				{ ItemId.WoodPine, 20 },
			}) },
			{ "Папоротник", new ShopFurnitureBuy("prop_plant_int_03b", "Растения", 22, new Dictionary<ItemId, int>()
			{
				{ ItemId.WoodOak, 60 },
			}) },
			{ "Денежное дерево", new ShopFurnitureBuy("prop_plant_int_04b", "Растения", 23, new Dictionary<ItemId, int>()
			{
				{ ItemId.WoodMaple, 40 },
			}) },
			{ "Кактус", new ShopFurnitureBuy("vw_prop_casino_art_plant_12a", "Растения", 24, new Dictionary<ItemId, int>()
			{
				{ ItemId.WoodMaple, 27 },
			}) },

			{ "Ёлка", new ShopFurnitureBuy("prop_xmas_tree_int", "Ёлки", 25, new Dictionary<ItemId, int>()
			{
				{ ItemId.WoodPine, 60 },
			}) },
			{ "Бриллиантовая ёлка", new ShopFurnitureBuy("ch_prop_ch_diamond_xmastree", "Ёлки", 26, new Dictionary<ItemId, int>()
			{
				{ ItemId.Ruby, 150 },
			}) },

			{ "Счётная машинка", new ShopFurnitureBuy("bkr_prop_money_counter", "Драгоценности", 27, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 200 },
			}) },
			{ "Горка денег", new ShopFurnitureBuy("bkr_prop_moneypack_03a", "Драгоценности", 28, new Dictionary<ItemId, int>()
			{
				{ ItemId.Gold, 1000 },
			}) },
			{ "Гора денег", new ShopFurnitureBuy("ba_prop_battle_moneypack_02a", "Драгоценности", 29, new Dictionary<ItemId, int>()
			{
				{ ItemId.Gold, 3000 },
			}) },
			{ "Ящик денег", new ShopFurnitureBuy("ex_prop_crate_money_bc", "Драгоценности", 30, new Dictionary<ItemId, int>()
			{
				{ ItemId.Gold, 5000 },
			}) },
			{ "Ящик с золотом", new ShopFurnitureBuy("prop_ld_gold_chest", "Драгоценности", 31, new Dictionary<ItemId, int>()
			{
				{ ItemId.Gold, 300 },
			}) },
			{ "Тележка с золотом", new ShopFurnitureBuy("p_large_gold_s", "Драгоценности", 32, new Dictionary<ItemId, int>()
			{
				{ ItemId.Gold, 10000 },
			}) },
			{ "Кейс с деньгами", new ShopFurnitureBuy("prop_cash_case_02", "Драгоценности", 33, new Dictionary<ItemId, int>()
			{
				{ ItemId.Gold, 1700 },
			}) },

			{ "Ящик пива", new ShopFurnitureBuy("hei_heist_cs_beer_box", "Алкоголь", 34, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 50 },
			}) },
			{ "Романтический набор", new ShopFurnitureBuy("ba_prop_club_champset", "Алкоголь", 35, new Dictionary<ItemId, int>()
			{
				{ ItemId.WoodOak, 110 },
			}) },

			{ "Фигурная", new ShopFurnitureBuy("vw_prop_casino_art_vase_08a", "Вазы", 36, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 100 },
			}) },
			{ "Кувшин", new ShopFurnitureBuy("vw_prop_casino_art_vase_08a", "Вазы", 37, new Dictionary<ItemId, int>()
			{
				{ ItemId.Emerald, 13 },
			}) },
			{ "Дутая", new ShopFurnitureBuy("vw_prop_casino_art_vase_05a", "Вазы", 38, new Dictionary<ItemId, int>()
			{
				{ ItemId.Ruby, 7 },
			}) },
			{ "Симметричная", new ShopFurnitureBuy("apa_mp_h_acc_vase_06", "Вазы", 39, new Dictionary<ItemId, int>()
			{
				{ ItemId.WoodOak, 40 },
			}) },
			{ "Зеркальная", new ShopFurnitureBuy("apa_mp_h_acc_vase_05", "Вазы", 40, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 100 },
			}) },

			// ===== Мебель для обустройства квартиры (цены здесь; в settings/pricesSettings.json только первые 40 позиций). Только покупка, без крафта.
			// Диваны
			{ "Угловой диван «Лофт»", new ShopFurnitureBuy("apa_mp_h_stn_sofacorn_01", "Диваны", 45000, new Dictionary<ItemId, int>()) },
			{ "Угловой диван «Модерн»", new ShopFurnitureBuy("apa_mp_h_stn_sofacorn_05", "Диваны", 50000, new Dictionary<ItemId, int>()) },
			{ "Угловой диван «Бежевый»", new ShopFurnitureBuy("apa_mp_h_stn_sofacorn_06", "Диваны", 48000, new Dictionary<ItemId, int>()) },
			{ "Угловой диван «Графит»", new ShopFurnitureBuy("apa_mp_h_stn_sofacorn_07", "Диваны", 52000, new Dictionary<ItemId, int>()) },
			{ "Угловой диван «Бархат»", new ShopFurnitureBuy("apa_mp_h_stn_sofacorn_08", "Диваны", 55000, new Dictionary<ItemId, int>()) },
			{ "Угловой диван «Кожа»", new ShopFurnitureBuy("apa_mp_h_stn_sofacorn_09", "Диваны", 60000, new Dictionary<ItemId, int>()) },
			{ "Угловой диван «Премиум»", new ShopFurnitureBuy("apa_mp_h_stn_sofacorn_10", "Диваны", 65000, new Dictionary<ItemId, int>()) },
			{ "Двухместный диван", new ShopFurnitureBuy("apa_mp_h_stn_sofa2seat_02", "Диваны", 25000, new Dictionary<ItemId, int>()) },
			{ "Диван «Яхта»", new ShopFurnitureBuy("apa_mp_h_yacht_sofa_01", "Диваны", 40000, new Dictionary<ItemId, int>()) },
			{ "Диван «Марина»", new ShopFurnitureBuy("apa_mp_h_yacht_sofa_02", "Диваны", 42000, new Dictionary<ItemId, int>()) },
			// Кресла и стулья
			{ "Кресло «Классика»", new ShopFurnitureBuy("apa_mp_h_stn_chairarm_01", "Кресла и стулья", 12000, new Dictionary<ItemId, int>()) },
			{ "Кресло «Ракушка»", new ShopFurnitureBuy("apa_mp_h_stn_chairarm_02", "Кресла и стулья", 12000, new Dictionary<ItemId, int>()) },
			{ "Кресло «Лаунж»", new ShopFurnitureBuy("apa_mp_h_stn_chairarm_03", "Кресла и стулья", 14000, new Dictionary<ItemId, int>()) },
			{ "Кресло «Мягкое»", new ShopFurnitureBuy("apa_mp_h_stn_chairarm_09", "Кресла и стулья", 13000, new Dictionary<ItemId, int>()) },
			{ "Кресло «Дизайнерское»", new ShopFurnitureBuy("apa_mp_h_stn_chairarm_11", "Кресла и стулья", 15000, new Dictionary<ItemId, int>()) },
			{ "Кресло «Кожаное»", new ShopFurnitureBuy("apa_mp_h_stn_chairarm_12", "Кресла и стулья", 16000, new Dictionary<ItemId, int>()) },
			{ "Кресло «Яйцо»", new ShopFurnitureBuy("apa_mp_h_stn_chairarm_13", "Кресла и стулья", 18000, new Dictionary<ItemId, int>()) },
			{ "Кресло «Ретро»", new ShopFurnitureBuy("apa_mp_h_stn_chairarm_23", "Кресла и стулья", 14000, new Dictionary<ItemId, int>()) },
			{ "Кресло «Яхта»", new ShopFurnitureBuy("apa_mp_h_yacht_armchair_01", "Кресла и стулья", 14000, new Dictionary<ItemId, int>()) },
			{ "Шезлонг", new ShopFurnitureBuy("apa_mp_h_stn_chairstrip_01", "Кресла и стулья", 15000, new Dictionary<ItemId, int>()) },
			{ "Пуф", new ShopFurnitureBuy("apa_mp_h_stn_foot_stool_01", "Кресла и стулья", 4000, new Dictionary<ItemId, int>()) },
			{ "Барный стул", new ShopFurnitureBuy("apa_mp_h_stn_chairstool_12", "Кресла и стулья", 5000, new Dictionary<ItemId, int>()) },
			{ "Стул «Лофт»", new ShopFurnitureBuy("apa_mp_h_din_chair_04", "Кресла и стулья", 4000, new Dictionary<ItemId, int>()) },
			{ "Стул «Белый»", new ShopFurnitureBuy("apa_mp_h_din_chair_08", "Кресла и стулья", 4000, new Dictionary<ItemId, int>()) },
			{ "Стул «Дерево»", new ShopFurnitureBuy("apa_mp_h_din_chair_09", "Кресла и стулья", 4500, new Dictionary<ItemId, int>()) },
			{ "Стул «Мягкий»", new ShopFurnitureBuy("apa_mp_h_din_chair_12", "Кресла и стулья", 5000, new Dictionary<ItemId, int>()) },
			// Столы
			{ "Обеденный стол «Стекло»", new ShopFurnitureBuy("apa_mp_h_din_table_01", "Столы", 18000, new Dictionary<ItemId, int>()) },
			{ "Обеденный стол «Дуб»", new ShopFurnitureBuy("apa_mp_h_din_table_04", "Столы", 20000, new Dictionary<ItemId, int>()) },
			{ "Обеденный стол «Мрамор»", new ShopFurnitureBuy("apa_mp_h_din_table_05", "Столы", 24000, new Dictionary<ItemId, int>()) },
			{ "Обеденный стол «Лофт»", new ShopFurnitureBuy("apa_mp_h_din_table_06", "Столы", 20000, new Dictionary<ItemId, int>()) },
			{ "Обеденный стол «Круглый»", new ShopFurnitureBuy("apa_mp_h_din_table_11", "Столы", 16000, new Dictionary<ItemId, int>()) },
			{ "Журнальный столик «Стекло»", new ShopFurnitureBuy("apa_mp_h_tab_coffee_05", "Столы", 8000, new Dictionary<ItemId, int>()) },
			{ "Журнальный столик «Дерево»", new ShopFurnitureBuy("apa_mp_h_tab_coffee_07", "Столы", 8000, new Dictionary<ItemId, int>()) },
			{ "Журнальный столик «Модерн»", new ShopFurnitureBuy("apa_mp_h_tab_coffee_08", "Столы", 9000, new Dictionary<ItemId, int>()) },
			{ "Журнальный столик «Яхта»", new ShopFurnitureBuy("apa_mp_h_yacht_coffee_table_01", "Столы", 9000, new Dictionary<ItemId, int>()) },
			{ "Приставной стол", new ShopFurnitureBuy("apa_mp_h_tab_sidelrg_01", "Столы", 6000, new Dictionary<ItemId, int>()) },
			{ "Консоль", new ShopFurnitureBuy("apa_mp_h_tab_sidelrg_02", "Столы", 7000, new Dictionary<ItemId, int>()) },
			{ "Консоль «Мрамор»", new ShopFurnitureBuy("apa_mp_h_tab_sidelrg_04", "Столы", 8000, new Dictionary<ItemId, int>()) },
			{ "Консоль «Лофт»", new ShopFurnitureBuy("apa_mp_h_tab_sidelrg_07", "Столы", 7000, new Dictionary<ItemId, int>()) },
			{ "Тумбочка", new ShopFurnitureBuy("apa_mp_h_tab_sidesml_01", "Столы", 3500, new Dictionary<ItemId, int>()) },
			{ "Тумбочка «Белая»", new ShopFurnitureBuy("apa_mp_h_tab_sidesml_02", "Столы", 3500, new Dictionary<ItemId, int>()) },
			// Кровати
			{ "Кровать «Модерн»", new ShopFurnitureBuy("apa_mp_h_bed_double_08", "Кровати", 35000, new Dictionary<ItemId, int>()) },
			{ "Кровать «Классика»", new ShopFurnitureBuy("apa_mp_h_bed_double_09", "Кровати", 35000, new Dictionary<ItemId, int>()) },
			{ "Широкая кровать", new ShopFurnitureBuy("apa_mp_h_bed_wide_05", "Кровати", 45000, new Dictionary<ItemId, int>()) },
			{ "Кровать с тумбами", new ShopFurnitureBuy("apa_mp_h_bed_with_table_02", "Кровати", 50000, new Dictionary<ItemId, int>()) },
			{ "Кровать «Яхта»", new ShopFurnitureBuy("apa_mp_h_yacht_bed_01", "Кровати", 55000, new Dictionary<ItemId, int>()) },
			{ "Кровать «Люкс»", new ShopFurnitureBuy("apa_mp_h_yacht_bed_02", "Кровати", 60000, new Dictionary<ItemId, int>()) },
			// Свет
			{ "Торшер «Дуга»", new ShopFurnitureBuy("apa_mp_h_floorlamp_a", "Свет", 5000, new Dictionary<ItemId, int>()) },
			{ "Торшер «Тренога»", new ShopFurnitureBuy("apa_mp_h_floorlamp_b", "Свет", 5000, new Dictionary<ItemId, int>()) },
			{ "Торшер «Шар»", new ShopFurnitureBuy("apa_mp_h_floorlamp_c", "Свет", 5500, new Dictionary<ItemId, int>()) },
			{ "Торшер «Модерн»", new ShopFurnitureBuy("apa_mp_h_lit_floorlamp_01", "Свет", 6000, new Dictionary<ItemId, int>()) },
			{ "Торшер «Цилиндр»", new ShopFurnitureBuy("apa_mp_h_lit_floorlamp_02", "Свет", 6000, new Dictionary<ItemId, int>()) },
			{ "Торшер «Лофт»", new ShopFurnitureBuy("apa_mp_h_lit_floorlamp_03", "Свет", 6000, new Dictionary<ItemId, int>()) },
			{ "Торшер «Тонкий»", new ShopFurnitureBuy("apa_mp_h_lit_floorlamp_05", "Свет", 6000, new Dictionary<ItemId, int>()) },
			{ "Торшер «Яхта»", new ShopFurnitureBuy("apa_mp_h_yacht_floor_lamp_01", "Свет", 6000, new Dictionary<ItemId, int>()) },
			{ "Настольная лампа", new ShopFurnitureBuy("apa_mp_h_lit_lamptable_005", "Свет", 2500, new Dictionary<ItemId, int>()) },
			{ "Настольная лампа «Шар»", new ShopFurnitureBuy("apa_mp_h_lit_lamptable_02", "Свет", 2500, new Dictionary<ItemId, int>()) },
			{ "Настольная лампа «Классика»", new ShopFurnitureBuy("apa_mp_h_lit_lamptable_04", "Свет", 2500, new Dictionary<ItemId, int>()) },
			{ "Настольная лампа «Модерн»", new ShopFurnitureBuy("apa_mp_h_lit_lamptable_09", "Свет", 3000, new Dictionary<ItemId, int>()) },
			{ "Подвесной светильник", new ShopFurnitureBuy("apa_mp_h_lit_lightpendant_01", "Свет", 4000, new Dictionary<ItemId, int>()) },
			// Техника
			{ "Большой телевизор", new ShopFurnitureBuy("prop_tv_flat_01", "Техника", 30000, new Dictionary<ItemId, int>()) },
			{ "Телевизор", new ShopFurnitureBuy("prop_tv_flat_02", "Техника", 20000, new Dictionary<ItemId, int>()) },
			{ "Небольшой телевизор", new ShopFurnitureBuy("prop_tv_flat_03", "Техника", 12000, new Dictionary<ItemId, int>()) },
			{ "Плазма «Кинотеатр»", new ShopFurnitureBuy("prop_tv_flat_michael", "Техника", 45000, new Dictionary<ItemId, int>()) },
			{ "Телевизор «Офис»", new ShopFurnitureBuy("ex_prop_ex_tv_flat_01", "Техника", 25000, new Dictionary<ItemId, int>()) },
			{ "ТВ-тумба большая", new ShopFurnitureBuy("apa_mp_h_str_avunitl_01_b", "Техника", 18000, new Dictionary<ItemId, int>()) },
			{ "ТВ-тумба средняя", new ShopFurnitureBuy("apa_mp_h_str_avunitm_01", "Техника", 14000, new Dictionary<ItemId, int>()) },
			{ "ТВ-тумба малая", new ShopFurnitureBuy("apa_mp_h_str_avunits_01", "Техника", 10000, new Dictionary<ItemId, int>()) },
			{ "Домашний кинотеатр", new ShopFurnitureBuy("hei_heist_str_avunitl_03", "Техника", 35000, new Dictionary<ItemId, int>()) },
			{ "Музыкальный центр", new ShopFurnitureBuy("prop_hifi_01", "Техника", 8000, new Dictionary<ItemId, int>()) },
			{ "Колонка", new ShopFurnitureBuy("prop_speaker_06", "Техника", 5000, new Dictionary<ItemId, int>()) },
			{ "Ноутбук", new ShopFurnitureBuy("prop_laptop_01a", "Техника", 7000, new Dictionary<ItemId, int>()) },
			// Шкафы и полки
			{ "Стеллаж", new ShopFurnitureBuy("apa_mp_h_str_shelffloorm_02", "Шкафы и полки", 10000, new Dictionary<ItemId, int>()) },
			{ "Настенная полка", new ShopFurnitureBuy("apa_mp_h_str_shelfwallm_01", "Шкафы и полки", 5000, new Dictionary<ItemId, int>()) },
			{ "Комод «Дуб»", new ShopFurnitureBuy("apa_mp_h_str_sideboardl_06", "Шкафы и полки", 14000, new Dictionary<ItemId, int>()) },
			{ "Комод «Белый»", new ShopFurnitureBuy("apa_mp_h_str_sideboardl_09", "Шкафы и полки", 14000, new Dictionary<ItemId, int>()) },
			{ "Комод «Модерн»", new ShopFurnitureBuy("apa_mp_h_str_sideboardl_11", "Шкафы и полки", 15000, new Dictionary<ItemId, int>()) },
			{ "Комод «Лофт»", new ShopFurnitureBuy("apa_mp_h_str_sideboardl_13", "Шкафы и полки", 15000, new Dictionary<ItemId, int>()) },
			{ "Комод «Графит»", new ShopFurnitureBuy("apa_mp_h_str_sideboardl_14", "Шкафы и полки", 16000, new Dictionary<ItemId, int>()) },
			{ "Буфет", new ShopFurnitureBuy("apa_mp_h_str_sideboardm_02", "Шкафы и полки", 12000, new Dictionary<ItemId, int>()) },
			{ "Тумба «Минимал»", new ShopFurnitureBuy("apa_mp_h_str_sideboards_01", "Шкафы и полки", 8000, new Dictionary<ItemId, int>()) },
			{ "Тумба «Дерево»", new ShopFurnitureBuy("apa_mp_h_str_sideboards_02", "Шкафы и полки", 8000, new Dictionary<ItemId, int>()) },
			// Декор
			{ "Ковёр средний", new ShopFurnitureBuy("apa_mp_h_acc_rugwoolm_01", "Декор", 6000, new Dictionary<ItemId, int>()) },
			{ "Ковёр большой", new ShopFurnitureBuy("apa_mp_h_acc_rugwooll_04", "Декор", 9000, new Dictionary<ItemId, int>()) },
			{ "Ковёр малый", new ShopFurnitureBuy("apa_mp_h_acc_rugwools_01", "Декор", 4000, new Dictionary<ItemId, int>()) },
			{ "Свечи", new ShopFurnitureBuy("apa_mp_h_acc_candles_02", "Декор", 1500, new Dictionary<ItemId, int>()) },
			{ "Свечи «Трио»", new ShopFurnitureBuy("apa_mp_h_acc_candles_04", "Декор", 1500, new Dictionary<ItemId, int>()) },
			{ "Декоративная голова", new ShopFurnitureBuy("apa_mp_h_acc_dec_head_01", "Декор", 5000, new Dictionary<ItemId, int>()) },
			{ "Декоративная тарелка", new ShopFurnitureBuy("apa_mp_h_acc_dec_plate_01", "Декор", 2000, new Dictionary<ItemId, int>()) },
			{ "Скульптура", new ShopFurnitureBuy("apa_mp_h_acc_dec_sculpt_01", "Декор", 7000, new Dictionary<ItemId, int>()) },
			{ "Цветы «Розы»", new ShopFurnitureBuy("apa_mp_h_acc_vase_flowers_01", "Декор", 3000, new Dictionary<ItemId, int>()) },
			{ "Цветы «Лилии»", new ShopFurnitureBuy("apa_mp_h_acc_vase_flowers_02", "Декор", 3000, new Dictionary<ItemId, int>()) },
			{ "Цветы «Орхидеи»", new ShopFurnitureBuy("apa_mp_h_acc_vase_flowers_03", "Декор", 3000, new Dictionary<ItemId, int>()) },
			{ "Высокое растение", new ShopFurnitureBuy("apa_mp_h_acc_plant_tall_01", "Декор", 5000, new Dictionary<ItemId, int>()) },
			{ "Пальма в кадке", new ShopFurnitureBuy("apa_mp_h_acc_plant_palm_01", "Декор", 6000, new Dictionary<ItemId, int>()) },
			{ "Фруктовая ваза", new ShopFurnitureBuy("apa_mp_h_acc_fruitbowl_01", "Декор", 2000, new Dictionary<ItemId, int>()) },
			{ "Керамическая чаша", new ShopFurnitureBuy("apa_mp_h_acc_bowl_ceramic_01", "Декор", 2000, new Dictionary<ItemId, int>()) },
			{ "Шкатулка", new ShopFurnitureBuy("apa_mp_h_acc_box_trinket_01", "Декор", 2500, new Dictionary<ItemId, int>()) },
			{ "Бюст", new ShopFurnitureBuy("hei_prop_hei_bust_01", "Декор", 12000, new Dictionary<ItemId, int>()) },
			// Картины
			{ "Абстракция", new ShopFurnitureBuy("apa_p_h_acc_artwalll_01", "Картины", 8000, new Dictionary<ItemId, int>()) },
			{ "Закат", new ShopFurnitureBuy("apa_p_h_acc_artwalll_02", "Картины", 8000, new Dictionary<ItemId, int>()) },
			{ "Минимализм", new ShopFurnitureBuy("apa_p_h_acc_artwallm_01", "Картины", 6000, new Dictionary<ItemId, int>()) },
			{ "Этюд", new ShopFurnitureBuy("apa_p_h_acc_artwalls_03", "Картины", 4000, new Dictionary<ItemId, int>()) },
			// Кухня и ванная
			{ "Холодильник", new ShopFurnitureBuy("prop_fridge_01", "Кухня и ванная", 15000, new Dictionary<ItemId, int>()) },
			{ "Холодильник двухдверный", new ShopFurnitureBuy("prop_fridge_03", "Кухня и ванная", 22000, new Dictionary<ItemId, int>()) },
			{ "Микроволновка", new ShopFurnitureBuy("prop_micro_01", "Кухня и ванная", 3000, new Dictionary<ItemId, int>()) },
			{ "Тостер", new ShopFurnitureBuy("prop_toaster_01", "Кухня и ванная", 1500, new Dictionary<ItemId, int>()) },
			{ "Кофемашина", new ShopFurnitureBuy("prop_coffee_mac_02", "Кухня и ванная", 4000, new Dictionary<ItemId, int>()) },
			{ "Стиральная машина", new ShopFurnitureBuy("prop_washer_02", "Кухня и ванная", 9000, new Dictionary<ItemId, int>()) },
			{ "Унитаз", new ShopFurnitureBuy("prop_toilet_01", "Кухня и ванная", 4000, new Dictionary<ItemId, int>()) },
			{ "Раковина", new ShopFurnitureBuy("prop_sink_06", "Кухня и ванная", 5000, new Dictionary<ItemId, int>()) },
			// Досуг
			{ "Бильярдный стол", new ShopFurnitureBuy("prop_pooltable_02", "Досуг", 40000, new Dictionary<ItemId, int>()) },
			{ "Игровой автомат", new ShopFurnitureBuy("prop_arcade_01", "Досуг", 25000, new Dictionary<ItemId, int>()) },
			{ "Музыкальный автомат", new ShopFurnitureBuy("prop_jukebox_01", "Досуг", 20000, new Dictionary<ItemId, int>()) },
			{ "Дартс", new ShopFurnitureBuy("prop_dart_bd_cab_01", "Досуг", 6000, new Dictionary<ItemId, int>()) },
			{ "Скамья для жима", new ShopFurnitureBuy("prop_muscle_bench_03", "Досуг", 10000, new Dictionary<ItemId, int>()) },
			{ "Велотренажёр", new ShopFurnitureBuy("prop_exer_bike_01", "Досуг", 12000, new Dictionary<ItemId, int>()) },
			/*{ "цветок", new ShopFurnitureBuy("apa_mp_h_acc_plant_tall_01", "Вазы", 41, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 100 },
			}) },
			{ "цветок", new ShopFurnitureBuy("prop_fbibombplant", "Вазы", 40, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 100 },
			}) },
			{ "цветок", new ShopFurnitureBuy("prop_fbibombplant", "Вазы", 40, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 100 },
			}) },
			{ "свечи", new ShopFurnitureBuy("apa_mp_h_acc_candles_02", "Вазы", 40, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 100 },
			}) },
			{ "маска", new ShopFurnitureBuy("apa_mp_h_acc_dec_head_01", "Вазы", 40, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 100 },
			}) },
			{ "пива", new ShopFurnitureBuy("beerrow_local", "Вазы", 40, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 100 },
			}) },
			{ "дом кинотеатр", new ShopFurnitureBuy("hei_heist_str_avunitl_03", "Вазы", 40, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 100 },
			}) },
			{ "дом кинотеатр", new ShopFurnitureBuy("apa_mp_h_str_avunitl_01_b", "Вазы", 40, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 100 },
			}) },
			{ "статуя голова", new ShopFurnitureBuy("hei_prop_hei_bust_01", "Вазы", 40, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 100 },
			}) },
			{ "статуя оружие", new ShopFurnitureBuy("ch_prop_ch_trophy_gunner_01a", "Вазы", 40, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 100 },
			}) },
			{ "склад для оружия", new ShopFurnitureBuy("bkr_prop_gunlocker_01a", "Вазы", 40, new Dictionary<ItemId, int>()
			{
				{ ItemId.Iron, 100 },
			}) },*/
		};
        public static async Task Save(ServerBD db, int houseId)
        {
            try
            {
	            if (HouseFurnitures.ContainsKey(houseId))
	            {
		            await db.Furniture
			            .Where(f => f.Uuid == houseId)
			            .Set(f => f.Furniture, JsonConvert.SerializeObject(HouseFurnitures[houseId]))
			            .UpdateAsync();
	            }
            }
            catch (Exception e)
            {
                Log.Write($"Save Exception: {e.ToString()}");
            }
        }
        /// <summary>Сразу записать мебель всех домов с несохранёнными изменениями (при рестарте).</summary>
        public static async Task SaveFurnitureNow(ServerBD db)
        {
            foreach (var house in HouseManager.Houses.Where(h => h.IsFurnitureSave).ToList())
            {
                house.IsFurnitureSave = false;
                await Save(db, house.ID);
            }
        }

        public static void Create(int id)
        {
            try
            {
                if (!HouseFurnitures.ContainsKey(id))
                {
                    using MySqlCommand cmd = new MySqlCommand
                    {
                        CommandText = "INSERT INTO `furniture`(`uuid`,`furniture`,`access`) VALUES (@val0,@val1,@val3)"
                    };
                    cmd.Parameters.AddWithValue("@val0", id);
                    cmd.Parameters.AddWithValue("@val1", JsonConvert.SerializeObject(new Dictionary<int, HouseFurniture>()));
                    cmd.Parameters.AddWithValue("@val3", JsonConvert.SerializeObject(new List<string>()));
                    MySQL.Query(cmd);
                    // Без этого новый дом (купленный/созданный после старта) не мог купить мебель до рестарта
                    HouseFurnitures[id] = new Dictionary<int, HouseFurniture>();
                }
            }
            catch (Exception e)
            {
                Log.Write($"Create Exception: {e.ToString()}");
            }
        }

        public static void NewFurniture(int id, string name)
        {
            try
            {
                if (!HouseFurnitures.ContainsKey(id)) 
                    Create(id);
                
                var houseFurniture = HouseFurnitures[id];
                
                int i = 0;
                while (houseFurniture.ContainsKey(i)) 
                    i++;
                
                var furn = new HouseFurniture(i, name, NameModels[name].Prop);
                houseFurniture.Add(i, furn);
                
                if (NameModels[name].Type.Equals("Хранилища")) 
                    Chars.Repository.RemoveAll($"furniture_{id}_{i}"); //оставалось видимо в хранилище, тестануть так
            }
            catch (Exception e)
            {
                Log.Write($"newFurniture Exception: {e.ToString()}");
            }
        }

        [RemoteEvent("acceptEdit")]
        public void ClientEvent_acceptEdit(ExtPlayer player, float X, float Y, float Z, float XX, float YY, float ZZ)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                if (!player.IsCharacterData()) return;
                if (!sessionData.HouseData.Editing) return;
                sessionData.HouseData.Editing = false;
                var house = HouseManager.GetHouse(player, true);
                if (house == null)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.NoHome), 3000);
                    return;
                }
                Vector3 pos = new Vector3(X, Y, Z);
                if (player.Position.DistanceTo(pos) >= 6f)
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.MebelDomTooFar), 5000);
                    return;
                }
                if (!HouseFurnitures.ContainsKey(house.ID))
                {
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.MebelError), 5000);
                    return;
                }

                var furnitures = HouseFurnitures[house.ID];
                foreach (HouseFurniture p in furnitures.Values)
                {
                    if (p != null && p.IsSet && p.Position != null && p.Position.DistanceTo(pos) <= 0.5f)
                    {
                        Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, LangFunc.GetText(LangType.Ru, DataName.MebelTooNear), 3000);
                        return;
                    }
                }
                int id = sessionData.HouseData.EditID;
                furnitures[id].IsSet = true;
                Vector3 rot = new Vector3(XX, YY, ZZ);
                furnitures[id].Position = pos;
                furnitures[id].Rotation = rot;
                house.DestroyFurnitures();
                house.CreateAllFurnitures();
                house.IsFurnitureSave = true;
            }
            catch (Exception e)
            {
                Log.Write($"ClientEvent_acceptEdit Exception: {e.ToString()}");
            }
        }

        [RemoteEvent("cancelEdit")]
        public void ClientEvent_cancelEdit(ExtPlayer player)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null) return;
                sessionData.HouseData.Editing = false;
            }
            catch (Exception e)
            {
                Log.Write($"ClientEvent_cancelEdit Exception: {e.ToString()}");
            }
        }
    }
}
