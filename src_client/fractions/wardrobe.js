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

const fail = (where, e) => {
    mp.events.call("notify", 1, 9, `Гардероб: ${String(e && e.message ? e.message : e)}`, 6000);
    try { mp.events.callRemote("client_trycatch", "fractions/wardrobe", where, String(e)); } catch (err) {}
};

// Число вариантов: разные сборки RAGE называют функции по-разному — пробуем все
const drawableCount = (p, component) => {
    try { return mp.game.ped.getNumberOfPedDrawableVariations(p.handle, component); } catch (e) {}
    try { return p.getNumberOfDrawableVariations(component); } catch (e) {}
    return 0;
};
const textureCount = (p, component, drawable) => {
    try { return mp.game.ped.getNumberOfPedTextureVariations(p.handle, component, drawable); } catch (e) {}
    try { return p.getNumberOfTextureVariations(component, drawable); } catch (e) {}
    return 1;
};

// Компактный формат вещи с сервера: [id, drawable, torso, [текстуры], название|null, tname]
const unpackItem = (a) => ({ id: a[0], drawable: a[1], torso: a[2], textures: a[3] || [0], name: a[4], tname: a[5] || "" });

const openWardrobe = (json) => {
    try {
        if (isOpen) return;
        if (global.menuCheck()) {
            mp.events.call("notify", 4, 9, "Закройте другие окна и попробуйте снова", 3000);
            return;
        }
        const data = JSON.parse(json);
        data.categories.forEach((c) => {
            c.items = (c.items || []).map((x) => (Array.isArray(x) ? unpackItem(x) : x));
            c.items.forEach((item) => (item.title = itemName(item)));
        });
        // Торс (компонент 3 — руки и тело под одеждой): все варианты из игры
        const p = global.localplayer;
        const count = drawableCount(p, 3);
        const torsos = [];
        for (let d = 0; d < count; d++)
            torsos.push({ id: d, drawable: d, textures: Math.max(1, textureCount(p, 3, d)) });
        data.torsos = torsos;

        original = remember();
        previewProps = {};
        isOpen = true;
        lockHeading = p.getHeading();
        global.menuOpen();
        // Окно — сразу, камера — следом (её плавный переезд и давал задержку)
        mp.gui.emmit(`window.router.setView("FractionWardrobe", ${JSON.stringify(JSON.stringify(data))});`);
        try {
            p.clearTasksImmediately();
            p.freezePosition(true);
            global.createCamera("char", p);
        } catch (e) {
            fail("camera", e);
        }
    } catch (e) {
        fail("client.wardrobe.open", e);
        if (isOpen) close(false);
    }
};

// Пока выбираем одежду, персонаж стоит смирно: всё управление выключено, кроме мыши для камеры,
// направление зафиксировано (иначе он поворачивается за мышью)
let lockHeading = 0;
gm.events.add("render", () => {
    if (!isOpen) return;
    try {
        mp.game.controls.disableAllControlActions(0);
        [1, 2, 237, 238, 239, 240, 241, 242].forEach((c) => mp.game.controls.enableControlAction(0, c, true));
        const p = global.localplayer;
        if (Math.abs(p.getHeading() - lockHeading) > 0.5) p.setHeading(lockHeading);
    } catch (e) {}
});

// Данные приходят частями (у армии сотни вещей) — собираем и открываем
let parts = [];
gm.events.add("client.wardrobe.part", (index, total, chunk) => {
    try {
        if (index === 0) parts = [];
        parts[index] = chunk;
        if (parts.filter((x) => typeof x === "string").length === total) {
            const json = parts.join("");
            parts = [];
            openWardrobe(json);
        }
    } catch (e) {
        fail("client.wardrobe.part", e);
    }
});

gm.events.add("client.wardrobe.open", (json) => openWardrobe(json));

// Реквизит в предпросмотре (шапка, очки…): GTA снимает головной убор при смене маски/верха с капюшоном,
// поэтому после каждой смены компонента реквизит ставится заново (выбранный или тот, что был на персонаже)
let previewProps = {};
let propsTimer = null;
const reapplyProps = () => {
    if (propsTimer) clearTimeout(propsTimer);
    propsTimer = setTimeout(() => {
        propsTimer = null;
        if (!isOpen) return;
        try {
            const p = global.localplayer;
            PROPS.forEach((slot) => {
                const v = previewProps[slot];
                if (v) p.setPropIndex(slot, v[0], v[1], true);
                else restoreSlot(slot, true);
            });
        } catch (e) {}
    }, 60);
};

