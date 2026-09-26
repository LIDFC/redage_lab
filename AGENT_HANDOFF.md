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
| *(последний коммит)* | Такси-NPC, NPC-трафик, NPC-работодатели, новое окно аренды (см. п. 7) |

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

## 8. Что осталось или стоит проверить

- В игре не проверены (проверены только в стенде или сборкой):
  - посадка NPC в такси (нативный пед `createPed`, `taskEnterVehicle`, принудительная посадка через 7 с);
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
