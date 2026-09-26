// Общественные склады: окно выбора ячейки (Warehouses/WarehouseManager.cs на сервере)
gm.events.add('client.warehouse.open', (data) => {
    if (global.menuCheck() && !global.warehouseOpen) return;
    global.warehouseOpen = true;
    mp.gui.emmit(`window.router.setView("PlayerWarehouse", '${data.replace(/\\/g, "\\\\").replace(/'/g, "\\'")}')`);
    global.menuOpen();
});

gm.events.add('client.warehouse.close', () => {
    closeWarehouse();
});

gm.events.add('warehouse', (act, jsonData) => {
    const data = JSON.parse(jsonData || "{}");
    switch (act) {
        case "buy":
            mp.events.callRemote("server.warehouse.buy", data.buildingId, data.slot, !!data.family);
            break;
        case "enter":
            mp.events.callRemote("server.warehouse.enter", data.unitId);
            break;
        case "sell":
            mp.events.callRemote("server.warehouse.sell", data.unitId);
            break;
        case "close":
            closeWarehouse();
            break;
    }
});

function closeWarehouse() {
    if (!global.warehouseOpen)
        return;
    global.warehouseOpen = false;
    mp.gui.emmit(`window.router.setHud();`);
    global.menuClose();
}
