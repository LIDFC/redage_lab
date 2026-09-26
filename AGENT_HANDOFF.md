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

Инструкция: `DEPLOY.md` и `deploy/` (`redage.env.example`, `redage.service`, `start-windows.bat`).

### Где что лежит у пользователя (проверено на VPS)

- **Сервер запускается из `/opt/ragemp-srv`** (`WorkingDirectory=/opt/ragemp-srv`, `ExecStart=/opt/ragemp-srv/ragemp-server`). Сервис `redage` (systemd), пользователь `ragemp`.
- `/opt/redage-srv` — лишняя копия, сервер её **не читает**. Из-за неё одно обновление «не применялось»: файлы копировали туда. Перед любым обновлением проверять: `systemctl cat redage | grep -iE "WorkingDirectory|ExecStart"`.
- Пользователь обновляет так: собирает на Windows-виртуалке (Visual Studio, конфигурация **Debug**) или берёт репозиторий из `master` с GitHub, кладёт его в `/tmp/redage_lab` на VPS и копирует нужное вручную.
- Пароли задаются через `/etc/redage/redage.env` (права 640, `root:ragemp`). Переменные `REDAGE_DB_*`, `REDAGE_REDIS_*` перекрывают `settings/mainDB.json`.
- Для .NET Core 3.1 на 24.04 нужен `libicu66` (deb-пакет). Используется `dotnet/runtime` из Linux-пакета RAGE:MP. Сигнатуру событий решил `dotnet/runtime/Bootstrapper.dll` из репозитория.
- `conf.json`: bind `0.0.0.0`, порты 22005 (UDP+TCP) и 22006 (TCP). Клиент нужен GTA V **Legacy**.
- `SET GLOBAL max_connections` падает с access denied — это безвредно (значение задаётся в cnf MariaDB). Нужна папка для бэкапов.
- Тестовый режим — `ServerId: 0`, бой — `ServerId: 1` и `DirectorLogins` в `settings/serverSettings.json`.

### Интерфейс грузился с чужого CDN

Старый `main.js` при `ServerId != 0` открывал `client_packages/interface/cloud.html`, а тот брал `bundle.js` и `bundle.css` с `https://cdn-ra3.ragemp.pro`, то есть со старым оригинальным интерфейсом (imgur, `undefined`). Сейчас:
- `src_client/utils/cef.js` всегда открывает `local.html`;
- `cloud.html` — точная копия `local.html` (коммит `a37a374`).

Если снова кажется, что «интерфейс не обновился», первым делом сравнить хеши `bundle.js` в репозитории и в `/opt/ragemp-srv`, затем проверить `cloud.html`.

### Обновление на VPS (что копировать)

Копировать **не всю** `client_packages`: она большая, и `cp -a` / `rsync` без прогресса выглядят как зависание. Пользователь из-за этого прерывал копирование.

```bash
S=/opt/ragemp-srv
systemctl stop redage            # перед этим сохранение — как привык пользователь
rsync -a --info=progress2 /tmp/redage_lab/client_packages/interface/ $S/client_packages/interface/
cp -v /tmp/redage_lab/client_packages/main.js $S/client_packages/main.js
cp -r /tmp/redage_lab/dotnet/resources/NeptuneEvo/bin/. $S/dotnet/resources/NeptuneEvo/bin/
cp -v /tmp/redage_lab/dotnet/resources/NeptuneEvo/meta.xml $S/dotnet/resources/NeptuneEvo/meta.xml
cp -v /tmp/redage_lab/settings/marketplace.json $S/settings/   # один раз, конфиг маркетплейса
md5sum /tmp/redage_lab/client_packages/interface/build/bundle.js $S/client_packages/interface/build/bundle.js
chown -R ragemp:ragemp $S && systemctl start redage
```

