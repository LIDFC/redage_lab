// Подработки (сервер Jobs/DayLabor/DayLabor.cs): грузчик в порту и посадка рассады на ферме.
// Ферма: окно мини-игры JobFarmGame (src_cef/src/views/jobs/farm) — закопать рассаду лопаткой и полить из лейки.

// ---- Порт: пока в руках ящик — без бега, прыжков, оружия и посадки в машину
let carrying = false;

gm.events.add("client.daylabor.carry", (state) => {
    carrying = !!state;
});

gm.events.add("render", () => {
    if (!carrying) return;
    mp.game.controls.disableControlAction(0, 21, true); // бег
    mp.game.controls.disableControlAction(0, 22, true); // прыжок
    mp.game.controls.disableControlAction(0, 23, true); // сесть в машину
    mp.game.controls.disableControlAction(0, 24, true); // атака
    mp.game.controls.disableControlAction(0, 25, true); // прицел
    mp.game.controls.disableControlAction(0, 37, true); // выбор оружия
    if (global.localplayer.isInAnyVehicle(false) || global.localplayer.isRagdoll()) {
        carrying = false;
        mp.events.callRemote("server.daylabor.drop");
    }
});

// ---- Ферма: окно мини-игры
let isFarmOpen = false;

const closeFarm = () => {
    if (!isFarmOpen) return false;
    isFarmOpen = false;
    global.menuClose();
    mp.gui.emmit(`window.router.setHud()`);
    return true;
};

gm.events.add("client.daylabor.farm.open", () => {
    if (isFarmOpen || global.menuCheck())
        return mp.events.callRemote("server.daylabor.farm.exit");
    isFarmOpen = true;
    global.menuOpen();
    mp.gui.emmit(`window.router.setView("JobFarmGame")`);
});

gm.events.add("client.daylabor.farm.exit", () => {
    if (closeFarm())
        mp.events.callRemote("server.daylabor.farm.exit");
});

gm.events.add("client.daylabor.farm.finished", () => {
    if (closeFarm())
        mp.events.callRemote("server.daylabor.farm.finished");
});

// Сервер закрыл (смерть, конец смены)
gm.events.add("client.daylabor.farm.close", () => closeFarm());
