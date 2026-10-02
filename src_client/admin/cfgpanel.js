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

// Справочник команд приходит частями (client.cfgpanel.commands) — собираем и отдаём окну.
// Окно может ещё не успеть открыться, поэтому оно само просит данные (client.cfgpanel.commandsReady).
let commandParts = [];
let commandsJson = null;
const sendCommands = () => {
    if (!isOpenCfgPanel || commandsJson === null) return;
    mp.gui.emmit(`window.events.callEvent("cef.cfgpanel.commands", ${JSON.stringify(commandsJson)})`);
};

gm.events.add("client.cfgpanel.commands", (index, total, chunk) => {
    if (index === 0) commandParts = [];
    commandParts[index] = chunk;
    if (commandParts.filter((x) => typeof x === "string").length === total) {
        commandsJson = commandParts.join("");
        commandParts = [];
        sendCommands();
    }
});

gm.events.add("client.cfgpanel.commandsReady", () => sendCommands());

gm.events.add("client.cfgpanel.docReload", () => {
    if (!isOpenCfgPanel) return;
    mp.events.callRemote("server.cfgpanel.docReload");
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
