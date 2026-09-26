const
    clientName = "client.phone.taxijob.",
    rpcName = "rpc.phone.taxijob.",
    serverName = "server.phone.taxijob.";


let selectedOrders = {};
let orders = []
global.isInitTaxiList = false;

// ===================== NPC-заказы =====================
// Заказы от NPC создаёт и проверяет сервер (Players/Phone/Taxi/Bots/Repository.cs).
// Клиент отвечает только за визуал: спавн педа, посадку и высадку.

const botSpawnDistance = 120;     // на каком расстоянии создаём педа у точки посадки
const botBoardDistance = 25;      // на каком расстоянии пассажир начинает садиться
const botHintDistance = 70;       // подсказка «остановитесь рядом с пассажиром»
const botWarpMs = 7000;           // если пед не сел сам за это время — сажаем принудительно
const botFinishDistance = 25;     // радиус точки назначения
const botResendMs = 5000;         // повтор запроса к серверу, если ответа нет

let activeBotTrip = null;

const isBotId = (id) => id <= -100000;

const dist2d = (a, b) => global.vdist2(a, b, false);

const getBotSpawnPos = (pos) => {
    let z = pos.z;
    try {
        const groundZ = mp.game.gameplay.getGroundZFor3dCoord(pos.x, pos.y, pos.z + 1.5, 0, false);
        if (groundZ && Math.abs(groundZ - pos.z) < 6)
            z = groundZ;
    } catch (e) {}

    return new mp.Vector3(pos.x, pos.y, z + 1.0);
};

// Пассажир — обычный GTA-пед (CREATE_PED), а не mp.peds.new:
// клиентские педы RAGE статичны, движок возвращает их на место каждый кадр,
// поэтому пассажир делал шаг и «замерзал». Нативный пед полностью слушается задач.
// Нативы GTA вызываем напрямую по хешам: обёртки mp.game.entity.* есть не во всех версиях клиента,
// и без них пед не получал защиту — реагировал на удары и мог выкинуть водителя из машины.
const N = {
    DOES_ENTITY_EXIST: "0x7239B21A38F536BA",
    GET_ENTITY_COORDS: "0x3FEF770D40960D5A",
    SET_ENTITY_AS_MISSION_ENTITY: "0xAD738C3085FE7E11",
    SET_ENTITY_INVINCIBLE: "0x3882114BDE571AD4",
    SET_ENTITY_VISIBLE: "0xEA1C610A04DB6BBB",
    SET_ENTITY_COORDS_NO_OFFSET: "0x239A3351AC1DA385",
    SET_BLOCKING_OF_NON_TEMPORARY_EVENTS: "0x9F8AA94D6D97DBF4",
    TASK_SET_BLOCKING_OF_NON_TEMPORARY_EVENTS: "0x90D2156198831D69",
    SET_PED_CAN_RAGDOLL: "0xB128377056A54E2A",
    SET_PED_FLEE_ATTRIBUTES: "0x70A2D1137C8ED7C9",
    SET_PED_COMBAT_ATTRIBUTES: "0x9F7794730795E019",
    SET_PED_CONFIG_FLAG: "0x1913FE4CBF41C463",
    SET_PED_CAN_BE_DRAGGED_OUT: "0xC1670E958EEE24E5",
    SET_PED_RELATIONSHIP_GROUP_HASH: "0xC80A74AC829DDD92",
    SET_PED_KEEP_TASK: "0x971D38760FBC02EF",
    IS_PED_IN_COMBAT: "0x4859F1FC66A6278E",
    IS_PED_FLEEING: "0xBBCCE00B381F8482",
    CLEAR_PED_TASKS: "0xE1EF3C1216AFF2CD",
    CLEAR_PED_TASKS_IMMEDIATELY: "0xAAA34F8A7CB32098",
    TASK_ENTER_VEHICLE: "0xC20E50AA46D09CA8",
    TASK_START_SCENARIO_IN_PLACE: "0x142A02425FF02BD9",
    TASK_LEAVE_VEHICLE: "0xD3DBCE61A490BE02",
    TASK_WANDER_STANDARD: "0xBB9CE077274F6A1B",
    SET_PED_INTO_VEHICLE: "0xF75B0D629E1C063D",
};

