// Взлом замка отмычкой (CEF PlayerLockBreak, сервер Crime/LockBreak.cs)
let isOpenLockBreak = false;

gm.events.add("client.lockbreak.open", (json) => {
    if (isOpenLockBreak) return;
    isOpenLockBreak = true;
    global.menuOpen();
    mp.gui.emmit(`window.router.setView("PlayerLockBreak", ${JSON.stringify(json)})`);
});

gm.events.add("client.lockbreak.close", () => {
    if (!isOpenLockBreak) return;
    isOpenLockBreak = false;
    global.menuClose();
    mp.gui.emmit('window.router.setHud()');
});

gm.events.add("client.lockbreak.newpick", (count) => {
    if (!isOpenLockBreak) return;
    mp.gui.emmit(`window.events.callEvent("cef.lockbreak.newpick", ${Number(count) || 0})`);
});

gm.events.add("client.lockbreak.opened", () => {
    if (!isOpenLockBreak) return;
    mp.events.callRemote("server.lockbreak.opened");
});

gm.events.add("client.lockbreak.broken", () => {
    if (!isOpenLockBreak) return;
    mp.events.callRemote("server.lockbreak.broken");
});

gm.events.add("client.lockbreak.cancel", () => {
    if (!isOpenLockBreak) return;
    mp.events.callRemote("server.lockbreak.cancel");
});
