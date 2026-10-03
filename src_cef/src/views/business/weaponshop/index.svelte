<script>
    // Оружейный магазин (бизнес). Данные: client.gunshop.open → window.weaponshop(weapons, ammo),
    // модификации: client.weaponshop.components → window.weaponshopcomponents(components, types).
    // Картинки — иконки предметов с CDN проекта (как в инвентаре), по itemId из json/itemsInfo.
    import { executeClient } from 'api/rage'
    import { translateText } from 'lang'
    import { format } from 'api/formatter'
    import { itemsInfo, ItemId } from 'json/itemsInfo'
    import weaponsinfo from './assets/js/weaponsinfo'

    // Индекс 4 — «Ближний бой» (сервер Core/Businesses.cs gunsCat, MeleeCategory): без лицензии, патронов и модификаций
    const categoryNames = ['Пистолеты', 'Дробовики', 'Пистолеты-пулемёты', 'Штурмовые винтовки', 'Ближний бой'];
    const MELEE = 4;
    const maxAmmo = [100, 50, 300, 250, 48];

    // Тип модификации → предмет-обвес (для картинки и названия раздела)
    const componentItems = {
        1: { item: ItemId.cVarmod, name: 'Раскраски' },
        2: { item: ItemId.cClip, name: 'Магазины' },
        3: { item: ItemId.cSuppressor, name: 'Глушители' },
        4: { item: ItemId.cScope, name: 'Прицелы' },
        5: { item: ItemId.cMuzzlebrake, name: 'Дульные тормоза' },
        6: { item: ItemId.cBarrel, name: 'Стволы' },
        7: { item: ItemId.cFlashlight, name: 'Фонари' },
        8: { item: ItemId.cGrip, name: 'Рукояти' },
        9: { item: ItemId.cVarmod, name: 'Отделка' },
    };

    let weapons = [];
    let ammo = [];
    let components = [];
    let ctypes = [];

    let category = 0;
    let weaponIndex = 0;
    let componentType = 0;
    let componentIndex = 0;
    let ammoCount = "";

    window.weaponshop = (weaponJson, ammoJson) => {
        weapons = JSON.parse(weaponJson) || [];
        ammo = JSON.parse(ammoJson) || [];
        category = weapons.findIndex(c => c && c.length) >= 0 ? weapons.findIndex(c => c && c.length) : 0;
        weaponIndex = 0;
    }

    window.weaponshopcomponents = (componentsJson, ctypesJson) => {
        components = JSON.parse(componentsJson) || [];
        ctypes = JSON.parse(ctypesJson) || [];
        if (ctypes.length) selectComponentType(ctypes[0]);
    }

    // Иконка предмета с CDN: сначала по классу иконки, потом по названию
    const itemIdByIcon = {};
    const itemIdByName = {};
    for (const id in itemsInfo) {
        const info = itemsInfo[id];
        if (!info) continue;
        if (info.Icon) itemIdByIcon[info.Icon] = id;
        if (info.Name) itemIdByName[info.Name.toLowerCase()] = id;
    }
    const weaponImage = (w) => {
        if (!w) return "";
        const id = itemIdByIcon[w.Icon] || itemIdByName[String(w.Name).toLowerCase()];
        return id !== undefined ? `${document.cloud}inventoryItems/items/${id}.png` : "";
    }
    const componentImage = (type) => {
        const c = componentItems[type];
        return c ? `${document.cloud}inventoryItems/items/${c.item}.png` : "";
    }

    $: list = weapons[category] || [];
    $: weapon = list[weaponIndex] || null;
    $: info = weapon ? weaponsinfo[weapon.Name] : null;
    $: ammoPrice = Number(ammo[category]) || 0;
    $: ammoN = Math.min(Math.max(0, Math.floor(Number(ammoCount) || 0)), maxAmmo[category] || 100);
    $: modsMode = ctypes.length > 0;
    $: melee = category === MELEE;
    $: shownComponents = components.map((c, index) => ({ ...c, index })).filter(c => c.type == componentType);
    $: component = components[componentIndex] || null;

    const selectCategory = (i) => {
        category = i;
        weaponIndex = 0;
        ammoCount = "";
    }
    const selectWeapon = (i) => {
        weaponIndex = i;
        ammoCount = "";
    }
    const selectComponentType = (type) => {
        componentType = type;
        const first = components.findIndex(c => c.type == type);
        componentIndex = first >= 0 ? first : 0;
    }

    const buyWeapon = () => weapon && executeClient('client.weaponshop.buy', category, weaponIndex);
    const buyAmmo = () => ammoN > 0 && executeClient('client.weaponshop.buyAmmo', category, ammoN);
    const openMods = () => weapon && executeClient('client.weaponshop.components', weapon.Name.replace(/\s/g, ''));
    const buyComponent = () => component && executeClient('client.weaponshop.buyComponent', category, weaponIndex, component.hash);
    const backFromMods = () => {
        components = [];
        ctypes = [];
    }
    const exit = () => executeClient('client.weaponshop.close');

    const onKey = (e) => {
        if (e.keyCode !== 27) return;
        if (modsMode) backFromMods();
        else exit();
    }

    const stats = [
        { key: 'damage', name: 'Урон' },
        { key: 'ratefire', name: 'Скорострельность' },
        { key: 'accuracy', name: 'Точность' },
        { key: 'range', name: 'Дальность' },
    ];
