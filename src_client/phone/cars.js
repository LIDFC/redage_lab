const
    clientName = "client.phone.cars.",
    rpcName = "rpc.phone.cars.",
    serverName = "server.phone.cars.";

let vehiclesList = [];
let carlist = JSON.stringify("[]");
let inGarage = false;
let isOwner = false;
let garageInfo = "null";

gm.events.add(clientName + "load", () => {
    mp.events.callRemote(serverName + "load");
});


const headerName = (isSell) => {
    if (typeof isSell !== "number" && !isSell) {
        return isOwner ? translateText("Подселённого") : translateText("Домовладельца");
    }

    return translateText("Личный");
}

let filterData = [];

mp.events.add("client.gta5devmenucars", () => {
    mp.gui.emmit(`window.gta5devmenucars(${carlist},${inGarage},${isOwner});`);
});

gm.events.add(clientName + "init", (vehiclesJson, _inGarage, _isOwner, _garageInfo) => {
    filterData = [];
    garageInfo = typeof _garageInfo === "string" ? _garageInfo : "null";

    inGarage = _inGarage;
    isOwner = _isOwner;

    carlist = JSON.stringify(vehiclesJson);

    vehiclesJson = JSON.parse(vehiclesJson);

    vehiclesList = [];
    vehiclesJson.forEach((item) => {
        if (item[0] === "rent") {
            vehiclesList.push({
                isRent: true,
                model: item[1],
                number: item[2],
                date: item[3],
                rentPrice: item[4],
                isJob: item[5],
                header: translateText("Аренда"),
            })

            if (!filterData.includes(translateText("Аренда")))
                filterData.push(translateText("Аренда"))
        } else {
            vehiclesList.push({
                sqlId: item[0],
                model: item[1],
                number: item[2],
                isCarGarage: item[3],
                place: item[4],
                ticket: item[5],
                isAir: item[6],
                isCreate: item[7],
                color: item[8],
                sell: item[9],
                header: headerName (item[9])
            })
            if (!filterData.includes(headerName (item[9])))
                filterData.push(headerName (item[9]))
        }
    });
    mp.gui.emmit(`window.listernEvent ('phoneCarsLoad')`)
});

gm.events.add(clientName + "error", () => {
    mp.gui.emmit(`window.listernEvent ('phoneCarsLoad')`)
});

rpc.register(rpcName + "filterData", () => {
    return JSON.stringify(filterData);
});

rpc.register(rpcName + "getCarsList", () => {
    return JSON.stringify(vehiclesList);
});

rpc.register(rpcName + "inGarage", () => {
    return !!inGarage;
});

rpc.register(rpcName + "isOwner", () => {
    return !!isOwner;
});

rpc.register(rpcName + "garageInfo", () => {
    return garageInfo;
});

// ─── Панель автомобиля (приложение «Авто») ───────────────────────────────────

const RADIO_OFF = 255;
// Кости дверей в порядке DoorId: водительская, пассажирская, задние, капот, багажник
const DOOR_BONES = ["door_dside_f", "door_pside_f", "door_dside_r", "door_pside_r", "bonnet", "boot"];

const getSeat = (vehicle) => {
    for (let i = -1; i < Math.max(vehicle.getMaxNumberOfPassengers(), 3); i++) {
        if (vehicle.getPedInSeat(i) === global.localplayer.handle)
            return i;
    }
    return -2;
};

const radioName = () => {
    try {
        const index = mp.game.invoke(global.getNative("GET_PLAYER_RADIO_STATION_INDEX"));
        if (index === RADIO_OFF)
            return translateText("Выключено");
        const label = mp.game.audio.getPlayerRadioStationName();
        const text = label ? mp.game.ui.getLabelText(label) : "";
        return text && text !== "NULL" ? text : `${translateText("Станция")} ${index + 1}`;
    } catch (e) {
        return "";
    }
};

