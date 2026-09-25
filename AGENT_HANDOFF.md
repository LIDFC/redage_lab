# Итог работы: RedAge v3 (RAGE:MP), ветка `claude/new-session-2adzoy`

Документ для агента, который продолжит работу. Описывает проект, всё, что сделано в этой сессии, и как проверять изменения.

## 1. Проект

- Сервер RAGE:MP 1.1 (NeptuneEvo / RedAge v3), C#-bridge на **netcoreapp3.1**: `dotnet/resources/NeptuneEvo.sln`.
- БД MariaDB, три базы: `ra3_main`, `ra3_mainconfig`, `ra3_mainlogs`. Доступ через linq2db. Также используется Redis.
- Клиентские скрипты: исходники в `src_client/` (webpack) → `client_packages/main.js`.
- Интерфейс (CEF): Svelte 3 в `src_cef/` (webpack 5) → `client_packages/interface/build/`. Картинки, импортированные в Svelte, file-loader кладёт в `client_packages/interface/src/views/...`.
- **Собранные бандлы лежат в git.** После любых правок в `src_client` или `src_cef` их нужно пересобрать и закоммитить.
- CDN: `document.cloud` = `https://cdn-ra3.ragemp.pro/cloud/` (задаётся в `client_packages/interface/local.html`). Клиент всегда грузит интерфейс из `package://interface/local.html`, а не с CDN.
- Пользователь выложил полный архив CDN на Google Drive (id `1qXBy4JNhTarZnxkhENtog_4EqOBo5a-1`). Скачивание: `curl "https://drive.usercontent.google.com/download?id=...&export=download&confirm=t&uuid=<uuid со страницы>"`. Структура архива: `cdn/cloud/…` (зеркало CDN), `cdn/src/…`, `cdn/npc/…` (только звуки).

## 2. Сборка и проверка

```bash
# клиентские скрипты
cd src_client && npm install && npm run build

# интерфейс (Node 17+ нужен legacy OpenSSL и много памяти)
cd src_cef && npm install --legacy-peer-deps
NODE_OPTIONS="--openssl-legacy-provider --max-old-space-size=12288" npm run build   # ~2 минуты, ~1165 warnings — норма

# сервер (dotnet-sdk-8.0 собирает netcoreapp3.1)
dotnet build dotnet/resources/NeptuneEvo.sln   # эталон: 9 warnings, 0 errors
```

Интерфейс без игры проверялся стендом на esbuild и Playwright (Chromium: `/opt/pw-browsers/chromium`):

- мок `api/rage` журналирует `executeClient` и отдаёт фикстуры;
- страница открывается по `http://127.0.0.1:8123`, `cloud/` — симлинк на распакованный архив;
- в стенде нет `window.events`, `window.getItemToCount`, `window.notificationAdd`, `CountUp`. Ошибки про них — артефакт стенда, в игре эти функции есть (`api/events.js`, инвентарь в `PlayerGameMenu` смонтирован всегда).

Стенд лежал во временной папке сессии. Если нужен снова, его придётся собрать заново по этой схеме.

## 3. Развёртывание (VPS Ubuntu 24.04 + MariaDB)

Инструкция: `DEPLOY.md` и `deploy/` (`redage.env.example`, `redage.service`, `start-windows.bat`). Что выяснили при установке у пользователя:

- Сервер лежит в `/opt/redage-srv`, git-клон в `/opt/redage-git`. Пользователь `ragemp`, сервис `redage` (systemd).
- Пароли задаются через `/etc/redage/redage.env` (права 640, `root:ragemp`). Переменные `REDAGE_DB_*`, `REDAGE_REDIS_*` перекрывают `settings/mainDB.json`.
- Для .NET Core 3.1 на 24.04 нужен `libicu66` (ставится deb-пакетом). Берётся `dotnet/runtime` из Linux-пакета RAGE:MP. Сигнатуру событий решил `dotnet/runtime/Bootstrapper.dll` из репозитория.
- `conf.json`: bind `0.0.0.0`, порты 22005 (UDP+TCP) и 22006 (TCP, раздача `client_packages`). Клиент нужен GTA V **Legacy**, не Enhanced.
- `SET GLOBAL max_connections` падает с access denied — это безвредно, значение задаётся в cnf MariaDB. Нужна папка для бэкапов.
- Тестовый режим — `ServerId: 0` (у всех админ-команды). Бой — `ServerId: 1` и `DirectorLogins` в `settings/serverSettings.json`.
- Обновление на VPS:
  ```bash
  cd /opt/redage-git && git pull
  dotnet build dotnet/resources/NeptuneEvo/NeptuneEvo.csproj -c Debug
  # остановить сервер с сохранением данных
  sudo rsync -a --delete client_packages/ /opt/redage-srv/client_packages/
  sudo rsync -a --exclude obj dotnet/resources/ /opt/redage-srv/dotnet/resources/
  sudo chown -R ragemp:ragemp /opt/redage-srv && sudo systemctl start redage
  ```
  Игрокам после обновления интерфейса иногда нужно очистить кэш `client_resources/<ip>_<порт>`.

