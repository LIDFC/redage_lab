<script>
    // Гардероб фракции: сотрудник собирает форму из разрешённых вещей и выбирает торс.
    // Сервер: Fractions/Wardrobe/Wardrobe.cs, клиент: src_client/fractions/wardrobe.js
    import { executeClient } from 'api/rage'
    import { onDestroy } from 'svelte'
    import { fade, fly } from 'svelte/transition'

    export let viewData;

    const parse = (d) => {
        try {
            return typeof d === "string" ? JSON.parse(d) : d || {};
        } catch (e) {
            return {};
        }
    };

    let data = parse(viewData);
    data.categories = data.categories || [];
    data.torsos = data.torsos || [];

    const ACCENT = { 6: "#d4a72c", 7: "#3b82f6", 8: "#ef4444", 9: "#64748b", 14: "#6b8e23", 15: "#a855f7", 18: "#b7791f" };
    const accent = ACCENT[data.fraction] || "#6b8e23";
    const accentRgb = [1, 3, 5].map((i) => parseInt(accent.substr(i, 2), 16)).join(",");

    const ICONS = {
        Tops: "M7 4l5 2 5-2 4 4-3 3v9H6v-9L3 8z",
        Undershort: "M8 4h8l3 4-2 2v10H7V10L5 8z",
        Legs: "M7 3h10l1 18h-4l-2-11-2 11H6z",
        Shoes: "M3 15c3 0 5-3 6-6h3l1 4 8 2v4H3z",
        Hat: "M4 15h16M6 15c0-5 2.5-8 6-8s6 3 6 8",
        Glasses: "M3 11h18M6 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6zm12 0a3 3 0 1 0 0-6 3 3 0 0 0 0 6z",
        Masks: "M4 7c5-2 11-2 16 0 0 7-3 11-8 11S4 14 4 7z",
        Accessories: "M12 3l2.5 5 5.5.8-4 3.9.9 5.5-4.9-2.6-4.9 2.6.9-5.5-4-3.9 5.5-.8z",
        BodyArmors: "M12 3l7 3v5c0 4.5-3 8.3-7 10-4-1.7-7-5.5-7-10V6z",
        Decals: "M5 5h14v14H5zM9 9h6v6H9z",
        Bugs: "M6 8h12l1 12H5zM9 8a3 3 0 0 1 6 0",
        Ears: "M5 13a7 7 0 0 1 14 0v5h-3v-5M5 13v5h3v-5",
        Watches: "M9 3h6l1 4M9 21h6l1-4M12 9v3l2 2M12 17a5 5 0 1 0 0-10 5 5 0 0 0 0 10z",
        Bracelets: "M12 18a6 6 0 1 0 0-12 6 6 0 0 0 0 12z",
        Torso: "M12 3a2.5 2.5 0 1 1 0 5 2.5 2.5 0 0 1 0-5zM7 21l1-9-3-3 4-1h6l4 1-3 3 1 9",
    };

    const CAMERA = { Legs: "legs", Shoes: "shoes", Hat: "hat", Glasses: "hat", Masks: "hat", Ears: "hat" };

    // Рабочая копия образа
    let outfit = {
        items: { ...((data.outfit && data.outfit.items) || {}) },
        torso: data.outfit && data.outfit.torso ? [...data.outfit.torso] : null,
    };

    const tabs = [...data.categories, { key: "Torso", title: "Торс", isProp: false, slot: 3, items: data.torsos.map((t) => ({ id: t.id, drawable: t.drawable, title: `Торс ${t.id}`, textures: Array.from({ length: t.textures }, (_, i) => i) })) }];
    let current = tabs.length ? tabs[0].key : "Torso";
    $: category = tabs.find((t) => t.key === current) || tabs[0];
    $: selected = category ? (current === "Torso" ? outfit.torso : outfit.items[current]) : null;
    $: selectedItem = category && selected ? category.items.find((i) => i.id === selected[0]) : null;

    let message = "";
    let ok = true;
    let search = "";
    $: shown = category ? category.items.filter((i) => !search.trim() || String(i.title).toLowerCase().includes(search.trim().toLowerCase()) || String(i.id) === search.trim()) : [];

    const selectTab = (key) => {
        current = key;
        search = "";
        executeClient("client.wardrobe.camera", CAMERA[key] || "top");
    };

    const preview = (cat, item, texture) => {
        if (cat.key === "Torso") executeClient("client.wardrobe.previewTorso", item.drawable, texture);
        else executeClient("client.wardrobe.preview", cat.slot, cat.isProp, item.drawable, texture, outfit.torso ? -1 : (item.torso === undefined ? -1 : item.torso));
    };

    const pick = (item) => {
        const texture = selected && selected[0] === item.id ? selected[1] : item.textures[0] || 0;
        if (current === "Torso") outfit.torso = [item.id, texture];
        else outfit.items[current] = [item.id, texture];
        outfit = outfit;
        preview(category, item, texture);
    };

    const pickTexture = (texture) => {
        if (!selectedItem) return;
        if (current === "Torso") outfit.torso = [selectedItem.id, texture];
        else outfit.items[current] = [selectedItem.id, texture];
        outfit = outfit;
        preview(category, selectedItem, texture);
    };

    const remove = (key) => {
        const cat = tabs.find((t) => t.key === key);
        if (key === "Torso") outfit.torso = null;
        else delete outfit.items[key];
        outfit = outfit;
        if (cat) executeClient("client.wardrobe.clear", cat.slot, cat.isProp);
    };

    const itemTitle = (key, value) => {
        const cat = tabs.find((t) => t.key === key);
        const item = cat && cat.items.find((i) => i.id === value[0]);
        return item ? item.title : `#${value[0]}`;
    };

    $: chosen = [
        ...tabs.filter((t) => t.key !== "Torso" && outfit.items[t.key]).map((t) => ({ key: t.key, title: t.title, value: outfit.items[t.key] })),
        ...(outfit.torso ? [{ key: "Torso", title: "Торс", value: outfit.torso }] : []),
    ];

    const payload = () => JSON.stringify({ items: outfit.items, torso: outfit.torso });
    const duty = () => executeClient("client.wardrobe.save", payload(), true);
    const save = () => executeClient("client.wardrobe.save", payload(), false);
    const takeoff = () => executeClient("client.wardrobe.takeoff");
    const exit = () => executeClient("client.wardrobe.exit");

    const onResult = (text, success) => {
        message = text;
        ok = !!success;
    };
    window.events.addEvent("cef.wardrobe.result", onResult);
    onDestroy(() => {
        window.events.removeEvent("cef.wardrobe.result");
        executeClient("client.camera.toggled", false);
    });

    const onKey = (e) => {
        if (e.keyCode === 27) exit();
    };

    // Вращение камеры мышью — только над персонажем
    const camera = (on) => executeClient("client.camera.toggled", on);
