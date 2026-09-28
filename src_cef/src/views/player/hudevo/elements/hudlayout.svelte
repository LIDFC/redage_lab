<script>
    // Расположение блоков HUD: смещение и масштаб каждого блока (настройка HudLayout, хранится на сервере в ChatData).
    // Формат строки без кавычек (её передают в CEF в одинарных кавычках): "info:1.2,-3.5,1;speed:0,0,1.1".
    // x — в vw, y — в vh, s — масштаб. Редактор: Настройки → Настройки худа → «Расположение HUD».
    import { executeClient } from 'api/rage';
    import { storeSettings } from 'store/settings';
    import { onDestroy } from 'svelte';

    const BLOCKS = {
        info: { sel: ".hudevo__playerinfo", name: "Деньги и статистика" },
        keys: { sel: ".buttonsinfo", name: "Подсказки клавиш" },
        place: { sel: ".hudevo__placetime", name: "Место и время" },
        quest: { sel: ".hudevo__quests", name: "Задание" },
        speed: { sel: ".hudevo__speedometr", name: "Спидометр (виден в машине)" },
    };

    const parse = (text) => {
        const layout = {};
        String(text || "").split(";").forEach((part) => {
            const [key, values] = part.split(":");
            if (!BLOCKS[key] || !values) return;
            const [x, y, s] = values.split(",").map(Number);
            layout[key] = { x: Number.isFinite(x) ? x : 0, y: Number.isFinite(y) ? y : 0, s: Number.isFinite(s) && s > 0 ? s : 1 };
        });
        return layout;
    };
    const serialize = (layout) => Object.keys(layout)
        .filter((key) => BLOCKS[key])
        .map((key) => `${key}:${layout[key].x.toFixed(2)},${layout[key].y.toFixed(2)},${layout[key].s.toFixed(2)}`)
        .join(";");

    const apply = (layout) => {
        const root = document.documentElement.style;
        Object.keys(BLOCKS).forEach((key) => {
            const block = layout[key] || { x: 0, y: 0, s: 1 };
            root.setProperty(`--hl-${key}-x`, `${block.x}vw`);
            root.setProperty(`--hl-${key}-y`, `${block.y}vh`);
            root.setProperty(`--hl-${key}-s`, `${block.s}`);
        });
    };

    let saved = {};
    let layout = {};
    let editing = false;

    const unsubscribe = storeSettings.subscribe((value) => {
        saved = parse(value && value.HudLayout);
        if (!editing) {
            layout = JSON.parse(JSON.stringify(saved));
            apply(layout);
        }
    });
    onDestroy(unsubscribe);

    window.hudLayout = {
        edit: (value) => {
            editing = !!value;
            document.body.classList.toggle("hudlayout-editing", editing);
            if (!editing) {
                layout = JSON.parse(JSON.stringify(saved));
                apply(layout);
            }
        },
    };

    // Перетаскивание мышью, колёсико — масштаб
    let drag = null;
    const findBlock = (target) => {
        for (const key of Object.keys(BLOCKS)) {
            if (target && target.closest && target.closest(BLOCKS[key].sel)) return key;
        }
        return null;
    };
    const onMouseDown = (event) => {
        if (!editing) return;
        const key = findBlock(event.target);
        if (!key) return;
        event.preventDefault();
        const block = layout[key] || { x: 0, y: 0, s: 1 };
        drag = { key, startX: event.clientX, startY: event.clientY, x: block.x, y: block.y, s: block.s };
    };
    const onMouseMove = (event) => {
        if (!editing || !drag) return;
        const x = drag.x + ((event.clientX - drag.startX) / window.innerWidth) * 100;
        const y = drag.y + ((event.clientY - drag.startY) / window.innerHeight) * 100;
        layout = { ...layout, [drag.key]: { x: Math.max(-90, Math.min(90, x)), y: Math.max(-90, Math.min(90, y)), s: drag.s } };
        apply(layout);
    };
    const onMouseUp = () => (drag = null);
    const onWheel = (event) => {
        if (!editing) return;
        const key = findBlock(event.target);
        if (!key) return;
        const block = layout[key] || { x: 0, y: 0, s: 1 };
        const s = Math.max(0.6, Math.min(1.5, block.s + (event.deltaY < 0 ? 0.05 : -0.05)));
        layout = { ...layout, [key]: { ...block, s } };
        apply(layout);
    };

    const onSave = () => executeClient("client.hudlayout.save", serialize(layout));
    const onCancel = () => executeClient("client.hudlayout.cancel");
    const onReset = () => {
        layout = {};
        apply(layout);
    };