rpc.register(rpcName + "panelState", () => {
    const vehicle = global.localplayer.vehicle;
    if (!vehicle || !mp.vehicles.exists(vehicle))
        return "null";

    const seat = getSeat(vehicle);
    const doors = [];
    const hasDoor = [];
    DOOR_BONES.forEach((bone, i) => {
        doors.push(vehicle.getDoorAngleRatio(i) > 0.05);
        hasDoor.push(vehicle.getBoneIndexByName(bone) !== -1);
    });

    let il = vehicle.getVariable("vIL");
    il = typeof il === "string" ? il.split("|").map(Number) : [0, 0];

    const mode = vehicle.getVariable("vDriveMode");

    return JSON.stringify({
        model: mp.game.vehicle.getDisplayNameFromVehicleModel(vehicle.model),
        number: vehicle.getNumberPlateText(),
        seat: seat,
        driver: seat === -1,
        engine: !!vehicle.getVariable("vEngine"),
        locked: !!vehicle.getVariable("vLock"),
        belt: global.isBeltOn ? global.isBeltOn() : false,
        right: !!il[0],
        left: !!il[1],
        doors: doors,
        hasDoor: hasDoor,
        windows: vehicle.getVariable("vWindows") || 0,
        lights: vehicle.getVariable("vLights") || 0,
        interior: !!vehicle.getVariable("vInterior"),
        drive: typeof mode === "number" ? mode : 1,
        radio: radioName(),
        speed: Math.round(vehicle.getSpeed() * 3.6),
        fuel: vehicle.getVariable("PETROL") || 0,
        health: Math.round(vehicle.getEngineHealth() / 10),
    });
});

let lastPanelAction = 0;
gm.events.add(clientName + "panel", (action, value) => {
    try {
        const vehicle = global.localplayer.vehicle;
        if (!vehicle || !mp.vehicles.exists(vehicle))
            return;
        if (Date.now() - lastPanelAction < 400)
            return;
        lastPanelAction = Date.now();

        const isDriver = vehicle.getPedInSeat(-1) === global.localplayer.handle;
        value = Number(value) || 0;

        switch (action) {
            case "engine":
                if (isDriver) mp.events.callRemote("engineCarPressed");
                break;
            case "lock":
                mp.events.callRemote("lockCarPressed");
                break;
            case "belt":
                global.binderFunctions.onBelt();
                break;
            case "left":
                if (isDriver) mp.events.callRemote("VehStream_SetIndicatorLightsData", vehicle, true, false);
                break;
            case "right":
                if (isDriver) mp.events.callRemote("VehStream_SetIndicatorLightsData", vehicle, false, true);
                break;
            case "hazard":
                if (isDriver) mp.events.callRemote("VehStream_SetIndicatorLightsData", vehicle, true, true);
                break;
            case "radio": {
                if (!isDriver) return;
                if (value === 0) {
                    mp.game.audio.setRadioToStationName("OFF");
                } else {
                    const count = 20;
                    let index = mp.game.invoke(global.getNative("GET_PLAYER_RADIO_STATION_INDEX"));
                    index = index === RADIO_OFF ? 0 : (index + value + count) % count;
                    mp.game.invoke(global.getNative("SET_FRONTEND_RADIO_ACTIVE"), true);
                    mp.game.invoke(global.getNative("SET_RADIO_TO_STATION_INDEX"), index);
                }
                break;
            }
            case "door":
            case "window":
            case "lights":
            case "interior":
            case "drive":
                mp.events.callRemote("server.vehicle.panel", action, value);
                break;
        }
    } catch (e) {
        mp.events.callRemote("client_trycatch", "phone/cars", "panel", e.toString());
    }
});

// Место машины в гараже (раньше — меню дома «Парковка»)
gm.events.add(clientName + "parking", (sqlId, place) => {
    mp.events.callRemote("server.garage.parking", Number(sqlId), Number(place));
});
