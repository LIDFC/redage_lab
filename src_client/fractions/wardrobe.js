// Гардероб фракций (сервер Fractions/Wardrobe/Wardrobe.cs, CEF views/fractions/wardrobe).
// Предпросмотр — локально на самом игроке; при выходе без сохранения внешний вид откатывается.
let isOpen = false;
let original = null;

const PROPS = [0, 1, 2, 6, 7];

const remember = () => {
    const p = global.localplayer;
    const comps = {};
    for (let i = 0; i <= 11; i++)
        comps[i] = [p.getDrawableVariation(i), p.getTextureVariation(i)];
    const props = {};
    PROPS.forEach((i) => {
        try {
            props[i] = [mp.game.ped.getPedPropIndex(p.handle, i), mp.game.ped.getPedPropTextureIndex(p.handle, i)];
        } catch (e) {
            props[i] = [-1, 0];
        }
    });
    return { comps, props };
};

const restoreSlot = (slot, isProp) => {
    if (!original) return;
    const p = global.localplayer;
    if (isProp) {
        const v = original.props[slot];
        if (!v || v[0] < 0) p.clearProp(slot);
        else p.setPropIndex(slot, v[0], v[1], true);
    } else {
        const v = original.comps[slot];
        if (v) p.setComponentVariation(slot, v[0], v[1], 0);
    }
};

const restoreAll = () => {
    if (!original) return;
    for (let i = 0; i <= 11; i++) restoreSlot(i, false);
    PROPS.forEach((i) => restoreSlot(i, true));
};

// Название вещи: своё (clothesNames) → игровое по GXT → «Вариант N»
const itemName = (item) => {
    if (item.name) return item.name;
    try {
        if (item.tname && item.tname.length > 1) {
            const label = mp.game.ui.getLabelText(`${item.tname}${item.textures[0] || 0}`);
            if (label && label !== "NULL") return label;
        }
    } catch (e) {}
    return `Вариант ${item.id}`;
};

const close = (applied) => {
    if (!isOpen) return;
    isOpen = false;
    if (!applied) restoreAll();
    original = null;
    global.cameraManager.stopCamera();
    global.localplayer.freezePosition(false);
    global.menuClose();
    mp.gui.emmit(`window.router.setHud();`);
};

gm.events.add("client.wardrobe.open", (json) => {
    try {
        if (isOpen || global.menuCheck()) return;
        const data = JSON.parse(json);
        data.categories.forEach((c) => c.items.forEach((item) => (item.title = itemName(item))));
        // Торс — все варианты компонента 3 из игры
        const p = global.localplayer;
        const count = mp.game.ped.getNumberOfPedDrawableVariations(p.handle, 3);
        const torsos = [];
        for (let d = 0; d < count; d++)
            torsos.push({ id: d, drawable: d, textures: Math.max(1, mp.game.ped.getNumberOfPedTextureVariations(p.handle, 3, d)) });
        data.torsos = torsos;

        original = remember();
        isOpen = true;
        global.menuOpen();
        p.freezePosition(true);
        global.createCamera("char", p);
        mp.gui.emmit(`window.router.setView("FractionWardrobe", ${JSON.stringify(JSON.stringify(data))});`);
    } catch (e) {
        mp.events.callRemote("client_trycatch", "fractions/wardrobe", "client.wardrobe.open", e.toString());
    }
});

gm.events.add("client.wardrobe.preview", (slot, isProp, drawable, texture, torso) => {
    if (!isOpen) return;
    const p = global.localplayer;
    if (isProp) p.setPropIndex(slot, drawable, texture, true);
    else {
        p.setComponentVariation(slot, drawable, texture, 0);
        if (slot === 11 && torso >= 0) p.setComponentVariation(3, torso, 0, 0);
    }
});

gm.events.add("client.wardrobe.previewTorso", (drawable, texture) => {
    if (!isOpen) return;
    global.localplayer.setComponentVariation(3, drawable, texture, 0);
});

gm.events.add("client.wardrobe.clear", (slot, isProp) => {
    if (!isOpen) return;
    restoreSlot(slot, !!isProp);
});

gm.events.add("client.wardrobe.camera", (bone) => {
    if (!isOpen) return;
    global.updateCameraToBone(bone, global.localplayer);
});

gm.events.add("client.wardrobe.save", (json, duty) => {
    if (!isOpen || !global.antiFlood("wardrobe.save", 800)) return;
    mp.events.callRemote("server.wardrobe.save", json, !!duty);
});

gm.events.add("client.wardrobe.takeoff", () => {
    if (!isOpen || !global.antiFlood("wardrobe.takeoff", 800)) return;
    mp.events.callRemote("server.wardrobe.takeoff");
});

gm.events.add("client.wardrobe.exit", () => close(false));

// Сервер надел/снял форму — окно закрываем без отката
gm.events.add("client.wardrobe.close", (applied) => close(!!applied));

gm.events.add("client.wardrobe.result", (text, ok) => {
    mp.gui.emmit(`window.events.callEvent("cef.wardrobe.result", ${JSON.stringify(String(text || ""))}, ${!!ok})`);
});
