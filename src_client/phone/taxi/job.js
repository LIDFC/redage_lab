const
    clientName = "client.phone.taxijob.",
    rpcName = "rpc.phone.taxijob.",
    serverName = "server.phone.taxijob.";

let selectedOrders = {};
let orders = [];
global.isInitTaxiList = false;

const botOrderPrefix = -100000;
let botOrderSeq = 0;
let botOrders = {};
let botOrdersTimer = null;   // setInterval handle
let botOrdersInitTimer = null; // setTimeout handle (первый запуск)
let activeBotTrip = null;

// ── ИСПРАВЛЕНИЕ 1: интервал ровно 30 секунд, одновременно только 1 бот-заказ
const BOT_ORDER_INTERVAL_MS = 30000;

// Список безопасных точек появления ботов — только на суше, проверенные координаты
// Используем их как кандидатов для выбора точки РЯДОМ с игроком
const safeSpawnAnchors = [
    new mp.Vector3(215.8, -810.1, 30.7),
    new mp.Vector3(119.0, -1072.6, 29.2),
    new mp.Vector3(454.8, -660.0, 28.5),
    new mp.Vector3(894.9, -179.1, 74.7),
    new mp.Vector3(-553.2, -191.6, 38.2),
    new mp.Vector3(-1215.6, -338.6, 37.8),
    new mp.Vector3(-47.7, -1758.6, 29.4),
    new mp.Vector3(169.9, -1469.3, 29.1),
    new mp.Vector3(-716.5, -915.6, 19.2),
    new mp.Vector3(-1034.6, -2733.6, 20.1),
    new mp.Vector3(380.5, -990.0, 29.6),
    new mp.Vector3(-260.0, -959.0, 31.2),
    new mp.Vector3(800.0, -750.0, 26.0),
    new mp.Vector3(-800.0, -510.0, 24.0),
    new mp.Vector3(140.0, -630.0, 43.0),
    new mp.Vector3(-500.0, -1340.0, 20.0),
    new mp.Vector3(350.0, -1650.0, 29.0),
    new mp.Vector3(-1100.0, -300.0, 37.0),
    new mp.Vector3(950.0, -1000.0, 41.0),
    new mp.Vector3(-300.0, -600.0, 34.0),
];

const botPickupModels = [
    "a_m_m_business_01",
    "a_m_m_bevhills_01",
    "a_m_m_eastsa_01",
    "a_m_y_business_02",
    "a_f_y_business_02",
    "a_f_y_tourist_01",
    "a_m_y_hipster_01",
    "a_f_m_bevhills_01",
];

const botDestinations = [
    new mp.Vector3(215.8, -810.1, 30.7),
    new mp.Vector3(119.0, -1072.6, 29.2),
    new mp.Vector3(-47.7, -1758.6, 29.4),
    new mp.Vector3(454.8, -660.0, 28.5),
    new mp.Vector3(894.9, -179.1, 74.7),
    new mp.Vector3(-553.2, -191.6, 38.2),
    new mp.Vector3(-1215.6, -338.6, 37.8),
    new mp.Vector3(-1034.6, -2733.6, 20.1),
    new mp.Vector3(169.9, -1469.3, 29.1),
    new mp.Vector3(-716.5, -915.6, 19.2),
    new mp.Vector3(380.5, -990.0, 29.6),
    new mp.Vector3(-260.0, -959.0, 31.2),
    new mp.Vector3(140.0, -630.0, 43.0),
    new mp.Vector3(-500.0, -1340.0, 20.0),
    new mp.Vector3(350.0, -1650.0, 29.0),
];

// ── ИСПРАВЛЕНИЕ 3: выбираем точку спавна из белого списка, ближайшую к игроку
// в диапазоне 300–1500м, а не случайный вектор который может попасть в воду
const getSafeSpawnNearPlayer = (playerPos, minDist = 300, maxDist = 1500) => {
    // Отфильтровать якоря в нужном радиусе
    const candidates = safeSpawnAnchors.filter(anchor => {
        const d = Math.round(global.vdist2(anchor, playerPos, true));
        return d >= minDist && d <= maxDist;
    });

    if (candidates.length === 0) {
        // Нет подходящих — берём любой ближайший
        let best = safeSpawnAnchors[0];
        let bestD = Math.round(global.vdist2(best, playerPos, true));
        for (const anchor of safeSpawnAnchors) {
            const d = Math.round(global.vdist2(anchor, playerPos, true));
            if (d < bestD) { bestD = d; best = anchor; }
        }
        return best;
    }

    // Случайный из подходящих
    return candidates[Math.floor(Math.random() * candidates.length)];
};