const native = (hash, ...args) => {
    try { return mp.game.invoke(hash, ...args); } catch (e) { return undefined; }
};

// mp.game.invoke передаёт целые числа как int: нативам с float-параметром нужна дробная часть
const float = (value) => Number.isInteger(value) ? value + 0.0001 : value;

class BotPed {
    constructor(handle) {
        this.handle = handle;
    }
    exists() {
        if (!this.handle)
            return false;
        const result = native(N.DOES_ENTITY_EXIST, this.handle);
        // Если invoke недоступен — считаем, что пед есть (handle получен от createPed)
        return result === undefined ? true : !!result;
    }
    get position() {
        try {
            const pos = mp.game.invokeVector(N.GET_ENTITY_COORDS, this.handle, true);
            if (pos && (pos.x || pos.y))
                return new mp.Vector3(pos.x, pos.y, pos.z);
        } catch (e) {}
        return null;
    }
    // Пассажир не реагирует на удары/выстрелы/сигнал, не убегает, не дерётся и не падает
    makeCalm() {
        const h = this.handle;
        native(N.SET_ENTITY_INVINCIBLE, h, true);
        native(N.SET_PED_CAN_RAGDOLL, h, false);
        native(N.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, h, true);
        native(N.TASK_SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, h, true);
        native(N.SET_PED_FLEE_ATTRIBUTES, h, 0, false);
        native(N.SET_PED_COMBAT_ATTRIBUTES, h, 17, true);   // при угрозе только убегать, не нападать
        native(N.SET_PED_COMBAT_ATTRIBUTES, h, 46, false);
        native(N.SET_PED_CONFIG_FLAG, h, 17, true);         // BlockNonTemporaryEvents
        native(N.SET_PED_CONFIG_FLAG, h, 281, true);        // без «корчится от боли»
        native(N.SET_PED_CONFIG_FLAG, h, 32, false);        // не вылетает через лобовое
        native(N.SET_PED_CAN_BE_DRAGGED_OUT, h, false);
        native(N.SET_PED_RELATIONSHIP_GROUP_HASH, h, mp.game.joaat("PLAYER"));
        native(N.SET_PED_KEEP_TASK, h, true);
    }
    isAgitated() {
        return !!native(N.IS_PED_IN_COMBAT, this.handle, 0) || !!native(N.IS_PED_FLEEING, this.handle);
    }
    clearTasks(immediately = false) {
        native(immediately ? N.CLEAR_PED_TASKS_IMMEDIATELY : N.CLEAR_PED_TASKS, this.handle);
    }
    idle() {
        native(N.TASK_START_SCENARIO_IN_PLACE, this.handle, "WORLD_HUMAN_STAND_MOBILE", 0, true);
    }
    enterVehicle(vehicle, seat, timeout) {
        // flag 1 — продолжать после прерывания; без флагов «угона», поэтому водителя он не вытащит
        native(N.TASK_ENTER_VEHICLE, this.handle, vehicle.handle, timeout, seat, float(1.5), 1, 0);
    }
    leaveVehicle(vehicle) {
        native(N.TASK_LEAVE_VEHICLE, this.handle, vehicle.handle, 0);
    }
    wander() {
        native(N.TASK_WANDER_STANDARD, this.handle, float(10), 10);
    }
    warpIntoVehicle(vehicle, seat) {
        native(N.SET_PED_INTO_VEHICLE, this.handle, vehicle.handle, seat);
    }
    destroy() {
        if (!this.handle)
            return;
        const handle = this.handle;
        this.handle = 0;
        native(N.SET_ENTITY_AS_MISSION_ENTITY, handle, true, true);
        try { mp.game.ped.deletePed(handle); return; } catch (e) {}
        try { mp.game.entity.deleteEntity(handle); return; } catch (e) {}
        // Удалить не удалось — хотя бы убираем с глаз
        native(N.SET_ENTITY_VISIBLE, handle, false, false);
        native(N.SET_ENTITY_COORDS_NO_OFFSET, handle, float(0), float(0), float(-100), false, false, false);
    }
}

