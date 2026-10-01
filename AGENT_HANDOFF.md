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

## 7i. Чёрный рынок и крипта (`NeptuneEvo/BlackMarket`)

- Доступ: VPN в настройках телефона (только на сессию, `Vpn/VpnState.cs`), при включённом VPN в меню телефона появляется «Чёрный рынок» (`views/blackmarket`, клиент `src_client/phone/blackmarket.js`). Сервер проверяет VPN на `server.blackmarket.open` и каждом `server.blackmarket.action`.
- Крипта (`Crypto/`): личные кошельки (`crypto_wallets`, `reserved` — заблокировано P2P), кошельки криминальных фракций (банды, мафии, байкеры), системный кошелёк комиссий. Все мутации — под `BlackMarketCore.Sync` в главном потоке; запись в БД — очередь `BlackMarketRepository` (один поток, по порядку), `Flush()` при сохранении сервера.
- Лоты (`Methods/Lots.cs`): товар уходит из инвентаря в локацию `bmlot_{id}` (обычная `items_data`), частичная покупка, смена цены, снятие/срок → возврат в инвентарь, остаток — на личный склад. Whitelist и все параметры — `settings/blackmarket.json` (строится по `ItemsInfo`).
- Закладки (`Deliveries/DropManager.cs`): после оплаты товар → `bmdrop_{id}`, объект `prop_mp_drug_package` в случайной точке (по умолчанию — точки аирдропов дальше 220 м от участков полиции), блип только покупателю, забрать может любой (E + 5 с). 90 мин / 30 мин после выхода покупателя, восстанавливаются после рестарта.
- P2P (`P2P/P2PManager.cs`): BTC за наличные, частичная покупка, комиссия в BTC на системный кошелёк. Переводы по номеру телефона, взнос/вывод кошелька банды, обменник $→BTC по курсу из конфига.
- История без имён — `crypto_history`, полный аудит — `blackmarket_audit`. Команды: `/crypto`, `/fcrypto`, `/bm` (см. `Admin/`). SQL-схема: `database/systems/blackmarket.sql`.
- Обнал (`Crypto/CashOut.cs`, вкладка «Обнал»): только у Мавра (`cashoutPoint`, 5 м). Сумка `BagWithMoney` → BTC (комиссия 5%), BTC → наличные (комиссия 10%, шанс розыска 15% — +1 звезда). Параметры `launderFeePercent`, `cashoutFeePercent`, `cashoutWantedChance`, `/bm cfg launderfee|cashoutfee|wanted`.

## 7j. Приложение «Авто» в телефоне (бывший «Транспорт»)

- CEF `phonenew/components/cars/`: `index.svelte` (вкладки «Панель» / «Мои машины»), `panel.svelte`, `list.svelte` (карточка гаража + улучшение), `car.svelte` (GPS, эвакуация, ключ, замки, восстановление, продажа, место в гараже), стили `auto.sass`.
- Клиент `src_client/phone/cars.js`: rpc `panelState`, `garageInfo`; события `client.phone.cars.panel|parking|upgradeGarage`. Синхронизация окон/фар/салона — `src_client/vehicle/panel.js` (shared data `vWindows` битовая маска, `vLights` 0/1/2, `vInterior`), режим езды `vDriveMode` множит крутящий момент в `player/render.js`.
- Сервер `Core/VehiclePanel.cs` (`server.vehicle.panel`): водитель — всё, пассажир — только своя дверь/окно. Расход топлива по режиму — `VehiclePanel.FuelRate` в `Core/Vehicle.cs` FuelControl.
- Эвакуация, смена замков и места в гараже больше не требуют быть у гаража (`HouseManager.VehicleAction`, `GarageManager.UpdateCarSlots` — добавлены проверки индекса места и принадлежности машины дому). Кнопка «Выбор парковки» в «Имуществе» ведёт в «Авто», меню дома `HouseMenu` для машин не открывается.

- Тёмная тема «Авто» (стили `cars/auto.sass`, статусы машин `cars/data.js::carState`). Улучшения гаражей в приложении нет — на сервере такой системы не используем.

