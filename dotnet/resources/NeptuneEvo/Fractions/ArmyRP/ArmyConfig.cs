using System;
using System.Collections.Generic;
using System.IO;
using GTANetworkAPI;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Fractions.ArmyRP
{
    public class ArmyPost
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("position")] public Vector3 Position { get; set; }
        [JsonProperty("radius")] public float Radius { get; set; } = 4f;
    }

    public class ArmyBarrier
    {
        [JsonProperty("position")] public Vector3 Position { get; set; }
        [JsonProperty("heading")] public float Heading { get; set; }
    }

    /// <summary>
    /// RP-механики армии: settings/army.json (создаётся со значениями по умолчанию).
    /// Точки можно переставить в игре: /armyset (Fractions/ArmyRP/ArmyAdmin.cs), числа — во вкладке «Армия» в /cfg.
    /// </summary>
    public class ArmyConfig
    {
        public static readonly nLog Log = new nLog("Fractions.ArmyRP");
        private static string FilePath => Path.Combine("settings", "army.json");
        public static ArmyConfig Current { get; private set; } = new ArmyConfig();

        // --- А. Служба и дисциплина
        /// <summary>Плац (построение /formation, учебная тревога /drill).</summary>
        [JsonProperty("paradePoint")] public Vector3 ParadePoint { get; set; } = new Vector3(-2254.3242, 3340.5566, 33.251175);
        [JsonProperty("paradeRadius")] public float ParadeRadius { get; set; } = 15f;
        [JsonProperty("formationMinutes")] public int FormationMinutes { get; set; } = 3;
        [JsonProperty("posts")] public List<ArmyPost> Posts { get; set; } = new List<ArmyPost>();
        /// <summary>Сколько минут стоять на посту, чтобы смена засчиталась.</summary>
        [JsonProperty("postMinutes")] public int PostMinutes { get; set; } = 10;
        [JsonProperty("postReward")] public int PostReward { get; set; } = 500;
        [JsonProperty("guardhouseMaxMinutes")] public int GuardhouseMaxMinutes { get; set; } = 60;

        // --- База в Форт Занкудо (Fractions/Army.cs). Точки меняются /armyset point ..., применяются после рестарта.
        /// <summary>Ремонт наземной техники — у ангаров.</summary>
        [JsonProperty("groundRepairPoint")] public Vector3 GroundRepairPoint { get; set; } = new Vector3(-1850.0, 3082.0, 32.81);
        /// <summary>Ремонт воздушной техники — на рулёжке.</summary>
        [JsonProperty("airRepairPoint")] public Vector3 AirRepairPoint { get; set; } = new Vector3(-1866.3154, 3210.4468, 33.255733);
        /// <summary>Кнопка общей тревоги — на плацу.</summary>
        [JsonProperty("alarmPoint")] public Vector3 AlarmPoint { get; set; } = new Vector3(-2250.3242, 3344.5566, 33.251175);
        /// <summary>NPC «Рекрут — вызвать сотрудника» у главного КПП (со стороны трассы 68).</summary>
        [JsonProperty("recruiterPoint")] public Vector3 RecruiterPoint { get; set; } = new Vector3(-1589.5, 2795.0, 17.0);
        [JsonProperty("recruiterHeading")] public float RecruiterHeading { get; set; } = 225f;
        /// <summary>Куда выпускают с гауптвахты — у штаба.</summary>
        [JsonProperty("guardhouseExit")] public Vector3 GuardhouseExit { get; set; } = new Vector3(-2361.488, 3208.3765, 30.2);
        /// <summary>Армейская заправка: гос. транспорт заправляется за счёт штата.</summary>
        [JsonProperty("fuelPoint")] public Vector3 FuelPoint { get; set; } = new Vector3(-1890.0, 3070.0, 32.81);
        /// <summary>Цена литра для списания с лимита фракции (как на обычной АЗС).</summary>
        [JsonProperty("fuelPrice")] public int FuelPrice { get; set; } = 3;

        // --- Наряды (Fractions/ArmyRP/ArmyDuty.cs)
        /// <summary>Доска нарядов: E — окно нарядов.</summary>
        [JsonProperty("dutyBoard")] public Vector3 DutyBoard { get; set; }
        [JsonProperty("kitchenPoints")] public List<Vector3> KitchenPoints { get; set; } = new List<Vector3>();
        [JsonProperty("cleanPoints")] public List<Vector3> CleanPoints { get; set; } = new List<Vector3>();
        /// <summary>Сколько секунд работать на одной точке.</summary>
        [JsonProperty("dutyActionSeconds")] public int DutyActionSeconds { get; set; } = 8;
        /// <summary>Премия за добровольный наряд (по наказанию — без премии).</summary>
        [JsonProperty("dutyReward")] public int DutyReward { get; set; } = 700;
        /// <summary>Срок наряда — минут онлайна игрока.</summary>
        [JsonProperty("dutyOnlineMinutes")] public int DutyOnlineMinutes { get; set; } = 60;

        // --- Б. Режимная зона и КПП
        /// <summary>Периметр Форт Занкудо (многоугольник, по порядку обхода).</summary>
        [JsonProperty("zone")] public List<Vector3> Zone { get; set; } = new List<Vector3>();
        [JsonProperty("zoneWarnSeconds")] public int ZoneWarnSeconds { get; set; } = 30;
        [JsonProperty("zoneWantedLevel")] public int ZoneWantedLevel { get; set; } = 2;
        [JsonProperty("barriers")] public List<ArmyBarrier> Barriers { get; set; } = new List<ArmyBarrier>();
        [JsonProperty("barrierModel")] public string BarrierModel { get; set; } = "prop_sec_barrier_ld_01a";
        [JsonProperty("passMaxHours")] public int PassMaxHours { get; set; } = 72;

        // --- В. Конвой
        /// <summary>Премия каждому военному рядом с грузовиком при разгрузке на складе получателя.</summary>
        [JsonProperty("convoyReward")] public int ConvoyReward { get; set; } = 1500;
        [JsonProperty("convoyCrewRadius")] public float ConvoyCrewRadius { get; set; } = 50f;
        /// <summary>Сколько материалов вытаскивает грабитель за одно вскрытие кузова.</summary>
        [JsonProperty("convoyRobAmount")] public int ConvoyRobAmount { get; set; } = 300;

        // --- Г. Подготовка
        [JsonProperty("rangePoint")] public Vector3 RangePoint { get; set; } = new Vector3(-2296.6648, 3296.6392, 33.184296);
        [JsonProperty("rangeHeading")] public float RangeHeading { get; set; } = 330f;
        [JsonProperty("rangeSeconds")] public int RangeSeconds { get; set; } = 60;
        [JsonProperty("rangeTargets")] public int RangeTargets { get; set; } = 15;
        /// <summary>Попаданий для зачёта по стрельбе.</summary>
        [JsonProperty("rangePass")] public int RangePass { get; set; } = 10;
        /// <summary>Полоса препятствий — чекпоинты по порядку.</summary>
        [JsonProperty("course")] public List<Vector3> Course { get; set; } = new List<Vector3>();
        /// <summary>Зачёт по полосе, если уложился в столько секунд.</summary>
        [JsonProperty("coursePassSeconds")] public int CoursePassSeconds { get; set; } = 120;
        [JsonProperty("drillMinutes")] public int DrillMinutes { get; set; } = 5;
        [JsonProperty("repairSeconds")] public int RepairSeconds { get; set; } = 20;

        public static void Load()
        {
            ArmyConfig config = null;
            try
            {
                if (File.Exists(FilePath))
                    config = JsonConvert.DeserializeObject<ArmyConfig>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Log.Write($"Не удалось прочитать {FilePath}: {e.Message}");
            }
            var save = false;
            if (config == null)
            {
                config = new ArmyConfig();
                save = true;
            }
            config.Posts ??= new List<ArmyPost>();
            config.Barriers ??= new List<ArmyBarrier>();
            if (config.Zone == null || config.Zone.Count < 3)
            {
                config.Zone = DefaultZone();
                save = true;
            }
            config.KitchenPoints ??= new List<Vector3>();
            if (config.CleanPoints == null || config.CleanPoints.Count == 0)
            {
                // Уборка плаца: точки вокруг места построения
                var c = config.ParadePoint;
                config.CleanPoints = new List<Vector3>
                {
                    new Vector3(c.X + 8, c.Y, c.Z), new Vector3(c.X, c.Y + 8, c.Z),
                    new Vector3(c.X - 8, c.Y, c.Z), new Vector3(c.X, c.Y - 8, c.Z),
                };
                save = true;
            }
            if (config.DutyBoard == null)
            {
                config.DutyBoard = new Vector3(config.ParadePoint.X + 4, config.ParadePoint.Y + 4, config.ParadePoint.Z);
                save = true;
            }
            if (config.Course == null || config.Course.Count < 2)
            {
                config.Course = DefaultCourse();
                save = true;
            }
            Current = config;
            if (save)
                Save();
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory("settings");
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(Current, Formatting.Indented));
            }
            catch (Exception e)
            {
                Log.Write($"Не удалось сохранить {FilePath}: {e.Message}");
            }
        }

        /// <summary>Примерный периметр Форт Занкудо (без трассы 68 и моста). Точнее — /armyset zone.</summary>
        private static List<Vector3> DefaultZone() => new List<Vector3>
        {
            new Vector3(-2890, 3080, 0),
            new Vector3(-2580, 3480, 0),
            new Vector3(-1810, 3480, 0),
            new Vector3(-1560, 3000, 0),
            new Vector3(-1640, 2830, 0),
            new Vector3(-2380, 2900, 0),
        };

        /// <summary>Полоса по точкам патруля на аэродроме (замкнутый круг). Точнее — /armyset course.</summary>
        private static List<Vector3> DefaultCourse() => new List<Vector3>
        {
            new Vector3(-2296.6648, 3296.6392, 33.184296),
            new Vector3(-2254.3242, 3340.5566, 33.251175),
            new Vector3(-2169.6182, 3364.2, 33.432278),
            new Vector3(-2254.3242, 3340.5566, 33.251175),
            new Vector3(-2296.6648, 3296.6392, 33.184296),
        };

        /// <summary>Точка внутри многоугольника (по X/Y).</summary>
        public bool InZone(Vector3 p)
        {
            var inside = false;
            var zone = Zone;
            for (int i = 0, j = zone.Count - 1; i < zone.Count; j = i++)
            {
                if (((zone[i].Y > p.Y) != (zone[j].Y > p.Y)) &&
                    (p.X < (zone[j].X - zone[i].X) * (p.Y - zone[i].Y) / (zone[j].Y - zone[i].Y) + zone[i].X))
                    inside = !inside;
            }
            return inside;
        }
    }
}
