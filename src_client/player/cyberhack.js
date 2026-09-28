// Программатор «Breach protocol» (CEF PlayerCyberHack, сервер Crime/CyberHack.cs)
let isOpenCyberHack = false;

gm.events.add("client.cyberhack.open", (json) => {
    if (isOpenCyberHack) return;
    isOpenCyberHack = true;
    global.menuOpen();
    mp.gui.emmit(`window.router.setView("PlayerCyberHack", ${JSON.stringify(json)})`);
});

gm.events.add("client.cyberhack.close", () => {
    if (!isOpenCyberHack) return;
    isOpenCyberHack = false;
    global.menuClose();
    mp.gui.emmit('window.router.setHud()');
});

gm.events.add("client.cyberhack.finish", (success) => {
    if (!isOpenCyberHack) return;
    mp.events.callRemote("server.cyberhack.result", !!success);
});

gm.events.add("client.cyberhack.cancel", () => {
    if (!isOpenCyberHack) return;
    mp.events.callRemote("server.cyberhack.cancel");
});
