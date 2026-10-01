// Выносливость (бег). Свой запас 0–100 вместо бесконечного бега:
//  - при спринте тратится; длительность бега зависит от показателя «Выносливость» (World/Gym/Fitness.cs):
//    30 (без качалки) — около 12 с, 100 — около 60 с; при ходьбе и стоянии восстанавливается;
//  - кончился запас на бегу — персонаж падает (ragdoll), встаёт и теряет немного здоровья (не ниже 10 HP);
//  - пока запас не восстановится до 30%, бежать нельзя (одышка);
//  - тонкая полоска внизу экрана видна, пока запас не полный.
global.fitStamina = 30;

let stamina = 100;
let exhaustedUntil = 0;
let tired = false;
let last = Date.now();

const runSeconds = () => 12 + Math.max(0, Math.min(1, (global.fitStamina - 30) / 70)) * 48;

const ignored = () =>
    !global.loggedin || global.isDeath === true || global.admingm || global.cuffed ||
    global.localplayer.isInAnyVehicle(false) || global.localplayer.isSwimming();

const fall = () => {
    exhaustedUntil = Date.now() + 3500;
    tired = true;
    try {
        global.localplayer.setToRagdoll(2500, 3000, 0, false, false, false);
        const hp = global.localplayer.getHealth();
        const loss = 3 + Math.floor(Math.random() * 3); // 3–5 HP
        if (hp - loss >= 10)
            global.localplayer.applyDamageTo(loss, true);
    } catch (e) {}
    mp.events.call("notify", 4, 9, "Вы выдохлись и упали. Тренируйте выносливость в качалке", 3500);
};

gm.events.add("render", () => {
    const now = Date.now();
    const dt = Math.min(0.2, (now - last) / 1000);
    last = now;
    // Нативную выносливость держим полной — ограничение считаем сами
    mp.game.player.restoreStamina(100);
    if (ignored()) {
        stamina = Math.min(100, stamina + dt * 20);
        return;
    }

    const sprinting = global.localplayer.isSprinting();
    if (tired) {
        mp.game.controls.disableControlAction(0, 21, true); // бег недоступен, пока не отдышится
        if (now > exhaustedUntil && stamina >= 30)
            tired = false;
    }

    if (sprinting && !tired) {
        stamina -= dt * (100 / runSeconds());
        if (stamina <= 0) {
            stamina = 0;
            fall();
        }
    } else {
        const regen = global.localplayer.isRunning() ? 6 : 14;
        stamina = Math.min(100, stamina + dt * regen);
    }

    if (stamina < 99.5) {
        const w = 0.12, h = 0.006, x = 0.5, y = 0.965;
        mp.game.graphics.drawRect(x, y, w, h, 0, 0, 0, 140);
        const fill = w * stamina / 100;
        const red = stamina < 25 || tired;
        mp.game.graphics.drawRect(x - w / 2 + fill / 2, y, fill, h, red ? 230 : 120, red ? 80 : 200, red ? 80 : 255, 220);
    }
});