const createBotPed = async (model, pos, heading) => {
    const hash = mp.game.joaat(model);
    if (!(await global.loadModel(hash)))
        return null;
    const handle = mp.game.ped.createPed(4, hash, pos.x, pos.y, pos.z, heading || 0, false, true);
    try { mp.game.streaming.setModelAsNoLongerNeeded(hash); } catch (e) {}
    if (!handle)
        return null;
    native(N.SET_ENTITY_AS_MISSION_ENTITY, handle, true, true);
    const ped = new BotPed(handle);
    ped.makeCalm();
    return ped;
};

const destroyBotPed = () => {
    if (activeBotTrip && activeBotTrip.ped) {
        try {
            if (activeBotTrip.ped.exists())
                activeBotTrip.ped.destroy();
        } catch (e) {}
        activeBotTrip.ped = null;
    }
};

const resetBotTrip = (reloadUi = true) => {
    if (activeBotTrip) {
        destroyBotPed();
        mp.events.call('deleteWorkBlip');
    }

    activeBotTrip = null;
    selectedOrders = {};
    global.isTaxiOrder = false;

    if (reloadUi) {
        mp.gui.emmit(`window.listernEvent ('phone.taxijob.update');`);
        mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
    }
};

const setBotSelected = (pos) => {
    selectedOrders.pos = pos;
    selectedOrders.dist = Math.round(dist2d(pos, mp.players.local.position));
    selectedOrders.aStreet = global.getStreetName(pos.x, pos.y, pos.z);
    selectedOrders.aArea = global.getAreaName(pos.x, pos.y, pos.z);
    selectedOrders.area = selectedOrders.aStreet + " - " + selectedOrders.aArea;
};

const removeOrderFromList = (id) => {
    const index = orders.findIndex(o => o.id === id);
    if (index !== -1)
        orders.splice(index, 1);
};

// Сервер предложил NPC-заказ
gm.events.add(clientName + "botAdd", (id, name, posX, posY, posZ) => {
    if (!global.isInitTaxiList)
        return;

    removeOrderFromList(id);

    const pos = new mp.Vector3(posX, posY, posZ);
    const order = {
        id,
        name,
        pos,
        isBot: true,
    };
    order.dist = Math.round(dist2d(pos, mp.players.local.position));
    order.aStreet = global.getStreetName(pos.x, pos.y, pos.z);
    order.aArea = global.getAreaName(pos.x, pos.y, pos.z);
    order.area = order.aStreet + " - " + order.aArea;

    orders.push(order);

    mp.gui.emmit(`window.listernEvent ('phone.taxijob.update');`);
    mp.events.call('phone.notify', 228, translateText("Диспетчер: новый заказ от пассажира ({0}м)", order.dist), 4);
});

// Сервер подтвердил, что таксист принял NPC-заказ
gm.events.add(clientName + "botAccepted", (id, name, model, posX, posY, posZ, heading) => {
    resetBotTrip(false);
    removeOrderFromList(id);

    const pickupPos = new mp.Vector3(posX, posY, posZ);

    activeBotTrip = {
        id,
        model,
        heading,
        stage: "pickup",
        pickupPos,
        destinationPos: null,
        ped: null,
        pedReady: false,
        seat: -2,
        stageTime: Date.now(),
        lastRequestAt: 0,
    };

    selectedOrders = { name, isBot: true };
    setBotSelected(pickupPos);
    global.isTaxiOrder = true;

    mp.events.call('createWaypoint', pickupPos.x, pickupPos.y);
    mp.events.call('createWorkBlip', pickupPos, 5);
    mp.events.call('phone.notify', 228, translateText("Заказ принят. Пассажир ждёт на точке"), 4);

    mp.gui.emmit(`window.listernEvent ('phone.taxijob.update');`);
    mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
});

// Сервер принял посадку и выдал точку назначения
gm.events.add(clientName + "botDestination", (id, posX, posY, posZ) => {
    if (!activeBotTrip || activeBotTrip.id !== id)
        return;

    const destinationPos = new mp.Vector3(posX, posY, posZ);

    activeBotTrip.stage = "to_destination";
    activeBotTrip.destinationPos = destinationPos;
    activeBotTrip.stageTime = Date.now();
    activeBotTrip.lastRequestAt = 0;

    setBotSelected(destinationPos);

    mp.events.call('createWaypoint', destinationPos.x, destinationPos.y);
    mp.events.call('createWorkBlip', destinationPos, 5);
    mp.events.call('phone.notify', 228, translateText("Пассажир в машине, везите его по адресу на карте"), 4);
    mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
});

