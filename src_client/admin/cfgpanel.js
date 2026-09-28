// Админ-панель настроек settings/*.json (CEF AdminConfigPanel, сервер Functions/ConfigPanel.cs, команда /cfg)
let isOpenCfgPanel = false;

gm.events.add("client.cfgpanel.open", (json) => {
    if (isOpenCfgPanel) {
        mp.gui.emmit(`window.events.callEvent("cef.cfgpanel.update", true, "", ${JSON.stringify(json)})`);
        return;
    }
    isOpenCfgPanel = true;
    global.menuOpen();
    mp.gui.emmit(`window.router.setView("AdminConfigPanel", ${JSON.stringify(json)})`);
});

gm.events.add("client.cfgpanel.close", () => {
    if (!isOpenCfgPanel) return;
    isOpenCfgPanel = false;
    global.menuClose();
    mp.gui.emmit('window.router.setHud()');
});

gm.events.add("client.cfgpanel.save", (changes) => {
    if (!isOpenCfgPanel) return;
    mp.events.callRemote("server.cfgpanel.save", changes);
});

gm.events.add("client.cfgpanel.result", (ok, message, json) => {
    if (!isOpenCfgPanel) return;
    mp.gui.emmit(`window.events.callEvent("cef.cfgpanel.update", ${!!ok}, ${JSON.stringify(message)}, ${JSON.stringify(json)})`);
});

gm.events.add("client.cfgpanel.rollback", (historyId) => {
    if (!isOpenCfgPanel) return;
    mp.events.callRemote("server.cfgpanel.rollback", Number(historyId) || 0);
});

gm.events.add("client.cfgpanel.reload", (sectionId) => {
    if (!isOpenCfgPanel) return;
    mp.events.callRemote("server.cfgpanel.reload", String(sectionId));
});

gm.events.add("client.cfgpanel.preset.save", (name, changes) => {
    if (!isOpenCfgPanel) return;
    mp.events.callRemote("server.cfgpanel.preset.save", String(name), changes);
});

gm.events.add("client.cfgpanel.preset.apply", (name) => {
    if (!isOpenCfgPanel) return;
    mp.events.callRemote("server.cfgpanel.preset.apply", String(name));
});

gm.events.add("client.cfgpanel.preset.delete", (name) => {
    if (!isOpenCfgPanel) return;
    mp.events.callRemote("server.cfgpanel.preset.delete", String(name));
});