</script>

<svelte:window on:keyup={onKey} />

<div class="ws">
    <div class="ws__panel">
        <div class="ws__head">
            <div>
                <div class="ws__caption">Ammu-Nation</div>
                <div class="ws__title">{modsMode ? `Модификации · ${weapon ? weapon.Name : ""}` : translateText('business', 'Магазин Оружия')}</div>
            </div>
            <div class="ws__head-right">
                {#if modsMode}
                    <div class="ws__btn" on:click={backFromMods}>← Назад к оружию</div>
                {/if}
                <div class="ws__btn" on:click={exit}>Выйти <span>ESC</span></div>
            </div>
        </div>

        <div class="ws__tabs">
            {#if modsMode}
                {#each ctypes as type}
                    <div class="ws__tab" class:active={componentType === type} on:click={() => selectComponentType(type)}>
                        {componentItems[type] ? componentItems[type].name : `Тип ${type}`}
                    </div>
                {/each}
            {:else}
                {#each weapons as cat, i}
                    {#if cat && cat.length}
                        <div class="ws__tab" class:active={category === i} on:click={() => selectCategory(i)}>{categoryNames[i] || `Категория ${i + 1}`}</div>
                    {/if}
                {/each}
            {/if}
        </div>

        <div class="ws__body">
            <div class="ws__grid">
                {#if modsMode}
                    {#each shownComponents as c (c.index)}
                        <div class="ws__card" class:active={componentIndex === c.index} on:click={() => componentIndex = c.index}>
                            <div class="ws__img" style="background-image: url({componentImage(c.type)})"></div>
                            <div class="ws__name">{@html c.Name}</div>
                            <div class="ws__price">${format("money", c.Mats)}</div>
                        </div>
                    {:else}
                        <div class="ws__empty">Для этого оружия нет модификаций</div>
                    {/each}
                {:else}
                    {#each list as w, i}
                        <div class="ws__card" class:active={weaponIndex === i} on:click={() => selectWeapon(i)}>
                            <div class="ws__img" style="background-image: url({weaponImage(w)})"></div>
                            <div class="ws__name">{w.Name}</div>
                            <div class="ws__price">${format("money", w.Mats)}</div>
                        </div>
                    {:else}
                        <div class="ws__empty">Нет товара</div>
                    {/each}
                {/if}
            </div>

            <div class="ws__side">
                {#if modsMode}
                    {#if component}
                        <div class="ws__big" style="background-image: url({componentImage(component.type)})"></div>
                        <div class="ws__side-title">{@html component.Name}</div>
                        <div class="ws__desc">{@html component.Desc || ""}</div>
                        <div class="ws__buy" on:click={buyComponent}>Купить за ${format("money", component.Mats)}</div>
                    {/if}
                {:else if weapon}
                    <div class="ws__big" style="background-image: url({weaponImage(weapon)})"></div>
                    <div class="ws__side-title">{weapon.Name}</div>
                    <div class="ws__desc">{info ? info.desc : (melee ? "Оружие ближнего боя. Лицензия на оружие не нужна." : "")}</div>
                    {#if info}
                        <div class="ws__stats">
                            {#each stats as s}
                                <div class="ws__stat">
                                    <span>{s.name}</span>
                                    <div class="ws__bar"><div style="width: {Math.min(5, info[s.key] || 0) * 20}%"></div></div>
                                </div>
                            {/each}
                        </div>
                    {/if}
                    <div class="ws__buy" on:click={buyWeapon}>Купить за ${format("money", weapon.Mats)}</div>
                    {#if !melee}
                    <div class="ws__btn wide" on:click={openMods}>Модификации</div>

                    <div class="ws__ammo">
                        <div class="ws__ammo-head">
                            <span>Патроны</span>
                            <span class="ws__muted">1 шт. = ${ammoPrice}</span>
                        </div>
                        <div class="ws__row">
                            <input class="ws__input" type="number" min="1" max={maxAmmo[category] || 100} bind:value={ammoCount} placeholder="Количество (до {maxAmmo[category] || 100})" />
                            <div class="ws__btn" class:disabled={ammoN <= 0} on:click={buyAmmo}>Купить{ammoN > 0 ? ` · $${format("money", ammoN * ammoPrice)}` : ""}</div>
                        </div>
                    </div>
                    {/if}
                {/if}
            </div>
        </div>
    </div>
</div>

<style>
    .ws {
        position: absolute;
        inset: 0;
        z-index: 1000;
        display: flex;
        align-items: center;
        justify-content: center;
        background: radial-gradient(circle at 25% 15%, rgba(40, 32, 18, 0.9), rgba(6, 7, 10, 0.95) 60%);
        color: #ececec;
        font-family: 'TTNorms-Regular';
        font-size: 1.5vh;
    }
    .ws__panel {
        width: 150vh;
        max-width: 96vw;
        height: 82vh;
        display: flex;
        flex-direction: column;
        padding: 2.6vh 3vh;
        border-radius: 1.4vh;
        background: rgba(12, 13, 17, 0.95);
        border: 1px solid rgba(255, 255, 255, 0.06);
        box-shadow: 0 2vh 6vh rgba(0, 0, 0, 0.55);
    }
    .ws__head {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-bottom: 2vh;
    }
    .ws__caption {
        color: #f0a93b;
        font-size: 1.2vh;
        letter-spacing: 0.25vh;
        text-transform: uppercase;
    }
    .ws__title { font-family: 'TTNorms-Bold'; font-size: 2.8vh; }
    .ws__head-right { display: flex; gap: 1vh; }
    .ws__btn {
        cursor: pointer;
        padding: 1.1vh 1.8vh;
        border-radius: 0.9vh;
        background: rgba(255, 255, 255, 0.07);
        text-align: center;
        white-space: nowrap;
        transition: background 0.15s;
    }
    .ws__btn:hover { background: rgba(255, 255, 255, 0.13); }
    .ws__btn span {
        margin-left: 0.6vh;
        padding: 0.1vh 0.5vh;
        border-radius: 0.4vh;
        background: rgba(255, 255, 255, 0.1);
        font-size: 1.1vh;
    }
    .ws__btn.wide { margin-top: 1vh; }
    .ws__btn.disabled { opacity: 0.45; pointer-events: none; }
    .ws__tabs {
        display: flex;
        flex-wrap: wrap;
        gap: 0.8vh;
        margin-bottom: 1.8vh;
    }
    .ws__tab {
        cursor: pointer;
        padding: 0.9vh 1.6vh;
        border-radius: 2vh;
        background: rgba(255, 255, 255, 0.05);
        color: rgba(255, 255, 255, 0.65);
        transition: all 0.15s;
    }
    .ws__tab.active {
        background: rgba(240, 169, 59, 0.18);
        color: white;
        box-shadow: inset 0 0 0 1px rgba(240, 169, 59, 0.7);
    }
    .ws__body {
        flex: 1;
        min-height: 0;
        display: flex;
        gap: 2.4vh;
    }
    .ws__grid {
        flex: 1;
        min-width: 0;
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        grid-auto-rows: min-content;
        gap: 1.2vh;
        overflow-y: auto;
        padding-right: 0.6vh;
    }
    .ws__grid::-webkit-scrollbar { width: 0.4vh; }
    .ws__grid::-webkit-scrollbar-thumb { background: rgba(255, 255, 255, 0.12); border-radius: 0.4vh; }
    .ws__card {
        cursor: pointer;
        display: flex;
        flex-direction: column;
        align-items: center;
        padding: 1.6vh 1.2vh;
        border-radius: 1.1vh;
        background: rgba(255, 255, 255, 0.035);
        border: 1px solid rgba(255, 255, 255, 0.06);
        transition: all 0.15s;
    }
    .ws__card:hover { background: rgba(255, 255, 255, 0.06); }
    .ws__card.active {
        border-color: rgba(240, 169, 59, 0.8);
        background: rgba(240, 169, 59, 0.08);
    }
    .ws__img {
        width: 100%;
        height: 10vh;
        background-size: contain;
        background-position: center;
        background-repeat: no-repeat;
        margin-bottom: 1vh;
    }
    .ws__name { font-family: 'TTNorms-Bold'; text-align: center; }
    .ws__price { margin-top: 0.4vh; color: #f0a93b; font-family: 'TTNorms-Bold'; }
    .ws__empty { grid-column: 1 / -1; margin-top: 4vh; text-align: center; color: rgba(255, 255, 255, 0.4); }
    .ws__side {
        width: 46vh;
        flex-shrink: 0;
        display: flex;
        flex-direction: column;
        padding: 2vh;
        border-radius: 1.2vh;
        background: rgba(255, 255, 255, 0.03);
        border: 1px solid rgba(255, 255, 255, 0.06);
        overflow-y: auto;
    }
    .ws__big {
        height: 16vh;
        background-size: contain;
        background-position: center;
        background-repeat: no-repeat;
        background-color: rgba(0, 0, 0, 0.25);
        border-radius: 1vh;
        margin-bottom: 1.4vh;
    }
    .ws__side-title { font-family: 'TTNorms-Bold'; font-size: 2.2vh; }
    .ws__desc { margin: 0.8vh 0 1.4vh; color: rgba(255, 255, 255, 0.6); line-height: 1.4; }
    .ws__stats { display: flex; flex-direction: column; gap: 0.8vh; margin-bottom: 1.6vh; }
    .ws__stat { display: grid; grid-template-columns: 16vh 1fr; align-items: center; color: rgba(255, 255, 255, 0.7); }
    .ws__bar { height: 0.8vh; border-radius: 0.4vh; background: rgba(255, 255, 255, 0.08); overflow: hidden; }
    .ws__bar div { height: 100%; border-radius: 0.4vh; background: linear-gradient(90deg, #f0a93b, #ffcf7a); }
    .ws__buy {
        cursor: pointer;
        padding: 1.3vh;
        border-radius: 0.9vh;
        background: #f0a93b;
        color: #1a1204;
        text-align: center;
        font-family: 'TTNorms-Bold';
        transition: background 0.15s;
    }
    .ws__buy:hover { background: #ffbb52; }
    .ws__ammo {
        margin-top: 2vh;
        padding-top: 1.6vh;
        border-top: 1px solid rgba(255, 255, 255, 0.07);
    }
    .ws__ammo-head { display: flex; justify-content: space-between; margin-bottom: 1vh; }
    .ws__muted { color: rgba(255, 255, 255, 0.5); }
    .ws__row { display: flex; gap: 0.8vh; }
    .ws__input {
        flex: 1;
        min-width: 0;
        padding: 1.1vh 1.2vh;
        border-radius: 0.9vh;
        border: 1px solid rgba(255, 255, 255, 0.08);
        background: rgba(255, 255, 255, 0.04);
        color: white;
        font-family: inherit;
        font-size: 1.4vh;
        outline: none;
    }
    .ws__input:focus { border-color: rgba(240, 169, 59, 0.6); }
</style>