gm.events.add("client.wardrobe.preview", (slot, isProp, drawable, texture, torso) => {
    if (!isOpen) return;
    const p = global.localplayer;
    if (isProp) {
        previewProps[slot] = [drawable, texture];
        p.setPropIndex(slot, drawable, texture, true);
    } else {
        p.setComponentVariation(slot, drawable, texture, 0);
        if (slot === 11 && torso >= 0) p.setComponentVariation(3, torso, 0, 0);
        reapplyProps();
    }
});

gm.events.add("client.wardrobe.previewTorso", (drawable, texture) => {
    if (!isOpen) return;
    global.localplayer.setComponentVariation(3, drawable, texture, 0);
    reapplyProps();
});

gm.events.add("client.wardrobe.clear", (slot, isProp) => {
    if (!isOpen) return;
    if (isProp) delete previewProps[slot];
    restoreSlot(slot, !!isProp);
    if (!isProp) reapplyProps();
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

// Образы фракции (ранг 9+ сохраняет и удаляет, примеряют все)
gm.events.add("client.wardrobe.presetSave", (name, json) => {
    if (!isOpen || !global.antiFlood("wardrobe.preset", 1000)) return;
    mp.events.callRemote("server.wardrobe.presetSave", String(name || ""), json);
});

gm.events.add("client.wardrobe.presetDelete", (id) => {
    if (!isOpen || !global.antiFlood("wardrobe.preset", 800)) return;
    mp.events.callRemote("server.wardrobe.presetDelete", String(id));
});

gm.events.add("client.wardrobe.presets", (json) => {
    if (!isOpen) return;
    mp.gui.emmit(`window.events.callEvent("cef.wardrobe.presets", ${JSON.stringify(json)})`);
});

// Каталог всей одежды (только админ 9): страницы по 60 вещей запрашиваются по одной
gm.events.add("client.wardrobe.catalog", (key, page, search) => {
    if (!isOpen || !global.antiFlood("wardrobe.catalog", 250)) return;
    mp.events.callRemote("server.wardrobe.catalog", String(key), Number(page) || 0, String(search || ""));
});

gm.events.add("client.wardrobe.catalogPage", (json) => {
    if (!isOpen) return;
    try {
        const data = JSON.parse(json);
        data.items = (data.items || []).map((a) => {
            const item = unpackItem(a);
            item.inForm = !!a[6];
            item.extra = !!a[7];
            item.custom = Number(a[8]) || 0; // номер в паке кастомной одежды (0 — стандартная вещь GTA)
            item.title = itemName(item);
            return item;
        });
        mp.gui.emmit(`window.events.callEvent("cef.wardrobe.catalogPage", ${JSON.stringify(JSON.stringify(data))})`);
    } catch (e) {
        fail("client.wardrobe.catalogPage", e);
    }
});

gm.events.add("client.wardrobe.catalogToggle", (key, id, add) => {
    if (!isOpen || !global.antiFlood("wardrobe.catalogToggle", 600)) return;
    mp.events.callRemote("server.wardrobe.catalogToggle", String(key), Number(id), !!add);
});

// Калибровка сдвига кастомной одежды (админ 9): сервер Chars/ClothesOffsets.cs OnCalibrate
gm.events.add("client.wardrobe.calibrate", (key, gender, vanilla) => {
    if (!isOpen || !global.antiFlood("wardrobe.calibrate", 2000)) return;
    mp.events.callRemote("server.clothes.calibrate", String(key), !!gender, Number(vanilla) || 0);
});

gm.events.add("client.wardrobe.calibrated", (key, vanilla) => {
    if (!isOpen) return;
    mp.gui.emmit(`window.events.callEvent("cef.wardrobe.calibrated", ${JSON.stringify(String(key))}, ${Number(vanilla)})`);
});

gm.events.add("client.wardrobe.catalogChanged", (key, id, add) => {
    if (!isOpen) return;
    mp.gui.emmit(`window.events.callEvent("cef.wardrobe.catalogChanged", ${JSON.stringify(String(key))}, ${Number(id)}, ${!!add})`);
});

gm.events.add("client.wardrobe.exit", () => close(false));

// Сервер надел/снял форму — окно закрываем без отката
gm.events.add("client.wardrobe.close", (applied) => close(!!applied));

gm.events.add("client.wardrobe.result", (text, ok) => {
    mp.gui.emmit(`window.events.callEvent("cef.wardrobe.result", ${JSON.stringify(String(text || ""))}, ${!!ok})`);
});
