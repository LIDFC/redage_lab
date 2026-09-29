using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using GTANetworkAPI;
using MySqlConnector;
using NeptuneEvo.Character;
using NeptuneEvo.Handles;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.MoneySystem
{
    /// <summary>
    /// История денег игрока для телефона (Fleeca → «История»): последние операции наличными и по карте.
    /// Записи берутся из GameLog.Money (там уже есть «откуда → куда» и код операции), складываются
    /// в компактную таблицу `money_history` с индексом по игроку (moneylog в базе логов слишком большой
    /// и без индекса). Храним 30 дней. Пишем через общую очередь записи (Database/DbQueue).
    /// </summary>
    public static class MoneyHistory
    {
        private static readonly nLog Log = new nLog("MoneySystem.MoneyHistory");
        private static readonly Regex PlayerRegex = new Regex(@"^player\((\d+)\)$", RegexOptions.Compiled);
        public const int Limit = 50;
        private static bool _ready;

        public static void Init()
        {
            try
            {
                using (var create = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS `money_history` (
                    `id` BIGINT NOT NULL AUTO_INCREMENT,
                    `uuid` INT NOT NULL,
                    `time` DATETIME NOT NULL,
                    `amount` BIGINT NOT NULL,
                    `code` VARCHAR(64) NOT NULL DEFAULT '',
                    PRIMARY KEY (`id`),
                    KEY `uuid_time` (`uuid`, `time`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"))
                    MySQL.Query(create);
                NeptuneEvo.Database.DbQueue.Enqueue("DELETE FROM `money_history` WHERE `time` < @t", ("@t", DateTime.Now.AddDays(-30)));
                _ready = true;
            }
            catch (Exception e)
            {
                Log.Write($"Init Exception: {e}");
            }
        }

        /// <summary>Вызывается из GameLog.Money: запись для игрока-отправителя (минус) и игрока-получателя (плюс).</summary>
        public static void Record(string from, string to, long amount, string comment)
        {
            if (!_ready || amount == 0 || from == to)
                return;
            try
            {
                var code = (comment ?? "").Length > 64 ? comment.Substring(0, 64) : comment ?? "";
                var now = DateTime.Now;
                var fromMatch = PlayerRegex.Match(from ?? "");
                if (fromMatch.Success)
                    Add(int.Parse(fromMatch.Groups[1].Value), now, -Math.Abs(amount), code);
                var toMatch = PlayerRegex.Match(to ?? "");
                if (toMatch.Success)
                    Add(int.Parse(toMatch.Groups[1].Value), now, Math.Abs(amount), code);
            }
            catch (Exception e)
            {
                Log.Write($"Record Exception: {e}");
            }
        }

        private static void Add(int uuid, DateTime time, long amount, string code) =>
            NeptuneEvo.Database.DbQueue.Enqueue(
                "INSERT INTO `money_history` (`uuid`,`time`,`amount`,`code`) VALUES (@u,@t,@a,@c)",
                ("@u", uuid), ("@t", time), ("@a", amount), ("@c", code));

        /// <summary>Отправить последние операции в телефон (асинхронно, чтобы не держать главный поток).</summary>
        public static void Send(ExtPlayer player, string clientEvent)
        {
            var uuid = player.GetUUID();
            Trigger.SetTask(() =>
            {
                string json = "[]";
                try
                {
                    var table = NeptuneEvo.Database.DbQueue.Read(
                        "SELECT `time`,`amount`,`code` FROM `money_history` WHERE `uuid`=@u ORDER BY `id` DESC LIMIT " + Limit, ("@u", uuid));
                    var list = new List<object>();
                    if (table != null)
                    {
                        foreach (DataRow row in table.Rows)
                        {
                            var amount = Convert.ToInt64(row["amount"]);
                            var code = row["code"].ToString();
                            list.Add(new
                            {
                                date = Convert.ToDateTime(row["time"]),
                                amount,
                                text = Describe(code, amount),
                            });
                        }
                    }
                    json = JsonConvert.SerializeObject(list);
                }
                catch (Exception e)
                {
                    Log.Write($"Send Exception: {e}");
                }
                NAPI.Task.Run(() =>
                {
                    if (player.IsCharacterData())
                        Trigger.ClientEvent(player, clientEvent, json);
                });
            });
        }

        // Код операции (начало комментария GameLog.Money до первой скобки) → понятный текст
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "payday", "Зарплата (PayDay)" }, { "nopayday", "PayDay не начислен" }, { "bonusVIP", "VIP-бонус" },
            { "GiveBonus", "Бонус" }, { "BonusReward", "Бонус" }, { "GiveEverydayAward", "Ежедневная награда" },
            { "BPGiveBonus", "Боевой пропуск" }, { "BPsetMissions", "Боевой пропуск" }, { "BPsetMissionsAll", "Боевой пропуск" },
            { "admin", "Администрация" }, { "system", "Система" }, { "server", "Система" }, { "Compensation", "Компенсация" },
            { "CompensationDonate", "Компенсация" }, { "CompensationItem", "Компенсация" }, { "PromoReward", "Промокод" },
            { "PromoOwnerReward", "Промокод (владелец)" }, { "promoCodeVIP", "Промокод VIP" }, { "RefReward", "Реферальная награда" },
            { "RefOwnerReward", "Реферальная награда" }, { "RefVIP", "Реферальная награда" }, { "rewardSubscribe", "Награда за подписку" },
            { "transfer", "Перевод" }, { "trade", "Обмен с игроком" }, { "atmIn", "Банкомат: взнос на карту" }, { "atmOut", "Банкомат: снятие" },
            { "atmHouse", "Оплата налога за дом" }, { "atmBiz", "Оплата налога за бизнес" }, { "phoneHouse", "Оплата налога за дом" },
            { "phoneBiz", "Оплата налога за бизнес" }, { "houseTax", "Дом изъят за неуплату" }, { "bizTax", "Бизнес изъят за неуплату" },
            { "parkTax", "Парковка изъята за неуплату" },
            { "buyCar", "Покупка транспорта" }, { "carSell", "Продажа транспорта" }, { "testDrive", "Тест-драйв" }, { "rentCar", "Аренда транспорта" },
            { "buyTuning", "Тюнинг" }, { "buyPetrol", "Заправка" }, { "carwash", "Автомойка" }, { "carRepair", "Ремонт транспорта" },
            { "carEvac", "Эвакуация транспорта" }, { "buyVehNumber", "Номерной знак" }, { "vNumSwap", "Смена номера" },
            { "serviceMechanic", "Услуги механика" }, { "mechanicRepair", "Услуги механика" }, { "mechanicFuel", "Услуги механика" },
            { "mechanicBuyFuel", "Топливо для механика" }, { "mechanicRent", "Аренда эвакуатора" }, { "ImpoundLot", "Штрафстоянка" },
            { "houseBuy", "Покупка дома" }, { "houseSell", "Продажа дома" }, { "parkBuy", "Покупка парковки" }, { "parkSell", "Продажа парковки" },
            { "garageUpgrade", "Улучшение гаража" }, { "buyFurn", "Покупка мебели" }, { "sellFurn", "Продажа мебели" },
            { "hotelRent", "Аренда номера в отеле" }, { "buyRieltagency", "Риелторское агентство" },
            { "buyBiz", "Покупка бизнеса" }, { "sellBiz", "Продажа бизнеса" }, { "takeoffBiz", "Выручка бизнеса" },
            { "buyShop", "Покупка в магазине" }, { "buyShopSim", "Покупка SIM-карты" }, { "buyLottery", "Лотерейный билет" },
            { "LotteryWin", "Выигрыш в лотерею" }, { "buyClothes", "Магазин одежды" }, { "buyBarber", "Барбершоп" },
            { "tattooRemove", "Удаление тату" }, { "buyWShop", "Оружейный магазин" }, { "buyAlco", "Покупка алкоголя" },
            { "buyHuntingShop", "Охотничий магазин" }, { "buySaluteShop", "Фейерверки" }, { "buyPet", "Покупка питомца" }, { "sellPet", "Продажа питомца" },
            { "buyGunlic", "Лицензия на оружие" }, { "sellGunlic", "Продажа лицензии на оружие" }, { "buyPmlic", "Мед. карта" }, { "sellPmlic", "Продажа мед. карты" },
            { "payHeal", "Лечение" }, { "revieve", "Реанимация" },
            { "busCheck", "Работа: водитель автобуса" }, { "busPay", "Проезд в автобусе" }, { "taxiPay", "Такси" }, { "taxiBotRide", "Такси" },
            { "metro", "Метро" }, { "electricianCheck", "Работа: электрик" }, { "collectorCheck", "Работа: инкассатор" },
            { "lawnCheck", "Работа: газонокосильщик" }, { "lawnBonusWay", "Работа: газонокосильщик" }, { "truckerCheck", "Работа: дальнобойщик" },
            { "postalCheck", "Работа: почтальон" }, { "sellTrees", "Работа: лесоруб" }, { "sellOre", "Работа: шахтёр" }, { "Patrolling", "Патрулирование" },
            { "policeAward", "Награда полиции" }, { "TakeIllegalStuff", "Изъятие запрещённого" }, { "arrestCar", "Арест транспорта" },
            { "casinoBet", "Казино" }, { "BJBet", "Казино: блэкджек" }, { "BJWin", "Казино: блэкджек" }, { "BJDouble", "Казино: блэкджек" },
            { "BJSplit", "Казино: блэкджек" }, { "BJWinSplit", "Казино: блэкджек" }, { "BJDraw", "Казино: блэкджек" }, { "BJDrawSplit", "Казино: блэкджек" },
            { "BJBetReturn", "Казино: блэкджек" }, { "RouletteBet", "Казино: рулетка" }, { "RouletteWin", "Казино: рулетка" },
            { "RouletteBetReturn", "Казино: рулетка" }, { "RouletteBetsReturn", "Казино: рулетка" }, { "HorseBet", "Казино: скачки" },
            { "HorseWin", "Казино: скачки" }, { "HorseBetReturn", "Казино: скачки" }, { "SpinBet", "Казино: слоты" }, { "SpinWin", "Казино: слоты" },
            { "SpinBetReturn", "Казино: слоты" }, { "diceWin", "Кости" }, { "caseWin", "Кейс" }, { "caseWinItem", "Кейс" }, { "caseWinLic", "Кейс" },
            { "caseWinExp", "Кейс" }, { "caseWinVIP", "Кейс" }, { "caseWinMask", "Кейс" }, { "caseWinCar", "Кейс" },
            { "createLobby", "Мероприятие" }, { "joinLobby", "Мероприятие" }, { "lobbyWin", "Мероприятие: победа" }, { "lobbyDraw", "Мероприятие" },
            { "TankRoyaleRegister", "Мероприятие" }, { "TankRoyaleWinner", "Мероприятие: победа" }, { "TankRoyaleRestore", "Мероприятие" },
            { "TSGiveBonus", "Бонус" }, { "TSGiveBonusOffline", "Бонус" }, { "Wedding", "Свадьба" }, { "Divorcio", "Развод" },
            { "auction", "Аукцион" }, { "buyAirDropInfo", "Аирдроп" }, { "buyAirDropOrder", "Аирдроп" },
            { "winCapture", "Война за территорию" }, { "winBiz", "Война за бизнес" }, { "robbery", "Ограбление" }, { "moneyFlow", "Отмыв денег" },
            { "hijackVehicle", "Взлом транспорта" }, { "hijackWeaponSafe", "Взлом сейфа" }, { "hijackSubjectSafe", "Взлом сейфа" },
            { "hijackClothesSafe", "Взлом сейфа" }, { "hijackEmptySafe", "Взлом сейфа" },
            { "buyMavr", "Мавр: чёрный рынок" }, { "fence", "Мавр: скупка краденого" }, { "blackmarketCashout", "Чёрный рынок: обнал" },
            { "blackmarketExchange", "Чёрный рынок: обмен" }, { "burglary", "Ограбление дома" }, { "weedSell", "Продажа травы" },
            { "weedDestroy", "Уничтожение куста" }, { "theftSuper", "Угон: бонус Мавра" }, { "questZdobichFinal", "Квест новичка: награда" },
            { "CreateOrg", "Создание организации" }, { "OrgUpgrade", "Улучшение организации" }, { "OrgStockBuy", "Склад организации" },
            { "OrgScheme", "Организация" }, { "NewLeader", "Смена лидера организации" }, { "UnactiveOrg", "Роспуск организации" },
            { "putStock", "Взнос в организацию" }, { "takeStock", "Выплата из организации" }, { "orgCarEvac", "Эвакуация авто организации" },
            { "orgCarSell", "Продажа авто организации" }, { "buyOrgCar", "Авто для организации" }, { "buyOrgTuning", "Тюнинг авто организации" },
            { "fracCarEvac", "Эвакуация авто фракции" }, { "orderCancel", "Отмена заказа" },
            { "rentTent", "Аренда палатки" }, { "rentTimeTent", "Аренда палатки" }, { "itemTent", "Торговля в палатке" },
            { "buyWarehouseUnit", "Склад: покупка" }, { "sellWarehouseUnit", "Склад: продажа" },
        };

        public static string Describe(string code, long amount)
        {
            var key = code ?? "";
            var cut = key.IndexOfAny(new[] { '(', ' ', ',' });
            if (cut > 0)
                key = key.Substring(0, cut);
            if (Labels.TryGetValue(key, out var label))
                return label;
            return amount >= 0 ? "Поступление" : "Списание";
        }
    }
}
