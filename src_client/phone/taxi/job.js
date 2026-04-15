const
    clientName = "client.phone.taxijob.",
    rpcName = "rpc.phone.taxijob.",
    serverName = "server.phone.taxijob.";


let selectedOrders = {};
let orders = []
global.isInitTaxiList = false;

const botOrderPrefix = -100000;
let botOrderSeq = 0;
let botOrders = {};
let botOrdersTimer = null;
let activeBotTrip = null;
let nextBotOrderAt = 0;

// ── ИСПРАВЛЕНИЕ 1: интервал проверки 10сек, но кулдаун на новый заказ 30сек
const botOrderCooldownMs = 30000;

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
];

const botPickupModels = [
    "a_m_m_business_01",
    "a_m_m_bevhills_01",
    "a_m_m_eastsa_01",
    "a_m_y_business_02",
    "a_f_y_business_02",
    "a_f_y_tourist_01",
];

const getPlayerPos = () => mp.players.local.position;

// ── ИСПРАВЛЕНИЕ 3: фиксированные точки спавна — гарантированно на суше
const safeSpawnPoints = [
    new mp.Vector3(215.8,   -810.1,  30.7),
    new mp.Vector3(119.0,  -1072.6,  29.2),
    new mp.Vector3(380.5,   -990.0,  29.6),
    new mp.Vector3(454.8,   -660.0,  28.5),
    new mp.Vector3(-260.0,  -959.0,  31.2),
    new mp.Vector3(894.9,   -179.1,  74.7),
    new mp.Vector3(-553.2,  -191.6,  38.2),
    new mp.Vector3(-1215.6, -338.6,  37.8),
    new mp.Vector3(-47.7,  -1758.6,  29.4),
    new mp.Vector3(169.9,  -1469.3,  29.1),
    new mp.Vector3(-716.5,  -915.6,  19.2),
    new mp.Vector3(-1034.6,-2733.6,  20.1),
    new mp.Vector3(140.0,   -630.0,  43.0),
    new mp.Vector3(-500.0, -1340.0,  20.0),
    new mp.Vector3(350.0,  -1650.0,  29.0),
    new mp.Vector3(-800.0,  -510.0,  24.0),
    new mp.Vector3(800.0,   -750.0,  26.0),
    new mp.Vector3(-1100.0, -300.0,  37.0),
    new mp.Vector3(-300.0,  -600.0,  34.0),
    new mp.Vector3(950.0,  -1000.0,  41.0),
];

const getSafeSpawnNear = (playerPos, minDist, maxDist) => {
    minDist = minDist || 300;
    maxDist = maxDist || 1500;

    var candidates = safeSpawnPoints.filter(function(p) {
        var d = Math.round(global.vdist2(p, playerPos, true));
        return d >= minDist && d <= maxDist;
    });

    if (!candidates.length) {
        // берём ближайший если диапазон пустой
        var best = safeSpawnPoints[0];
        var bestD = Math.round(global.vdist2(best, playerPos, true));
        for (var i = 1; i < safeSpawnPoints.length; i++) {
            var d = Math.round(global.vdist2(safeSpawnPoints[i], playerPos, true));
            if (d < bestD) { bestD = d; best = safeSpawnPoints[i]; }
        }
        return best;
    }

    return candidates[Math.floor(Math.random() * candidates.length)];
};

const getSafeZ = (pos) => {
    let z = mp.game.gameplay.getGroundZFor3dCoord(pos.x, pos.y, 1000, 0, false);
    if (!z || z <= 0)
        z = pos.z;
    return new mp.Vector3(pos.x, pos.y, z + 0.2);
};

const getBotDestination = (fromPos) => {
    let destination = botDestinations[Math.floor(Math.random() * botDestinations.length)];
    if (Math.round(global.vdist2(fromPos, destination, true)) < 250)
        destination = botDestinations[(Math.floor(Math.random() * botDestinations.length) + 1) % botDestinations.length];
    return destination;
};