`bin/` появляется после сборки в Visual Studio (или `dotnet build ... -c Debug` прямо на VPS). После обновления интерфейса игроку нужно удалить `C:\RAGEMP\client_resources`.

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
| `8e226e8` | Автосалон и 24/7 (см. п. 6) |
| `a37a374` | `cloud.html` грузит локальный интерфейс, а не CDN оригинального RedAge |
| `2d3b81c`, `098ca0b` | Такси-NPC, NPC-трафик, NPC-работодатели, новое окно аренды, G-меню (см. п. 7, 7a) |
| `8b00a36` | Склады, маркетплейс, такси-NPC через `invoke` (см. п. 7b, 7c) |
| `bc8296f` | Картинки маркетплейса, многоквартирные дома, новая автошкола (см. п. 7d) |
| `2706daa` | Ремонт через HotWire, новое окно АЗС (см. п. 7e) |
| `5b37f7c`, `a25a6c1` | DLC-интерьеры квартир, электрик с мини-игрой, прозрачный фон мини-игр (см. п. 7f) |
| *(последний коммит)* | Подъезды и коридоры с дверьми и лифтом, лофты clawles (см. п. 7g) |

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

## 6. Автосалон и 24/7

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

## 7. Такси, трафик, работодатели, аренда (последний коммит)

**NPC-пассажир такси** (`src_client/phone/taxi/job.js`):
- Пед из `mp.peds.new` по умолчанию заморожен и не выполнял `taskEnterVehicle`. Теперь `freezePosition(false)` вызывается при создании и перед посадкой, а перед посадкой делается `clearPedTasksImmediately`.
- Запертую дверь пед открыть не может, поэтому замок открывается локально (`setDoorsLocked(1)`).
- Радиус посадки 25 м (был 15), подсказка «остановитесь рядом» появляется с 70 м.
- Если за 7 с пед не сел сам, его принудительно сажают через `ped.setIntoVehicle`. Дальше, как раньше, `server.phone.taxijob.botBoarded`, а сервер проверяет дистанцию до точки посадки (60 м).

**NPC-трафик** (`src_client/pritonCode/trafficWithoutSync/index.js`):
- Было: отладочная клавиша HOME, плотность ×3, при входе трафик выключался.
- Стало: трафик включён по умолчанию с умеренной плотностью (машины 0.45, пешеходы 0.6, припаркованные 0.3, бюджет 2).
- В RAGE:MP трафик **не синхронизируется**: у каждого игрока свои NPC-машины. Поэтому выключены NPC-полиция и розыск (`setMaxWantedLevel(0)`, dispatch 1–15), случайные копы, поезда, лодки, мусоровозы и выпадение денег с педов. Посадка в NPC-машину запрещена (`getVehicleIsTryingToEnter` + `mp.vehicles.atHandle`), иначе это был бы бесплатный транспорт, который видит только сам игрок.
- HOME — личный переключатель трафика. Событие `setTraffic` (метро, логин) идёт через `global.setAmbientTrafficBudget`.
- Плотность задаётся константами `TRAFFIC` в начале файла.

**NPC-работодатели** (`dotnet/resources/NeptuneEvo/Jobs/JobEmployers.cs`):
- Диалоги `src_cef/src/json/quests/work/npc_*.json` существовали, но на сервере их никто не создавал и не обрабатывал.
- Теперь на базах с рабочей арендой (`Rentcar.RentPedsData`, зоны `RentCarId.Job*`) вместо безымянного арендодателя стоит работодатель: своя модель, имя из `quests.js`, questName = actor.
- Колшейп остаётся `RentCar`: от него аренда берёт точку спавна. `OnRentMenu` для рабочих зон открывает диалог.
- «Устроиться на работу» (perform) → `qMain.QuestPerform` → `JobEmployers.TryPerform` → `WorkManager.JobJoin` с теми же проверками.
- Второе действие (action) → `qMain.QuestAction` → `TryAction` → `Rentcar.OpenRentMenuInZone`.
- Электрик: отдельный NPC у маркера смены (новый `ColShapeEnums.JobEmployer`, добавлен в конец enum), его action — начать или закончить смену (`Electrician.OnElectrician`).
- Тексты диалогов переписаны; голос охотника, который играл у всех работодателей, убран.