// Сервер засчитал поездку
gm.events.add(clientName + "botFinished", (id) => {
    if (!activeBotTrip || activeBotTrip.id !== id)
        return;

    const ped = activeBotTrip.ped;
    const vehicle = mp.players.local.vehicle;

    activeBotTrip.ped = null; // пед уходит сам и удаляется по таймеру

    if (ped && ped.exists()) {
        try {
            if (vehicle)
                ped.leaveVehicle(vehicle);
            setTimeout(() => {
                try {
                    if (ped.exists())
                        ped.wander();
                } catch (e) {}
            }, 2500);
        } catch (e) {}

        setTimeout(() => {
            try {
                if (ped.exists())
                    ped.destroy();
            } catch (e) {}
        }, 12000);
    }

    resetBotTrip();
    mp.events.call('phone.notify', 228, translateText("Поездка завершена"), 4);
});

// Заказ отменён/просрочен/завершён со стороны сервера
gm.events.add(clientName + "botReset", (id) => {
    removeOrderFromList(id);

    if (activeBotTrip && activeBotTrip.id === id)
        resetBotTrip();
    else
        mp.gui.emmit(`window.listernEvent ('phone.taxijob.update');`);
});

const requestBotServer = (eventName) => {
    const now = Date.now();
    if (now - activeBotTrip.lastRequestAt < botResendMs)
        return;

    activeBotTrip.lastRequestAt = now;
    mp.events.callRemote(serverName + eventName, activeBotTrip.id);
};

const spawnBotPed = () => {
    if (activeBotTrip.spawning)
        return;
    activeBotTrip.spawning = true;
    activeBotTrip.pedReady = false;

    const trip = activeBotTrip;
    const spawnPos = getBotSpawnPos(trip.pickupPos);
    createBotPed(trip.model, spawnPos, trip.heading).then((ped) => {
        trip.spawning = false;
        if (!ped)
            return;
        // Поездку успели отменить, пока грузилась модель
        if (activeBotTrip !== trip || trip.ped) {
            ped.destroy();
            return;
        }
        trip.ped = ped;
    });
};

// Пед создаётся асинхронно — настраиваем его, когда появится handle
const prepareBotPed = () => {
    const ped = activeBotTrip.ped;
    if (!ped || activeBotTrip.pedReady || !ped.exists())
        return;

    ped.makeCalm();
    ped.idle();
    activeBotTrip.calmCheckAt = Date.now();

    activeBotTrip.pedReady = true;
};

const getFreePassengerSeat = (vehicle) => {
    const seats = [1, 2, 0]; // сначала задние места
    for (const seat of seats) {
        try {
            if (vehicle.isSeatFree(seat))
                return seat;
        } catch (e) {}
    }
    return -2;
};

