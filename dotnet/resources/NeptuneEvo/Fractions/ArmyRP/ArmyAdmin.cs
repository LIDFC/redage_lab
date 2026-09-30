using System;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;

namespace NeptuneEvo.Fractions.ArmyRP
{
    /// <summary>
    /// /armyset — расстановка точек армии в игре (сохраняется в settings/army.json):
    ///  parade | post add Название | post del | zone clear | zone add | barrier add | barrier del |
    ///  range | course clear | course add | board | kitchen add|clear | clean add|clear | info | reload |
    ///  point groundrepair|airrepair|alarm|recruiter|guardhouse | vehmove ground|air
    /// </summary>
    class ArmyAdmin : Script
    {
        private static ArmyConfig Cfg => ArmyConfig.Current;

        [Command(AdminCommands.armyset, GreedyArg = true)]
        public static void CMD_ArmySet(ExtPlayer player, string args = "")
        {
            try
            {
                if (!player.IsCharacterData() || !CommandsAccess.CanUseCmd(player, AdminCommands.armyset))
                    return;
                var parts = (args ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var what = parts.Length > 0 ? parts[0].ToLower() : "";
                var action = parts.Length > 1 ? parts[1].ToLower() : "";
                var pos = player.Position;
                string result;

                switch (what)
                {
                    case "parade":
                        Cfg.ParadePoint = pos;
                        result = "Плац перенесён сюда";
                        break;
                    case "post" when action == "add":
                        {
                            var name = parts.Length > 2 ? string.Join(" ", parts.Skip(2)) : $"Пост {Cfg.Posts.Count + 1}";
                            Cfg.Posts.Add(new ArmyPost { Name = name, Position = pos });
                            ArmyService.CreatePostLabels();
                            result = $"Пост «{name}» добавлен ({Cfg.Posts.Count} всего)";
                            break;
                        }
                    case "post" when action == "del":
                        {
                            var index = Cfg.Posts.FindIndex(p => p.Position.DistanceTo(pos) < 5);
                            if (index == -1) { player.SendChatMessage("Рядом нет поста"); return; }
                            Cfg.Posts.RemoveAt(index);
                            ArmyService.CreatePostLabels();
                            result = "Пост удалён";
                            break;
                        }
                    case "zone" when action == "clear":
                        Cfg.Zone.Clear();
                        result = "Периметр очищен — пока в нём меньше 3 точек, зона не работает. Обойди периметр по кругу: /armyset zone add";
                        break;
                    case "zone" when action == "add":
                        Cfg.Zone.Add(new Vector3(pos.X, pos.Y, 0));
                        result = $"Точка периметра {Cfg.Zone.Count} добавлена";
                        break;
                    case "barrier" when action == "add":
                        Cfg.Barriers.Add(new ArmyBarrier { Position = new Vector3(pos.X, pos.Y, pos.Z - 1.0), Heading = player.Heading });
                        ArmyBase.SpawnBarriers();
                        result = $"Шлагбаум поставлен ({Cfg.Barriers.Count} всего). Встань ровно по направлению проезда перед добавлением";
                        break;
                    case "barrier" when action == "del":
                        {
                            var index = Cfg.Barriers.FindIndex(b => b.Position.DistanceTo(pos) < 6);
                            if (index == -1) { player.SendChatMessage("Рядом нет шлагбаума"); return; }
                            Cfg.Barriers.RemoveAt(index);
                            ArmyBase.SpawnBarriers();
                            result = "Шлагбаум удалён";
                            break;
                        }
                    case "range":
                        Cfg.RangePoint = pos;
                        Cfg.RangeHeading = player.Heading;
                        result = "Огневой рубеж здесь, мишени появятся перед тобой (по направлению взгляда)";
                        break;
                    case "course" when action == "clear":
                        Cfg.Course.Clear();
                        result = "Полоса очищена. Добавляй точки по порядку: /armyset course add";
                        break;
                    case "course" when action == "add":
                        Cfg.Course.Add(pos);
                        result = $"Точка полосы {Cfg.Course.Count} добавлена";
                        break;
                    case "board":
                        Cfg.DutyBoard = pos;
                        ArmyDuty.CreatePoints();
                        result = "Доска нарядов перенесена сюда";
                        break;
                    case "kitchen" when action == "add":
                        Cfg.KitchenPoints.Add(pos);
                        ArmyDuty.CreatePoints();
                        result = $"Точка кухни {Cfg.KitchenPoints.Count} добавлена (по порядку обхода)";
                        break;
                    case "kitchen" when action == "clear":
                        Cfg.KitchenPoints.Clear();
                        ArmyDuty.CreatePoints();
                        result = "Точки кухни очищены";
                        break;
                    case "clean" when action == "add":
                        Cfg.CleanPoints.Add(pos);
                        ArmyDuty.CreatePoints();
                        result = $"Точка уборки {Cfg.CleanPoints.Count} добавлена (по порядку обхода)";
                        break;
                    case "clean" when action == "clear":
                        Cfg.CleanPoints.Clear();
                        ArmyDuty.CreatePoints();
                        result = "Точки уборки очищены — добавьте новые: /armyset clean add";
                        break;
                    case "point":
                        switch (action)
                        {
                            case "groundrepair": Cfg.GroundRepairPoint = pos; break;
                            case "airrepair": Cfg.AirRepairPoint = pos; break;
                            case "alarm": Cfg.AlarmPoint = pos; break;
                            case "recruiter": Cfg.RecruiterPoint = pos; Cfg.RecruiterHeading = player.Heading; break;
                            case "guardhouse": Cfg.GuardhouseExit = pos; break;
                            default:
                                player.SendChatMessage("/armyset point groundrepair|airrepair|alarm|recruiter|guardhouse — точка встанет на твоё место");
                                return;
                        }
                        result = action == "guardhouse"
                            ? "Выход с гауптвахты перенесён сюда"
                            : "Точка сохранена, на карте появится после рестарта сервера";
                        break;
                    case "vehmove" when action == "ground" || action == "air":
                        {
                            var moved = ArmyVehicles.MoveFromPlayer(player, action == "air");
                            Cfg.VehiclesMoved = true;
                            result = $"{(action == "air" ? "Авиация" : "Наземная техника")}: {moved} шт. выстроены рядом вправо от тебя (лицом туда же, куда смотришь ты)";
                            break;
                        }
                    case "reload":
                        ArmyConfig.Load();
                        ArmyService.CreatePostLabels();
                        ArmyBase.SpawnBarriers();
                        ArmyDuty.CreatePoints();
                        result = "settings/army.json перечитан";
                        break;
                    case "info":
                        player.SendChatMessage($"Плац {Cfg.ParadePoint}, постов {Cfg.Posts.Count}, точек периметра {Cfg.Zone.Count}, шлагбаумов {Cfg.Barriers.Count}, точек полосы {Cfg.Course.Count}, рубеж {Cfg.RangePoint}");
                        return;
                    default:
                        player.SendChatMessage("/armyset parade | post add Название | post del | zone clear | zone add | barrier add | barrier del | range | course clear | course add | board | kitchen add|clear | clean add|clear | point ... | vehmove ground|air | info | reload");
                        return;
                }
                ArmyConfig.Save();
                GameLog.Admin(player.Name, $"armyset {args}", "");
                player.SendChatMessage(result);
            }
            catch (Exception e)
            {
                ArmyConfig.Log.Write($"CMD_ArmySet Exception: {e}");
            }
        }
    }
}