**Аренда** (`src_cef/src/views/player/rentcar/index.svelte`, `Core/Rentcar.cs`, `src_client/vehicle/rentcar.js`):
- **Было:**
  - у рабочих машин блок цены был скрыт, часов 0, в интерфейсе «К оплате: $0»;
  - сервер считал `цена × 0` и выдавал рабочий транспорт бесплатно;
  - скидка VIP в интерфейсе (5/10/15/20%) не совпадала с серверной (10/15/20/25%);
  - кнопка «Картой» ничего не делала.
- **Стало:**
  - сервер присылает итоговую цену `FinalPrice` (VIP + уровень, `GetRentCarCash`) пятым полем;
  - в `server.rentcar.buy` для рабочего транспорта `hour = 1` (разовая оплата за смену, аренда не истекает), для обычной аренды — `Math.Clamp(hour, 1, 8)` (защита от 0 и отрицательных значений).
- Окно переделано в стиле центра занятости Эммы Смит: сетка машин с картинками и названиями из справочника автосалона, справа цена, срок, часы, цвет, «К оплате» и «Арендовать». Это единственное окно аренды в проекте (`PlayerRentCar`), им пользуются и прокат, и рабочие базы.

## 7a. Доработки после проверки в игре

- **Такси-NPC снова не садился.** Пед делал шаг и замирал: клиентские педы RAGE (`mp.peds.new`) статичны, и движок возвращает их на место, `freezePosition(false)` не помогает. Пассажир теперь создаётся нативно, `mp.game.ped.createPed(4, hash, ...)` (класс `BotPed` в `src_client/phone/taxi/job.js`), модель грузится через `global.loadModel`, удаление — `deleteEntity`. Все задачи идут через `mp.game.ai.*` / `mp.game.ped.*` по handle.
- **G-меню** (`src_client/player/circle.js` + `src_cef/src/popups/circle/index.svelte`):
  - белый прямоугольник в центре — это спрайт `circleMenu` из `client_packages/game_resources/raw/redage_textures_001.ytd`, который клиент рисовал каждый кадр без проверки загрузки словаря. Теперь кольцо с подсвеченным сектором к курсору рисует интерфейс (SVG), `OnRenderCircle` пустой;
  - иконки не показывались ни у одного пункта: в шрифте они называются `circle-c-<func>`, а префикс был `circle-`. Префикс исправлен, для действий без глифа (лифты, парные анимации, планшеты, `vmuted` и т.д.) — запасные иконки (`iconFallback`);
  - у пункта `vmuted` не было названия, поэтому он не показывался. Теперь «Заглушить игрока» / «Включить звук игрока».
  - В `redage_textures_001` нет спрайта `zamok`, который используется в `world/doors.js`, так что у запертых дверей может рисоваться белый квадрат. Не исправлялось.

## 7b. Такси-NPC: нативы через `mp.game.invoke`

После перехода на `createPed` пассажир всё равно не садился и дрался, если его сбить (выкинул водителя из машины). Похоже, обёртки `mp.game.entity.doesEntityExist` / `getEntityCoords` / `setEntityInvincible` в этом клиенте RAGE отсутствуют: `exists()` всегда возвращал false, поэтому защита не применялась и стадия посадки не запускалась. Теперь все вызовы идут через `mp.game.invoke("0x…")` (таблица `N` в `src_client/phone/taxi/job.js`):
- `makeCalm()`: блокировка реакций, без рэгдолла, бессмертие, `SET_PED_CAN_BE_DRAGGED_OUT false`, группа `PLAYER`. Повторяется раз в секунду; если пед всё-таки в бою или убегает, задачи сбрасываются;
- `TASK_ENTER_VEHICLE` без флага угона. У float-аргументов добавляется `+0.0001`, иначе `invoke` передаёт их как int.

