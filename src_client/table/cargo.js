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