## 7k. Банкомат (FLEECA)

- `src_cef/src/views/player/atm/`: дизайн по архиву пользователя (фон `images/bg.jpg`, карта `images/card.png`), шрифт Gilroy из HUD. Протокол прежний (`window.atm.open/reset`, `atmCB`, `atmVal`, `MoneySystem/Bank.cs`).
- Исправлено: данные `setatm` теперь обновляются в открытом окне; выбор бизнеса из списка (раньше открывался последний); ввод только цифр; `atmClose` возвращает в меню.

## 7l. Планшет (K)

- CEF `hudevo/tablet/` (оболочка, `apps/business/*`, `apps/forbes.svelte`), клиент `src_client/tablet/index.js` (бинд №63 «Планшет», K), сервер `Players/Tablet/Events.cs` (анимация + проп `tablet` из `inventory/attachments.js`).
- Меню фракции/организации — только на планшете (вкладки из «I» убраны, `client.tablet.openApp` из круга и `open_Table`).
- Бизнес: `Businesses/Tablet/*` (`server.tablet.business.load/history/action`). Касса `businesses.cash` и себестоимость `businesshistory.cost` — колонки создаются при старте (`BusinessManager.Init`). Выручка из `takeProd` идёт в кассу; при смене владельца касса выплачивается прежнему.
- Из телефона убраны: бизнес в «Имуществе», Маркетплейс, Forbes. Новое меню создания организации — `views/fractions/create/index.svelte`.

## 7m. Строительные подряды для законных организаций (`Organizations/Contracts`)
- Законная организация = `CrimeOptions == false`. Право `RankToAccess.OrganizationContracts` (63) добавляется в `DefaultAccess` только законным (`ContractsCore.ApplyTypeAccess`), остальным выдаётся рангами.
- Конфиг `settings/org_contracts.json` (время генерации, лимиты, материалы, грузовики, склады, NPC) и шаблоны `settings/org_contract_templates.json` создаются при первом старте. Админ: `/orgc` (5 ур. просмотр, 8 ур. изменения).
- Жизненный цикл: `Manager.cs` под `ContractsCore.Sync`; статус контракта + `organizations.Money/reputation` пишутся одной транзакцией (`ContractsRepository.EnqueueTransaction`). Таблицы: `org_contracts`, `org_contract_gen`, `org_contract_npc`, `org_cargo`, `org_contract_audit` + колонка `organizations.reputation`.
- Склады: бизнес типа 16 (государственный, не продаётся), автосоздание по `shops` из конфига (`MaterialShop.Seed`), ассортимент на склад. Окно — CEF `FractionsContractShop`.
- Груз: `Cargo/*` — универсальный модуль (1 объект = паллета), груз в кузове привязан к номеру машины (`org_cargo.vehicle_number`), `CARGO_COUNT` shared data для кругового меню. Взять — E у паллеты, положить в кузов/взять — круговое меню машины.
- Сдача: `Methods/Delivery.cs` — зона у точки подряда, разгрузка по паллете каждые 3 с, завершение → `ContractsManager.Complete`.
- CEF: раздел «Подряды» в меню организации (`fractions/elements/contracts`), события `server.org.main.contracts.load/action`.
- NPC-бонус: `Npc/ContractNpc.cs`, диалог через `openDialog` → `dialogCallback` case `ORG_CONTRACT_NPC`.

## 7n. Грузовой автосалон, банк Fleeca, пополнение счёта организации
- Бизнес типа 17 «Грузовой автосалон» (`BusinessManager.TruckDealerType`, модели `TruckModels`): грузовики убраны из остальных салонов (`SetupTruckDealer` + `UpdateBusProd`), недостающие цены добавляются в `BusProductsData` в памяти. Автосоздание одного салона — `Businesses/TruckDealer.cs`. Индекс списка машин салона — `BusinessManager.CarsIndex(type)`.
- Банк: общие операции в `MoneySystem/BankOps.cs` (лимиты налогов по VIP, `PrepareTransfer` → диалог `AcceptBankTransfer`, `PayTaxFromCard`, `OrgDeposit`). Банкомат: пункты 5/6 (счёт организации с карты / наличными). Телефон: приложение Fleeca — сервер `Players/Phone/Fleeca` (не `Bank`: namespace `...Phone.Bank` перекрыл бы класс `MoneySystem.Bank`), клиент `src_client/phone/bank.js`, CEF `phonenew/components/bank`.
- Склад организации/фракции (`FractionsStock`) переписан, события прежние (`stockTake/stockPut/stockExit`).
- В `.gitignore` есть `[Ll]ogs/` — не называть папки с кодом `Logs`.

