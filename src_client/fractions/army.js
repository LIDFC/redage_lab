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
