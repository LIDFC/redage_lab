// Приложение Fleeca (банк в телефоне): данные и действия — сервер Players/Phone/Fleeca
const
    clientName = "client.phone.bank.",
    serverName = "server.phone.bank.";

gm.events.add(clientName + "load", () => {
    mp.events.callRemote(serverName + "load");
});

gm.events.add(clientName + "data", (json) => {
    mp.gui.emmit(`window.listernEvent ('phone.bank.data', ${JSON.stringify(json)})`);
});

gm.events.add(clientName + "action", (action, arg1 = 0, arg2 = 0) => {
    if (!global.antiFlood("phone.bank.action", 800)) return;
    mp.events.callRemote(serverName + "action", String(action), Number(arg1) || 0, Number(arg2) || 0);
});

gm.events.add(clientName + "result", (ok, message, json) => {
    mp.gui.emmit(`window.listernEvent ('phone.bank.result', ${!!ok}, ${JSON.stringify(message)}, ${JSON.stringify(json)})`);
});

gm.events.add(clientName + "historyLoad", () => {
    if (!global.antiFlood("phone.bank.history", 800)) return;
    mp.events.callRemote(serverName + "history");
});

gm.events.add(clientName + "history", (json) => {
    mp.gui.emmit(`window.listernEvent ('phone.bank.history', ${JSON.stringify(json)})`);
});