## 7o. Криминал: ограбление домов, трава, скупка у Мавра, угон (`NeptuneEvo/Crime`)
- Общее: `Crime/CrimeCore.cs` (`IsCriminal` — банды/байкеры/мафия или орг. с `CrimeOptions`; `IsNight` 22–06 с учётом `Admin.SetTime`; `CallPolice` — [F] сообщение POLICE/SHERIFF/FIB, метка на 5 мин, всегда звёзды). Новые предметы 393–398 (`WeedSeed`, `WeedRaw`, `StolenElectronics`, `StolenJewelry`, `StolenCarParts`, `WaterBottle`), иконки лежат локально в CEF `inventory/localitems`.
- Мини-игра взлома: CEF `views/player/lockbreak` (порт rage-lock-break, Apache-2.0, NOTICE в папке), клиент `player/lockbreak.js`, сервер `Crime/LockBreak.cs` (`LockBreak.Start(...)`, сломанная отмычка снимается из инвентаря, открытие проверяется по времени/дистанции).
- Ограбление домов `Crime/Burglary/BurglaryManager.cs`: заменило старый взлом ломом (`HouseManager.OnEnterHouse`, мебель в `Selecting.cs` закрыта). Диалог `BURGLARY_START`, 3 точки лута (`ColShapeEnums.BurglaryLoot`), кулдауны дом 6 ч / игрок 15 мин.
- Трава `Crime/Weed/` (`settings/weed.json`, таблица `weed_plants`): семена/вода — меню Мавра 502/503 (+ вода в 24/7), посадка — «Использовать» семена у точек `spots` (±4 м) или в своём доме; полив/сбор/уничтожение полицией — `ColShapeEnums.WeedPlant`; «Использовать» свежую коноплю через `dryMinutes` → `ItemId.Drugs`. Покупатели (`WeedBuyer`, ввод `weed_sell`) автоматически ставятся у точек разгрузки 24/7 и сохраняются в конфиг.
- Скупка краденого `BlackMarket/Fence/FenceManager.cs`: меню Мавра 501 → приложение ЧР, вкладка «Скупка» (`Fence.svelte`), действие `fenceSell` работает без VPN у Мавра. Цена падает от насыщения (`fence.items[].capacity`), восстановление `recoverPercentPerHour`, таблица `blackmarket_fence_demand`, BTC с бонусом `btcBonusPercent`.
- Справка: вкладка «Криминал» в меню фракции/организации (CEF `fractions/elements/crime`, сервер `Crime/CrimeGuide.cs`, события `server.crime.guide.load/gps`) — показывается только криминалу. Поляны травы видны криминалу на карте и маркером на земле (`client.weed.spots`).
- Коэффициент выплат `CrimeCore.PayoutFactor`: фракционные банды/мафия/байкеры ×1.0 (игроку объясняется «20% в общак банды», реально никуда не переводится), криминальные организации ×1.25. Применяется к скупке, NPC-покупателям травы и наличным при ограблении.
- Угон: машина заказа заперта. Дешёвая — «Использовать» отмычку рядом (LockBreak), дорогая (~35%) — «Программатор» (ItemId 399, Мавр пункт 505, $3000; мини-игра `Crime/CyberHack.cs` + CEF `views/player/cyberhack` из присланного пакета cyber-hack; провал — программатор сгорает, сигнализация). Хук в `ItemsUse` → `CarTheftManager.OnUseTool`. Проп в руке: отвёртка `crime_lockpick` (LockBreak) и планшет `tablet` (CyberHack).
- Угон, уровни машин (`CarTheftManager.Cars`): дешёвые (отмычка), дорогие ~30% (программатор), эксклюзив ~10% — кастомные `lx570`, `g636x6`, `mb63gls`, `bmwx6` (программатор 7×7, розыск 90% +3 звезды, 14–16 деталей и бонус Мавра `SuperBonus`). Взлом также через G → «Взломать транспорт» (перехват в `Selecting.cs vehicleSelected case 6` → `CarTheftManager.BreakIn`, прибор выбирается сам). В инвентаре «Использовать» для 393/394/399 — белый список `getItemsUse` в `inventory.svelte`.
- Ограбление: в любое время, жильцы онлайн не мешают (им приходит тревога + метка).
- Угон `Crime/CarTheft/CarTheftManager.cs`: заказ выдаёт NPC — Carter Scott (пункт банды «Угон автотранспорта») или Мавр (пункт 504, для любого криминала); кулдаун 5 мин на команду (банда / организация / игрок), зона разборки 20 м. Машина на случайной точке `dropPoints`, 30% сигнализация → звёзды, разборка в зоне `ColShapeEnums.ChopShop` у Мавра (или `chopPoint` в `blackmarket.json`) → `StolenCarParts`. Машины угона помечены `DeliveryGang`, но обычная сдача их не принимает.

