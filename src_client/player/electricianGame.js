// Мини-игра электрика на стройке (сервер: Jobs/Electrician.cs, CEF: views/jobs/electrician)
let isOpenElectricianGame = false;

const closeElectricianGame = () => {
    if (!isOpenElectricianGame)
        return;
    isOpenElectricianGame = false;
    global.menuClose();
    mp.gui.emmit(`window.router.setHud()`);
};

gm.events.add('client.electrician.game.open', () => {
    if (isOpenElectricianGame || global.menuCheck())
        return mp.events.callRemote('server.electrician.game.exit');

    isOpenElectricianGame = true;
    global.menuOpen();
    mp.gui.emmit(`window.router.setView("JobElectricianGame")`);
});

gm.events.add('client.electrician.game.exit', () => {
    if (!isOpenElectricianGame)
        return;
    closeElectricianGame();
    mp.events.callRemote('server.electrician.game.exit');
});

gm.events.add('client.electrician.game.finished', () => {
    if (!isOpenElectricianGame)
        return;
    closeElectricianGame();
    mp.events.callRemote('server.electrician.game.finished');
});
