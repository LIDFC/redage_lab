// Криминал (сервер NeptuneEvo/Crime): маркеры точек обыска при ограблении дома.
let burglaryPoints = [];

gm.events.add("client.burglary.points", (json) => {
    try {
        burglaryPoints = JSON.parse(json).map((p) => ({ x: p.x, y: p.y, z: p.z, taken: false }));
    } catch (e) {
        burglaryPoints = [];
    }
});

gm.events.add("client.burglary.taken", (index) => {
    if (burglaryPoints[index]) burglaryPoints[index].taken = true;
});

gm.events.add("client.burglary.clear", () => {
    burglaryPoints = [];
});

gm.events.add("render", () => {
    if (!burglaryPoints.length) return;
    const player = global.localplayer.position;
    const now = Date.now();
    burglaryPoints.forEach((p) => {
        if (p.taken) return;
        const dist = mp.game.system.vdist(p.x, p.y, p.z, player.x, player.y, player.z);
        if (dist > 30) return;
        const bounce = Math.sin(now / 300) * 0.08;
        mp.game.graphics.drawMarker(20, p.x, p.y, p.z + 0.2 + bounce, 0, 0, 0, 0, 180, 0, 0.35, 0.35, 0.35, 245, 165, 36, 200, false, true, 2, false, null, null, false);
        if (dist < 6)
            mp.game.graphics.drawText(dist < 1.6 ? "[E] Обыскать" : "Обыскать", [p.x, p.y, p.z + 0.55], {
                font: 4,
                color: [245, 165, 36, 230],
                scale: [0.35, 0.35],
                outline: true,
                centre: true,
            });
    });
});

// Трава: сервер помечает куст shared-данными "weedPlant" — кладём его на землю (как паллеты грузов).
let lastWeedScan = 0;
gm.events.add("render", () => {
    const now = Date.now();
    if (now - lastWeedScan < 1000) return;
    lastWeedScan = now;
    mp.objects.forEachInStreamRange((object) => {
        if (!object || !mp.objects.exists(object) || !object.handle || object.weedGrounded) return;
        if (object.getVariable("weedPlant") === undefined || object.getVariable("weedPlant") === null) return;
        object.weedGrounded = true;
        try {
            object.placeOnGroundProperly();
        } catch (e) {}
    });
});

// Метки своих уличных кустов
let weedBlips = [];
gm.events.add("client.weed.blips", (json) => {
    weedBlips.forEach((blip) => {
        try {
            if (mp.blips.exists(blip)) blip.destroy();
        } catch (e) {}
    });
    weedBlips = [];
    let list = [];
    try {
        list = JSON.parse(json) || [];
    } catch (e) {}
    list.forEach((p) => {
        weedBlips.push(
            mp.blips.new(469, new mp.Vector3(p.x, p.y, p.z), {
                name: "Мой куст конопли",
                color: 2,
                scale: 0.7,
                shortRange: true,
                dimension: 0,
            })
        );
    });
});

// Угон: сработала сигнализация
gm.events.add("client.crime.alarm", (vehicle) => {
    try {
        if (!vehicle || !mp.vehicles.exists(vehicle)) return;
        vehicle.setAlarm(true);
        vehicle.startAlarm();
    } catch (e) {}
});

// Поляны для посадки конопли (видят только криминальные): метки на карте + маркер на земле рядом
let weedSpots = [];
let weedSpotBlips = [];
gm.events.add("client.weed.spots", (json) => {
    let list = [];
    try {
        list = JSON.parse(json) || [];
    } catch (e) {}
    weedSpotBlips.forEach((blip) => {
        try {
            if (mp.blips.exists(blip)) blip.destroy();
        } catch (e) {}
    });
    weedSpotBlips = [];
    weedSpots = list;
    list.forEach((p) => {
        weedSpotBlips.push(
            mp.blips.new(496, new mp.Vector3(p.x, p.y, p.z), {
                name: p.free ? "Поляна конопли (свободна)" : "Поляна конопли (занята)",
                color: p.free ? 2 : 1,
                scale: 0.55,
                shortRange: true,
                dimension: 0,
            })
        );
    });
});

gm.events.add("render", () => {
    if (!weedSpots.length || global.localplayer.dimension !== 0) return;
    const player = global.localplayer.position;
    weedSpots.forEach((p) => {
        if (!p.free) return;
        const dist = mp.game.system.vdist(p.x, p.y, p.z, player.x, player.y, player.z);
        if (dist > 30) return;
        const z = player.z - 0.95;
        mp.game.graphics.drawMarker(25, p.x, p.y, z + 0.05, 0, 0, 0, 0, 0, 0, 8.0, 8.0, 1.0, 126, 211, 33, 90, false, false, 2, false, null, null, false);
        if (dist < 10)
            mp.game.graphics.drawText("Поляна конопли\nИнвентарь → «Семена конопли» → Использовать", [p.x, p.y, z + 1.2], {
                font: 4,
                color: [126, 211, 33, 230],
                scale: [0.35, 0.35],
                outline: true,
                centre: true,
            });
    });
});

// Вкладка «Криминал» в меню фракции (CEF fractions/elements/crime)
gm.events.add("client.crime.guide.load", () => {
    if (!global.antiFlood("crime.guide.load", 300)) return;
    mp.events.callRemote("server.crime.guide.load");
});

gm.events.add("client.crime.guide.data", (json) => {
    mp.gui.emmit(`window.listernEvent ('table.crimeguide.flag', ${JSON.stringify(json)});`);
    mp.gui.emmit(`window.listernEvent ('table.crimeguide', ${JSON.stringify(json)});`);
});

gm.events.add("client.crime.guide.gps", (target) => {
    if (!global.antiFlood("crime.guide.gps", 700)) return;
    mp.events.callRemote("server.crime.guide.gps", String(target));
});