// Скорректировать Z через getGroundZFor3dCoord
const getSafeZ = (pos) => {
    let z = mp.game.gameplay.getGroundZFor3dCoord(pos.x, pos.y, 1000, 0, false);
    if (!z || z <= 0.5) z = pos.z; // не менять если не нашли землю
    return new mp.Vector3(pos.x, pos.y, z + 0.3);
};

const getBotDestination = (fromPos) => {
    // Нельзя назначать точку назначения слишком близко к пикапу
    const shuffled = botDestinations.slice().sort(() => Math.random() - 0.5);
    for (const dest of shuffled) {
        if (Math.round(global.vdist2(fromPos, dest, true)) >= 300)
            return dest;
    }
    return botDestinations[Math.floor(Math.random() * botDestinations.length)];
};

// ── ИСПРАВЛЕНИЕ 1 + 3: главная функция добавления бот-заказа ──────────────────
const addBotOrder = () => {
    if (!global.isInitTaxiList || global.isTaxiOrder) return;
    if (orders.length >= 30) return;

    // ── ИСПРАВЛЕНИЕ 1: удаляем предыдущий бот-заказ перед созданием нового
    const existingBotOrder = orders.find(o => o.isBot);
    if (existingBotOrder) {
        const oldId = existingBotOrder.id;
        const oldBotData = botOrders[oldId];
        if (oldBotData) {
            if (oldBotData.lifeTimer) clearTimeout(oldBotData.lifeTimer);
            if (oldBotData.ped) oldBotData.ped.destroy();
            delete botOrders[oldId];
        }
        const idx = orders.findIndex(o => o.id === oldId);
        if (idx !== -1) orders.splice(idx, 1);
    }

    // Не создаём если активная поездка уже идёт
    if (activeBotTrip) return;

    const playerPos = mp.players.local.position;

    // ── ИСПРАВЛЕНИЕ 3: безопасная точка спавна из белого списка
    let rawPos = getSafeSpawnNearPlayer(playerPos);
    const spawnPos = getSafeZ(rawPos);

    // Дополнительная проверка Z — если ниже 0, значит точка в воде/под землёй
    if (spawnPos.z <= 0.5) return;

    const id = botOrderPrefix - (botOrderSeq++);
    const pedModel = botPickupModels[Math.floor(Math.random() * botPickupModels.length)];

    const order = {
        id,
        name: translateText("Пассажир NPC"),
        pos: spawnPos,
        isBot: true,
    };

    order.dist = Math.round(global.vdist2(order.pos, playerPos, true));
    order.aStreet = global.getStreetName(order.pos.x, order.pos.y, order.pos.z);
    order.aArea = global.getAreaName(order.pos.x, order.pos.y, order.pos.z);
    order.area = order.aStreet + " - " + order.aArea;

    // Автоудаление через 30 секунд если не принят (= следующий тик таймера)
    const lifeTimer = setTimeout(() => {
        const botData = botOrders[id];
        if (!botData || (activeBotTrip && activeBotTrip.orderId === id)) return;

        if (botData.ped) botData.ped.destroy();

        const index = orders.findIndex(o => o.id === id);
        if (index !== -1) orders.splice(index, 1);

        delete botOrders[id];
        mp.gui.emmit(`window.listernEvent ('phone.taxijob.update');`);
    }, BOT_ORDER_INTERVAL_MS);

    botOrders[id] = {
        ped: null,           // ── ИСПРАВЛЕНИЕ 2: пед создаётся только когда водитель подъезжает
        model: pedModel,
        pickupPos: spawnPos,
        lifeTimer,
        createdAt: Date.now(),
        pedSpawned: false,   // флаг: был ли уже заспавнен пед
    };

    orders.push(order);

    mp.gui.emmit(`window.listernEvent ('phone.taxijob.update');`);
    mp.events.call('phone.notify', 228, translateText("Появился новый заказ от диспетчера"), 4);
};