## 7c. Готовые системы с форумов

Правило пользователя: с форумов напрямую не качать, только читать обсуждения и инструкции. Код берётся с GitHub или из архива пользователя.

**Склады (семейные и личные)**, источник: github.com/drainerw/Advanced-Family-Personal-Warehouse-Storage-RedAge-v3-. Код переписан:
- сервер: `dotnet/resources/NeptuneEvo/Warehouses/`, SQL: `database/systems/warehouse.sql`;
- клиент: `src_client/player/warehouse.js`, UI: `src_cef/src/views/player/warehouse` (view `PlayerWarehouse`);
- инвентарь `publicwarehouse`, `otherType 13`, 300 слотов;
- каждая ячейка — отдельное измерение `10000 + id`;
- у автора не было семейного режима и проверок дистанции. Добавлено: семейную ячейку покупает владелец семьи, пользуются члены с правом `OpenStock`, продать можно только пустую (возврат 50%);
- `/reloadwarehouses` (admin 8+).

**Маркетплейс MAJESTIC** (EternalDev, архив пользователя):
- сервер: `NeptuneEvo/EternalDev/`, конфиг `settings/marketplace.json`, SQL: `database/systems/marketplace.sql`;
- клиент: `src_client/EternalDev/`; CEF: `src_cef/src/eternal-core`, `views/eternal-dev/marketPlace`, `store/marketPlace.js`, иконка в телефоне;
- DLL автора (`EternalCore`) **не используется**: логгер заменён на `MarketLog` (`nLog`), JSON читает `MarketPlaceConfig.Load()`;
- `svelte-range-slider-pips` заменён на обычный `<input type=range>`;
- интерьер аукциона (MLO `q_auc_milo_`) отсутствует, поэтому `interior_positions: []`, а точка аукциона стоит на улице (−827, −699);
- **изменён геймплей:** в payday дома и бизнесы должников уходят на аукцион маркетплейса, а не риелтору/государству (`Main.cs`, `SetPropertyToAuction`);
- продажа дома, бизнеса или машины, выставленных на маркетплейс, заблокирована (`IsOnMarketplace`).

SQL на VPS: `mysql -u root -p <база> < database/systems/<файл>.sql` (таблицы создаются через `IF NOT EXISTS`).

## 7d. Квартиры, автошкола, картинки маркетплейса

**Картинки маркетплейса** (`views/eternal-dev/marketPlace/modules/picture.js`):
- машины и предметы берутся с нашего CDN (`document.cloud`), логика та же, что в инвентаре (`getPng`), в том числе для одежды и купонов на машину;
- дома и бизнесы — локальные скриншоты из `views/player/help/images` (по типу бизнеса);
- аватар — `marketPlace/assets/avatar.svg`. Ссылок на cdn.majestic-files.com больше нет.

**Многоквартирные дома** (`Houses/Apartments/ApartmentManager.cs`, SQL: `database/systems/apartments.sql`). Идея из архива «Apartment System for RedAge 1.1» (koltr). Его `houses.sql` делает `DROP TABLE houses` — **не выполнять**.
- Квартира — обычный `House` плюс строка в `apartment_flats`. Колонку в `houses` не добавляли.
- `House.AttachToApartment` убирает уличный маркер, подпись и блип; `Position` = подъезд.
- `Garage.AttachToApartment` убирает маркер гаража. Въезд у всех квартир общий: `ColShapeEnums.ApartmentGarage` → `GarageManager.OnEnterGarage` с гаражом игрока.
- При старте (`Main.cs` после `HouseManager.Init`) квартиры досоздаются по колонке `plan` дома: House + Garage + банковский счёт.
- Покупка идёт через общий `HouseManager.TryBuyHouse` (вынесен из `server.houseinfo.action`), по двум путям:
  - у подъезда — CEF `HouseApartments`, клиент `src_client/house/apartments.js`;
  - в риэлторском агентстве — вкладка «Многоквартирные дома», `server.rieltagency.buyApartment`.
