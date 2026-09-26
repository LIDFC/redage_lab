# Развёртывание RedAge (Windows — тесты, Ubuntu VPS — бой)

## 0. Перед первой сборкой — закоммитить недостающие исходники

Раньше `.gitignore` скрывал от git две папки с исходниками. Без них C#-проект **не соберётся**:

- `dotnet/resources/NeptuneEvo/Fractions/Table/Logs/`
- `dotnet/resources/NeptuneEvo/Organizations/Table/Logs/`

Правило в `.gitignore` уже исправлено. Эти папки есть только на машине автора. Их нужно добавить и запушить:

```bash
git add dotnet/resources/NeptuneEvo/Fractions/Table/Logs dotnet/resources/NeptuneEvo/Organizations/Table/Logs
git commit -m "Add fraction/organization log sources"
```

Папка `src_client/debug/` пострадала от того же правила (`[Dd]ebug/`). Она уже восстановлена из собранного `client_packages/main.js` и лежит в репозитории.

## 1. Что нужно установить

| Компонент | Windows (тест) | Ubuntu 22.04/24.04 (VPS) |
|---|---|---|
| Сервер RAGE:MP 1.1 с C#-bridge | `server-files` из установщика RAGE:MP | официальный Linux-пакет `server-files` (linux_x64) |
| .NET SDK (для сборки) | .NET SDK 8 или Visual Studio 2022 | `sudo apt install dotnet-sdk-8.0` |
| MariaDB 10.6+ / MySQL 8 | MariaDB для Windows | `sudo apt install mariadb-server` |
| Redis | Memurai или Redis в WSL | `sudo apt install redis-server` |
| Node.js 18+ (только для пересборки клиента/UI) | nodejs.org | `nvm` или пакет nodejs |

> **Важно для Linux.** `dotnet/runtime/` в репозитории — это **Windows-версия** C#-bridge (`coreclr.dll`). На VPS берите `dotnet/runtime` из Linux-пакета сервера RAGE:MP. Не копируйте Windows-runtime на Linux.

## 2. Сборка серверного мода

```bash
dotnet build dotnet/resources/NeptuneEvo/NeptuneEvo.csproj -c Debug
```

Результат: `dotnet/resources/NeptuneEvo/bin/Debug/netcoreapp3.1/NeptuneEvo.dll`. Этот путь указан в `meta.xml`, конфигурация должна быть именно `Debug`.

Клиент (`client_packages/main.js`) и интерфейс (`client_packages/interface/build`) уже собраны и лежат в репозитории. Пересобирать их нужно только после изменений в `src_client` или `src_cef`:

```bash
# клиентские скрипты
cd src_client && npm install && npm run build

# интерфейс (старый webpack: нужны флаги для Node 17+ и много памяти)
cd src_cef && npm install --legacy-peer-deps
NODE_OPTIONS="--openssl-legacy-provider --max-old-space-size=12288" npm run build
# Windows PowerShell:
# $env:NODE_OPTIONS="--openssl-legacy-provider --max-old-space-size=12288"; npm run build
```

## 3. База данных

```sql
CREATE DATABASE ra3_main       CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE ra3_mainconfig CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE ra3_mainlogs   CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE USER 'redage'@'localhost' IDENTIFIED BY 'сложный-пароль';
GRANT ALL PRIVILEGES ON ra3_main.*       TO 'redage'@'localhost';
GRANT ALL PRIVILEGES ON ra3_mainconfig.* TO 'redage'@'localhost';
GRANT ALL PRIVILEGES ON ra3_mainlogs.*   TO 'redage'@'localhost';
FLUSH PRIVILEGES;
```

```bash
mysql -u root -p ra3_main       < database/main.sql
mysql -u root -p ra3_mainconfig < database/mainconfig.sql
mysql -u root -p ra3_mainlogs   < database/mainlogs.sql
```

Имена `…config` и `…logs` сервер строит сам из `REDAGE_DB_NAME`.

При старте сервер выполняет `SET GLOBAL max_connections = 500`. У отдельного пользователя на это нет прав, поэтому пропишите значение в конфиге MariaDB (`/etc/mysql/mariadb.conf.d/50-server.cnf` → `max_connections = 500`).

> Старый пароль root (`LOLGGRIP12a`) остался в истории git. **Не используйте его нигде** и смените, если он где-то стоял.

## 4. Раскладка файлов сервера

Корень репозитория повторяет структуру папки сервера RAGE:MP. В папку с `ragemp-server(.exe)` скопируйте:

```
conf.json
client_packages/
settings/
json/
dotnet/settings.xml
dotnet/resources/          (с собранным bin/Debug/netcoreapp3.1)
```

`dotnet/runtime/` возьмите из пакета сервера под вашу ОС.

## 5. Конфигурация: тест и бой

`settings/serverSettings.json` в репозитории — **боевой**:

| Параметр | Значение | Зачем |
|---|---|---|
| `ServerId` | `1` | `0` = тестовый режим: **все** игроки получают `/givemoney`, `/givegun`, `/additem`, `/restart`…, отключаются проверки Social Club и антиспам работ |
| `DirectorLogins` | `[]` | логины с доступом к директорским командам. Раньше были захардкожены в коде |
| `IsSqlTrace` | `false` | запись всех SQL-запросов в `mainDB.txt`. Включать только для отладки |
| `IsCheckOnlineLogin` | `true` | запрет двойного входа |

Для **локального теста на Windows** можно временно поставить `"ServerId": 0`: все проверки упрощены, у каждого есть админ-команды. **Не коммитьте это и никогда не запускайте так на VPS.**

`conf.json`: включены `voice-chat` (без него не работает голосовой чат мода), `enable-http-security`, выключен `allow-cef-debugging`. Чтобы сервер появился в списке RAGE:MP, поставьте `"announce": true` и задайте `name`.

### Секреты

Пароли БД и Redis не хранятся в git. Задайте их переменными окружения — они перекрывают `settings/mainDB.json` и `settings/donationsSettings.json`:

`REDAGE_DB_HOST`, `REDAGE_DB_NAME`, `REDAGE_DB_USER`, `REDAGE_DB_PASSWORD`, `REDAGE_DONATE_DB_*`, `REDAGE_REDIS_HOST`, `REDAGE_REDIS_PASSWORD`. Пример: `deploy/redage.env.example`.

## 6. Запуск

### Windows (тест)
1. Скопируйте `deploy/start-windows.bat` к `ragemp-server.exe` и впишите пароль БД.
2. Запустите MariaDB и Redis, затем `start-windows.bat`.
3. Подключитесь клиентом RAGE:MP к `127.0.0.1:22005`.

### Ubuntu VPS (бой)
```bash
sudo useradd -r -m -d /opt/ragemp-srv ragemp
# распаковать сервер RAGE:MP в /opt/ragemp-srv и скопировать файлы из п.4
sudo chown -R ragemp:ragemp /opt/ragemp-srv
sudo chmod +x /opt/ragemp-srv/ragemp-server

sudo mkdir -p /etc/redage
sudo cp deploy/redage.env.example /etc/redage/redage.env
sudo nano /etc/redage/redage.env           # пароли
sudo chmod 600 /etc/redage/redage.env

sudo cp deploy/redage.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now redage
journalctl -u redage -f                    # логи

# порты: 22005 (игра, UDP+TCP) и 22006 (скачивание client_packages, TCP)
sudo ufw allow 22005/udp && sudo ufw allow 22005/tcp && sudo ufw allow 22006/tcp
```

MariaDB и Redis наружу не открывайте: сервер подключается к ним по `127.0.0.1`.
