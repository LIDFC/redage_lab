using System;
using System.Collections.Generic;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Houses;
using NeptuneEvo.Players;
using NeptuneEvo.Players.Phone.Messages.Models;
using Newtonsoft.Json;
using Redage.SDK;

namespace NeptuneEvo.Crime.Burglary
{
    /// <summary>
    /// Ограбление жилых домов (заменяет старый взлом ломом).
    /// Кто: криминал (банды, байкеры, мафия, криминальные организации), можно в одиночку.
    /// Когда: в любое время, в маске, с отмычкой. Куда: любой запертый чужой или ничейный (NPC) дом; жильцы в игре получают тревогу.
    /// Взлом — мини-игра LockBreak, внутри 3 точки обыска (техника, украшения, наличные), соседи могут вызвать полицию.
    /// </summary>
    public class BurglaryManager : Script
    {
        private const int HouseCooldownHours = 6;
        private const int PlayerCooldownMinutes = 15;
        private const int SearchSeconds = 6;
        private const int InsideMinutes = 5;

        private class LootPoint
        {
            public Vector3 Position;
            public ExtColShape Shape;
            public bool Taken;
            public bool Searching;
        }

        private class Session
        {
            public int Uuid;
            public int HouseId;
            public string Owner;
            public List<LootPoint> Points = new List<LootPoint>();
            public DateTime Started;
            public bool Looted;
            public bool LateAlert;
        }

        private static readonly Dictionary<int, Session> Sessions = new Dictionary<int, Session>();
        private static readonly Dictionary<int, DateTime> HouseCooldown = new Dictionary<int, DateTime>();
        private static readonly Dictionary<int, DateTime> PlayerCooldown = new Dictionary<int, DateTime>();
        private static readonly Dictionary<int, int> Pending = new Dictionary<int, int>();
        private static readonly HashSet<int> Busy = new HashSet<int>();
        private static bool _timer;

        // ------------------------------------------------------------------ проверки

        /// <summary>Хозяин и сожители, которые сейчас в игре (им приходит тревога о взломе).</summary>
        private static List<ExtPlayer> OnlineResidents(House house)
        {
            var result = new List<ExtPlayer>();
            if (string.IsNullOrEmpty(house.Owner))
                return result;
            var names = new List<string> { house.Owner };
            names.AddRange(house.Roommates.Keys);
            foreach (var name in names)
            {
                if (Main.PlayerUUIDs.TryGetValue(name, out var uuid))
                {
                    var resident = Main.GetPlayerByUUID(uuid);
                    if (resident != null)
                        result.Add(resident);
                }
            }
            return result;
        }

        /// <summary>Сколько минут игроку ещё «лежать на дне» (для справки в меню фракции).</summary>
        public static int CooldownMinutes(int uuid) =>
            PlayerCooldown.TryGetValue(uuid, out var time) && time > DateTime.Now ? (int)Math.Ceiling((time - DateTime.Now).TotalMinutes) : 0;

        /// <summary>Причина, по которой нельзя грабить (null — можно).</summary>
        private static string Check(ExtPlayer player, House house)
        {
            if (house == null || house.Type == 7 || house.ApartmentId != -1 || house.CustomInterior != null)
                return "Этот дом нельзя ограбить";
            if (!FunctionsAccess.IsWorking("lockpick"))
                return "Функция временно отключена";
            if (!CrimeCore.IsCriminal(player))
                return "Дверь заперта";
            if (!CrimeCore.HasMask(player))
                return "Наденьте маску, иначе соседи вас узнают";
            if (LockBreak.CountPicks(player) <= 0)
                return "Нужна отмычка";
            if (HouseCooldown.TryGetValue(house.ID, out var houseTime) && houseTime > DateTime.Now)
                return "Этот дом недавно грабили";
            if (PlayerCooldown.TryGetValue(player.GetUUID(), out var playerTime) && playerTime > DateTime.Now)
                return $"Залягте на дно ещё {Math.Ceiling((playerTime - DateTime.Now).TotalMinutes)} мин";
            if (Busy.Contains(house.ID) || Sessions.Values.Any(s => s.HouseId == house.ID))
                return "Этот дом уже кто-то вскрывает";
            if (LockBreak.IsBusy(player) || Sessions.ContainsKey(player.GetUUID()))
                return "Вы уже заняты";
            return null;
        }