- Из обычного списка домов агентства и из заданий почтальона квартиры исключены.
- Админ-команды (уровень 8+): `/apartlist`, `/apartcreate Название`, `/apartentrance id`, `/apartgarage id` (сидя в машине), `/apartaddflats id класс цена гараж кол-во`.
- Координаты 6 домов (Integrity Way, Del Perro Heights, Richards Majestic, Tinsel Towers, Weazel Plaza, Alta St) взяты по памяти и в игре не проверены, особенно въезды в гаражи. Eclipse Towers не используется: у входа стоит NPC регистрации семьи.
- Склады перенесены в измерения 2 000 000+ (`WarehouseManager.BaseDimension`): 10000+ пересекалось с измерениями домов.

**Автошкола** (`Core/DrivingSchool.cs`, CEF: `views/player/drivingschool`, клиент: `src_client/player/drivingschool.js`):
- вместо списков-попапов — окно с вкладками «Лицензии», «Теория» и «Экзамен»;
- теория: 10 случайных вопросов из 20, для сдачи нужно 8 (было 3 из 10). Ответы на все вопросы есть в `drivingschool/theory.js`;
- после сданной теории практику можно начать позже без повторной оплаты (`DSchoolData.TheoryPassed`);
- практика: HUD `DrivingPracticeHud` показывает точки, скорость, лимит, ошибки и таймер. Клиент ловит превышение (80 км/ч, для C — 70, дольше 2 с) и удары (падение `bodyHealth` ≥ 35) и шлёт `server.drivingschool.penalty`; 3 ошибки — провал.

## 7e. Ремонт машины (HotWire), АЗС, DLC-квартиры

- **Ремонт.** G → «Машина» → «Починить машину» (`veh_fix`, `vehicleSelected` index 7) → `Core/VehicleRepair.cs`.
  - Нужен открытый капот.
  - Есть ключ (`ItemId.Wrench`, в руке или в инвентаре) — 15 секунд анимации, ключ расходуется.
  - Ключа нет — мини-игра HotWire. CEF `VehicleHotWire` (`views/vehicle/hotwire`) — порт github.com/NikaKondr/hotwire (MIT) с React на Svelte. Клиент: `src_client/vehicle/hotwire.js`, сервер: `server.hotwire.finished` / `server.hotwire.exit`.
  - Защита: сессия на сервере, минимум 3 с на прохождение, 30 с между попытками, проверка дистанции.
  - `pin.svg` автора (7.7 МБ) переведён в PNG; фон-скриншот из Forza убран.
- **АЗС.** `OpenPetrolMenu` передаёт JSON: цена, остаток на станции, бак, топливо, наличные, доступна ли заправка за счёт штата. Новое окно `views/player/gasStation`: шкала бака, литры, слайдер, быстрые 25/50/75%/полный, сумма. Серверная логика `petrol` не менялась.
- **DLC-квартиры GTA5RP (архив пользователя).** Все четыре `dlc.rpf` (`GTA5RP_APARTMENT`, `gta5rp_locations`, `GTA5RP_META`, `gta5rp_ymap`) зашифрованы NG (`0x0FEFFFFF`). Без ключей из GTA5.exe их не прочитать, поэтому координаты интерьеров нужно выгрузить в CodeWalker или OpenIV на стороне пользователя (ymap/ytyp → XML).

## 7f. DLC-интерьеры квартир, электрик на стройке