## 4. Что сделано (коммиты по порядку)

| Коммит | Суть |
|---|---|
| `181404f` | NPC-заказы такси: сервер сам генерирует поездки (`Players/Phone/Taxi/Bots`), проверяет точки, платит водителю |
| `f3de652` | Цены в интерфейсе синхронизированы с серверными |
| `0718611` | Новый интерфейс центра занятости (NPC Эмма Смит, `src_cef/src/views/player/jobselector`) |
| `da3d247` | Боевой конфиг (`ServerId 1`, `DirectorLogins` вместо захардкоженных логинов, `voice-chat`, секреты через env) |
| `ec8514f`, `61103e5` | Восстановлены исходники, которые скрывал `.gitignore` (`Fractions/Organizations/Table/Logs`, `src_client/debug`); починена сборка CEF |
| `123e258` | Пересборка `client_packages` |
| `5044a45` | Интерфейс всегда грузится локально (`src_client/utils/cef.js`) |
| `ec963b1` | Такси: точки посадки у гаражей домов и точек разгрузки бизнесов, проверка земли (больше не на крышах). Лицензии на аренду рабочего транспорта: `WorkManager.GetRentLicense` / `CheckRentLicense` — B для такси, почты и механика; C для автобуса, дальнобойщика и инкассатора |
| `6e4a651` | Меню F3: все картинки imgur и beget заменены (см. п. 5), настоящие каталоги одежды и транспорта, исправлены падения |
| *(этот коммит)* | Автосалон и 24/7 (см. п. 6) |

## 5. Меню F3 (`src_cef/src/views/player/gta5devmenu`)

- Локальные картинки лежат в `gta5devmenu/assets/`: `packs/1..11` (стартовые наборы, `item.id + 1`), `money/1..6`, `premium/*` (короны из архива; локальный `bronze.svg` из основного доната битый), `clothes/0..35` (иконки по id `DonateClothesList`), `quest/*` (логотипы фракций).
- **Одежда:** `shop/elements/shopcl/catalog.js` повторяет `DonateClothesList` в `dotnet/resources/NeptuneEvo/Chars/Donate.cs`: id 0–19 мужская, 20–35 женская. **При изменении каталога на сервере нужно обновить этот файл.** Покупка через `client.donate.buy.clothes` (id); результат сообщает сервер.
- **Транспорт:** новое серверное событие `server.donate.vehicles.load` (`VehicleModel/DonateAutoRoom.cs`). Оно отдаёт `bus_products` с `Type = Donate`, `OtherPrice > 0` и `Toggled`. Клиент (`src_client/gta5devmenu/index.js`) добавляет название из игры и признак вертолёта и вызывает `window.gta5devmenuDonateVehicles`. Кнопка «Купить» ставит метку на донат-салон (`client.donate.vehicles.route`), сама покупка идёт у NPC.
- **Валюта:** обмен RB→$ считается как на сервере: `rb * round(10 * serverDonateDoubleConvert)`.
- **Квесты:** портретов NPC нет ни в архиве, ни на CDN. Поэтому стоят логотипы фракций (мэрия, полиция, EMS, News, организации) или инициалы (`elements/quest/avatar.svelte`). Исправлено падение меню, когда квесты есть, но ни один не закреплён.
- **Прочее:**
  - `src_client/phone/cars.js`: начальное значение `carlist = JSON.stringify("[]")` (раньше в CEF уходил синтаксически битый код);
  - убраны фиктивные `carsList`;
  - аватар, иконка репорта, фоны дома и бизнеса взяты из `views/player/hudevo/phonenew/assets/images`;
  - превью анимаций подключено через import.

