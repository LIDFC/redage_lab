// RP-механики армии, клиент (сервер Fractions/ArmyRP/*.cs):
// сирена тревоги, предупреждение в режимной зоне, метки нарушителей и конвоев на карте.

// ---------------------------------------------------------------- сирена
const ALARM = "PORT_OF_LS_HEIST_FORT_ZANCUDO_ALARMS";

const playAlarm = async (seconds) => {
    try {
        for (let i = 0; i < 20 && !mp.game.audio.prepareAlarm(ALARM); i++) await mp.game.waitAsync(100);
        mp.game.audio.startAlarm(ALARM, false);
        setTimeout(() => {
            try { mp.game.audio.stopAlarm(ALARM, true); } catch (e) {}
        }, seconds * 1000);
    } catch (e) {
        // запасной вариант — короткие сигналы
        for (let i = 0; i < 5; i++) setTimeout(() => mp.game.audio.playSoundFrontend(-1, "Beep_Red", "DLC_HEIST_HACKING_SNAKE_SOUNDS", true), i * 600);
    }
};

gm.events.add("client.army.alarm", () => playAlarm(15));

gm.events.add("client.army.zoneWarning", () => {
    for (let i = 0; i < 3; i++) setTimeout(() => mp.game.audio.playSoundFrontend(-1, "Beep_Red", "DLC_HEIST_HACKING_SNAKE_SOUNDS", true), i * 700);
});

// ---------------------------------------------------------------- метки: нарушители и конвои
// id → { blip, seen }. Метка удаляется, если сервер перестал её присылать.
const markers = {};

const upsertBlip = (key, x, y, z, sprite, color, name) => {
    const pos = new mp.Vector3(x, y, z);
    let marker = markers[key];
    if (marker && mp.blips.exists(marker.blip)) {
        marker.blip.setCoords(pos);
    } else {
        const blip = mp.blips.new(sprite, pos, { name, color, scale: 1.0, shortRange: false, dimension: -1 });
        marker = markers[key] = { blip };
    }
    marker.seen = Date.now();
};

setInterval(() => {
    const now = Date.now();
    for (const key of Object.keys(markers)) {
        if (now - markers[key].seen < 8000) continue;
        try { if (mp.blips.exists(markers[key].blip)) markers[key].blip.destroy(); } catch (e) {}
        delete markers[key];
    }
}, 2000);

gm.events.add("client.army.intruder", (remoteId, x, y, z) => {
    upsertBlip(`intruder_${remoteId}`, x, y, z, 303, 1, "Нарушитель на базе");
});

gm.events.add("client.army.convoys", (json) => {
    let list = [];
    try { list = JSON.parse(json) || []; } catch (e) {}
    for (const convoy of list) upsertBlip(`convoy_${convoy.id}`, convoy.x, convoy.y, convoy.z, 477, 52, convoy.name);
});

// ---------------------------------------------------------------- тир (/range)
const RANGE_MODEL = mp.game.joaat("prop_range_target_01");
let range = null;

const rangeFinish = () => {
    if (!range) return;
    const { hits, shots, target } = range;
    if (target && mp.objects.exists(target)) target.destroy();
    clearInterval(range.timer);
    range = null;
    mp.events.callRemote("server.army.range.result", hits, shots);
};

const rangeSpawn = () => {
    if (!range) return;
    if (range.target && mp.objects.exists(range.target)) range.target.destroy();
    range.target = null;
    if (range.spawned >= range.total || Date.now() > range.until) return rangeFinish();
    const rad = (range.heading * Math.PI) / 180;
    const forward = 15 + Math.random() * 15;
    const side = (Math.random() - 0.5) * 16;
    const x = range.x - Math.sin(rad) * forward + Math.cos(rad) * side;
    const y = range.y + Math.cos(rad) * forward + Math.sin(rad) * side;
    const ground = mp.game.gameplay.getGroundZFor3dCoord(x, y, range.z + 5, 0, false) || range.z - 1;
    range.target = mp.objects.new(RANGE_MODEL, new mp.Vector3(x, y, ground), { rotation: new mp.Vector3(0, 0, range.heading + 180), dimension: global.localplayer.dimension });
    range.spawnedAt = Date.now();
    range.spawned++;
};

gm.events.add("client.army.range.start", (targets, seconds, x, y, z, heading) => {
    if (range) return;
    mp.game.streaming.requestModel(RANGE_MODEL);
    range = { total: targets, until: Date.now() + seconds * 1000, x, y, z, heading, hits: 0, shots: 0, spawned: 0, target: null, spawnedAt: 0 };
    mp.events.call("notify", 2, 9, `Стрельбы: ${targets} мишеней, ${seconds} сек. Огонь!`, 4000);
    range.timer = setInterval(() => {
        try {
            if (!range) return;
            const pos = global.localplayer.position;
            if (mp.game.gameplay.getDistanceBetweenCoords(pos.x, pos.y, pos.z, range.x, range.y, range.z, true) > 8) {
                mp.events.call("notify", 1, 9, "Вы покинули огневой рубеж", 3000);
                return rangeFinish();
            }
            const target = range.target;
            if (target && mp.objects.exists(target) && target.handle && mp.game.invoke("0x605F5A140F202491", target.handle)) {
                range.hits++;
                mp.game.audio.playSoundFrontend(-1, "HACKING_SUCCESS", "", true);
                rangeSpawn();
                return;
            }
            if (!target || Date.now() - range.spawnedAt > 3000) rangeSpawn();
        } catch (e) {}
    }, 100);
});

