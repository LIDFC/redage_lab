// Государственный склад стройматериалов (подряды организаций): окно покупки материалов за бюджет организации.
const
    clientName = "client.orgcontracts.shop.",
    serverName = "server.orgcontracts.shop.";

let isOpenContractShop = false;

gm.events.add(clientName + "open", (json) => {
    if (global.menuCheck() || isOpenContractShop) return;

    global.menuOpen();
    isOpenContractShop = true;
    mp.gui.emmit(`window.router.setView("FractionsContractShop", ${JSON.stringify(json)})`);
});

gm.events.add(clientName + "update", (ok, message, json) => {
    if (!isOpenContractShop) return;
    mp.gui.emmit(`window.events.callEvent("cef.orgcontracts.shop.update", ${!!ok}, ${JSON.stringify(message)}, ${JSON.stringify(json)})`);
});

gm.events.add(clientName + "close", () => {
    if (!isOpenContractShop) return;

    isOpenContractShop = false;
    global.menuClose();
    mp.gui.emmit('window.router.setHud()');
    mp.events.callRemote(serverName + "close");
});

gm.events.add(clientName + "buy", (contractId, material, units) => {
    if (!isOpenContractShop || !global.antiFlood("orgcontracts.shop.buy", 700)) return;

    mp.events.callRemote(serverName + "buy", Number(contractId), String(material), Number(units));
});
