// Переноска груза (Organizations/Contracts/Cargo): паллета «в руках» — коробка + анимация с сервера.
// Здесь только ограничения управления и автосброс груза при падении/плавании/смерти.
global.cargoCarrying = false;

gm.events.add("client.cargo.carry", (state, name, quantity) => {
    global.cargoCarrying = !!state;
    if (state)
        mp.events.call('notify', 2, 9, `Вы несёте: ${name} ×${quantity}. Меню машины — «Положить груз в кузов», [E] — положить на землю`, 6000);
});

let lastDrop = 0;

gm.events.add("render", () => {
    if (!global.cargoCarrying) return;

    // Бег, прыжок, посадка в транспорт, атака, прицел, укрытие, колесо оружия
    [21, 22, 23, 24, 25, 37, 44, 140, 141, 142, 257, 263].forEach((control) => mp.game.controls.disableControlAction(0, control, true));

    const player = global.localplayer;
    if (player.vehicle || player.isFalling() || player.isRagdoll() || player.isSwimming() || player.isClimbing() || player.isDead() || player.getHealth() <= 0) {
        const now = Date.now();
        if (now - lastDrop > 2000) {
            lastDrop = now;
            mp.events.callRemote('server.cargo.drop');
        }
    }
});

// ---------------------------------------------------------------- паллеты в мире
// Сервер помечает объект паллеты shared-данными "cargoPallet" = "org:<id>:<кол-во>:<название>".
// Раз в секунду собираем паллеты в зоне стрима: новые кладём на землю, свои подсвечиваем стрелкой.
let pallets = [];
let lastScan = 0;

const scanPallets = () => {
    const found = [];
    mp.objects.forEachInStreamRange((object) => {
        if (!object || !mp.objects.exists(object) || !object.handle) return;
        const value = object.getVariable("cargoPallet");
        if (typeof value !== "string") return;
        const [ownerType, ownerId, quantity, name] = value.split(":");
        if (!object.cargoGrounded) {
            object.cargoGrounded = true;
            try {
                object.placeOnGroundProperly();
            } catch (e) {}
        }
        found.push({ object, own: ownerType === "org" && Number(ownerId) === Number(global.organizationId) && global.organizationId > 0, quantity, name });
    });
    pallets = found;
};

gm.events.add("render", () => {
    const now = Date.now();
    if (now - lastScan > 1000) {
        lastScan = now;
        scanPallets();
    }
    if (!pallets.length || global.cargoCarrying) return;

    const player = global.localplayer.position;
    pallets.forEach((pallet) => {
        if (!pallet.own || !mp.objects.exists(pallet.object)) return;
        const pos = pallet.object.position;
        const dist = mp.game.system.vdist(pos.x, pos.y, pos.z, player.x, player.y, player.z);
        if (dist > 60) return;
        const bounce = Math.sin(now / 300) * 0.12;
        // Стрелка над своей паллетой
        mp.game.graphics.drawMarker(2, pos.x, pos.y, pos.z + 2.2 + bounce, 0, 0, 0, 180, 0, 0, 0.6, 0.6, 0.6, 245, 165, 36, 210, false, true, 2, false, null, null, false);
        if (dist < 20)
            mp.game.graphics.drawText(`Ваш груз · ${pallet.name} ×${pallet.quantity}${dist < 3 ? " · [E] взять" : ""}`, [pos.x, pos.y, pos.z + 2.8], {
                font: 4,
                color: [245, 165, 36, 230],
                scale: [0.35, 0.35],
                outline: true,
                centre: true,
            });
    });
});