gm.events.add("render", () => {
    if (!activeBotTrip || !global.isTaxiOrder)
        return;

    const player = mp.players.local;

    // Пед у точки посадки создаётся независимо от того, в машине ли игрок
    if (activeBotTrip.stage === "pickup" && !activeBotTrip.ped && !activeBotTrip.spawning && dist2d(player.position, activeBotTrip.pickupPos) <= botSpawnDistance)
        spawnBotPed();

    prepareBotPed();

    // Раз в секунду: если пассажира всё-таки что-то разозлило/напугало — успокаиваем
    const calmPed = activeBotTrip.ped;
    if (calmPed && activeBotTrip.pedReady && Date.now() - (activeBotTrip.calmCheckAt || 0) > 1000) {
        activeBotTrip.calmCheckAt = Date.now();
        calmPed.makeCalm();
        if (calmPed.isAgitated()) {
            calmPed.clearTasks(true);
            if (activeBotTrip.stage === "pickup")
                calmPed.idle();
            else if (activeBotTrip.stage === "boarding" && mp.players.local.vehicle)
                calmPed.warpIntoVehicle(mp.players.local.vehicle, activeBotTrip.seat);
        }
    }

    const vehicle = player.vehicle;
    if (!vehicle || vehicle.getPedInSeat(-1) !== player.handle)
        return;

    switch (activeBotTrip.stage) {
        case "pickup": {
            const ped = activeBotTrip.ped;
            if (!ped || !activeBotTrip.pedReady)
                return;

            // Если координаты педа не читаются — он стоит на точке посадки
            const pedDist = dist2d(player.position, ped.position || activeBotTrip.pickupPos);
            if (pedDist > botBoardDistance || vehicle.getSpeed() > 2.0) {
                if (pedDist <= botHintDistance && !activeBotTrip.hintShown) {
                    activeBotTrip.hintShown = true;
                    mp.events.call('phone.notify', 228, translateText("Остановитесь рядом с пассажиром, он сядет сам"), 3);
                }
                return;
            }

            const seat = getFreePassengerSeat(vehicle);
            if (seat === -2) {
                if (Date.now() - activeBotTrip.stageTime > 5000) {
                    activeBotTrip.stageTime = Date.now();
                    mp.events.call('phone.notify', 228, translateText("В машине нет свободного места для пассажира"), 3);
                }
                return;
            }

            ped.clearTasks(true);
            // Пед не может открыть запертую дверь: открываем замок локально, только для этого пассажира
            try { vehicle.setDoorsLocked(1); } catch (e) {}
            ped.enterVehicle(vehicle, seat, botWarpMs);

            activeBotTrip.stage = "boarding";
            activeBotTrip.seat = seat;
            activeBotTrip.stageTime = Date.now();
            mp.events.call('phone.notify', 228, translateText("Пассажир садится в машину"), 3);
            return;
        }
        case "boarding": {
            const ped = activeBotTrip.ped;
            if (!ped || !ped.exists()) {
                // пед пропал — создадим заново на точке
                activeBotTrip.ped = null;
                activeBotTrip.stage = "pickup";
                return;
            }

            // Уехал, не дождавшись пассажира
            if (dist2d(player.position, activeBotTrip.pickupPos) > botBoardDistance * 2) {
                ped.clearTasks();
                activeBotTrip.pedReady = false;
                activeBotTrip.stage = "pickup";
                return;
            }

            const inSeat = vehicle.getPedInSeat(activeBotTrip.seat) === ped.handle;
            if (!inSeat && Date.now() - activeBotTrip.stageTime < botWarpMs)
                return;

            if (!inSeat) {
                // Не дошёл сам (заперто, мешает препятствие) — сажаем на место
                ped.clearTasks(true);
                ped.warpIntoVehicle(vehicle, activeBotTrip.seat);
            }

            activeBotTrip.stage = "wait_destination";
            activeBotTrip.lastRequestAt = 0;
            requestBotServer("botBoarded");
            return;
        }
        case "wait_destination":
            requestBotServer("botBoarded");
            return;
        case "to_destination": {
            if (dist2d(player.position, activeBotTrip.destinationPos) > botFinishDistance || vehicle.getSpeed() > 3.0)
                return;

            requestBotServer("botFinish");
            return;
        }
    }
});

gm.events.add(clientName + "init", (json) => {
    orders = [];

    const playerPos = global.localplayer.position;

    json = JSON.parse(json);
    json.forEach((item) => {

        let newItem = {};

        newItem.id = item[0];
        newItem.name = item[1];
        newItem.pos = new mp.Vector3(item[2], item[3], item[4]);
        newItem.dist = Math.round(global.vdist2(newItem.pos, playerPos, true));
        newItem.aStreet = global.getStreetName(newItem.pos.x, newItem.pos.y, newItem.pos.z);
        newItem.aArea = global.getAreaName(newItem.pos.x, newItem.pos.y, newItem.pos.z);
        newItem.area = global.getStreetName(newItem.pos.x, newItem.pos.y, newItem.pos.z) + " - " + global.getAreaName(newItem.pos.x, newItem.pos.y, newItem.pos.z);

        orders.push(newItem);
    });

    global.isInitTaxiList = true;
    mp.gui.emmit(`window.listernEvent ('phone.taxi.getMenu');`);
})


gm.events.add(clientName + "jobEnd", () => {
    orders = [];

    global.isInitTaxiList = false;
    resetBotTrip(false);
    mp.gui.emmit(`window.listernEvent ('phone.taxi.getMenu');`);
})