// ── ИСПРАВЛЕНИЕ 1: ровно 30 секунд
const startBotOrdersTimer = () => {
    if (botOrdersTimer || botOrdersInitTimer) return;
    botOrdersInitTimer = setTimeout(() => {
        botOrdersInitTimer = null;
        addBotOrder();
        botOrdersTimer = setInterval(addBotOrder, BOT_ORDER_INTERVAL_MS);
    }, 5000);
};

const stopBotOrdersTimer = () => {
    if (botOrdersInitTimer) {
        clearTimeout(botOrdersInitTimer);
        botOrdersInitTimer = null;
    }
    if (botOrdersTimer) {
        clearInterval(botOrdersTimer);
        botOrdersTimer = null;
    }
};

const clearBotOrders = () => {
    for (const key in botOrders) {
        const botData = botOrders[key];
        if (botData && botData.lifeTimer) clearTimeout(botData.lifeTimer);
        if (botData && botData.ped) botData.ped.destroy();
    }
    botOrders = {};
};

const clearActiveBotTrip = () => {
    if (activeBotTrip && activeBotTrip.orderId && botOrders[activeBotTrip.orderId]) {
        const botData = botOrders[activeBotTrip.orderId];
        if (botData && botData.lifeTimer) clearTimeout(botData.lifeTimer);
        if (botData && botData.ped) botData.ped.destroy();
        delete botOrders[activeBotTrip.orderId];
    }
    activeBotTrip = null;
};

// ──────────────────────────────────────────────────────────────────────────────
// СОБЫТИЯ
// ──────────────────────────────────────────────────────────────────────────────

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
        newItem.area = newItem.aStreet + " - " + newItem.aArea;
        orders.push(newItem);
    });

    global.isInitTaxiList = true;
    startBotOrdersTimer();
    mp.gui.emmit(`window.listernEvent ('phone.taxi.getMenu');`);
});

gm.events.add(clientName + "jobEnd", () => {
    orders = [];
    global.isInitTaxiList = false;
    stopBotOrdersTimer();
    clearBotOrders();
    clearActiveBotTrip();
    mp.gui.emmit(`window.listernEvent ('phone.taxi.getMenu');`);
});

gm.events.add(clientName + "add", (id, name, posX, posY, posZ) => {
    if (!global.isInitTaxiList) return;

    const playerPos = mp.players.local.position;
    let newItem = {};
    newItem.id = id;
    newItem.name = name;
    newItem.pos = new mp.Vector3(posX, posY, posZ);
    newItem.dist = Math.round(global.vdist2(newItem.pos, playerPos, true));
    newItem.aStreet = global.getStreetName(newItem.pos.x, newItem.pos.y, newItem.pos.z);
    newItem.aArea = global.getAreaName(newItem.pos.x, newItem.pos.y, newItem.pos.z);
    newItem.area = newItem.aStreet + " - " + newItem.aArea;
    orders.push(newItem);

    mp.gui.emmit(`window.listernEvent ('phone.taxijob.update');`);
    mp.gui.chat.push(translateText("!{#00a86b}[ДИСПЕТЧЕР]: !{#ffffff}Игрок {0} вызвал такси !{#ffcc00}({1}м)!{#ffffff}. Откройте телефон чтобы принять вызов", name, newItem.dist));
    mp.events.call('phone.notify', 228, translateText("Появился новый заказ! :)"), 4);
});

gm.events.add(clientName + "dell", (id) => {
    if (!global.isInitTaxiList) return;

    const index = orders.findIndex(o => o.id === id);
    if (orders[index]) orders.splice(index, 1);

    if (botOrders[id]) {
        if (botOrders[id].lifeTimer) clearTimeout(botOrders[id].lifeTimer);
        if (botOrders[id].ped) botOrders[id].ped.destroy();
        delete botOrders[id];
    }

    mp.gui.emmit(`window.listernEvent ('phone.taxijob.update');`);
});

