using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GTANetworkAPI;
using NeptuneEvo.Character;
using NeptuneEvo.Core;
using NeptuneEvo.Functions;
using NeptuneEvo.Handles;
using NeptuneEvo.Organizations.Contracts.Config;
using NeptuneEvo.Organizations.Contracts.Generators;
using NeptuneEvo.Organizations.Contracts.Audit;
using NeptuneEvo.Organizations.Contracts.Methods;
using NeptuneEvo.Organizations.Contracts.Models;

namespace NeptuneEvo.Organizations.Contracts.Admin
{
    /// <summary>
    /// /orgc — строительные подряды (просмотр с 5 уровня, изменения с 8):
    ///  list | taken | info id | tpl | rep orgId
    ///  create [шаблон|*] [кол-во] | gen | delete id | complete id | fail id
    ///  setrep orgId значение | addrep orgId дельта | point шаблон | times 08:00,16:00,00:00 | cfg [ключ значение] | reload | logs [c:id|o:id]
    /// </summary>
    class ContractCommands : Script
    {
        public const int ManageLevel = 8;

        [Command(AdminCommands.orgcontracts, GreedyArg = true)]
        public static void CMD_OrgContracts(ExtPlayer player, string args)
        {
            if (!CommandsAccess.CanUseCmd(player, AdminCommands.orgcontracts))
                return;
            if (!ContractsManager.Ready)
            {
                Chat(player, "Модуль подрядов не загружен (см. лог OrgContracts)");
                return;
            }

            var parts = (args ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var sub = parts.Length > 0 ? parts[0].ToLower() : "";
            int Arg(int i) => parts.Length > i && int.TryParse(parts[i], out var v) ? v : 0;
            var uuid = player.GetUUID();

            switch (sub)
            {
                case "list":
                case "taken":
                    {
                        var list = ContractsManager.GetAll().Where(c => sub == "list" || c.IsActive).ToList();
                        Chat(player, $"Набор {ContractsManager.CurrentSlot ?? "-"}, в памяти {list.Count}, следующая генерация {ContractGenerator.NextSlot(DateTime.Now):dd.MM HH:mm}");
                        foreach (var c in list.Take(25))
                            Chat(player, Short(c));
                    }
                    return;
                case "info":
                    {
                        var c = ContractsManager.Get(Arg(1));
                        if (c == null) { Chat(player, "Контракт не найден (закрытые — в /orgc logs c:id)"); return; }
                        Chat(player, Short(c));
                        Chat(player, $"Шаблон {c.TemplateId} ({c.Type}), точка «{c.PointName}» {c.DeliveryPosition.X:0.#} {c.DeliveryPosition.Y:0.#} {c.DeliveryPosition.Z:0.#}");
                        Chat(player, $"Реп: нужно {c.RequiredReputation}, +{c.ReputationReward}/-{c.ReputationPenalty}; материалы ~{ContractsCore.Money(ContractGenerator.MaterialsCost(c))}, вес {ContractGenerator.TotalKg(c):0} кг");
                        Chat(player, "Материалы: " + string.Join(", ", c.Materials.Select(m => $"{MaterialName(m.Material)} {m.Delivered}/{m.Required} (куплено {m.Purchased})")));
                        if (c.IsActive)
                            Chat(player, $"Принял {c.AcceptedByName} (UUID {c.AcceptedByUuid}) {c.AcceptedAt:dd.MM HH:mm}, срок до {c.DeadlineAt:dd.MM HH:mm}");
                    }
                    return;
                case "tpl":
                    foreach (var t in ContractTemplates.All)
                        Chat(player, $"{t.Id} [{t.Type}] {(t.Enabled ? "" : "(выкл) ")}реп {t.RequiredReputation}, x{t.RewardFactor}, неуст. {t.PenaltyPercent}%, {t.DeadlineMinutes} мин, «{t.PointName}»" +
                            (ContractGenerator.IsValid(t) ? "" : " — ОШИБКА материалов"));
                    return;
                case "rep":
                    {
                        var org = Manager.GetOrganizationData(Arg(1));
                        if (org == null) { Chat(player, "Организация не найдена"); return; }
                        Chat(player, $"{org.Name} (#{org.Id}): репутация {org.Reputation}, бюджет {ContractsCore.Money(org.Money)}, {(ContractsCore.IsLegal(org) ? "законная" : "криминальная/неактивна")}, активных подрядов {ContractsManager.GetActive(org.Id).Count}");
                    }
                    return;
                case "logs":
                    {
                        int contractId = 0, orgId = 0;
                        if (parts.Length > 1 && parts[1].StartsWith("c:")) int.TryParse(parts[1].Substring(2), out contractId);
                        if (parts.Length > 1 && parts[1].StartsWith("o:")) int.TryParse(parts[1].Substring(2), out orgId);
                        Trigger.SetTask(async () =>
                        {
                            var lines = await ContractAudit.Read(contractId, orgId, 15);
                            NAPI.Task.Run(() =>
                            {
                                if (!player.IsCharacterData()) return;
                                Chat(player, $"Аудит: {lines.Count} записей");
                                foreach (var line in lines)
                                    Chat(player, line);
                            });
                        });
                    }
                    return;
            }

            if (!CanManage(player))
                return;

            switch (sub)
            {
                case "create":
                    {
                        var templateId = parts.Length > 1 && parts[1] != "*" ? parts[1] : null;
                        var count = Math.Max(1, Math.Min(10, parts.Length > 2 ? Arg(2) : 1));
                        var created = ContractsManager.CreateByAdmin(templateId, count, uuid);
                        if (created.Count == 0) { Chat(player, "Шаблон не найден или с ошибкой (/orgc tpl)"); return; }
                        GameLog.Admin(player.Name, $"orgc create {templateId ?? "*"} {count}", "");
                        foreach (var c in created)
                            Chat(player, "Создан " + Short(c));
                    }
                    return;
                case "gen":
                    ContractsManager.ForceGenerate();
                    GameLog.Admin(player.Name, "orgc gen", "");
                    Chat(player, $"Новый набор создан: {ContractsManager.CurrentSlot}");
                    return;
                case "delete":
                    Result(player, ContractsManager.Delete(Arg(1), uuid), $"orgc delete {Arg(1)}", "Контракт удалён без неустойки");
                    return;
                case "complete":
                    Result(player, ContractsManager.Complete(Arg(1), uuid, $"Администратор {player.Name}"), $"orgc complete {Arg(1)}", "Контракт завершён, награда выплачена");
                    return;
                case "fail":
                    Result(player, ContractsManager.Fail(Arg(1), uuid, $"Администратор {player.Name}"), $"orgc fail {Arg(1)}", "Контракт провален, неустойка применена");
                    return;
                case "setrep":
                case "addrep":
                    if (parts.Length < 3) { Chat(player, $"/orgc {sub} orgId значение"); return; }
                    Result(player, ContractsManager.SetReputation(Arg(1), Arg(2), uuid, sub == "addrep"), $"orgc {sub} {Arg(1)} {Arg(2)}",
                        $"Репутация: {Manager.GetOrganizationData(Arg(1))?.Reputation}");
                    return;
                case "point":
                    {
                        var template = ContractTemplates.Get(parts.Length > 1 ? parts[1] : "");
                        if (template == null) { Chat(player, "/orgc point шаблон — точка сдачи на вашу позицию"); return; }
                        var pos = player.Position;
                        template.PointX = pos.X; template.PointY = pos.Y; template.PointZ = pos.Z;
                        if (parts.Length > 2)
                            template.PointName = string.Join(" ", parts.Skip(2));
                        ContractTemplates.Save();
                        GameLog.Admin(player.Name, $"orgc point {template.Id}", "");
                        ContractAudit.Write("admin_point", actorUuid: uuid, details: new { template.Id, pos.X, pos.Y, pos.Z, template.PointName });
                        Chat(player, $"Точка «{template.PointName}» шаблона {template.Id} сохранена (для новых контрактов)");
                    }
                    return;
                case "times":
                    {
                        var times = (parts.Length > 1 ? parts[1] : "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
                        var valid = times.All(t => DateTime.TryParseExact(t, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
                        if (times.Count == 0 || !valid) { Chat(player, "/orgc times 08:00,16:00,00:00"); return; }
                        ContractsConfig.Current.GenerationTimes = times;
                        ContractsConfig.Save();
                        GameLog.Admin(player.Name, $"orgc times {parts[1]}", "");
                        ContractAudit.Write("admin_cfg", actorUuid: uuid, details: new { times });
                        Chat(player, $"Время генерации: {string.Join(", ", times)}");
                    }
                    return;
                case "cfg":
                    Config(player, parts, uuid);
                    return;
                case "shop":
                    Shop(player, parts, uuid);
                    return;
                case "reload":
                    ContractsConfig.Load();
                    ContractTemplates.Load();
                    ContractsManager.RegisterCargoTypes();
                    GameLog.Admin(player.Name, "orgc reload", "");
                    Chat(player, $"Конфиг перечитан: шаблонов {ContractTemplates.All.Count}, материалов {ContractsConfig.Current.Materials.Count}, машин {ContractsConfig.Current.Vehicles.Count}");
                    return;
            }

            Chat(player, "/orgc list | taken | info id | tpl | rep orgId | logs [c:id|o:id]");
            Chat(player, "/orgc shop list|add|mat|seed — склады стройматериалов");
            Chat(player, "/orgc create [шаблон|*] [кол-во] | gen | delete id | complete id | fail id | setrep/addrep orgId N | point шаблон [название] | times a,b,c | cfg [ключ значение] | reload");
        }

        /// <summary>/orgc shop list | add материалы|* название | mat bizId материалы | seed</summary>
        private static void Shop(ExtPlayer player, string[] parts, int uuid)
        {
            var action = parts.Length > 1 ? parts[1].ToLower() : "list";
            List<string> ParseMaterials(string value)
            {
                if (string.IsNullOrEmpty(value) || value == "*")
                    return new List<string>();
                var list = value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(m => m.Trim().ToLower()).ToList();
                return list.All(m => ContractsConfig.Current.GetMaterial(m) != null) ? list : null;
            }

            switch (action)
            {
                case "list":
                    foreach (var spot in ContractsConfig.Current.Shops)
                        Chat(player, $"{spot.Id}: «{spot.Name}» бизнес #{spot.BusinessId}{(spot.AutoCreate ? "" : " (без автосоздания)")} — {(spot.Materials.Count == 0 ? "все материалы" : string.Join(", ", spot.Materials))}");
                    Chat(player, "Материалы: " + string.Join(", ", ContractsConfig.Current.Materials.Select(m => $"{m.Id} ({m.Name})")));
                    return;
                case "add":
                    {
                        var materials = ParseMaterials(parts.Length > 2 ? parts[2] : "*");
                        if (materials == null) { Chat(player, "/orgc shop add concrete,brick|* Название склада"); return; }
                        var name = string.Join(" ", parts.Skip(3));
                        MaterialShop.AddByAdmin(player, name, materials, biz =>
                        {
                            GameLog.Admin(player.Name, $"orgc shop add {biz.ID}", "");
                            ContractAudit.Write("admin_shop_add", actorUuid: uuid, details: new { biz.ID, name, materials });
                            Chat(player, $"Склад создан: бизнес #{biz.ID}. Площадка погрузки — ваша позиция; поменять: /createunloadpoint {biz.ID}");
                        });
                    }
                    return;
                case "mat":
                    {
                        var spot = MaterialShop.GetSpot(Arg(parts, 2));
                        var materials = ParseMaterials(parts.Length > 3 ? parts[3] : "");
                        if (spot == null || materials == null) { Chat(player, "/orgc shop mat bizId concrete,steel|*"); return; }
                        spot.Materials = materials;
                        ContractsConfig.Save();
                        if (BusinessManager.BizList.TryGetValue(spot.BusinessId, out var biz))
                            biz.UpdateLabel();
                        GameLog.Admin(player.Name, $"orgc shop mat {spot.BusinessId} {parts[3]}", "");
                        Chat(player, $"«{spot.Name}»: {(materials.Count == 0 ? "все материалы" : string.Join(", ", materials))}");
                    }
                    return;
                case "seed":
                    MaterialShop.Seed();
                    Chat(player, "Проверка складов запущена (/orgc shop list)");
                    return;
            }
            Chat(player, "/orgc shop list | add материалы|* название | mat bizId материалы|* | seed");
        }

        private static int Arg(string[] parts, int i) => parts.Length > i && int.TryParse(parts[i], out var v) ? v : 0;

        private static void Config(ExtPlayer player, string[] parts, int uuid)
        {
            var cfg = ContractsConfig.Current;
            if (parts.Length < 3)
            {
                Chat(player, $"perGen={cfg.ContractsPerGeneration} starter={cfg.StarterContracts} maxActive={cfg.MaxActivePerOrganization} minFactor={cfg.MinRewardFactor} npcChance={cfg.NpcChance}");
                return;
            }
            var key = parts[1].ToLower();
            var ok = double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0;
            if (ok)
            {
                switch (key)
                {
                    case "pergen": cfg.ContractsPerGeneration = Math.Min(20, (int)value); break;
                    case "starter": cfg.StarterContracts = (int)value; break;
                    case "maxactive": cfg.MaxActivePerOrganization = Math.Max(1, (int)value); break;
                    case "minfactor": cfg.MinRewardFactor = Math.Max(1.0, value); break;
                    case "npcchance": cfg.NpcChance = Math.Min(100, (int)value); break;
                    default: ok = false; break;
                }
            }
            if (!ok) { Chat(player, "Ключи: pergen, starter, maxactive, minfactor, npcchance"); return; }
            ContractsConfig.Save();
            GameLog.Admin(player.Name, $"orgc cfg {key} {parts[2]}", "");
            ContractAudit.Write("admin_cfg", actorUuid: uuid, details: new { key, value });
            Chat(player, $"{key} = {parts[2]}");
        }

        private static void Result(ExtPlayer player, bool ok, string log, string success)
        {
            if (!ok)
            {
                Chat(player, "Не найдено или уже закрыто");
                return;
            }
            GameLog.Admin(player.Name, log, "");
            Chat(player, success);
        }

        private static string Short(Contract c)
        {
            var org = c.OrganizationId > 0 ? Manager.GetOrganizationData(c.OrganizationId)?.Name ?? $"#{c.OrganizationId}" : "";
            return $"#{c.Id} {Status(c.Status)}{(org.Length > 0 ? $" [{org}]" : "")} «{c.Title}» {ContractsCore.Money(c.Reward)} / неуст. {ContractsCore.Money(c.Penalty)}, реп {c.RequiredReputation}, {c.ProgressPercent}%";
        }

        public static string Status(ContractStatus status) => status switch
        {
            ContractStatus.Available => "свободен",
            ContractStatus.Active => "в работе",
            ContractStatus.Completed => "выполнен",
            ContractStatus.Failed => "провален",
            ContractStatus.Cancelled => "отменён",
            ContractStatus.Expired => "истёк",
            _ => "удалён",
        };

        private static string MaterialName(string id) => ContractsConfig.Current.GetMaterial(id)?.Name ?? id;

        public static bool CanManage(ExtPlayer player)
        {
            var characterData = player.GetCharacterData();
            if (characterData != null && characterData.AdminLVL >= ManageLevel)
                return true;
            Chat(player, $"Изменение доступно с {ManageLevel} уровня администратора");
            return false;
        }

        public static void Chat(ExtPlayer player, string text) =>
            Trigger.SendChatMessage(player, $"!{{#f5a524}}[Подряды] !{{#fff}}{text}");
    }
}
