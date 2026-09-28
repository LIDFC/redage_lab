gm.events.add('client.parking.open', async () => {
	try
	{
        await global.awaitMenuCheck ();
        //

        global.menuOpen();
        mp.gui.emmit(
            `window.router.setView("HouseMenu")`
        );

	}
	catch (e) 
	{
		mp.events.callRemote("client_trycatch", "house/index", "client.house.open", e.toString());
	}
});

gm.events.add('client.parking.close', () => {//+
    mp.gui.emmit(`window.router.setHud();`);
    global.menuClose();
})

gm.events.add('client.garage.parking', (number, place) => {//+
    mp.events.callRemote('server.garage.parking', number, place);
});

//gm.events.add('client.parking.confirm', (sqlId, place) => {//+
//    mp.gui.emmit(`window.events.callEvent("cef.parking.confirm", ${sqlId}, ${place})`);
//});

gm.events.add('client.parking.updateCar', (json) => {//+
    mp.gui.emmit(`window.events.callEvent("cef.parking.carsData", '${json}')`);
    mp.gui.emmit(`window.events.callEvent("cef.parking.confirm")`);
});

gm.events.add('client.vehicle.action', (number, action) => {//+
    if (action === "sell")
        mp.events.call('client.house.close');

    mp.events.callRemote('server.vehicle.action', number, action);
});

gm.events.add('client.garage.update', () => {//+
    mp.events.call('client.house.close');
    mp.events.callRemote('server.garage.update');
});

//

gm.events.add('client.houseinfo.open', (data) => {//+
    if (global.menuCheck()) return;

    global.menuOpen();
    gm.discord(translateText("Присматривает дом"));
    
    mp.gui.emmit(
        `window.router.setView("HouseBuy", '${data}')`
    );
});

gm.events.add('client.houseinfo.close', () => {//+
    global.menuClose();
    mp.gui.emmit(`window.router.setHud()`);
});

gm.events.add('client.houseinfo.action', (action) => {//+
    mp.events.call('client.houseinfo.close');
    mp.events.callRemote('server.houseinfo.action', action);
});

//////////////////////

let furnitureShopJson = null;
let furnitureCount = [-1, 100];

gm.events.add('client.furniture.open', (json) => {//+
    if (global.menuCheck()) return;
    furnitureShopJson = json;
    global.menuOpen();
    gm.discord(translateText("Присматривает мебель"));
    mp.gui.emmit(
        `window.router.setView("HouseFurniture", '${json}')`
    );
});

// Сколько мебели в доме игрока (-1 — дома нет)
gm.events.add('client.furniture.count', (count, max) => {
    furnitureCount = [count, max];
    mp.gui.emmit(`window.listernEvent ('furniture.count', ${Number(count)}, ${Number(max)});`);
});
gm.events.add('client.furniture.getCount', () => {
    mp.gui.emmit(`window.listernEvent ('furniture.count', ${furnitureCount[0]}, ${furnitureCount[1]});`);
});

// ─── Предпросмотр мебели: модель перед игроком, камера медленно облетает её. ESC или 20 с — назад в магазин ───
let preview = null;

const endPreview = (reopen = true) => {
    if (!preview) return;
    const p = preview;
    preview = null;
    global.escManager.remove("furniture.preview");
    try {
        if (p.object && mp.objects.exists(p.object)) p.object.destroy();
    } catch (e) {}
    try {
        mp.game.cam.renderScriptCams(false, true, 400, true, false);
        if (p.cam) p.cam.destroy();
    } catch (e) {}
    if (reopen && furnitureShopJson) {
        global.menuOpen();
        mp.gui.emmit(`window.router.setView("HouseFurniture", '${furnitureShopJson}')`);
    }
};

gm.events.add('client.furniture.preview', async (model, name) => {
    try {
        if (preview) endPreview(false);
        const hash = mp.game.joaat(model);
        if (!mp.game.streaming.isModelInCdimage(hash)) {
            mp.events.call('notify', 4, 9, "Модель недоступна для предпросмотра", 3000);
            return;
        }
        await global.loadModel(hash);

        const player = global.localplayer;
        const heading = player.getHeading() * Math.PI / 180;
        const dims = mp.game.gameplay.getModelDimensions(hash);
        const size = Math.max(Math.abs(dims.max.x - dims.min.x), Math.abs(dims.max.y - dims.min.y), Math.abs(dims.max.z - dims.min.z), 0.5);
        const distance = Math.min(8, Math.max(1.8, size * 1.4));
        const center = new mp.Vector3(player.position.x - Math.sin(heading) * distance, player.position.y + Math.cos(heading) * distance, player.position.z - 0.95);

        const object = mp.objects.new(hash, center, { rotation: new mp.Vector3(0, 0, 0), dimension: player.dimension });
        await global.IsLoadEntity(object);
        try { object.placeOnGroundProperly(); } catch (e) {}
        try { object.setCollision(false, false); } catch (e) {}

        mp.gui.emmit('window.router.setHud()');
        global.menuClose();

        const cam = mp.cameras.new('default', player.position, new mp.Vector3(0, 0, 0), 50);
        cam.setActive(true);
        mp.game.cam.renderScriptCams(true, true, 400, true, false);

        preview = { object, cam, center, distance: distance + 0.6, height: Math.max(0.8, size * 0.6), angle: heading + Math.PI, name: name || "", until: Date.now() + 20000 };
        global.escManager.push("furniture.preview", () => endPreview(true));
    } catch (e) {
        endPreview(true);
        mp.events.callRemote("client_trycatch", "house/index", "client.furniture.preview", e.toString());
    }
});

gm.events.add('render', () => {
    if (!preview) return;
    if (Date.now() > preview.until || !mp.objects.exists(preview.object)) {
        endPreview(true);
        return;
    }
    // Камера по кругу вокруг модели, игроку управление не нужно
    mp.game.controls.disableAllControlActions(0);
    preview.angle += 0.004;
    const pos = preview.object.position;
    const cx = pos.x + Math.sin(preview.angle) * preview.distance;
    const cy = pos.y - Math.cos(preview.angle) * preview.distance;
    preview.cam.setCoord(cx, cy, pos.z + preview.height + 0.4);
    preview.cam.pointAtCoord(pos.x, pos.y, pos.z + preview.height * 0.35);
    const left = Math.ceil((preview.until - Date.now()) / 1000);
    mp.game.graphics.drawText(`${preview.name}\n~c~ESC — вернуться в магазин (${left} с)`, [0.5, 0.86], {
        font: 4, color: [255, 255, 255, 235], scale: [0.5, 0.5], outline: true, centre: true,
    });
});

gm.events.add('client.furniture.buy', (name, type) => {//+
    mp.events.callRemote('server.furniture.buy', name, type);
});

gm.events.add('client.furniture.close', () => {//+
    endPreview(false);
    furnitureShopJson = null;
    global.menuClose();
    mp.gui.emmit(`window.router.setHud()`);
});

//


gm.events.add('client.vehicleair.open', (json) => {//+
    if (global.menuCheck()) return;
    global.menuOpen();
    gm.discord(translateText("В магазине вертолётов"));
    mp.gui.emmit(
        `window.router.setView("VehicleAir", '${json}')`
    );
});

gm.events.add('client.vehicleair.exit', () => {//+
    mp.gui.emmit(`window.router.setHud();`);
    global.menuClose();
})