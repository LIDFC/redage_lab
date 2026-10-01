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
    // Гантели и штанги, лежащие у тренажёров (Muscle Beach и др.)
    { model: "prop_barbell_01", type: "dumbbells", offset: [0, -0.6, 0], heading: 0 },
    { model: "prop_barbell_02", type: "dumbbells", offset: [0, -0.6, 0], heading: 0 },
    { model: "prop_barbell_10kg", type: "dumbbells", offset: [0, -0.6, 0], heading: 0 },
    { model: "prop_barbell_20kg", type: "dumbbells", offset: [0, -0.6, 0], heading: 0 },
    { model: "prop_barbell_30kg", type: "dumbbells", offset: [0, -0.6, 0], heading: 0 },
    { model: "prop_barbell_40kg", type: "dumbbells", offset: [0, -0.6, 0], heading: 0 },
    { model: "prop_barbell_50kg", type: "dumbbells", offset: [0, -0.6, 0], heading: 0 },
    { model: "prop_curl_bar_01", type: "dumbbells", offset: [0, -0.6, 0], heading: 0 },
    { model: "prop_weight_10k", type: "dumbbells", offset: [0, -0.6, 0], heading: 0 },
    { model: "prop_weight_15k", type: "dumbbells", offset: [0, -0.6, 0], heading: 0 },
    { model: "prop_weight_20k", type: "dumbbells", offset: [0, -0.6, 0], heading: 0 },
    { model: "prop_freeweight_01", type: "dumbbells", offset: [0, -0.6, 0], heading: 0 },
    { model: "prop_freeweight_02", type: "dumbbells", offset: [0, -0.6, 0], heading: 0 },
].map((item) => ({ ...item, hash: mp.game.joaat(item.model) }));

// Упражнения на снаряде: E — первое, во время занятия стрелки ← → переключают на следующее (без отдельных окон)
const VARIANTS = {
    chinup: ["chinup"],
    bench: ["bench"],
    weights: ["weights", "curls"],
    dumbbells: ["curls", "weights"],
    mat: ["situps", "pushups", "yoga", "stretch", "flex", "jog"],
};
let variants = [];
const EXERCISE_NAMES = {
    chinup: "Подтягивания", bench: "Жим лёжа", weights: "Штанга", curls: "Гантели (бицепс)",
    situps: "Пресс", pushups: "Отжимания", yoga: "Йога", stretch: "Растяжка", flex: "Позирование", jog: "Бег на месте",
};
let variantIndex = 0;

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
    if (show) mp.events.call("hud.oEnter", busy ? (variants.length > 1 ? "GymSwitch" : "GymStop") : "Gym");
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
    variants = VARIANTS[near.item.type] || [near.item.type];
    variantIndex = 0;
    const point = standPoint(near);
    mp.events.callRemote("server.gym.start", variants[0], point.x, point.y, point.z, point.heading);
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

// Шаг в сторону — закончить; стрелки ← → — другое упражнение на этом снаряде
let lastSwitch = 0;
gm.events.add("render", () => {
    if (!busy) return;
    if (variants.length > 1 && Date.now() - lastSwitch > 600) {
        const left = mp.game.controls.isDisabledControlJustPressed(0, 174) || mp.game.controls.isControlJustPressed(0, 174);
        const right = mp.game.controls.isDisabledControlJustPressed(0, 175) || mp.game.controls.isControlJustPressed(0, 175);
        if (left || right) {
            lastSwitch = Date.now();
            variantIndex = (variantIndex + (right ? 1 : variants.length - 1)) % variants.length;
            mp.events.callRemote("server.gym.switch", variants[variantIndex]);
            mp.events.call("notify", 0, 9, EXERCISE_NAMES[variants[variantIndex]] || variants[variantIndex], 1500);
        }
    }
    if (mp.game.controls.isControlJustPressed(0, 32) || mp.game.controls.isControlJustPressed(0, 33)
        || mp.game.controls.isControlJustPressed(0, 34) || mp.game.controls.isControlJustPressed(0, 35))
        stop();
});

// Форма игрока (сервер World/Gym/Fitness.cs): выносливость — дольше бег, сила — чуть сильнее удар (см. player/damage).
global.fitnessMeleeBonus = 0.25;
gm.events.add("client.fitness.apply", (stamina, strength, meleeBonus) => {
    try {
        global.fitnessMeleeBonus = typeof meleeBonus === "number" ? meleeBonus : 0.25;
        global.fitStamina = Number(stamina) || 30; // player/stamina.js — длительность бега
        for (const prefix of ["SP0_", "SP1_", "SP2_", "MP0_"]) {
            mp.game.stats.statSetInt(mp.game.joaat(prefix + "STAMINA"), stamina, true);
            mp.game.stats.statSetInt(mp.game.joaat(prefix + "STRENGTH"), strength, true);
        }
    } catch (e) {}
});