**DLC-квартиры.** Пользователь выгрузил в CodeWalker XML из `GTA5RP_APARTMENT` (сам `dlc.rpf` зашифрован NG).
- `Houses/Apartments/ApartmentInteriors.cs` — каталог 80 интерьеров: 5 MLO `int_ap_house_1_1..5_milo_` в (250|285|320|355|380, 0, −50), в каждом 16 комнат `House_S_N` по оси Y (размеры из `int_ap_house.ytyp`).
- Квартира получает интерьер по классу дома (`ApartmentInteriors.Pick`) и случайный стиль. Номер хранится в `apartment_flats.interior` (колонку сервер добавляет сам; запасной вариант — `database/systems/apartments_interiors.sql`).
- `House.SetCustomInterior` / `InteriorPosition` переносят вход, маркер выхода и аптечку. Питомцы в таких квартирах не появляются.
- Включается в `settings/apartments.json` → `dlcInteriors`. Без DLC у игроков будет пустота.
- Клиент подгружает IPL (`src_client/world/dlcApartments.js`). Сам DLC кладётся в `client_packages/game_resources/dlcpacks/GTA5RP_APARTMENT/dlc.rpf` (в репозитории его нет).
- Админ-команды: `/aptint id` — осмотреть интерьер, `/aptintset id` — сохранить точку входа (`settings/apartment_interiors.json`).
- Не подключено, потому что нет ytyp с комнатами: `clawles`, `kor_*` (коридоры), `stair_*`, `kor_bich*`, особняк, `int_garage` (нужны позиции машин).

**Электрик на стройке** (`Jobs/Electrician.cs`):
- смену начинает прораб (NPC `npc_electrician` из `JobEmployers`, теперь «Прораб»): форма и каска (мужская 145, женская 144);
- на точке по E открывается мини-игра «кабели RJ45»: порт мини-игры Farko с Vue на Svelte, `views/jobs/electrician`, клиент `src_client/player/electricianGame.js`;
- оплата только за пройденную игру: `ElectricianPayment × PaymentMultiplier (3)`;
- точки и прораб хранятся в `settings/electrician.json` (по умолчанию — старые точки подстанции). Настройка: `/elecforeman`, `/elecpointsclear`, `/elecpoint`.

## 7g. Подъезды и коридоры (int_mp_kor.ytyp)

`Houses/Apartments/ApartmentHalls.cs`: коридоры из DLC с координатами дверей, взятыми из entities MLO.
- `elit` — `kor_elit_1` (700, 1300, −186.3): 10 этажей × 10 дверей, лифт;
- `med` — `kor_med_1` (500, 1300, −186.3): 10 × 10, лифт;
- `bich1..5` — `kor_bichN_1` (−200, −100−10(N−1), −100): лестничный подъезд, N этажей × 4 двери, квартиры со 2-го этажа.

Как устроено:
- Тип подъезда — колонка `apartment_buildings.hall`, сервер добавляет её сам и назначает автоматически (Премиум/Люкс → `elit`, до 20 квартир → `bichN`, иначе `med`). Поменять: `/aparthall id elit|med|bich1..5|none`.
- У каждого дома свой экземпляр коридора: измерение `3 000 000 + id`.
- Дверь квартиры — обычный колшейп дома `EnterHouse` в измерении подъезда, поэтому покупка, осмотр, замок, приглашения и лом работают как у домов.
- `House.SetExit` — выход из квартиры к своей двери; `ExteriorPos` = подъезд, так что выход из игры в коридоре возвращает к дому.
- Меню у входа: кнопка «Войти в подъезд» (`action "hall"`). Внизу коридора — выход на улицу (`ApartmentHallExit`), на этажах — лифт (`ApartmentElevator`, список этажей).
- Интерьеры квартир дополнены «лофтами» `clawles`, id 81–125 (`int_mp_apartment_1.ytyp`).
- Точки у дверей рассчитаны на 0.8 м от петли двери; в игре не проверены.

## 7h. Имена машин, репорты, гаражи квартир, HUD MkeiitRR