## 7p. Квест новичка (Виталий Дебич, `Quests/Main/Zdobich.cs`, CEF `json/quests/npc_zdobich.json`)
- Этап 9 (рукопожатия) убран: 8 → 10; застрявшие на 9 переводятся на 10 при входе (`qMain.InitQuests`). `Handshaked` считается только для боевого пропуска.
- Этапы 31/33: тексты честно говорят про покупку мед. карты / лицензии у Мавра (пункты 79/78 засчитывают этап), при взятии этапа — метка на Мавра и сообщение в чат (`Zdobich.HintMavr`).
- Этап 34 выдаёт финальную награду: $50 000 и +10 опыта.

## 7q. Мебель и маркетплейс
- Мебель: `Houses/HouseFurniture.cs` `NameModels` — +117 позиций (диваны, кресла, столы, кровати, свет, техника/ТВ, шкафы, декор, картины, кухня/ванная, досуг). Цены первых 40 — из `settings/pricesSettings.json` (`FurtinurePrices`), у остальных — прямо в `NameModels` (`Main.cs` больше не падает на выходе за массив). Мебель без рецепта (`Items` пустой) только покупается. CEF `views/house/furniture`: вкладки по `type`, поиск, иконка категории, если картинки нет на CDN.
- Сохранение мебели: `FurnitureManager.Create` теперь регистрирует дом в памяти (раньше новые дома не могли купить мебель до рестарта); при рестарте мебель дописывается сразу (`FurnitureManager.SaveFurnitureNow`), флаг больше не сбрасывается.
- Маркетплейс (`src_client/EternalDev/marketPlace`): «Выйти» закрывает окно всегда (`close(true)`), смена раздела/открытие сбрасывают «залипшую» модалку, ESC при залипшей модалке сбрасывает её.

- Свои картинки мебели: `src_cef/src/views/house/furniture/props/<модель>.png` (подхватываются `require.context`, приоритет над CDN; используется и в телефоне). В архиве CDN (cdn.zip) картинки есть только у 40 старых предметов.
- Вкладка «Криминал» показывается только в панели криминальной группы (`fractionAllowed` / `orgAllowed` из `CrimeGuide`).
- Загрузка аптечек EMS («Humane Labs») перенесена из порта к настоящему Humane Labs (`Ems.HumaneLabsMedkits`), GPS в телефоне туда же.

