// Редактор расположения HUD (CEF hudevo/elements/hudlayout.svelte).
// Открывается из Настройки → Настройки худа → «Расположение HUD». Результат — поле HudLayout в настройках персонажа,
// сохраняется тем же путём, что и остальные настройки (событие chatconfig → сервер ChatData).
let editing = false;

const stopEditing = () => {
    if (!editing) return;
    editing = false;
    global.escManager.remove("hudlayout");
    mp.gui.emmit(`window.hudLayout && window.hudLayout.edit(false)`);
    global.menuClose();
};

gm.events.add("client.hudlayout.edit", () => {
    if (editing) return;
    global.binderFunctions.GameMenuClose();
    setTimeout(() => {
        editing = true;
        global.menuOpen();
        mp.gui.emmit(`window.hudLayout && window.hudLayout.edit(true)`);
        global.escManager.push("hudlayout", stopEditing);
    }, 200);
});

gm.events.add("client.hudlayout.cancel", stopEditing);

gm.events.add("client.hudlayout.save", (layout) => {
    if (!editing) return;
    let params = {};
    try {
        params = JSON.parse(global.lastSettingsRaw || "{}");
    } catch (e) {}
    params.HudLayout = String(layout || "").replace(/[^a-z0-9:;.,\-]/gi, "");
    stopEditing();
    mp.events.call("chatconfig", JSON.stringify(params));
    mp.events.call("notify", 2, 9, "Расположение HUD сохранено", 3000);
});
