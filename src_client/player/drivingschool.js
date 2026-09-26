// Автошкола: окно у NPC (CEF PlayerDrivingSchool) и панель практического экзамена (CEF DrivingPracticeHud).
// Сервер: dotnet/resources/NeptuneEvo/Core/DrivingSchool.cs
let isOpenSchool = false;
let practice = null; // { license, check, total, penalties, maxPenalties, limit, startedAt }

const emitHud = (value) => mp.gui.emmit(`window.events.callEvent("cef.drivingschool.hud", ${JSON.stringify(JSON.stringify(value))})`);

gm.events.add('client.drivingschool.open', (json) => {
    if (isOpenSchool) {
        mp.gui.emmit(`window.router.setView("PlayerDrivingSchool", ${JSON.stringify(json)})`);
        return;
    }
    if (global.menuCheck())
        return;

    isOpenSchool = true;
    global.menuOpen();
    mp.gui.emmit(`window.router.setView("PlayerDrivingSchool", ${JSON.stringify(json)})`);
});

gm.events.add('client.drivingschool.close', (fromCef = false) => {
    if (!isOpenSchool)
        return;

    if (fromCef)
        mp.events.callRemote('server.drivingschool.close'); // прервать теорию, если идёт

    isOpenSchool = false;
    global.menuClose();
    mp.gui.emmit(`window.router.setHud()`);
});

gm.events.add('client.drivingschool.start', (index) => {
    if (isOpenSchool)
        mp.events.callRemote('server.drivingschool.start', index);
});

gm.events.add('client.drivingschool.answer', (index) => {
    if (isOpenSchool)
        mp.events.callRemote('server.drivingschool.answer', index);
});

gm.events.add('client.drivingschool.practice', () => {
    if (isOpenSchool)
        mp.events.callRemote('server.drivingschool.practice');
});

gm.events.add('client.drivingschool.question', (json) => {
    if (isOpenSchool)
        mp.gui.emmit(`window.events.callEvent("cef.drivingschool.question", ${JSON.stringify(json)})`);
});

gm.events.add('client.drivingschool.result', (json) => {
    if (isOpenSchool)
        mp.gui.emmit(`window.events.callEvent("cef.drivingschool.result", ${JSON.stringify(json)})`);
});

// ---------- Практика ----------
const SPEED_TOLERANCE = 5;       // км/ч сверх лимита, которые прощаем
const SPEEDING_MS = 2000;        // сколько нужно ехать с превышением, чтобы получить ошибку
const SPEED_COOLDOWN_MS = 6000;  // между ошибками за скорость
const CRASH_DAMAGE = 35;         // падение «здоровья» кузова за один замер = столкновение
const CRASH_COOLDOWN_MS = 4000;

let speedingSince = 0;
let lastSpeedPenalty = 0;
let lastCrashPenalty = 0;
let lastBodyHealth = null;
let lastHudUpdate = 0;

gm.events.add('client.drivingschool.practice.start', (license, total, limit, maxPenalties) => {
    practice = { license, check: 0, total, penalties: 0, maxPenalties, limit, speed: 0, startedAt: Date.now() };
    speedingSince = 0;
    lastSpeedPenalty = 0;
    lastCrashPenalty = 0;
    lastBodyHealth = null;
    emitHud(practice);
});

gm.events.add('client.drivingschool.practice.progress', (check, total, penalties) => {
    if (!practice)
        return;
    practice.check = check;
    practice.total = total;
    practice.penalties = penalties;
    emitHud({ check, total, penalties });
});

gm.events.add('client.drivingschool.practice.end', () => {
    practice = null;
    mp.gui.emmit(`window.events.callEvent("cef.drivingschool.hud", null)`);
});

const penalty = (reason) => mp.events.callRemote('server.drivingschool.penalty', reason);

gm.events.add('render', () => {
    if (!practice)
        return;

    const vehicle = mp.players.local.vehicle;
    const now = Date.now();
    if (!vehicle || vehicle.getPedInSeat(-1) !== mp.players.local.handle) {
        lastBodyHealth = null;
        speedingSince = 0;
        return;
    }

    const speed = vehicle.getSpeed() * 3.6;

    // Превышение: держим дольше SPEEDING_MS — ошибка
    if (speed > practice.limit + SPEED_TOLERANCE) {
        if (!speedingSince)
            speedingSince = now;
        else if (now - speedingSince > SPEEDING_MS && now - lastSpeedPenalty > SPEED_COOLDOWN_MS) {
            lastSpeedPenalty = now;
            speedingSince = 0;
            penalty("speed");
        }
    } else
        speedingSince = 0;

    // Столкновение: резкое падение здоровья кузова
    let body = null;
    try { body = vehicle.getBodyHealth(); } catch (e) {}
    if (body !== null) {
        if (lastBodyHealth !== null && lastBodyHealth - body >= CRASH_DAMAGE && now - lastCrashPenalty > CRASH_COOLDOWN_MS) {
            lastCrashPenalty = now;
            penalty("crash");
        }
        lastBodyHealth = body;
    }

    if (now - lastHudUpdate > 250) {
        lastHudUpdate = now;
        practice.speed = speed;
        emitHud({ speed });
    }
});
