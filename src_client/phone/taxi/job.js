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
const botOrderCooldownMs = 15000;

const botDestinations = [
    new mp.Vector3(215.8, -810.1, 30.7),
    new mp.Vector3(-1034.6, -2733.6, 20.1),
    new mp.Vector3(-553.2, -191.6, 38.2),
    new mp.Vector3(119.0, -1072.6, 29.2),
    new mp.Vector3(-47.7, -1758.6, 29.4),
    new mp.Vector3(454.8, -660.0, 28.5),
    new mp.Vector3(894.9, -179.1, 74.7),
    new mp.Vector3(-1215.6, -338.6, 37.8),
    new mp.Vector3(1709.3, 3769.8, 34.1),
    new mp.Vector3(-296.9, 6256.8, 31.5),
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

const getRandomAround = (origin, minDist = 350, maxDist = 2000) => {
    const angle = Math.random() * Math.PI * 2;
    const distance = minDist + Math.random() * (maxDist - minDist);

    return new mp.Vector3(
        origin.x + Math.cos(angle) * distance,
        origin.y + Math.sin(angle) * distance,
        origin.z
    );
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

    if (Object.keys(botOrders).length > 0)
        return;

    if (Date.now() < nextBotOrderAt)
        return;

    const playerPos = getPlayerPos();
    let spawnPos = getRandomAround(playerPos);
    spawnPos = getSafeZ(spawnPos);

    if (spawnPos.z < 1)
        return;

    const id = botOrderPrefix - (botOrderSeq++);
    const pedModel = botPickupModels[Math.floor(Math.random() * botPickupModels.length)];

    const ped = mp.peds.new(
        mp.game.joaat(pedModel),
        spawnPos,
        0,
        0
    );

    if (!ped)
        return;

    ped.freezePosition(true);

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
    }, 30000);

    botOrders[id] = {
        ped,
        pickupPos: spawnPos,
        lifeTimer,
        createdAt: Date.now(),
    };

    orders.push(order);

    mp.gui.emmit(`window.listernEvent ('phone.taxijob.update');`);
    mp.events.call('phone.notify', 228, translateText("Появился новый заказ от диспетчера"), 4);
};

const startBotOrdersTimer = () => {
    if (botOrdersTimer)
        return;

    botOrdersTimer = setInterval(addBotOrder, 10000);
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
})


gm.events.add(clientName + "jobEnd", () => {
    orders = [];

    global.isInitTaxiList = false;
    stopBotOrdersTimer();
    clearBotOrders();
    clearActiveBotTrip();
    nextBotOrderAt = 0;
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

gm.events.add("render", () => {
    if (!activeBotTrip || !global.isTaxiOrder)
        return;

    const player = mp.players.local;
    if (!player.vehicle)
        return;

    const vehicle = player.vehicle;
    if (vehicle.getPedInSeat(-1) !== player.handle)
        return;

    if (activeBotTrip.stage === "pickup") {
        const pickupPos = activeBotTrip.pickupPos;

        if (Math.round(global.vdist2(player.position, pickupPos, true)) <= 12) {
            const botData = botOrders[activeBotTrip.orderId];
            const botPed = botData ? botData.ped : null;

            if (!botPed) {
                clearActiveBotTrip();
                nextBotOrderAt = Date.now() + botOrderCooldownMs;
                selectedOrders = {};
                global.isTaxiOrder = false;
                mp.gui.emmit(`window.listernEvent ('phone.taxijob.load');`);
                return;
            }

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