</script>

<svelte:window on:mousedown={onMouseDown} on:mousemove={onMouseMove} on:mouseup={onMouseUp} on:wheel={onWheel} />

{#if editing}
    <div class="hudlayout">
        <div class="hudlayout__title">Расположение HUD</div>
        <div class="hudlayout__text">Перетащите блоки мышью, колёсико — размер. Подсвечены: {Object.values(BLOCKS).map((b) => b.name).join(", ")}.</div>
        <div class="hudlayout__buttons">
            <div class="hudlayout__btn save" on:click={onSave}>Сохранить</div>
            <div class="hudlayout__btn" on:click={onReset}>Сбросить</div>
            <div class="hudlayout__btn" on:click={onCancel}>Отмена (ESC)</div>
        </div>
    </div>
{/if}

<style>
    :global(.hudevo__playerinfo) { transform: translate(var(--hl-info-x, 0vw), var(--hl-info-y, 0vh)) scale(var(--hl-info-s, 1)); }
    :global(.buttonsinfo) { transform: translate(var(--hl-keys-x, 0vw), var(--hl-keys-y, 0vh)) scale(var(--hl-keys-s, 1)); transform-origin: left center; }
    :global(.hudevo__placetime) { transform: translate(var(--hl-place-x, 0vw), var(--hl-place-y, 0vh)) scale(var(--hl-place-s, 1)); transform-origin: left bottom; }
    :global(.hudevo__quests) { transform: translate(var(--hl-quest-x, 0vw), var(--hl-quest-y, 0vh)) scale(var(--hl-quest-s, 1)); transform-origin: right top; }
    :global(.hudevo__speedometr) { transform: translate(var(--hl-speed-x, 0vw), var(--hl-speed-y, 0vh)) scale(var(--hl-speed-s, 1)); transform-origin: right bottom; }

    :global(body.hudlayout-editing .hudevo__playerinfo),
    :global(body.hudlayout-editing .buttonsinfo),
    :global(body.hudlayout-editing .hudevo__placetime),
    :global(body.hudlayout-editing .hudevo__quests),
    :global(body.hudlayout-editing .hudevo__speedometr) {
        outline: 0.2vh dashed #f5a524;
        outline-offset: 0.4vh;
        pointer-events: auto !important;
        cursor: move;
    }

    .hudlayout {
        position: fixed;
        top: 3vh;
        left: 50%;
        transform: translateX(-50%);
        z-index: 9999;
        width: 60vh;
        padding: 1.8vh 2vh;
        border-radius: 1.2vh;
        background: rgba(15, 18, 24, 0.94);
        border: 1px solid rgba(245, 165, 36, 0.5);
        color: #fff;
        font-family: "Gilroy", sans-serif;
        pointer-events: auto;
        text-align: center;
    }
    .hudlayout__title { font-size: 2vh; font-weight: 700; }
    .hudlayout__text { font-size: 1.35vh; opacity: 0.75; margin: 0.8vh 0 1.4vh; line-height: 1.4; }
    .hudlayout__buttons { display: flex; gap: 1vh; justify-content: center; }
    .hudlayout__btn { padding: 1vh 2vh; border-radius: 1vh; background: rgba(255, 255, 255, 0.08); font-size: 1.4vh; cursor: pointer; }
    .hudlayout__btn:hover { background: rgba(255, 255, 255, 0.16); }
    .hudlayout__btn.save { background: #f5a524; color: #16181d; font-weight: 700; }
</style>
