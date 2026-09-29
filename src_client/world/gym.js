// Уличная качалка: подходишь к турнику, скамье, стойке или коврику → E → анимация (сервер World/Gym/GymManager.cs).
// Тренажёры — объекты мира (Muscle Beach, тюрьма) и поставленные сервером из settings/gym.json.
// Анимация синхронизируется через shared data AnimToKey (synchronization/animation.js, ключи gym_*).
// Выход: E, ESC или шаг в сторону (W/A/S/D).

// Модель → тип упражнения и где встать относительно объекта (смещение в осях объекта, поворот к объекту)
const GYM_MODELS = [
    { model: "prop_a_base_bars_01", type: "chinup", offset: [0, 0.15, 0], heading: 180 },
    { model: "prop_beach_bars_01", type: "chinup", offset: [-1.3, 0.15, 0], heading: 180 },
    { model: "prop_beach_bars_02", type: "chinup", offset: [0, 0.15, 0], heading: 180 },
    { model: "prop_pris_bars_01", type: "chinup", offset: [-1.3, 0.15, 0], heading: 180 },
    { model: "prop_muscle_bench_01", type: "bench", offset: [0, 0.2, 0.3], heading: 180 },
    { model: "prop_muscle_bench_02", type: "bench", offset: [0, 0.2, 0.3], heading: 180 },
    { model: "prop_muscle_bench_03", type: "bench", offset: [0, 0.2, 0.3], heading: 180 },
    { model: "prop_muscle_bench_05", type: "bench", offset: [0, 0.2, 0.3], heading: 180 },
    { model: "prop_muscle_bench_06", type: "bench", offset: [0, 0.2, 0.3], heading: 180 },
    { model: "prop_weight_bench_02", type: "bench", offset: [0, 0.2, 0.3], heading: 180 },
    { model: "prop_pris_bench_01", type: "bench", offset: [0, 0.2, 0.3], heading: 180 },
    { model: "prop_weight_rack_01", type: "weights", offset: [0, -0.9, 0], heading: 0 },
    { model: "prop_weight_rack_02", type: "weights", offset: [0, -0.9, 0], heading: 0 },
    { model: "prop_yoga_mat_01", type: "mat", offset: [0, 0, 0], heading: 0 },
    { model: "prop_yoga_mat_02", type: "mat", offset: [0, 0, 0], heading: 0 },
    { model: "prop_yoga_mat_03", type: "mat", offset: [0, 0, 0], heading: 0 },
].map((item) => ({ ...item, hash: mp.game.joaat(item.model) }));

// На коврике — по очереди: пресс, отжимания, йога (повторное E на коврике меняет упражнение не нужно — выходим)
const MAT_EXERCISES = ["situps", "pushups", "yoga"];
let matIndex = 0;

const SEARCH_RADIUS = 2.2;
let near = null;
let busy = false;
let lastHint = false;

const findNear = () => {
    const pos = global.localplayer.position;
    let best = null;
    for (const item of GYM_MODELS) {
        const handle = mp.game.object.getClosestObjectOfType(pos.x, pos.y, pos.z, SEARCH_RADIUS, item.hash, false, false, false);
        if (!handle) continue;
        const coords = Natives.GET_ENTITY_COORDS(handle, false);
        const dist = mp.game.gameplay.getDistanceBetweenCoords(pos.x, pos.y, pos.z, coords.x, coords.y, coords.z, true);
        if (dist > SEARCH_RADIUS || (best && best.dist <= dist)) continue;
        best = { item, handle, coords, dist };
    }
    return best;
};

// Точка, где встать: смещение в локальных осях объекта
const standPoint = (target) => {
    const heading = Natives.GET_ENTITY_ROTATION(target.handle, 2).z;
    const rad = (heading * Math.PI) / 180;
    const [ox, oy, oz] = target.item.offset;
    const x = target.coords.x + ox * Math.cos(rad) - oy * Math.sin(rad);
    const y = target.coords.y + ox * Math.sin(rad) + oy * Math.cos(rad);
    const ground = mp.game.gameplay.getGroundZFor3dCoord(x, y, target.coords.z + 1.5, 0, false);
    const z = (ground || target.coords.z) + 1.0 + oz;
    return { x, y, z, heading: (heading + target.item.heading) % 360 };
};

const setHint = (show) => {
    if (show === lastHint) return;
    lastHint = show;
    if (show) mp.events.call("hud.oEnter", busy ? "GymStop" : "Gym");
    else mp.events.call("hud.cEnter");
};

setInterval(() => {
    try {
        if (!global.loggedin || global.localplayer.vehicle) {
            near = null;
            global.gymNear = false;
            setHint(false);
            return;
        }
        if (busy) return;
        near = findNear();
        global.gymNear = !!near;
        setHint(!!near);
    } catch (e) {}
}, 500);

const stop = () => {
    if (!busy) return;
    busy = false;
    global.gymBusy = false;
    global.escManager && global.escManager.remove("gym");
    mp.events.callRemote("server.gym.stop");
    lastHint = null;
    setHint(false);
};

const start = () => {
    if (busy || !near || global.ANTIANIM || global.isSeat || global.localplayer.vehicle) return;
    let type = near.item.type;
    if (type === "mat") {
        type = MAT_EXERCISES[matIndex % MAT_EXERCISES.length];
        matIndex++;
    }
    const point = standPoint(near);
    mp.events.callRemote("server.gym.start", type, point.x, point.y, point.z, point.heading);
};

gm.events.add("client.gym.toggle", () => {
    if (busy) stop();
    else start();
});

// Сервер разрешил: встать на точку, дальше анимацию включит AnimToKey
gm.events.add("client.gym.yes", (x, y, z, heading) => {
    busy = true;
    global.gymBusy = true;
    global.localplayer.setCoordsNoOffset(x, y, z, false, false, false);
    global.localplayer.setHeading(heading);
    global.escManager && global.escManager.push("gym", stop);
    lastHint = null;
    setHint(true);
});

gm.events.add("client.gym.stopped", () => {
    busy = false;
    global.gymBusy = false;
    global.escManager && global.escManager.remove("gym");
    lastHint = null;
    setHint(false);
});

// Шаг в сторону — закончить
gm.events.add("render", () => {
    if (!busy) return;
    if (mp.game.controls.isControlJustPressed(0, 32) || mp.game.controls.isControlJustPressed(0, 33)
        || mp.game.controls.isControlJustPressed(0, 34) || mp.game.controls.isControlJustPressed(0, 35))
        stop();
});

// Форма игрока (сервер World/Gym/Fitness.cs): выносливость — дольше бег, сила — чуть сильнее удар (см. player/damage).
global.fitnessMeleeBonus = 0.25;
gm.events.add("client.fitness.apply", (stamina, strength, meleeBonus) => {
    try {
        global.fitnessMeleeBonus = typeof meleeBonus === "number" ? meleeBonus : 0.25;
        for (const prefix of ["SP0_", "SP1_", "SP2_", "MP0_"]) {
            mp.game.stats.statSetInt(mp.game.joaat(prefix + "STAMINA"), stamina, true);
            mp.game.stats.statSetInt(mp.game.joaat(prefix + "STRENGTH"), strength, true);
        }
    } catch (e) {}
});