## 7r. ESC-менеджер, предпросмотр и перенос мебели
- `src_client/utils/escManager.js`: перехватывает `window.router.setView/setHud` (знает текущее окно `global.cefView`), стек `global.escManager.push/remove` для временных состояний, таблица «окно → событие закрытия». Вызывается из `bind.js c_globalEscape` (при смерти — `closeAll`). Если окно уже закрыто, а курсор остался — `unstick()` снимает блокировку.
- Предпросмотр мебели: CEF «Посмотреть» → `client.furniture.preview` (house/index.js): модель перед игроком, камера облетает её 20 с, ESC — назад в магазин.
- Перенос поставленной мебели: телефон → мебель → «Переместить» (`server.house.furniture.use` type 2): предмет прячется, открывается редактор; отмена (`cancelEdit`) возвращает всё на место.
- Счётчик мебели: `client.furniture.count` (магазин), телефон считает сам; лимит `FurnitureManager.MaxFurniture = 100`.
- Превью новой мебели: `tools/furniture_previews/previews.json` (адреса с Pleb Masters Forge) + `download.py` → `src_cef/.../house/furniture/props/*.jpg`. Домен `assets-gta.plebmasters.de` должен быть доступен.

## 7s. История денег, напоминания о налогах, расположение HUD
- История денег: `MoneySystem/MoneyHistory.cs` — таблица `money_history` (uuid, time, amount, code; индекс по uuid, хранение 30 дней), запись из `GameLog.Money` (разбор `player(N)` в from/to), подписи операций по коду в `Labels`. Fleeca → «История» (`Players/Phone/Fleeca`) показывает последние 50 операций.
- Налоги: `MoneySystem/TaxReminder.cs`, вызывается в `Main.payDayTrigger` после списания: SMS банка (4386) и уведомление на порогах 24/12/3/1 ч.
- Расположение HUD: поле `ChatData.HudLayout` (строка `блок:x,y,s;...`, без кавычек — её передают в CEF в одинарных кавычках), CEF `hudevo/elements/hudlayout.svelte` (CSS-переменные на корневых классах блоков, редактор перетаскиванием), клиент `player/hudlayout.js`, кнопка в Настройки → Настройки худа. Меню настроек отправляет `HudLayout` вместе со своими полями.