</script>

<svelte:window on:keyup={onKey} />

<div class="wr" style="--accent: {accent}; --accent-rgb: {accentRgb}" in:fade={{ duration: 150 }}>
    <div class="wr__left" in:fly={{ x: -40, duration: 220 }}>
        <div class="wr__head">
            <div class="wr__caption">Раздевалка</div>
            <div class="wr__title">Гардероб</div>
            <div class="wr__sub">Соберите свою форму из вещей фракции</div>
        </div>
        <div class="wr__body">
            <div class="wr__tabs">
                {#each tabs as tab}
                    <div class="wr__tab" class:active={tab.key === current} class:has={tab.key === "Torso" ? !!outfit.torso : !!outfit.items[tab.key]} on:click={() => selectTab(tab.key)}>
                        <svg viewBox="0 0 24 24"><path d={ICONS[tab.key] || ICONS.Accessories} /></svg>
                        <span>{tab.title}</span>
                    </div>
                {/each}
            </div>
            <div class="wr__items">
                <div class="wr__items_head">
                    <b>{category ? category.title : ""}</b>
                    <span>{shown.length}</span>
                </div>
                {#if current === "Torso"}
                    <div class="wr__hint">Торс — как выглядят руки и тело под одеждой. «Авто» — подбирается под верх.</div>
                    <div class="wr__auto" class:active={!outfit.torso} on:click={() => remove("Torso")}>Авто (по верху)</div>
                {/if}
                <input class="wr__search" placeholder="Поиск по названию или номеру" bind:value={search} />
                <div class="wr__grid">
                    {#each shown as item (item.id)}
                        <div class="wr__item" class:active={selected && selected[0] === item.id} on:click={() => pick(item)}>
                            <div class="wr__item_num">{current === "Torso" ? item.id : `#${item.id}`}</div>
                            <div class="wr__item_name">{item.title}</div>
                            {#if item.textures.length > 1}<div class="wr__item_colors">{item.textures.length} цв.</div>{/if}
                        </div>
                    {:else}
                        <div class="wr__empty">Ничего не найдено</div>
                    {/each}
                </div>
                {#if selectedItem && selectedItem.textures.length > 1}
                    <div class="wr__colors">
                        <span>Цвет</span>
                        {#each selectedItem.textures as texture}
                            <div class="wr__color" class:active={selected[1] === texture} on:click={() => pickTexture(texture)}>{texture + 1}</div>
                        {/each}
                    </div>
                {/if}
            </div>
        </div>
    </div>

    <div class="wr__center" on:mouseenter={() => camera(true)} on:mouseleave={() => camera(false)}>
        <div class="wr__rotate">Зажмите мышь над персонажем, чтобы повернуть камеру · колесо — приблизить</div>
    </div>

    <div class="wr__right" in:fly={{ x: 40, duration: 220 }}>
        <div class="wr__card">
            <div class="wr__card_title">Мой образ</div>
            {#if !chosen.length}
                <div class="wr__empty">Пока ничего не выбрано — выберите вещи слева</div>
            {:else}
                <div class="wr__chosen">
                    {#each chosen as entry (entry.key)}
                        <div class="wr__chosen_row">
                            <div class="wr__chosen_info" on:click={() => selectTab(entry.key)}>
                                <span>{entry.title}</span>
                                <b>{itemTitle(entry.key, entry.value)}{entry.value[1] ? ` · цвет ${entry.value[1] + 1}` : ""}</b>
                            </div>
                            <div class="wr__remove" title="Убрать" on:click={() => remove(entry.key)}>✕</div>
                        </div>
                    {/each}
                </div>
            {/if}
        </div>
        <div class="wr__msg" class:bad={!ok}>{message}</div>
        <div class="wr__buttons">
            <div class="wr__btn primary" on:click={duty}>{data.onDuty ? "Переодеться" : "Заступить на смену"}</div>
            <div class="wr__btn" on:click={save}>Сохранить образ</div>
            {#if data.onDuty}
                <div class="wr__btn danger" on:click={takeoff}>Снять форму</div>
            {/if}
            <div class="wr__btn ghost" on:click={exit}>Выйти <span>ESC</span></div>
        </div>
    </div>
</div>

<style>
    .wr {
        position: absolute;
        inset: 0;
        display: flex;
        justify-content: space-between;
        padding: 3vh 2.4vh;
        font-family: 'Gilroy', 'Montserrat', sans-serif;
        color: #fff;
        user-select: none;
        background: linear-gradient(90deg, rgba(0, 0, 0, 0.75) 0%, rgba(0, 0, 0, 0) 35%, rgba(0, 0, 0, 0) 70%, rgba(0, 0, 0, 0.7) 100%);
    }
    .wr__left {
        width: 54vh;
        display: flex;
        flex-direction: column;
        background: rgba(18, 20, 24, 0.92);
        border: 1px solid rgba(255, 255, 255, 0.08);
        border-radius: 1.6vh;
        overflow: hidden;
    }
    .wr__head {
        padding: 2.2vh 2.4vh 1.6vh;
        border-bottom: 1px solid rgba(255, 255, 255, 0.06);
        background: linear-gradient(135deg, rgba(var(--accent-rgb), 0.35), transparent 70%);
    }
    .wr__caption {
        font-size: 1.2vh;
        letter-spacing: 0.2em;
        text-transform: uppercase;
        color: var(--accent);
        filter: brightness(1.4);
    }
    .wr__title {
        font-size: 3vh;
        font-weight: 800;
    }
    .wr__sub {
        font-size: 1.35vh;
        opacity: 0.6;
    }
    .wr__body {
        flex: 1;
        display: flex;
        min-height: 0;
    }
    .wr__tabs {
        width: 13vh;
        padding: 1vh 0.8vh;
        overflow-y: auto;
        border-right: 1px solid rgba(255, 255, 255, 0.06);
    }
    .wr__tab {
        position: relative;
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 0.5vh;
        padding: 1vh 0.4vh;
        margin-bottom: 0.5vh;
        border-radius: 1vh;
        font-size: 1.2vh;
        text-align: center;
        opacity: 0.65;
        cursor: pointer;
        transition: background 0.15s, opacity 0.15s;
    }
    .wr__tab svg {
        width: 2.6vh;
        height: 2.6vh;
        fill: none;
        stroke: currentColor;
        stroke-width: 1.6;
        stroke-linecap: round;
        stroke-linejoin: round;
    }
    .wr__tab:hover {
        opacity: 1;
        background: rgba(255, 255, 255, 0.05);
    }
    .wr__tab.active {
        opacity: 1;
        background: rgba(var(--accent-rgb), 0.3);
    }
    .wr__tab.has::after {
        content: "";
        position: absolute;
        top: 0.7vh;
        right: 0.9vh;
        width: 0.7vh;
        height: 0.7vh;
        border-radius: 50%;
        background: var(--accent);
        filter: brightness(1.5);
    }
    .wr__items {
        flex: 1;
        display: flex;
        flex-direction: column;
        padding: 1.4vh 1.6vh;
        min-width: 0;
    }
    .wr__items_head {
        display: flex;
        justify-content: space-between;
        align-items: baseline;
        margin-bottom: 1vh;
        font-size: 1.8vh;
    }
    .wr__items_head span {
        font-size: 1.3vh;
        opacity: 0.5;
    }
    .wr__hint {
        font-size: 1.25vh;
        opacity: 0.6;
        margin-bottom: 0.8vh;
        line-height: 1.4;
    }
    .wr__auto {
        padding: 0.9vh;
        margin-bottom: 1vh;
        border-radius: 0.9vh;
        text-align: center;
        font-size: 1.35vh;
        background: rgba(255, 255, 255, 0.05);
        border: 1px solid transparent;
        cursor: pointer;
    }
    .wr__auto.active {
        border-color: var(--accent);
        background: rgba(var(--accent-rgb), 0.2);
    }
    .wr__search {
        width: 100%;
        padding: 0.9vh 1.2vh;
        margin-bottom: 1vh;
        border-radius: 0.9vh;
        border: 1px solid rgba(255, 255, 255, 0.1);
        background: rgba(255, 255, 255, 0.04);
        color: #fff;
        font-size: 1.35vh;
        outline: none;
        box-sizing: border-box;
    }
    .wr__grid {
        flex: 1;
        overflow-y: auto;
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 0.8vh;
        align-content: start;
        padding-right: 0.4vh;
    }
    .wr__item {
        position: relative;
        padding: 1vh 1.1vh;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.04);
        border: 1px solid rgba(255, 255, 255, 0.06);
        cursor: pointer;
        transition: border-color 0.15s, background 0.15s, transform 0.12s;
    }
    .wr__item:hover {
        background: rgba(255, 255, 255, 0.08);
        transform: translateY(-0.1vh);
    }
    .wr__item.active {
        border-color: var(--accent);
        background: rgba(var(--accent-rgb), 0.22);
    }
    .wr__item_num {
        font-size: 1.1vh;
        opacity: 0.45;
    }
    .wr__item_name {
        font-size: 1.4vh;
        font-weight: 600;
        line-height: 1.25;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
    }
    .wr__item_colors {
        font-size: 1.1vh;
        opacity: 0.5;
    }
    .wr__colors {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: 0.6vh;
        padding-top: 1.2vh;
        margin-top: 1vh;
        border-top: 1px solid rgba(255, 255, 255, 0.06);
        font-size: 1.3vh;
    }
    .wr__colors span {
        opacity: 0.6;
        margin-right: 0.4vh;
    }
    .wr__color {
        min-width: 3vh;
        height: 3vh;
        line-height: 3vh;
        text-align: center;
        border-radius: 0.8vh;
        background: rgba(255, 255, 255, 0.06);
        cursor: pointer;
    }
    .wr__color.active {
        background: var(--accent);
    }
    .wr__center {
        flex: 1;
        display: flex;
        align-items: flex-end;
        justify-content: center;
    }
    .wr__rotate {
        padding: 0.8vh 1.6vh;
        border-radius: 2vh;
        background: rgba(0, 0, 0, 0.5);
        font-size: 1.25vh;
        opacity: 0.75;
    }
    .wr__right {
        width: 38vh;
        display: flex;
        flex-direction: column;
        gap: 1.2vh;
        justify-content: flex-end;
    }
    .wr__card {
        padding: 2vh 2.2vh;
        border-radius: 1.6vh;
        background: rgba(18, 20, 24, 0.92);
        border: 1px solid rgba(255, 255, 255, 0.08);
        max-height: 55vh;
        overflow-y: auto;
    }
    .wr__card_title {
        font-size: 2vh;
        font-weight: 800;
        margin-bottom: 1.2vh;
    }
    .wr__chosen_row {
        display: flex;
        align-items: center;
        gap: 1vh;
        padding: 0.8vh 0;
        border-bottom: 1px solid rgba(255, 255, 255, 0.06);
    }
    .wr__chosen_info {
        flex: 1;
        min-width: 0;
        cursor: pointer;
    }
    .wr__chosen_info span {
        display: block;
        font-size: 1.15vh;
        opacity: 0.55;
    }
    .wr__chosen_info b {
        display: block;
        font-size: 1.45vh;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
    }
    .wr__remove {
        width: 2.8vh;
        height: 2.8vh;
        line-height: 2.8vh;
        text-align: center;
        border-radius: 0.8vh;
        background: rgba(255, 255, 255, 0.06);
        font-size: 1.2vh;
        cursor: pointer;
    }
    .wr__remove:hover {
        background: rgba(239, 68, 68, 0.4);
    }
    .wr__empty {
        font-size: 1.35vh;
        opacity: 0.5;
        padding: 1vh 0;
    }
    .wr__msg {
        min-height: 2vh;
        font-size: 1.35vh;
        color: #b5d77b;
        text-shadow: 0 0.2vh 0.6vh rgba(0, 0, 0, 0.9);
    }
    .wr__msg.bad {
        color: #ff8a8d;
    }
    .wr__buttons {
        display: grid;
        gap: 0.8vh;
    }
    .wr__btn {
        padding: 1.3vh;
        border-radius: 1vh;
        text-align: center;
        font-size: 1.5vh;
        font-weight: 700;
        background: rgba(255, 255, 255, 0.08);
        cursor: pointer;
        transition: filter 0.15s;
    }
    .wr__btn:hover {
        filter: brightness(1.2);
    }
    .wr__btn.primary {
        background: var(--accent);
    }
    .wr__btn.danger {
        background: rgba(239, 68, 68, 0.75);
    }
    .wr__btn.ghost {
        background: rgba(0, 0, 0, 0.55);
    }
    .wr__btn span {
        margin-left: 0.6vh;
        font-size: 1.1vh;
        opacity: 0.6;
    }
</style>
