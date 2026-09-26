// Чёрный рынок: VPN в настройках телефона и приложение (CEF view "BlackMarket").
// Сервер: dotnet/resources/NeptuneEvo/BlackMarket. Клиент ничего не решает — только передаёт запросы;
// VPN проверяется на сервере при открытии и каждом действии, состояние живёт до выхода из игры.
const clientName = "client.blackmarket.";

let vpn = false;
let opened = false;

rpc.register("rpc.phone.settings.isVpn", () => vpn);

gm.events.add("client.phone.settings.vpn", () => {
    mp.events.callRemote("server.blackmarket.vpn");
});

gm.events.add(clientName + "vpn", (on) => {
    vpn = !!on;
    mp.gui.emmit(`window.events.callEvent("phone.vpn", ${vpn})`);
    if (!vpn && opened)
        closeApp();
});

gm.events.add(clientName + "openApp", () => {
    mp.events.callRemote("server.blackmarket.open");
});

gm.events.add(clientName + "open", (json) => {
    mp.gui.emmit(`window.router.setView("BlackMarket", ${JSON.stringify(json)})`);
    if (!opened)
        global.menuOpen();
    opened = true;
});

const closeApp = () => {
    if (!opened)
        return;
    opened = false;
    mp.gui.emmit(`window.router.setHud()`);
    global.menuClose();
};

gm.events.add(clientName + "close", closeApp);

gm.events.add(clientName + "action", (action, json) => {
    mp.events.callRemote("server.blackmarket.action", String(action), json ? String(json) : "");
});

gm.events.add(clientName + "result", (action, ok, message, json) => {
    mp.gui.emmit(`window.events.callEvent("blackmarket.result", ${JSON.stringify(action)}, ${!!ok}, ${JSON.stringify(message)}, ${JSON.stringify(json)})`);
});

gm.events.add(clientName + "history", (scope, json) => {
    mp.gui.emmit(`window.events.callEvent("blackmarket.history", ${JSON.stringify(scope)}, ${JSON.stringify(json)})`);
});