## 7t. Админ-панель настроек (/cfg) и погода
- `/cfg` (смотреть с 5 lvl; менять обычные вкладки с 8, «Экономика», «Сервер и налоги», «Зарплаты фракций» — с 9 или логин из `DirectorLogins`) → `Functions/ConfigPanel.cs`, CEF `views/admin/configpanel` (`AdminConfigPanel`), клиент `src_client/admin/cfgpanel.js`.
- Вкладки: трава, ЧР, скупка, подряды (settings/*.json); экономика (таблица `economy`, UPDATE по колонкам, цены Мавра в `FractionDataMats`); сервер и налоги (`serverSettings` / `pricesSettings`: множители, налоги вкл/выкл, `HouseTaxPercent` — новое поле в SDK, применяется в `LoadServerSettings`); зарплаты гос. фракций (`fractionranks.payday`).
- Типы полей: int, float, bool (переключатель), select. Новое поле = одна строка `Int/Float/Bool/Select/Eco(...)` в `BuildSections`.
- Сохранение: права → диапазоны → бэкап (`settings/backup`, 10 последних на вкладку) → связанные поля (откат вкладки при ошибке) → Save/Persist/Apply → история (`settings/cfg_history.json`, 500 записей, откат по кнопке) → adminlog + чат админов `[CFG]`.
- «Перечитать с диска» (`Reload`) — трава (с перепривязкой покупателей `WeedManager.RebindConfig`), ЧР, скупка, подряды, экономика (`Economy.Init`).
- Пресеты — `settings/cfg_presets/<имя>.json` ({вкладка: {ключ: значение}}), применяются тем же путём, что и «Сохранить».
- Подсказки «в деньгах» (`Section.Info`): доход с куста, потери при обнале, диапазон цен скупки, налог дома, сумма зарплат на ближайший PayDay.
- Погода: в телефоне был захардкожен минус перед температурой; сервер (`World/Weather/Repository.cs`) сдвигает температуру по сезону (зима −10, весна −4, осень −7), минимум +2.

## 7u. Сдвиг кастомной одежды под версию GTA (/clothoff)
- Кастомная вещь в `mainconfig.clothes_*` — `variation = -1`, номер в игре = `MaxClothesComponent + cvariation − 1` (причёски `barber_*_hair`: `MaxBarberComponent + cvariation`). После обновления GTA стандартной одежды больше → кастомное съезжает.
- Наши модели — одна коллекция `mp_m/mp_f_clothespack` (ymt в `clothespack`, модели в `clothespack2…47`). Число моделей по слотам — `CustomClothesCount` в `Chars/ClothesOffsets.cs`, пересчёт: `python tools/clothes_offsets/count_custom.py <dlcpacks>`.
- `/clothoff` (6 lvl, `AdminCommands.Tsc`): клиент (`src_client/index.js`, `getOffsets`) на временных NPC считает всего моделей в игре → сервер вычитает кастомные, показывает разницу; `/clothoff apply` пишет `settings/clothesOffsets.json` и вызывает `OnResourceStart` (перечитать одежду без рестарта); `/clothoff reset` — отключить файл.
- Дамп DurtyFree (pedComponentVariations) для этого не годится — в нём нет DLC после mpchristmas3.

## 7v. Уличная качалка и RP-армия
- Качалка: `World/Gym/GymManager.cs` + `src_client/world/gym.js`. Тренажёр рядом (объекты мира Muscle Beach/тюрьма + свои из `settings/gym.json`) → E → анимация через shared data `AnimToKey` (ключи `gym_*` в `synchronization/animation.js`). `/gym add chinup|bench|weights|mat`, `/gym del`, `/gym list` (6 lvl).
- Армия — `Fractions/ArmyRP/*`, настройки `settings/army.json`, точки ставятся в игре `/armyset` (6 lvl): parade, post add/del, zone clear/add, barrier add/del, range, course clear/add, info, reload.
- А: `/salute` `/attention` `/atease`, `/formation` (офицер = доступ Invite), `/post`, `/guardhouse id мин причина` (ArrestType 3: копия камер КПЗ в измерении 3244600, выход у штаба в порту), `/unguardhouse`, `/returnguns` (армейский серийник 1014xxxxx), `/armylog`. Таблица `army_weapon_log` создаётся сама.
- Б: предмет `ArmyPass` (400), `/basepass id часы`, режимная зона (многоугольник) → предупреждение → метка военным + розыск, шлагбаумы `/gate`.
- В: `/convoy lspd|sheriff|ems|fib|city` после погрузки, метки у армии, премия при разгрузке (хук в `Manager.cs` unload_mats), тревога при уничтожении, `/robconvoy` через LockBreak.
- Г: `/range`, `/course`, `/armyfile`, `/drill`; ремонт на точках — CEF `ArmyRepair` (3 шага), подсказка при повышении (`SetFracRank`). Таблица `army_training` создаётся сама.
- Клиент армии: `src_client/fractions/army.js` (сирена Zancudo, метки, тир, полоса, ремонт).

- Наряды: `ArmyDuty.cs` + CEF `ArmyDuty` (доска нарядов по E, офицер назначает, срок — 1 ч онлайна, таблица `army_duty_orders`); точки `/armyset board|kitchen add|clear|clean add|clear`.
- Техника по рангам: в планшете «Парковка» кнопка «Для всех этой модели» (`server.frac.main.updateVehicleRankModel`).
- Форма: верхи 453/454 в армейском наборе; названия кастомной одежды — `settings/clothesNames.json` (`Chars/ClothesNames.cs`).
- Общая очередь записи в БД: `Database/DbQueue.cs` (ЧР остаётся на своей). Везде полное имя `NeptuneEvo.Database.DbQueue`.
- Качалка: `World/Gym/Fitness.cs` — сила/выносливость (`player_fitness`, лимит прироста в час, спад без тренировок), shared `fitStr` → урон кулаком (`player/damage/index.js`); платные зоны и тренер-NPC — `settings/gym_fitness.json`, `/gym zone add цена дни [радиус]|del`; абонемент — диалог `GymMembership` в `Main.cs`.
- Подработки: `Jobs/DayLabor/DayLabor.cs` (порт — ящики, ферма — CEF `JobFarmGame`), `settings/daylabor.json`, `/daylabor port foreman|pickup|drop [clear]`, `farm foreman|bed [clear]`; диалог `DayLabor` в `Main.cs`; клиент `src_client/jobs/daylabor.js`.
- Механик: после согласия клиента заказ ждёт механика (10 мин), капот открывается сам; механик G → Машина → «Починить машину» → HotWire с ключом (`mech_repair`), оплата после успеха (`AutoMechanic.CompleteRepairOrder`).
- Инкассатор: 4 с анимации у банкомата (`collector_atm`) перед выплатой.

- Армия в Форт Занкудо: штаб/раздевалка/склад в измерении 0, точки — `settings/army.json` (`/armyset point groundrepair|airrepair|alarm|recruiter|guardhouse|fuel`), машины — `/armyset vehmove ground|air` (`ArmyRP/ArmyVehicles.cs`). Блип «National Guard» — на штабе (`MatsWar` в `Manager.cs`), у войны за маты свой временный блип.
- `/dooropen` / `/doorclose` (`World/DoorsOpen.cs`, `settings/doors_open.json`) — открыть дверь, на которую смотрит админ, навсегда для всех.
- Гауптвахта: крупное уведомление, `army_guardhouse_log`; планшет → «Гауптвахта» (`hudevo/tablet/apps/guardhouse.svelte`, `server.tablet.guardhouse.load` в `ArmyDuty.cs`).
- Тренер качалки — окно `QuestsDialog` (`json/quests/work/npc_gym.json`); `QuestsDialog` умеет `{переменные}` из 7-го аргумента `client.quest.open`.
- Армейская заправка `ArmyRP/ArmyFuel.cs` (окно АЗС в режиме `govOnly`), лимит гос. заправки на АЗС теперь в долларах.
- Выносливость: `src_client/player/stamina.js` (свой запас бега, падение), `restoreStamina` из `render.js` убран; сила: −15%…+25% урона кулаком. Упражнения переключаются стрелками (`server.gym.switch`). F3 → Навыки: сила/выносливость (`PlayerStats` //41). Вкладка «Статистика» в меню I убрана.
- Гардероб фракций: `Fractions/Wardrobe/Wardrobe.cs` + CEF `FractionWardrobe` + `src_client/fractions/wardrobe.js`; образ в `fraction_outfits`, торс — переопределение в `ClothesComponents.SetTop`; `OnDutyName = "outfit"`. Наборы лидера скрыты в планшете.
- Биндер: колесо и боковые кнопки мыши (коды 4/5/6).

- Качалка: в Занкудо 4 тренажёра (`GymManager.DefaultSpots`), упражнения своего персонажа — сценарии GTA (`world/gym.js`, флаг `global.gymScenario`, `animation.js` пропускает gym_* для себя). Турник — 1 очко силы за 3 тика.
- Тренер: CEF `GymTrainer` (стиль jobselector), тарифы `plans` в `settings/gym_fitness.json`, `server.gym.buy`.
- Гардероб отправляется частями `client.wardrobe.part` (компактный формат), ошибки — уведомлением.
- Биндер: кнопки мыши 4/5/6 ловит окно биндера (`client:binder mouse`), в игре — опрос `mp.keys.isDown`.

- Стабильность: `Functions/LagMonitor.cs` (сторож игрового потока, «Фриз игрового потока: N мс»), `Timers` в SDK меряют каждый обработчик («Долгий таймер N мс [поток]: имя», порог `Timers.SlowMs`), разносят первый запуск повторяющихся таймеров и больше не перебирают все таймеры на каждом. `MySQL.Query/QueryRead` пишут «Медленный … N мс [ИГРОВОЙ ПОТОК — фриз]». Для чтения во время игры — `DbQueue.ReadThen(sql, table => …)` (фон → игровой поток); переведены Fitness, наряды, планшет-гауптвахта, гардероб.

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
