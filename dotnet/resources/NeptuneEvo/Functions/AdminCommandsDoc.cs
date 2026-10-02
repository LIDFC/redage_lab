using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GTANetworkAPI;
using NeptuneEvo.Handles;
using Redage.SDK;

namespace NeptuneEvo.Functions
{
    /// <summary>
    /// Справочник админ-команд для /cfg → «Команды».
    ///  Описания — Docs/admin_commands.md (копируется в bin при сборке) или settings/admin_commands.md, если он есть (правка без пересборки).
    ///  Уровень — реальный доступ с сервера (CommandsAccess.AdminAccess: код + таблица adminaccess), а не из md.
    ///  Показываются только команды, у которых есть обработчик [Command] — их ищем рефлексией при старте.
    /// </summary>
    public static class AdminCommandsDoc
    {
        private static readonly nLog Log = new nLog("AdminCommandsDoc");
        private const string SettingsPath = "settings/admin_commands.md";

        public class Entry
        {
            public string Short = "";
            public string Syntax = "";
            public List<string> Details = new List<string>();
        }

        private static Dictionary<string, Entry> _docs = new Dictionary<string, Entry>();
        /// <summary>имя команды → синтаксис из параметров обработчика (если в md его нет).</summary>
        private static Dictionary<string, string> _handlers;
        public static bool Loaded { get; private set; }
        public static string Source { get; private set; } = "";

        /// <summary>Где искать копию из сборки: рядом с dll (RAGE может грузить сборку из памяти — тогда Location пуст).</summary>
        private static IEnumerable<string> BinCandidates()
        {
            var location = typeof(AdminCommandsDoc).Assembly.Location;
            if (!string.IsNullOrEmpty(location))
                yield return Path.Combine(Path.GetDirectoryName(location) ?? ".", "Docs", "admin_commands.md");
            yield return Path.Combine(AppContext.BaseDirectory, "Docs", "admin_commands.md");
            yield return "dotnet/resources/NeptuneEvo/bin/Debug/netcoreapp3.1/Docs/admin_commands.md";
            yield return "dotnet/resources/NeptuneEvo/Docs/admin_commands.md";
        }

        private static string BinPath => BinCandidates().FirstOrDefault(File.Exists) ?? BinCandidates().First();

        /// <summary>Перечитать md. Возвращает текст для админа.</summary>
        public static string Load()
        {
            try
            {
                var path = File.Exists(SettingsPath) ? SettingsPath : BinPath;
                if (!File.Exists(path))
                {
                    Loaded = false;
                    Source = "";
                    Log.Write($"Справочник команд не найден ({SettingsPath}, {BinPath})");
                    return "Справочник команд не найден";
                }
                _docs = Parse(File.ReadAllLines(path));
                Loaded = _docs.Count > 0;
                Source = path == SettingsPath ? SettingsPath : "Docs/admin_commands.md";
                return $"Справочник перечитан: {_docs.Count} команд ({Source})";
            }
            catch (Exception e)
            {
                Loaded = false;
                Log.Write($"Load Exception: {e}");
                return "Ошибка чтения справочника, см. лог AdminCommandsDoc";
            }
        }

        private static readonly Regex EntryLine = new Regex(@"^\s*-\s*`([^`]+)`\s*[—–-]\s*(.*)$");

        private static Dictionary<string, Entry> Parse(IEnumerable<string> lines)
        {
            var result = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            var started = false;
            var skipSection = false;
            Entry current = null;
            foreach (var raw in lines)
            {
                var line = raw.TrimEnd();
                if (line.StartsWith("## "))
                {
                    started = true;
                    // «Не реализованы» — команды без обработчика, в окно не идут
                    skipSection = line.IndexOf("Не реализ", StringComparison.OrdinalIgnoreCase) >= 0;
                    current = null;
                    continue;
                }
                if (!started || skipSection)
                    continue;
                var m = EntryLine.Match(line);
                if (m.Success)
                {
                    current = new Entry { Short = m.Groups[2].Value.Trim() };
                    result[m.Groups[1].Value.Trim().ToLower()] = current;
                    continue;
                }
                var t = line.Trim();
                if (current != null && t.StartsWith(">"))
                {
                    var text = t.Substring(1).Trim();
                    if (text.Length == 0)
                        continue;
                    if (current.Syntax.Length == 0 && current.Details.Count == 0 && text.StartsWith("/"))
                        current.Syntax = text;
                    else
                        current.Details.Add(text);
                }
            }
            return result;
        }

        /// <summary>Все команды с обработчиком [Command]: имя → синтаксис по параметрам метода.</summary>
        private static Dictionary<string, string> Handlers
        {
            get
            {
                if (_handlers != null)
                    return _handlers;
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var type in typeof(AdminCommandsDoc).Assembly.GetTypes())
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        // Атрибут RAGE ищем по имени: первый аргумент конструктора — имя команды
                        var attr = method.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "CommandAttribute");
                        if (attr == null || attr.ConstructorArguments.Count == 0 || !(attr.ConstructorArguments[0].Value is string name))
                            continue;
                        var args = method.GetParameters().Skip(1)
                            .Select(p => p.HasDefaultValue ? $"[{p.Name}?]" : $"[{p.Name}]");
                        result[name.ToLower()] = ("/" + name.ToLower() + " " + string.Join(" ", args)).Trim();
                    }
                }
                catch (Exception e)
                {
                    Log.Write($"Handlers Exception: {e.Message}");
                }
                return _handlers = result;
            }
        }

        /// <summary>Команды, доступные админу этого уровня (уровень команды ≤ уровень админа), по уровню и имени.</summary>
        public static List<object> For(int adminLevel)
        {
            var handlers = Handlers;
            return CommandsAccess.GetAdminLevels()
                .Where(c => c.Value >= 1 && c.Value <= adminLevel && handlers.ContainsKey(c.Key))
                .OrderBy(c => c.Value).ThenBy(c => c.Key)
                .Select(c =>
                {
                    _docs.TryGetValue(c.Key, out var doc);
                    return (object) new
                    {
                        name = c.Key.ToLower(),
                        level = (int) c.Value,
                        @short = doc?.Short ?? "",
                        syntax = !string.IsNullOrEmpty(doc?.Syntax) ? doc.Syntax : handlers[c.Key],
                        details = doc != null ? string.Join("\n", doc.Details) : "",
                        documented = doc != null,
                    };
                })
                .ToList();
        }
    }
}