        /// <summary>
        /// Предложить взлом у двери. notifyWhy — показать причину отказа (для запертых чужих домов);
        /// для ничейных домов причина показывается только «грабителям» (криминал в маске), остальным — меню покупки.
        /// </summary>
        public static bool TryOffer(ExtPlayer player, House house, bool notifyWhy)
        {
            var error = Check(player, house);
            if (error != null)
            {
                if (notifyWhy || (CrimeCore.IsCriminal(player) && CrimeCore.HasMask(player)))
                    Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, error, 3500);
                return false;
            }
            Pending[player.GetUUID()] = house.ID;
            Trigger.ClientEvent(player, "openDialog", "BURGLARY_START",
                $"Взломать дом #{house.ID}? Понадобится отмычка. Соседи могут вызвать полицию.");
            return true;
        }

        /// <summary>«Да» в диалоге (Main.dialogCallback BURGLARY_START).</summary>
        public static void OnConfirm(ExtPlayer player)
        {
            if (!player.IsCharacterData() || !Pending.Remove(player.GetUUID(), out var houseId))
                return;
            var house = HouseManager.Houses.FirstOrDefault(h => h.ID == houseId);
            var error = Check(player, house);
            if (error == null && player.Position.DistanceTo(house.Position) > 3f)
                error = "Подойдите к двери";
            if (error != null)
            {
                Notify.Send(player, NotifyType.Error, NotifyPosition.BottomCenter, error, 3500);
                return;
            }

            // Сложность замка: дешёвые дома проще, дорогие — точнее угол
            var difficulty = house.Type <= 2 ? 8 : house.Type <= 5 ? 6 : 4;
            Busy.Add(house.ID);
            if (!LockBreak.Start(player, "burglary", house.ID, difficulty, $"Взлом дома #{house.ID}",
                p => { Busy.Remove(house.ID); Enter(p, house); },
                (p, reason) => Busy.Remove(house.ID)))
            {
                Busy.Remove(house.ID);
                return;
            }

            Commands.RPChat("sme", player, "ковыряется в дверном замке");
            // Жильцы в игре узнают о взломе сразу — могут успеть вернуться или вызвать полицию
            foreach (var resident in OnlineResidents(house))
            {
                if (resident == player)
                    continue;
                Notify.Send(resident, NotifyType.Warning, NotifyPosition.BottomCenter, $"Сигнализация: кто-то взламывает ваш дом #{house.ID}!", 8000);
                Trigger.ClientEvent(resident, "createWaypoint", house.Position.X, house.Position.Y);
            }
            // Соседи: у дорогих домов бдительнее
            var chance = house.Type >= 6 ? 50 : 35;
            if (CrimeCore.Roll(chance))
                CrimeCore.CallPolice(player, house.Position, $"house_{house.ID}",
                    $"Соседи сообщают о взломе дома #{house.ID}. Подозреваемый в маске.", 2, "Взлом дома");
            AuditStart(player, house);
        }

        private static void AuditStart(ExtPlayer player, House house) =>
            BlackMarket.Audit.AuditLog.Write("burglary_start", player.GetUUID(), details: new { house = house.ID, owner = house.Owner, type = house.Type });

        // ------------------------------------------------------------------ внутри дома

