using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Redage.SDK.Models;

namespace Redage.SDK
{
    public class Settings
    {
        public static T ReadAsync<T>(string filePath, T config)
        {
            var path = @$"settings/{filePath}.json";

            if (!File.Exists(path))
            {
                using var sw = File.CreateText(path);
                    
                sw.Write(JsonConvert.SerializeObject(config, Formatting.Indented));
                sw.Flush();
                sw.Close();
            }
            else
            {
                using var r = new StreamReader(path);
                string json = r.ReadToEnd();
                config = JsonConvert.DeserializeObject<T>(json);
                r.Close();
            }

            return config;
        }
        /// <summary>
        /// Читает настройки MySQL из settings/{filePath}.json и перекрывает их переменными окружения
        /// {envPrefix}_HOST, {envPrefix}_NAME, {envPrefix}_USER, {envPrefix}_PASSWORD.
        /// Так пароль от боевой БД не нужно хранить в git.
        /// </summary>
        public static T ReadMysql<T>(string filePath, T config, string envPrefix = "REDAGE_DB") where T : Mysql
        {
            config = ReadAsync(filePath, config);
            ApplyMysqlEnv(config, envPrefix);
            return config;
        }

        public static void ApplyMysqlEnv(Mysql config, string envPrefix)
        {
            if (config == null)
                return;

            var host = Environment.GetEnvironmentVariable($"{envPrefix}_HOST");
            var name = Environment.GetEnvironmentVariable($"{envPrefix}_NAME");
            var user = Environment.GetEnvironmentVariable($"{envPrefix}_USER");
            var password = Environment.GetEnvironmentVariable($"{envPrefix}_PASSWORD");

            if (!string.IsNullOrEmpty(host)) config.Server = host;
            if (!string.IsNullOrEmpty(name)) config.DataBase = name;
            if (!string.IsNullOrEmpty(user)) config.User = user;
            if (password != null) config.Password = password;
        }

        public static void Save<T>(string filePath, T config)
        {
            var path = @$"settings/{filePath}.json";

            File.WriteAllText(path, string.Empty);
            using var sw = new StreamWriter(path, true, Encoding.UTF8);
            sw.Write(JsonConvert.SerializeObject(config, Formatting.Indented));
            sw.Close();
        }
    }
}