mp.events.add("playerWeaponShot", () => {
    if (range) range.shots++;
});

// ---------------------------------------------------------------- полоса (/course)
let course = null;

const courseStop = (finished) => {
    if (!course) return;
    if (course.blip && mp.blips.exists(course.blip)) course.blip.destroy();
    course = null;
    global.escManager && global.escManager.remove("armyCourse");
    mp.events.callRemote("server.army.course.result", !!finished);
};

const courseBlip = () => {
    if (course.blip && mp.blips.exists(course.blip)) course.blip.destroy();
    const [x, y, z] = course.points[course.index];
    course.blip = mp.blips.new(1, new mp.Vector3(x, y, z), { name: "Полоса", color: 5, scale: 0.8, dimension: -1 });
    course.blip.setRoute(true);
};

gm.events.add("client.army.course.start", (json) => {
    if (course) return;
    let points = [];
    try { points = JSON.parse(json) || []; } catch (e) {}
    if (points.length < 2) return;
    course = { points, index: 1, started: Date.now(), blip: null };
    courseBlip();
    global.escManager && global.escManager.push("armyCourse", () => courseStop(false));
    mp.events.call("notify", 2, 9, "Полоса препятствий: бегом по точкам! ESC — сойти", 4000);
});

gm.events.add("render", () => {
    if (!course) return;
    if (global.localplayer.vehicle) {
        mp.events.call("notify", 1, 9, "Полоса проходится только пешком", 3000);
        return courseStop(false);
    }
    const [x, y, z] = course.points[course.index];
    mp.game.graphics.drawMarker(1, x, y, z - 1.0, 0, 0, 0, 0, 0, 0, 3.0, 3.0, 1.2, 255, 200, 0, 120, false, false, 2, false, null, null, false);
    const seconds = Math.floor((Date.now() - course.started) / 1000);
    mp.game.graphics.drawText(`Полоса: ${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}  точка ${course.index}/${course.points.length - 1}`, [0.5, 0.88], { font: 4, color: [255, 255, 255, 230], scale: [0.5, 0.5], outline: true });
    const pos = global.localplayer.position;
    if (mp.game.gameplay.getDistanceBetweenCoords(pos.x, pos.y, pos.z, x, y, z, true) > 3) return;
    mp.game.audio.playSoundFrontend(-1, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET", true);
    course.index++;
    if (course.index >= course.points.length) return courseStop(true);
    courseBlip();
});

// ---------------------------------------------------------------- ремонт мини-игрой
let repairOpen = false;

gm.events.add("client.army.repair.open", (json) => {
    if (repairOpen) return;
    repairOpen = true;
    global.menuOpen();
    mp.gui.emmit(`window.router.setView("ArmyRepair", ${JSON.stringify(json)})`);
});

const repairClose = () => {
    if (!repairOpen) return;
    repairOpen = false;
    global.menuClose();
    mp.gui.emmit("window.router.setHud()");
};

gm.events.add("client.army.repair.done", () => {
    if (!repairOpen) return;
    repairClose();
    mp.events.callRemote("server.army.repair.done");
});

gm.events.add("client.army.repair.cancel", () => {
    if (!repairOpen) return;
    repairClose();
    mp.events.callRemote("server.army.repair.cancel");
});

// ---------------------------------------------------------------- наряды (доска нарядов)
let dutyOpen = false;

gm.events.add("client.army.duty.open", (json) => {
    if (dutyOpen) return;
    dutyOpen = true;
    global.menuOpen();
    mp.gui.emmit(`window.router.setView("ArmyDuty", ${JSON.stringify(json)})`);
});

gm.events.add("client.army.duty.update", (json, message, ok) => {
    if (!dutyOpen) return;
    mp.gui.emmit(`window.events.callEvent("cef.army.duty.update", ${JSON.stringify(json)}, ${JSON.stringify(message)}, ${!!ok})`);
});

gm.events.add("client.army.duty.close", () => {
    if (!dutyOpen) return;
    dutyOpen = false;
    global.menuClose();
    mp.gui.emmit("window.router.setHud()");
});

gm.events.add("client.army.duty.take", (type) => {
    if (!dutyOpen || !global.antiFlood("army.duty", 1000)) return;
    mp.events.callRemote("server.army.duty.take", String(type));
});

// Объявления на доске нарядов (сервер ArmyRP/ArmyBoard.cs)
gm.events.add("client.army.board.list", (json) => {
    if (!dutyOpen) return;
    mp.gui.emmit(`window.events.callEvent("cef.army.board.list", ${JSON.stringify(json)})`);
});

gm.events.add("client.army.board.result", (text, ok) => {
    if (!dutyOpen) return;
    mp.gui.emmit(`window.events.callEvent("cef.army.board.result", ${JSON.stringify(String(text || ""))}, ${!!ok})`);
});

gm.events.add("client.army.board.post", (title, text) => {
    if (!dutyOpen || !global.antiFlood("army.board", 1500)) return;
    mp.events.callRemote("server.army.board.post", String(title || ""), String(text || ""));
});

gm.events.add("client.army.board.delete", (id) => {
    if (!dutyOpen || !global.antiFlood("army.board", 800)) return;
    mp.events.callRemote("server.army.board.delete", String(id));
});

gm.events.add("client.army.duty.assign", (targetId, type, punishment) => {
    if (!dutyOpen || !global.antiFlood("army.duty", 1000)) return;
    mp.events.callRemote("server.army.duty.assign", Number(targetId), String(type), !!punishment);
});