        private static List<Vector3> BuildPoints(House house)
        {
            var interior = house.InteriorPosition + new Vector3(0, 0, 1.12);
            var points = new List<Vector3>();
            if (house.Type >= 1 && house.Type - 1 < HouseManager.HouseHealkitPos.Length)
                points.Add(HouseManager.HouseHealkitPos[house.Type - 1]);
            var pet = HouseManager.HouseTypeList[house.Type].PetPosition;
            if (pet != null && (Math.Abs(pet.X) > 0.1 || Math.Abs(pet.Y) > 0.1))
                points.Add(pet + new Vector3(0, 0, 1.0));
            var anchor = points.Count > 0 ? points[points.Count - 1] : interior + new Vector3(2.0, 1.0, 0);
            points.Add(new Vector3((interior.X + anchor.X) / 2, (interior.Y + anchor.Y) / 2, (interior.Z + anchor.Z) / 2));
            var offsets = new[] { new Vector3(1.6, 0.6, 0), new Vector3(-1.4, 1.2, 0), new Vector3(0.8, -1.5, 0) };
            var i = 0;
            while (points.Count < 3)
                points.Add(interior + offsets[i++ % offsets.Length]);
            return points.Take(3).ToList();
        }

        private static void Enter(ExtPlayer player, House house)
        {
            if (!player.IsCharacterData())
                return;
            var uuid = player.GetUUID();
            house.SendPlayer(player);

            var session = new Session { Uuid = uuid, HouseId = house.ID, Owner = house.Owner, Started = DateTime.Now };
            foreach (var position in BuildPoints(house))
            {
                var point = new LootPoint { Position = position };
                point.Shape = CustomColShape.CreateCylinderColShape(position - new Vector3(0, 0, 1.2), 1.3f, 2.6f, (uint)house.Dimension,
                    ColShapeEnums.BurglaryLoot, session.Points.Count, house.ID);
                session.Points.Add(point);
            }
            Sessions[uuid] = session;
            HouseCooldown[house.ID] = DateTime.Now.AddHours(HouseCooldownHours);
            PlayerCooldown[uuid] = DateTime.Now.AddMinutes(PlayerCooldownMinutes);

            Trigger.ClientEvent(player, "client.burglary.points", JsonConvert.SerializeObject(session.Points.Select(p => new { x = p.Position.X, y = p.Position.Y, z = p.Position.Z })));
            Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Вы внутри. Обыщите отмеченные места — у вас {InsideMinutes} минут", 5000);
            if (!_timer)
            {
                _timer = true;
                Timers.Start("crime.burglary", 2000, Tick, true);
            }
        }

        [Interaction(ColShapeEnums.BurglaryLoot)]
        public static void OnLoot(ExtPlayer player, int index, int houseId)
        {
            try
            {
                var sessionData = player.GetSessionData();
                if (sessionData == null || !Sessions.TryGetValue(player.GetUUID(), out var session) || session.HouseId != houseId)
                    return;
                if (index < 0 || index >= session.Points.Count)
                    return;
                var point = session.Points[index];
                if (point.Taken || point.Searching || session.Points.Any(p => p.Searching))
                    return;
                point.Searching = true;
                Trigger.PlayAnimation(player, "amb@prop_human_bum_bin@base", "base", 1);
                Trigger.ClientEvent(player, "blockMove", true);
                Main.OnAntiAnim(player);
                NAPI.Task.Run(() => FinishLoot(player, session, point), SearchSeconds * 1000);
            }
            catch (Exception e)
            {
                CrimeCore.Log.Write($"Burglary OnLoot Exception: {e}");
            }
        }