gm.events.add(clientName + "add", (id, name, posX, posY, posZ) => {
    if (!global.isInitTaxiList)
        return;

    const playerPos = mp.players.local.position;

    let newItem = {};

    newItem.id = id;
    newItem.name = name;
    newItem.pos = new mp.Vector3(posX, posY, posZ);
    newItem.dist = Math.round(global.vdist2(newItem.pos, playerPos, true));
    newItem.aStreet = global.getStreetName(newItem.pos.x, newItem.pos.y, newItem.pos.z);
    newItem.aArea = global.getAreaName(newItem.pos.x, newItem.pos.y, newItem.pos.z);
    newItem.area = global.getStreetName(newItem.pos.x, newItem.pos.y, newItem.pos.z) + " - " + global.getAreaName(newItem.pos.x, newItem.pos.y, newItem.pos.z);

    orders.push(newItem);

    mp.gui.emmit(`window.listernEvent ('phone.taxijob.update');`);

    mp.gui.chat.push(translateText("!{#00a86b}[ДИСПЕТЧЕР]: !{#ffffff}Игрок {0} вызвал такси !{#ffcc00}({1}м)!{#ffffff}. Откройте телефон чтобы принять вызов", name, newItem.dist));
    mp.events.call('phone.notify', 228, translateText("Появился новый заказ! :)"), 4);
});

gm.events.add(clientName + "dell", (id) => {
    if (!global.isInitTaxiList)
        return;

    removeOrderFromList(id);

    mp.gui.emmit(`window.listernEvent ('phone.taxijob.update');`);
});

gm.events.add(clientName + "take", (id) => {
    if (!global.isInitTaxiList)
        return;

    if (activeBotTrip) {
        mp.events.call('phone.notify', 228, translateText("Сначала завершите текущий заказ"), 3);
        return;
    }

    // И заказы игроков, и NPC-заказы подтверждает сервер
    mp.events.callRemote(serverName + "take", id);
});

gm.events.add(clientName + "cancel", () => {
    if (activeBotTrip) {
        mp.events.callRemote(serverName + "botCancel", activeBotTrip.id);
        return;
    }

    mp.events.callRemote("server.phone.taxi.cancel");
});

gm.events.add(clientName + "successCancel", () => {
    selectedOrders = {};
    global.isTaxiOrder = false;
    mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
});

gm.events.add(clientName + "load", () => {
    // Во время NPC-поездки данные заказа хранятся на клиенте
    if (activeBotTrip) {
        mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
        return;
    }

    mp.events.callRemote(serverName + "load");
});

global.isTaxiOrder = false;

gm.events.add(clientName + "initSelect", (_selectedOrders, isTake) => {
    if (activeBotTrip) {
        mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
        return;
    }

    _selectedOrders = JSON.parse(_selectedOrders);

    selectedOrders = {};

    const playerPos = mp.players.local.position;

    if (_selectedOrders && _selectedOrders.length) {
        selectedOrders.name = _selectedOrders[0];
        global.isTaxiOrder = true;

        selectedOrders.pos = new mp.Vector3(_selectedOrders[1], _selectedOrders[2], _selectedOrders[3]);
        if (isTake)
            mp.events.call('createWaypoint', selectedOrders.pos.x, selectedOrders.pos.y);
        selectedOrders.dist = Math.round(global.vdist2(selectedOrders.pos, playerPos, true));

        selectedOrders.aStreet = global.getStreetName(selectedOrders.pos.x, selectedOrders.pos.y, selectedOrders.pos.z);
        selectedOrders.aArea = global.getAreaName(selectedOrders.pos.x, selectedOrders.pos.y, selectedOrders.pos.z);
        selectedOrders.area = global.getStreetName(selectedOrders.pos.x, selectedOrders.pos.y, selectedOrders.pos.z) + " - " + global.getAreaName(selectedOrders.pos.x, selectedOrders.pos.y, selectedOrders.pos.z);
    }
    else
        global.isTaxiOrder = false;

    mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
});

rpc.register(rpcName + "getList", () => {
    return JSON.stringify(orders);
});

rpc.register(rpcName + "getSelect", () => {
    return JSON.stringify(selectedOrders);
});
