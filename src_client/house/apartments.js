// Меню подъезда многоквартирного дома (сервер: Houses/Apartments/ApartmentManager.cs)
let isOpenApartments = false;

gm.events.add('client.apartments.open', (json) => {
    if (isOpenApartments) {
        // уже открыто — просто обновляем данные
        mp.gui.emmit(`window.router.setView("HouseApartments", ${JSON.stringify(json)})`);
        return;
    }
    if (global.menuCheck())
        return;

    isOpenApartments = true;
    global.menuOpen();
    mp.gui.emmit(`window.router.setView("HouseApartments", ${JSON.stringify(json)})`);
});

gm.events.add('client.apartments.close', () => {
    if (!isOpenApartments)
        return;

    isOpenApartments = false;
    global.menuClose();
    mp.gui.emmit(`window.router.setHud()`);
});

gm.events.add('client.apartments.action', (buildingId, houseId, action) => {
    if (!isOpenApartments)
        return;

    mp.events.callRemote('server.apartments.action', buildingId, houseId, action);
});