        private static void FinishLoot(ExtPlayer player, Session session, LootPoint point)
        {
            try
            {
                point.Searching = false;
                if (player == null || !player.IsCharacterData())
                    return;
                Trigger.StopAnimation(player);
                Trigger.ClientEvent(player, "blockMove", false);
                Main.OffAntiAnim(player);
                var characterData = player.GetCharacterData();
                if (!Sessions.ContainsKey(session.Uuid) || characterData.InsideHouseID != session.HouseId || player.Position.DistanceTo(point.Position) > 2.5f)
                    return;

                var house = HouseManager.Houses.FirstOrDefault(h => h.ID == session.HouseId);
                var mult = house == null ? 1.0 : house.Type >= 8 ? 2.2 : house.Type >= 5 ? 1.6 : house.Type >= 3 ? 1.2 : 1.0;
                var roll = CrimeCore.Rnd.Next(100);
                string found;
                if (roll < 45)
                    found = GiveItem(player, ItemId.StolenElectronics, 1) ? "Краденая техника" : null;
                else if (roll < 80)
                {
                    var count = 1 + (int)Math.Round(CrimeCore.Rnd.Next(0, 2) * mult);
                    found = GiveItem(player, ItemId.StolenJewelry, count) ? $"Краденые украшения ×{count}" : null;
                }
                else
                {
                    var cash = CrimeCore.Payout(player, (long)(CrimeCore.Rnd.Next(200, 901) * mult));
                    MoneySystem.Wallet.Change(player, cash);
                    GameLog.Money("server", $"player({characterData.UUID})", cash, $"burglary({session.HouseId})");
                    found = $"Наличные ${cash}{CrimeCore.FundSuffix(player)}";
                }
                if (found == null)
                    return; // инвентарь полон — точку можно обыскать ещё раз

                point.Taken = true;
                session.Looted = true;
                CustomColShape.DeleteColShape(point.Shape);
                point.Shape = null;
                Trigger.ClientEvent(player, "client.burglary.taken", session.Points.IndexOf(point));
                Notify.Send(player, NotifyType.Success, NotifyPosition.BottomCenter, $"Найдено: {found}", 3500);
                BlackMarket.Audit.AuditLog.Write("burglary_loot", player.GetUUID(), details: new { house = session.HouseId, found });
            }
            catch (Exception e)
            {
                CrimeCore.Log.Write($"Burglary FinishLoot Exception: {e}");
            }
        }

        private static bool GiveItem(ExtPlayer player, ItemId itemId, int count)
        {
            if (Chars.Repository.isFreeSlots(player, itemId, count) != 0)
                return false;
            return Chars.Repository.AddNewItem(player, $"char_{player.GetUUID()}", "inventory", itemId, count) != -1;
        }

        private static void Tick()
        {
            foreach (var session in Sessions.Values.ToList())
            {
                var player = Main.GetPlayerByUUID(session.Uuid);
                var inside = player != null && player.GetCharacterData()?.InsideHouseID == session.HouseId;
                if (!inside)
                {
                    End(session, player);
                    continue;
                }
                if (!session.LateAlert && (DateTime.Now - session.Started).TotalMinutes >= InsideMinutes)
                {
                    session.LateAlert = true;
                    var house = HouseManager.Houses.FirstOrDefault(h => h.ID == session.HouseId);
                    if (house != null)
                        CrimeCore.CallPolice(player, house.Position, $"house_{house.ID}",
                            $"Соседи видят свет и слышат шум в доме #{house.ID}.", 2, "Взлом дома");
                }
            }
        }

        private static void End(Session session, ExtPlayer player)
        {
            Sessions.Remove(session.Uuid);
            foreach (var point in session.Points)
            {
                if (point.Shape != null)
                    CustomColShape.DeleteColShape(point.Shape);
                point.Shape = null;
            }
            if (player != null && player.IsCharacterData())
                Trigger.ClientEvent(player, "client.burglary.clear");

            if (!session.Looted)
                return;
            var house = HouseManager.Houses.FirstOrDefault(h => h.ID == session.HouseId);
            if (house != null && player != null && CrimeCore.Roll(15))
                CrimeCore.CallPolice(player, house.Position, $"house_{house.ID}",
                    $"Из дома #{house.ID} вышел человек в маске с сумкой.", 2, "Кража из дома");
            if (!string.IsNullOrEmpty(session.Owner) && Main.PlayerUUIDs.TryGetValue(session.Owner, out var ownerUuid))
                Players.Phone.Messages.Repository.AddSystemMessageToUuid(ownerUuid, (int)DefaultNumber.Polic,
                    $"Ваш дом #{session.HouseId} ограбили. Проверьте имущество и обратитесь в полицию.", DateTime.Now);
        }

        [ServerEvent(Event.PlayerDisconnected)]
        public void OnPlayerDisconnected(ExtPlayer player, DisconnectionType type, string reason)
        {
            if (player == null)
                return;
            Pending.Remove(player.GetUUID());
            if (Sessions.TryGetValue(player.GetUUID(), out var session))
                End(session, null);
        }
    }
}
