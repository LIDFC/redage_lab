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
const botBoardDistance = 15;      // на каком расстоянии пассажир начинает садиться
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

const destroyBotPed = () => {
    if (activeBotTrip && activeBotTrip.ped) {
        try {
            if (mp.peds.exists(activeBotTrip.ped))
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

    if (ped && mp.peds.exists(ped)) {
        try {
            if (vehicle)
                mp.game.ai.taskLeaveVehicle(ped.handle, vehicle.handle, 0);
            setTimeout(() => {
                try {
                    if (mp.peds.exists(ped))
                        mp.game.ai.taskWanderStandard(ped.handle, 10.0, 10);
                } catch (e) {}
            }, 2500);
        } catch (e) {}

        setTimeout(() => {
            try {
                if (mp.peds.exists(ped))
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
    const spawnPos = getBotSpawnPos(activeBotTrip.pickupPos);

    activeBotTrip.ped = mp.peds.new(
        mp.game.joaat(activeBotTrip.model),
        spawnPos,
        activeBotTrip.heading || 0,
        mp.players.local.dimension
    );
    activeBotTrip.pedReady = false;
};

// Пед создаётся асинхронно — настраиваем его, когда появится handle
const prepareBotPed = () => {
    const ped = activeBotTrip.ped;
    if (!ped || activeBotTrip.pedReady || !mp.peds.exists(ped) || !ped.handle)
        return;

    // Каждый вызов отдельно: если какой-то метод недоступен в версии клиента, остальные всё равно применятся
    try { ped.setInvincible(true); } catch (e) {}
    try { ped.setCanRagdoll(false); } catch (e) {}
    try { ped.setBlockingOfNonTemporaryEvents(true); } catch (e) {}
    try { mp.game.ai.taskStartScenarioInPlace(ped.handle, "WORLD_HUMAN_STAND_MOBILE", 0, true); } catch (e) {}

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
    if (activeBotTrip.stage === "pickup" && !activeBotTrip.ped && dist2d(player.position, activeBotTrip.pickupPos) <= botSpawnDistance)
        spawnBotPed();

    prepareBotPed();

    const vehicle = player.vehicle;
    if (!vehicle || vehicle.getPedInSeat(-1) !== player.handle)
        return;

    switch (activeBotTrip.stage) {
        case "pickup": {
            const ped = activeBotTrip.ped;
            if (!ped || !activeBotTrip.pedReady)
                return;

            if (dist2d(player.position, ped.position) > botBoardDistance || vehicle.getSpeed() > 2.0)
                return;

            const seat = getFreePassengerSeat(vehicle);
            if (seat === -2) {
                if (Date.now() - activeBotTrip.stageTime > 5000) {
                    activeBotTrip.stageTime = Date.now();
                    mp.events.call('phone.notify', 228, translateText("В машине нет свободного места для пассажира"), 3);
                }
                return;
            }

            mp.game.ai.clearPedTasks(ped.handle);
            mp.game.ai.taskEnterVehicle(ped.handle, vehicle.handle, 10000, seat, 1.0, 1, 0);

            activeBotTrip.stage = "boarding";
            activeBotTrip.seat = seat;
            activeBotTrip.stageTime = Date.now();
            mp.events.call('phone.notify', 228, translateText("Пассажир садится в машину"), 3);
            return;
        }
        case "boarding": {
            const ped = activeBotTrip.ped;
            if (!ped || !mp.peds.exists(ped)) {
                // пед пропал — создадим заново на точке
                activeBotTrip.ped = null;
                activeBotTrip.stage = "pickup";
                return;
            }

            // Уехал, не дождавшись пассажира
            if (dist2d(player.position, activeBotTrip.pickupPos) > botBoardDistance * 2) {
                mp.game.ai.clearPedTasks(ped.handle);
                activeBotTrip.pedReady = false;
                activeBotTrip.stage = "pickup";
                return;
            }

            const inSeat = vehicle.getPedInSeat(activeBotTrip.seat) === ped.handle;
            if (!inSeat && Date.now() - activeBotTrip.stageTime < 12000)
                return;

            if (!inSeat)
                mp.game.ped.setPedIntoVehicle(ped.handle, vehicle.handle, activeBotTrip.seat);

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