const addBotOrder = () => {
    if (!global.isInitTaxiList || global.isTaxiOrder)
        return;
    if (orders.length >= 30)
        return;
    if (orders.some(function(o) { return o.isBot; }) || activeBotTrip)
        return;
    if (Date.now() < nextBotOrderAt)
        return;

    const playerPos = getPlayerPos();

    // ── ИСПРАВЛЕНИЕ 3: берём точку из белого списка, не случайные координаты
    let spawnPos = getSafeSpawnNear(playerPos);
    spawnPos = getSafeZ(spawnPos);

    if (spawnPos.z < 1)
        return;

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

    const lifeTimer = setTimeout(() => {
        const botData = botOrders[id];
        if (!botData || (activeBotTrip && activeBotTrip.orderId === id))
            return;
        if (botData.ped)
            botData.ped.destroy();
        const index = orders.findIndex(o => o.id === id);
        if (index !== -1)
            orders.splice(index, 1);
        delete botOrders[id];
        nextBotOrderAt = Date.now() + botOrderCooldownMs;
        mp.gui.emmit(`window.listernEvent ('phone.taxijob.update');`);
    }, botOrderCooldownMs);

    botOrders[id] = {
        ped: null,
        model: pedModel,
        pickupPos: spawnPos,
        lifeTimer,
        createdAt: Date.now(),
        // ── ИСПРАВЛЕНИЕ 2: флаг что модель уже грузится/загружена
        pedLoading: false,
    };

    orders.push(order);

    nextBotOrderAt = Date.now() + botOrderCooldownMs;

    mp.gui.emmit(`window.listernEvent ('phone.taxijob.update');`);
    mp.events.call('phone.notify', 228, translateText("Появился новый заказ от диспетчера"), 4);
};

const startBotOrdersTimer = () => {
    if (botOrdersTimer)
        return;
    // ── ИСПРАВЛЕНИЕ 1: проверяем каждые 10с, кулдаун на сам заказ 30с
    botOrdersTimer = setInterval(addBotOrder, 10000);
    // первый заказ сразу через 5 секунд
    setTimeout(addBotOrder, 5000);
};

const stopBotOrdersTimer = () => {
    if (!botOrdersTimer)
        return;
    clearInterval(botOrdersTimer);
    botOrdersTimer = null;
};

const clearBotOrders = () => {
    for (const key in botOrders) {
        const botData = botOrders[key];
        if (botData && botData.lifeTimer)
            clearTimeout(botData.lifeTimer);
        if (botData && botData.ped)
            botData.ped.destroy();
    }
    botOrders = {};
};

const clearActiveBotTrip = () => {
    if (activeBotTrip && activeBotTrip.orderId && botOrders[activeBotTrip.orderId]) {
        const botData = botOrders[activeBotTrip.orderId];
        if (botData && botData.lifeTimer)
            clearTimeout(botData.lifeTimer);
        if (botData && botData.ped)
            botData.ped.destroy();
        delete botOrders[activeBotTrip.orderId];
    }
    activeBotTrip = null;
};

// ── ИСПРАВЛЕНИЕ 2: отдельная async функция для спавна педа с загрузкой модели
const spawnBotPed = async (botData) => {
    if (botData.pedLoading || botData.ped)
        return;

    botData.pedLoading = true;

    // Загружаем модель перед созданием педа
    const loaded = await global.loadModel(botData.model);
    if (!loaded) {
        botData.pedLoading = false;
        return;
    }

    // Перепроверяем что поездка ещё актуальна
    if (!activeBotTrip || !botOrders[activeBotTrip.orderId]) {
        botData.pedLoading = false;
        return;
    }

    botData.ped = mp.peds.new(
        mp.game.joaat(botData.model),
        botData.pickupPos,
        0,
        0
    );

    if (!botData.ped) {
        botData.pedLoading = false;
        return;
    }

    botData.ped.freezePosition(true);
    botData.pedLoading = false;
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
        newItem.area = global.getStreetName(newItem.pos.x, newItem.pos.y, newItem.pos.z) + " - " + global.getAreaName(newItem.pos.x, newItem.pos.y, newItem.pos.z);
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
    nextBotOrderAt = 0;
    mp.gui.emmit(`window.listernEvent ('phone.taxi.getMenu');`);
});

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
    const index = orders.findIndex(o => o.id === id);
    if (orders[index])
        orders.splice(index, 1);
    if (botOrders[id]) {
        if (botOrders[id].lifeTimer)
            clearTimeout(botOrders[id].lifeTimer);
        if (botOrders[id].ped)
            botOrders[id].ped.destroy();
        delete botOrders[id];
    }
    mp.gui.emmit(`window.listernEvent ('phone.taxijob.update');`);
});