gm.events.add(clientName + "take", (id) => {
    if (!global.isInitTaxiList) return;

    const index = orders.findIndex(o => o.id === id);
    if (!orders[index]) return;

    if (id < 0) {
        // Бот-заказ
        const order = orders[index];

        selectedOrders = {
            name: order.name,
            pos: order.pos,
            aStreet: order.aStreet,
            aArea: order.aArea,
            area: order.area,
            isBot: true,
        };

        if (botOrders[id] && botOrders[id].lifeTimer)
            clearTimeout(botOrders[id].lifeTimer);

        activeBotTrip = {
            orderId: id,
            stage: "pickup",
            pickupPos: order.pos,
            destinationPos: null,
        };

        global.isTaxiOrder = true;
        orders.splice(index, 1);

        mp.events.call('createWaypoint', order.pos.x, order.pos.y);
        mp.gui.emmit(`window.listernEvent ('phone.taxijob.update');`);
        mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
        return;
    }

    mp.events.callRemote(serverName + "take", id);
});

gm.events.add(clientName + "cancel", () => {
    if (activeBotTrip) {
        clearActiveBotTrip();
        selectedOrders = {};
        global.isTaxiOrder = false;
        mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
        return;
    }
    mp.events.callRemote("server.phone.taxi.cancel");
});

gm.events.add(clientName + "successCancel", () => {
    clearActiveBotTrip();
    selectedOrders = {};
    global.isTaxiOrder = false;
    mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
});

gm.events.add(clientName + "load", () => {
    mp.events.callRemote(serverName + "load");
});

global.isTaxiOrder = false;

gm.events.add(clientName + "initSelect", (_selectedOrders, isTake) => {
    _selectedOrders = JSON.parse(_selectedOrders);
    selectedOrders = {};

    const playerPos = mp.players.local.position;

    if (_selectedOrders && _selectedOrders.length) {
        selectedOrders.name = _selectedOrders[0];
        global.isTaxiOrder = true;

        selectedOrders.pos = new mp.Vector3(_selectedOrders[1], _selectedOrders[2], _selectedOrders[3]);
        if (isTake) mp.events.call('createWaypoint', selectedOrders.pos.x, selectedOrders.pos.y);
        selectedOrders.dist = Math.round(global.vdist2(selectedOrders.pos, playerPos, true));
        selectedOrders.aStreet = global.getStreetName(selectedOrders.pos.x, selectedOrders.pos.y, selectedOrders.pos.z);
        selectedOrders.aArea = global.getAreaName(selectedOrders.pos.x, selectedOrders.pos.y, selectedOrders.pos.z);
        selectedOrders.area = selectedOrders.aStreet + " - " + selectedOrders.aArea;
    }

    mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
});

rpc.register(rpcName + "getList", () => {
    return JSON.stringify(orders);
});

rpc.register(rpcName + "getSelect", () => {
    return JSON.stringify(selectedOrders);
});

