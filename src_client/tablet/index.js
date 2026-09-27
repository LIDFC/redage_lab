// Планшет (клавиша K): фракция/организация, бизнес, маркетплейс, Forbes.
// CEF: src_cef/src/views/player/hudevo/tablet. Бизнес — server.tablet.business.* (Businesses/Tablet на сервере).

const clientName = "client.tablet.";

global.isTabletOpen = false;
let tabletAntiFlood = 0;
let inFocus = false;

global.binderFunctions.openTablet = () => {// K key
    mp.events.call(clientName + "open");
};

gm.events.add(clientName + "open", () => {
    if (!global.loggedin || global.chatActive || global.editing || global.cuffed || global.isDeath == true || global.isDemorgan == true || global.attachedtotrunk || inFocus || tabletAntiFlood > Date.now()) return;

    if (!global.isTabletOpen) {
        if (global.isPhoneOpen || global.menuCheck()) return;
        global.isTabletOpen = true;
        tabletAntiFlood = Date.now() + 500;
        mp.gui.emmit(`window.hudStore.isHudTablet (true)`);
        global.menuOpen(true);
        mp.events.callRemote("server.tablet.open");
    } else if (global.menuCheck())
        mp.events.call(clientName + "close");
});

gm.events.add(clientName + "close", () => {
    if (!global.isTabletOpen)
        return;
    tabletAntiFlood = Date.now() + 500;
    inFocus = false;
    global.isTabletOpen = false;
    mp.gui.emmit(`window.hudStore.isHudTablet (false)`);
    global.menuClose();
    mp.events.callRemote("server.tablet.close");
});

// Открыть планшет сразу в приложении (фракция/организация из круга и бинда «Планшет фракции»)
gm.events.add(clientName + "openApp", (appId) => {
    mp.gui.emmit(`window.tabletPendingApp = "${appId}"; window.tabletOpenApp && window.tabletOpenApp("${appId}");`);
    if (!global.isTabletOpen)
        setTimeout(() => mp.events.call(clientName + "open"), 50);
});

// Поле ввода в фокусе — не закрывать планшет по K/Esc из биндера
gm.events.add(clientName + "inputFocus", (toggled) => {
    if (!global.isTabletOpen)
        return;
    inFocus = toggled;
    global.menuOpen(!inFocus);
});

// Маркетплейс открывается своим окном — сначала закрываем планшет
gm.events.add(clientName + "marketplace", () => {
    mp.events.call(clientName + "close");
    setTimeout(() => mp.events.call("client.marketPlace.openApp"), 150);
});

// Действия, которые открывают серверный диалог (продажа бизнеса, заказ всего) — после закрытия планшета
gm.events.add(clientName + "businessDialog", (action) => {
    mp.events.call(clientName + "close");
    setTimeout(() => mp.events.callRemote("server.tablet.business.action", action, "{}"), 150);
});

// ─── Бизнес ─────────────────────────────────────────────────────────────────
gm.events.add(clientName + "business.load", (bizId, days) => {
    mp.events.callRemote("server.tablet.business.load", Number(bizId), Number(days));
});
gm.events.add(clientName + "business.history", (days) => {
    mp.events.callRemote("server.tablet.business.history", Number(days));
});
gm.events.add(clientName + "business.action", (action, json) => {
    if (!global.antiFlood("tablet.business", 400))
        return;
    mp.events.callRemote("server.tablet.business.action", action, json || "{}");
});
gm.events.add("client.tablet.business.data", (json) => {
    mp.gui.emmit(`window.events.callEvent("cef.tablet.business.data", ${JSON.stringify(json)})`);
});
gm.events.add("client.tablet.business.history", (json) => {
    mp.gui.emmit(`window.events.callEvent("cef.tablet.business.history", ${JSON.stringify(json)})`);
});
