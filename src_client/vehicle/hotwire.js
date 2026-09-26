// Мини-игра HotWire при ремонте машины без ключа (сервер: Core/VehicleRepair.cs, CEF: views/vehicle/hotwire)
let isOpenHotWire = false;

const closeHotWire = () => {
    if (!isOpenHotWire)
        return;
    isOpenHotWire = false;
    global.menuClose();
    mp.gui.emmit(`window.router.setHud()`);
};

gm.events.add('client.hotwire.open', () => {
    if (isOpenHotWire || global.menuCheck())
        return mp.events.callRemote('server.hotwire.exit');

    isOpenHotWire = true;
    global.menuOpen();
    mp.gui.emmit(`window.router.setView("VehicleHotWire")`);
});

gm.events.add('client.hotwire.exit', () => {
    if (!isOpenHotWire)
        return;
    closeHotWire();
    mp.events.callRemote('server.hotwire.exit');
});

gm.events.add('client.hotwire.finished', () => {
    if (!isOpenHotWire)
        return;
    closeHotWire();
    mp.events.callRemote('server.hotwire.finished');
});