- **Имена машин**: `src_cef/src/api/vehicleName.js` — `vehicleName("bmwm5")` → «BMW M5» по справочнику автосалона (`views/business/autoshop/authInfo.js`, марка + модель). Подключено: парковка дома, телефон (машины), фракционная парковка, штрафы, полицейский компьютер, ключи в инвентаре, аренда авиа, маркетплейс. Модели без записи в справочнике — в `Object.assign(names, …)` там же.
- **Репорты (F6)**: `views/player/reports/index.svelte` переписан: вкладки/поиск, действия с подписями, Enter — отправить, быстрые ответы + свои шаблоны (localStorage администратора). Логика событий прежняя (`src_client/player/report.js`).
- **Гаражи квартир по классу**: `Houses/Apartments/ApartmentGarages.cs`. Эконом → 2 места, Комфорт → 6 (GTA), Комфорт+ … Люкс → 5 залов DLC `int_garage` (650, 500, −50): 12/15/20/28/29 мест (типы гаража 10–14). Без DLC — GTA 10/15/23/38. Тип выставляется при старте по классу (`ApartmentManager.ApplyGarage`, `Garage.SetApartmentType`), улучшение гаража квартиры заблокировано. Параметр гаража в `/apartaddflats` и `plan` теперь игнорируется.
- **Этажи квартир**: квартиры раскладываются по этажам снизу вверх (`ApartmentHall.Slot`), меню подъезда и риэлтор показывают все этажи дома (`floors`, `firstFloor`), пустые — «не продаются».
- **HUD**: «MkeiitRR» вместо RedAge.net, логотип «M», скруглённые плашки и спидометр (блок в конце `hudevo/main.sass`). Заголовок паузы — `src_client/player/auth.js`. Имя сервера в плашке — `settings/serverSettings.json` → `ServerName`.
- Удалена реклама RAGEMP.PRO (base64-строка в `Character/Load/Repository.cs`).

## 8. Что осталось или стоит проверить

- В игре не проверены (проверены только в стенде или сборкой):
  - посадка NPC в такси (нативы через `mp.game.invoke`, принудительная посадка через 7 с);
  - склады и маркетплейс (сборка есть, в игре не проверены);
  - квартиры: точки подъездов и гаражей, генерация квартир при первом старте, въезд в общий гараж;
  - автошкола: новое окно, HUD практики, штрафы за скорость и удары;
  - G-меню: SVG-кольцо и иконки;
  - NPC-трафик: `enableDispatchService`, `setCreateRandomCops*` и т.п. обёрнуты в try; если в сборке RAGE их нет, они просто не сработают;
  - NPC-работодатели и открытие аренды из диалога;
  - F3 → «Транспорт»;
  - оценки 0–100 в автосалоне.
- `src_cef/src/api/imgSave.js`: загрузка фото на imgur (`document.imgurClientId` в `App.svelte`). Это отдельная функция, а не картинки. imgur из сети сервера может не работать.
- «Тип топлива» в автосалоне всегда «Regular», в том числе у электромобилей и вертолётов. На сервере различий нет, поэтому оставлено.
- У женских «Ластов» (donate clothes id 30) иконка в архиве — ботинки. Название взято из основного донат-магазина.
- В `gta5devmenu/elements/shop/elements/shopcl/` лежат неиспользуемые копии `cPopup` / `pPopup` / `popupname` / `img`. Их можно удалить.

## 9. Правила работы в этом репозитории

- Разработка в ветке `claude/new-session-2adzoy`, push: `git push -u origin claude/new-session-2adzoy`. PR создавать только по просьбе пользователя.
- В коммитах, PR и коде не упоминать модель ИИ.
- Работающие картинки с CDN не трогать. Заглушки и мёртвые ссылки заменять картинками из архива пользователя или локальными из репозитория. Картинки кладутся в репозиторий, VPS для хранения картинок не используется.
- Пользователь общается по-русски. Инструкции для VPS — готовые команды.