gm.events.add(clientName + "take", (id) => {
    if (!global.isInitTaxiList)
        return;
    const index = orders.findIndex(o => o.id === id);
    if (!orders[index])
        return;

    if (id < 0) {
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
        nextBotOrderAt = Date.now() + botOrderCooldownMs;
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
        if (isTake)
            mp.events.call('createWaypoint', selectedOrders.pos.x, selectedOrders.pos.y);
        selectedOrders.dist = Math.round(global.vdist2(selectedOrders.pos, playerPos, true));
        selectedOrders.aStreet = global.getStreetName(selectedOrders.pos.x, selectedOrders.pos.y, selectedOrders.pos.z);
        selectedOrders.aArea = global.getAreaName(selectedOrders.pos.x, selectedOrders.pos.y, selectedOrders.pos.z);
        selectedOrders.area = global.getStreetName(selectedOrders.pos.x, selectedOrders.pos.y, selectedOrders.pos.z) + " - " + global.getAreaName(selectedOrders.pos.x, selectedOrders.pos.y, selectedOrders.pos.z);
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
// RENDER — обработка активной поездки с ботом
// ──────────────────────────────────────────────────────────────────────────────
gm.events.add("render", () => {
    if (!activeBotTrip || !global.isTaxiOrder)
        return;

    const player = mp.players.local;
    if (!player.vehicle)
        return;

    const vehicle = player.vehicle;
    if (vehicle.getPedInSeat(-1) !== player.handle)
        return;

    // ── СТАДИЯ: едем к пассажиру
    if (activeBotTrip.stage === "pickup") {
        const pickupPos = activeBotTrip.pickupPos;

        if (Math.round(global.vdist2(player.position, pickupPos, true)) <= 12) {
            const botData = botOrders[activeBotTrip.orderId];
            if (!botData) {
                clearActiveBotTrip();
                nextBotOrderAt = Date.now() + botOrderCooldownMs;
                selectedOrders = {};
                global.isTaxiOrder = false;
                mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
                return;
            }

            // ── ИСПРАВЛЕНИЕ 2: запускаем async загрузку модели и спавн педа
            // spawnBotPed сам следит за pedLoading флагом — не вызовется дважды
            if (!botData.ped && !botData.pedLoading) {
                spawnBotPed(botData);
                return; // ждём следующего кадра
            }

            // Пед ещё грузится — ждём
            if (botData.pedLoading)
                return;

            // Пед готов — сажаем
            if (!botData.ped)
                return;

            const botPed = botData.ped;

            let freeSeat = 0;
            if (vehicle.getPedInSeat(0) !== 0)
                freeSeat = 1;
            if (vehicle.getPedInSeat(freeSeat) !== 0)
                freeSeat = 2;

            botPed.freezePosition(false);
            mp.game.ai.taskEnterVehicle(botPed.handle, vehicle.handle, 10000, freeSeat, 1.0, 1, 0);

            activeBotTrip.stage = "boarding";
            activeBotTrip.seat = freeSeat;
            activeBotTrip.boardingStart = Date.now();
            mp.events.call('phone.notify', 228, translateText("Пассажир садится в машину"), 3);
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

        if (vehicle.getPedInSeat(activeBotTrip.seat) === botPed.handle || Date.now() - activeBotTrip.boardingStart > 12000) {
            if (vehicle.getPedInSeat(activeBotTrip.seat) !== botPed.handle)
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

    // ── СТАДИЯ: к пункту назначения
    if (activeBotTrip.stage === "to_destination") {
        const destinationPos = activeBotTrip.destinationPos;

        if (Math.round(global.vdist2(player.position, destinationPos, true)) <= 18) {
            const distance = Math.max(200, Math.round(global.vdist2(activeBotTrip.pickupPos, destinationPos, true)));

            const botData = botOrders[activeBotTrip.orderId];
            const botPed = botData ? botData.ped : null;
            if (botData && botData.lifeTimer)
                clearTimeout(botData.lifeTimer);

            if (botPed)
                mp.game.ai.taskLeaveVehicle(botPed.handle, vehicle.handle, 0);

            setTimeout(() => {
                if (botPed) botPed.destroy();
                delete botOrders[activeBotTrip.orderId];

                mp.events.callRemote(serverName + "botFinish", distance);

                clearActiveBotTrip();
                nextBotOrderAt = Date.now() + botOrderCooldownMs;
                selectedOrders = {};
                global.isTaxiOrder = false;

                mp.events.call('deleteWorkBlip');
                mp.events.call('phone.notify', 228, translateText("Поездка завершена"), 4);
                mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
            }, 2000);
        }
    }
});
