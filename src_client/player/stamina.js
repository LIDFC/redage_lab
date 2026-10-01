// Выносливость (бег). Свой запас 0–100 вместо бесконечного бега:
//  - при спринте тратится; длительность бега зависит от показателя «Выносливость» (World/Gym/Fitness.cs):
//    30 (без качалки) — около 12 с, 100 — около 60 с; при ходьбе и стоянии восстанавливается;
//  - кончился запас на бегу — персонаж падает (ragdoll), встаёт и теряет немного здоровья (не ниже 10 HP);
//  - пока запас не восстановится до 30%, бежать нельзя (одышка);
// Спринт определяем по зажатому Shift и скорости (у ped нет надёжного isSprinting в RAGE).
global.fitStamina = 30;

let stamina = 100;
let exhaustedUntil = 0;
let tired = false;
let last = Date.now();
let lastErrorReport = 0;
let notifiedFall = false;

const runSeconds = () => 12 + Math.max(0, Math.min(1, (global.fitStamina - 30) / 70)) * 48;

const reportError = (where, e) => {
    if (Date.now() - lastErrorReport < 60000) return;
    lastErrorReport = Date.now();
    try {
        mp.events.callRemote("client_trycatch", "player/stamina", where, String(e));
    } catch (err) {}
};

const ignored = (p) =>
    !global.loggedin || global.isDeath === true || global.cuffed ||
    p.isInAnyVehicle(false) || p.isSwimming() || p.isFalling() || p.isRagdoll();

const isSprinting = (p) => {
    const shift = mp.game.controls.isControlPressed(0, 21) || mp.game.controls.isDisabledControlPressed(0, 21);
    return shift && p.getSpeed() > 5.5;
};

const fall = (p) => {
    exhaustedUntil = Date.now() + 3500;
    tired = true;
    try {
        p.setToRagdoll(2500, 3000, 0, false, false, false);
    } catch (e) {
        reportError("ragdoll", e);
    }
    try {
        const hp = p.getHealth();
        const loss = 3 + Math.floor(Math.random() * 3); // 3–5 HP
        if (!global.admingm && hp - loss >= 10)
            p.applyDamageTo(loss, true);
    } catch (e) {
        reportError("damage", e);
    }
    if (!notifiedFall) {
        notifiedFall = true; // подсказка — один раз за сессию
        mp.events.call("notify", 4, 9, "Вы выдохлись и упали. Тренируйте выносливость в качалке", 3500);
    }
};

gm.events.add("render", () => {
    try {
        const now = Date.now();
        const dt = Math.min(0.2, (now - last) / 1000);
        last = now;
        if (!global.loggedin) return;
        const p = global.localplayer;
        // Нативную выносливость держим полной — ограничение считаем сами
        mp.game.player.restoreStamina(100);

        if (tired) {
            mp.game.controls.disableControlAction(0, 21, true); // бег недоступен, пока не отдышится
            if (now > exhaustedUntil && stamina >= 30)
                tired = false;
        }

        if (ignored(p)) {
            if (!p.isRagdoll()) stamina = Math.min(100, stamina + dt * 20);
        } else if (!tired && isSprinting(p)) {
            stamina -= dt * (100 / runSeconds());
            if (stamina <= 0) {
                stamina = 0;
                fall(p);
            }
        } else {
            stamina = Math.min(100, stamina + dt * (p.getSpeed() > 2.5 ? 6 : 14));
        }

    } catch (e) {
        reportError("render", e);
    }
});