## 6. Автосалон и 24/7 (последний коммит)

**Автосалон** (`src_cef/src/views/business/autoshop`, `src_client/vehicle/autoshop.js`)

Было:
- в `authInfo.js` у 218 моделей стояла заглушка «Bravado Wiper» с одним и тем же описанием;
- 31 продаваемой модели (почти весь донат-салон) в справочнике не было, поэтому показывалось `undefined undefined`;
- «Торможение» и «Управляемость» выводили `undefined`, у управляемости стояла единица «км/ч»;
- «Бак» был всегда 100;
- донатная цена показывалась в $ из `gosPrice` (например, 10 000 000$ вместо 100 000 RB);
- логотип `img/autoshop_logo.png` отдавал 404;
- полоски характеристик грузились с imgur.

Стало:
- В `authInfo.js` у всех проданных моделей настоящие марка и модель. Добавлены недостающие (в том числе донатные и вертолёты AirAutoRoom). Опечатки исправлены (Lamgorghini, Cadilac, таб в Taipan, `EntityXF` img). 49 заглушек для моделей, которых нет ни в продаже, ни на CDN, удалены: для них название берётся из игры.
- Клиент передаёт `displayName` (из GXT-лейбла), `boost`, `break` и `ypr` как оценки 0–100 (разгон, торможение, сцепление из нативов). Бак берётся по классу ТС, как `VehicleManager.VehicleTank` на сервере. Удалён мёртвый код, который перезаписывал характеристики значениями первой машины.
- Цена показывается так же, как её спишет сервер (`Core/Carroom.cs`): в донат-салоне `price` в RB; в остальных салонах `price` для себя, а цена для организации (`gosPrice`) — в кнопке «Купить(ОРГ)», если отличается.
- Логотип заменён на `views/assets/images/logo.png`. Полоски характеристик сделаны на CSS.

**24/7** (`src_cef/src/views/business/menu`)

Было:
- логотип и иконки категорий грузились с `u90228c5.beget.tech` (мёртвый хостинг);
- сим-карта, лотерейный билет (у обоих `ItemId 0`), мяч (263) и сумка (-5) не попадали ни в одну категорию и не показывались;
- цены выводились неформатированно;
- `itemsInfo[0].Description` мог уронить интерфейс.

Стало:
- Логотип 24/7 — бейдж на CSS, иконки категорий — встроенные SVG.
- Категории соответствуют реальному ассортименту: `fillProductList` для типа 0 = все `bus_products` с `type = 0`. Всё неизвестное уходит в «Разное».
- Описания для сим-карты и лотереи; безопасные фолбэки картинок; цены через `format("money")`.
- Проверено в стенде: 24 из 24 товаров видны, 0 битых запросов.

## 7. Что осталось или стоит проверить

- В игре не проверялись: F3 → «Транспорт» (новое серверное событие), автосалон (оценки 0–100 из нативов `getVehicleModelMaxBraking` / `getVehicleModelMaxTraction`; если в сборке RAGE их нет, будет `client_trycatch` в логах), 24/7.
- `src_cef/src/api/imgSave.js`: загрузка фото на imgur (`document.imgurClientId` в `App.svelte`). Это отдельная функция, а не картинки. imgur из сети сервера может не работать.
- «Тип топлива» в автосалоне всегда «Regular», в том числе у электромобилей и вертолётов. На сервере различий нет, поэтому оставлено.
- У женских «Ластов» (donate clothes id 30) иконка в архиве — ботинки. Название взято из основного донат-магазина.
- В `gta5devmenu/elements/shop/elements/shopcl/` лежат неиспользуемые копии `cPopup` / `pPopup` / `popupname` / `img`. Их можно удалить.

## 8. Правила работы в этом репозитории

- Разработка в ветке `claude/new-session-2adzoy`, push: `git push -u origin claude/new-session-2adzoy`. PR создавать только по просьбе пользователя.
- В коммитах, PR и коде не упоминать модель ИИ.
- Работающие картинки с CDN не трогать. Заглушки и мёртвые ссылки заменять картинками из архива пользователя или локальными из репозитория. Картинки кладутся в репозиторий, VPS для хранения картинок не используется.
- Пользователь общается по-русски. Инструкции для VPS — готовые команды.