// ──────────────────────────────────────────────────────────────────────────────
// RENDER: обработка активной поездки с ботом
// ──────────────────────────────────────────────────────────────────────────────
gm.events.add("render", () => {
    if (!activeBotTrip || !global.isTaxiOrder) return;

    const player = mp.players.local;
    if (!player.vehicle) return;

    const vehicle = player.vehicle;
    if (vehicle.getPedInSeat(-1) !== player.handle) return;

    // ── СТАДИЯ: едем к пассажиру
    if (activeBotTrip.stage === "pickup") {
        const pickupPos = activeBotTrip.pickupPos;
        const distToPickup = Math.round(global.vdist2(player.position, pickupPos, true));

        if (distToPickup <= 20) {
            const botData = botOrders[activeBotTrip.orderId];
            if (!botData) {
                clearActiveBotTrip();
                selectedOrders = {};
                global.isTaxiOrder = false;
                mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
                return;
            }

            // ── ИСПРАВЛЕНИЕ 2: спавним педа только когда водитель подъехал (не заранее)
            if (!botData.ped && !botData.pedSpawned) {
                botData.pedSpawned = true;

                // Получаем корректный Z в точке спавна прямо сейчас
                const finalSpawnPos = getSafeZ(botData.pickupPos);

                try {
                    botData.ped = mp.peds.new(
                        mp.game.joaat(botData.model),
                        finalSpawnPos,
                        0,
                        0
                    );
                } catch (e) {
                    // Если не удалось создать — отменяем поездку
                    clearActiveBotTrip();
                    selectedOrders = {};
                    global.isTaxiOrder = false;
                    mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
                    return;
                }

                if (!botData.ped) {
                    clearActiveBotTrip();
                    selectedOrders = {};
                    global.isTaxiOrder = false;
                    mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
                    return;
                }

                botData.ped.freezePosition(true);
                mp.events.call('phone.notify', 228, translateText("Пассажир ждёт вас"), 3);
            }

            // Уже подъехали и пед есть — сажаем в машину
            if (botData.ped) {
                let freeSeat = 0;
                if (vehicle.getPedInSeat(0) !== 0) freeSeat = 1;
                if (vehicle.getPedInSeat(freeSeat) !== 0) freeSeat = 2;

                botData.ped.freezePosition(false);
                mp.game.ai.taskEnterVehicle(botData.ped.handle, vehicle.handle, 10000, freeSeat, 1.0, 1, 0);

                activeBotTrip.stage = "boarding";
                activeBotTrip.seat = freeSeat;
                activeBotTrip.boardingStart = Date.now();
                mp.events.call('phone.notify', 228, translateText("Пассажир садится в машину"), 3);
            }
        }

        return;
    }

    // ── СТАДИЯ: посадка
    if (activeBotTrip.stage === "boarding") {
        const botData = botOrders[activeBotTrip.orderId];
        const botPed = botData ? botData.ped : null;

        if (!botPed) {
            clearActiveBotTrip();
            selectedOrders = {};
            global.isTaxiOrder = false;
            mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
            return;
        }

        const isSeated = vehicle.getPedInSeat(activeBotTrip.seat) === botPed.handle;
        const boardingTimeout = Date.now() - activeBotTrip.boardingStart > 12000;

        if (isSeated || boardingTimeout) {
            if (!isSeated)
                mp.game.ped.setPedIntoVehicle(botPed.handle, vehicle.handle, activeBotTrip.seat);

            const destinationPos = getBotDestination(activeBotTrip.pickupPos);

            selectedOrders.pos = destinationPos;
            selectedOrders.aStreet = global.getStreetName(destinationPos.x, destinationPos.y, destinationPos.z);
            selectedOrders.aArea = global.getAreaName(destinationPos.x, destinationPos.y, destinationPos.z);
            selectedOrders.area = selectedOrders.aStreet + " - " + selectedOrders.aArea;

            activeBotTrip.stage = "to_destination";
            activeBotTrip.destinationPos = destinationPos;

            mp.events.call('createWaypoint', destinationPos.x, destinationPos.y);
            mp.events.call('phone.notify', 228, translateText("Пассажир в машине, везите по адресу"), 4);
            mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
        }

        return;
    }

    // ── СТАДИЯ: едем к пункту назначения
    if (activeBotTrip.stage === "to_destination") {
        const destinationPos = activeBotTrip.destinationPos;
        const distToDest = Math.round(global.vdist2(player.position, destinationPos, true));

        if (distToDest <= 20) {
            const distance = Math.max(200, Math.round(global.vdist2(activeBotTrip.pickupPos, destinationPos, true)));

            const botData = botOrders[activeBotTrip.orderId];
            const botPed = botData ? botData.ped : null;

            if (botData && botData.lifeTimer) clearTimeout(botData.lifeTimer);

            if (botPed)
                mp.game.ai.taskLeaveVehicle(botPed.handle, vehicle.handle, 0);

            setTimeout(() => {
                if (botPed) botPed.destroy();
                delete botOrders[activeBotTrip.orderId];

                mp.events.callRemote(serverName + "botFinish", distance);

                clearActiveBotTrip();
                selectedOrders = {};
                global.isTaxiOrder = false;

                mp.events.call('deleteWorkBlip');
                mp.events.call('phone.notify', 228, translateText("Поездка завершена"), 4);
                mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
            }, 2000);
        }
    }
});
