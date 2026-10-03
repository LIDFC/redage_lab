<script>
    import { translateText } from 'lang'
    import './fonts/style.css'
    import './stats.css'
    import logo from './img/logo.png'
    import { format } from 'api/formatter';
    import Color from './color.svelte'
    import { executeClient } from 'api/rage'
    import { charMoney } from 'store/chars'
    import { onDestroy } from 'svelte'

    let
        color = false,
        colorListsId = 0, // 0 - default, 1 - (0 - 159), 2 - (-1 - 12 Headlight Colors)
        category = [],
        selectCategory = -1,
        lists = [],
        selectItem = -1,
        installed = null,   // index пункта, который стоит на машине (null — не показываем)
        payMode = 0,        // 1 — платит организация: цену с наличными не сверяем
        listEl;

    // Характеристики: max — с полным тюнингом, base — как сейчас стоит на машине, cur — с примеркой
    const STATS = [
        { key: 'speed', title: 'Скорость', icon: 'lsc-ic1' },
        { key: 'boost', title: 'Ускорение', icon: 'lsc-ic2' },
        { key: 'brakes', title: 'Торможение', icon: 'lsc-ic3' },
        { key: 'clutch', title: 'Сцепление', icon: 'lsc-ic4' },
    ];
    let max = { speed: 100, brakes: 100, boost: 100, clutch: 100 };
    let base = { speed: 0, brakes: 0, boost: 0, clutch: 0 };
    let cur = { speed: 0, brakes: 0, boost: 0, clutch: 0 };

    const toStats = (speed, brakes, boost, clutch) => ({ speed: Number(speed) || 0, brakes: Number(brakes) || 0, boost: Number(boost) || 0, clutch: Number(clutch) || 0 });
    const pctOf = (value, m) => m > 0 ? Math.min(100, Math.max(0, value * 100 / m)) : 0;

    const onColor = (toggled, type) => {
        color = toggled;
        colorListsId = type;
    };
    const onCategories = (_category) => {
        category = JSON.parse (_category);
        selectCategory = -1;
        color = false;
        colorListsId = 0;
        lists = [];
        selectItem = -1;
        installed = null;
    };
    const onMaxStats = (s, b, a, c) => {
        max = toStats(s, b, a, c);
    };
    const onStats = (s, b, a, c, isBase) => {
        cur = toStats(s, b, a, c);
        if (isBase)
            base = { ...cur };
    };
    const onLists = (_lists) => {
        lists = JSON.parse (_lists);
        selectItem = -1;
        installed = null;
        if (listEl) listEl.scrollTop = 0;
        // единственный пункт (покраска, фары) — сразу выбран, остаётся нажать «Купить»
        if (lists.length === 1)
            onSelectItem (lists[0].index);
    };
    const onInstalled = (index) => {
        installed = index;
    };
    const onMode = (mode) => {
        payMode = Number(mode) || 0;
    };

    window.events.addEvent("cef.custom.color", onColor);
    window.events.addEvent("cef.custom.categories", onCategories);
    window.events.addEvent("cef.custom.vehicleMaxStats", onMaxStats);
    window.events.addEvent("cef.custom.vehicleStats", onStats);
    window.events.addEvent("cef.custom.lists", onLists);
    window.events.addEvent("cef.custom.installed", onInstalled);
    window.events.addEvent("cef.custom.mode", onMode);

    onDestroy(() => {
        window.events.removeEvent("cef.custom.color");
        window.events.removeEvent("cef.custom.categories");
        window.events.removeEvent("cef.custom.vehicleMaxStats");
        window.events.removeEvent("cef.custom.vehicleStats");
        window.events.removeEvent("cef.custom.lists");
        window.events.removeEvent("cef.custom.installed");
        window.events.removeEvent("cef.custom.mode");
    });

    const onSelectCategory = (index) => {
        selectCategory = index;
        lists = [];
        color = false;
        colorListsId = 0;
        selectItem = -1;
        installed = null;
        executeClient ('client.custom.category', category[index].category);
    }

    // Клик — примерка на машине; покупка только кнопкой «Купить»
    const onSelectItem = (index) => {
        selectItem = index;
        executeClient ('client.custom.item', index);
    }

    $: isOpen = lists.length > 0 && category[selectCategory] !== undefined;
    $: isWheelTypes = category.length > 0 && typeof category[0].category === "number";
    $: selected = lists.find(item => item.index === selectItem);
    $: money = Number($charMoney) || 0;
    $: lack = selected && payMode !== 1 ? Math.max(0, Number(selected.price) - money) : 0;
    $: isInstalled = selected && installed !== null && selected.index === installed;
    $: canBuy = !!selected && !isInstalled && !lack;
    $: diffs = STATS.map(s => {
        const before = pctOf(base[s.key], max[s.key]);
        const after = pctOf(cur[s.key], max[s.key]);
        const rel = base[s.key] > 0 ? Math.round((cur[s.key] - base[s.key]) * 100 / base[s.key]) : 0;
        return { ...s, before, after, rel };
    });

    const onBuy = () => {
        if (!canBuy) return;
        executeClient ('client.custom.buy');
    }

    const onKey = (e) => {
        if (!isOpen) return;
        const tag = e.target && e.target.tagName;
        if (tag === "INPUT" || tag === "TEXTAREA") return;
        if (e.key === "ArrowDown" || e.key === "ArrowUp") {
            e.preventDefault();
            const pos = lists.findIndex(item => item.index === selectItem);
            let next = e.key === "ArrowDown" ? pos + 1 : pos - 1;
            if (pos === -1) next = 0;
            next = Math.max(0, Math.min(lists.length - 1, next));
            onSelectItem (lists[next].index);
            const row = listEl && listEl.children[next];
            if (row) row.scrollIntoView({ block: "nearest" });
        } else if (e.key === "Enter") {
            onBuy ();
        }
    }

    const plural = (n) => {
        const m10 = n % 10, m100 = n % 100;
        const word = m10 === 1 && m100 !== 11 ? 'вариант' : (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14) ? 'варианта' : 'вариантов');
        return `${n} ${word}`;
    }

    const cameraOff = () => executeClient("client.camera.toggled", false);
    const cameraOn = () => executeClient("client.camera.toggled", true);
</script>

<svelte:window on:keydown={onKey} />

<div class="lsc">
    <div class="lsc__panel" on:mouseenter={cameraOff} on:mouseleave={cameraOn}>
        <div class="lsc__head">
            <img class="lsc__logo" src={logo} alt="" />
            <div class="lsc__wallet">
                <span>{payMode === 1 ? 'Оплата' : translateText('vehicle', 'Баланс')}</span>
                {#if payMode === 1}
                    <b>счёт организации</b>
                {:else}
                    <b>${format("money", money)}</b>
                {/if}
            </div>
        </div>

        <div class="lsc__body" class:open={isOpen}>
            <ul class="lsc__cats">
                {#if isWheelTypes}
                    <li class="lsc__cat lsc__cat--back" on:click={() => executeClient('client.custom.category', 'back')}>
                        <i class="lsc__back">←</i>
                        <div class="lsc__catname">Назад</div>
                    </li>
                {/if}
                {#each category as item, index}
                    <li class="lsc__cat" class:active={selectCategory === index} on:click={() => onSelectCategory (index)} title={item.desc || ''}>
                        <i class="icon ilsc-{typeof item.category === 'number' ? 'FrontWheels' : item.category}" />
                        <div class="lsc__catname">{item.title}</div>
                    </li>
                {/each}
            </ul>

            {#if isOpen}
                <div class="lsc__items">
                    <div class="lsc__itemshead">
                        <i class="icon ilsc-{typeof category[selectCategory].category === 'number' ? 'FrontWheels' : category[selectCategory].category}" />
                        <div>
                            <div class="lsc__itemstitle">{category[selectCategory].title}</div>
                            <div class="lsc__itemssub">{plural(lists.length)} · клик — примерить</div>
                        </div>
                    </div>
                    <ul class="lsc__list" bind:this={listEl}>
                        {#each lists as item}
                            <li class="lsc__item"
                                class:active={selectItem === item.index}
                                class:installed={installed !== null && installed === item.index}
                                on:click={() => onSelectItem (item.index)}>
                                <div class="lsc__itemname">{@html item.name}</div>
                                {#if installed !== null && installed === item.index}
                                    <span class="lsc__badge">Установлено</span>
                                {:else}
                                    <span class="lsc__price" class:lack={payMode !== 1 && Number(item.price) > money}>${format("money", item.price)}</span>
                                {/if}
                            </li>
                        {/each}
                    </ul>
                    <div class="lsc__buy">
                        {#if selected}
                            <div class="lsc__sel">
                                <span>Выбрано</span>
                                <b>{@html selected.name}</b>
                            </div>
                            <div class="lsc__btn" class:disabled={!canBuy} on:click={onBuy}>
                                {#if isInstalled}
                                    Уже установлено
                                {:else if lack}
                                    Не хватает ${format("money", lack)}
                                {:else}
                                    Купить за ${format("money", selected.price)}
                                {/if}
                            </div>
                            <div class="lsc__keys"><kbd>↑</kbd><kbd>↓</kbd> выбор · <kbd>Enter</kbd> купить</div>
                        {:else}
                            <div class="lsc__hint">Выберите вариант, чтобы примерить его на машине</div>
                        {/if}
                    </div>
                </div>
            {/if}
        </div>

        <div class="lsc__foot">
            <div class="lsc__exit" on:click={() => executeClient("client.custom.exit")}>{translateText('vehicle', 'Выйти')}</div>
            <div class="lsc__esc"><kbd>ESC</kbd> {translateText('vehicle', 'Выйти/Назад')}</div>
        </div>
    </div>

    <div class="lsc__right" on:mouseenter={cameraOff} on:mouseleave={cameraOn}>
        <div class="lsc__card">
            <div class="lsc__cardhead">{translateText('vehicle', 'Характеристики')}</div>
            {#each diffs as s}
                <div class="lsc__stat">
                    <div class="lsc__statrow">
                        <i class="lsc__staticon {s.icon}" />
                        <span>{s.title}</span>
                        {#if Math.abs(s.rel) >= 1}
                            <b class="lsc__delta" class:down={s.rel < 0}>{s.rel > 0 ? '+' : ''}{s.rel}%</b>
                        {/if}
                    </div>
                    <div class="lsc__bar">
                        <div class="lsc__fill" style="width: {Math.min(s.before, s.after)}%" />
                        {#if s.after > s.before}
                            <div class="lsc__fill lsc__fill--up" style="left: {s.before}%; width: {s.after - s.before}%" />
                        {:else if s.after < s.before}
                            <div class="lsc__fill lsc__fill--down" style="left: {s.after}%; width: {s.before - s.after}%" />
                        {/if}
                    </div>
                </div>
            {/each}
            <div class="lsc__legend"><i class="up" /> прирост с примеркой <i class="down" /> потеря · шкала — от максимума с полным тюнингом</div>
        </div>
        {#if color}
            <div class="lsc__color">
                <Color title={category[selectCategory] ? category[selectCategory].title : ""} lists={colorListsId} />
            </div>
        {/if}
    </div>
</div>

<style>
    .lsc {
        --acc: #ffb020;
        --acc-rgb: 255, 176, 32;
        position: absolute;
        inset: 0;
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        padding: 4vh 3.6vh;
        box-sizing: border-box;
        pointer-events: none;
        font-family: "Gilroy", "Montserrat", sans-serif;
        color: #e9ecf1;
    }
    .lsc__panel, .lsc__right {
        pointer-events: auto;
    }
    .lsc kbd {
        display: inline-block;
        padding: 0.1vh 0.6vh;
        border-radius: 0.5vh;
        background: rgba(255, 255, 255, 0.1);
        border: 1px solid rgba(255, 255, 255, 0.14);
        font-family: inherit;
        font-size: 1.1vh;
        font-weight: 700;
        color: #fff;
    }

    /* Левая панель */
    .lsc__panel {
        height: 92vh;
        display: flex;
        flex-direction: column;
        background: rgba(20, 23, 28, 0.94);
        border: 1px solid rgba(255, 255, 255, 0.08);
        border-top: 0.35vh solid var(--acc);
        border-radius: 1.6vh;
        box-shadow: 0 2.4vh 6vh rgba(0, 0, 0, 0.5);
        overflow: hidden;
    }
    .lsc__head {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 2vh;
        padding: 1.4vh 2vh;
        background: linear-gradient(90deg, rgba(var(--acc-rgb), 0.16), transparent 70%);
        border-bottom: 1px solid rgba(255, 255, 255, 0.06);
    }
    .lsc__logo {
        height: 5.4vh;
        object-fit: contain;
    }
    .lsc__wallet {
        text-align: right;
    }
    .lsc__wallet span {
        display: block;
        font-size: 1.1vh;
        letter-spacing: 0.12em;
        text-transform: uppercase;
        color: rgba(255, 255, 255, 0.5);
    }
    .lsc__wallet b {
        font-size: 2vh;
        color: #6fdc8c;
    }

    .lsc__body {
        flex: 1;
        min-height: 0;
        display: flex;
    }

    /* Категории: значок + название */
    .lsc__cats {
        width: 30vh;
        margin: 0;
        padding: 1vh;
        list-style: none;
        overflow-y: auto;
        box-sizing: border-box;
        transition: width 0.15s;
    }
    .lsc__body.open .lsc__cats {
        width: 23vh;
        border-right: 1px solid rgba(255, 255, 255, 0.06);
    }
    .lsc__cat {
        display: flex;
        align-items: center;
        gap: 1.2vh;
        padding: 0.9vh 1.2vh;
        margin-bottom: 0.3vh;
        border-radius: 1vh;
        cursor: pointer;
        color: rgba(255, 255, 255, 0.75);
        border: 1px solid transparent;
    }
    .lsc__cat:hover {
        background: rgba(255, 255, 255, 0.05);
        color: #fff;
    }
    .lsc__cat.active {
        background: rgba(var(--acc-rgb), 0.14);
        border-color: rgba(var(--acc-rgb), 0.45);
        color: #fff;
    }
    .lsc__cat .icon, .lsc__back {
        width: 3.4vh;
        flex-shrink: 0;
        font-size: 2.6vh;
        text-align: center;
        font-style: normal;
        color: rgba(255, 255, 255, 0.6);
    }
    .lsc__cat.active .icon {
        color: var(--acc);
    }
    .lsc__catname {
        font-size: 1.45vh;
        font-weight: 600;
        line-height: 1.2;
    }
    .lsc__cat--back {
        color: var(--acc);
    }

    /* Варианты выбранной категории */
    .lsc__items {
        width: 36vh;
        display: flex;
        flex-direction: column;
        min-height: 0;
    }
    .lsc__itemshead {
        display: flex;
        align-items: center;
        gap: 1.2vh;
        padding: 1.4vh 1.6vh;
        border-bottom: 1px solid rgba(255, 255, 255, 0.06);
    }
    .lsc__itemshead .icon {
        font-size: 3vh;
        color: var(--acc);
    }
    .lsc__itemstitle {
        font-size: 1.8vh;
        font-weight: 700;
    }
    .lsc__itemssub {
        font-size: 1.15vh;
        color: rgba(255, 255, 255, 0.45);
    }
    .lsc__list {
        flex: 1;
        min-height: 0;
        margin: 0;
        padding: 0.8vh;
        list-style: none;
        overflow-y: auto;
    }
    .lsc__item {
        display: flex;
        align-items: center;
        gap: 1vh;
        padding: 1.1vh 1.2vh;
        margin-bottom: 0.4vh;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.03);
        border: 1px solid rgba(255, 255, 255, 0.05);
        cursor: pointer;
    }
    .lsc__item:hover {
        background: rgba(255, 255, 255, 0.07);
    }
    .lsc__item.active {
        background: rgba(var(--acc-rgb), 0.16);
        border-color: var(--acc);
    }
    .lsc__item.installed {
        border-left: 0.4vh solid #6fdc8c;
    }
    .lsc__itemname {
        flex: 1;
        min-width: 0;
        font-size: 1.45vh;
        font-weight: 600;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
    }
    .lsc__price {
        font-size: 1.45vh;
        font-weight: 700;
        color: #6fdc8c;
    }
    .lsc__price.lack {
        color: #ff6b6b;
    }
    .lsc__badge {
        padding: 0.3vh 0.8vh;
        border-radius: 0.6vh;
        background: rgba(111, 220, 140, 0.15);
        color: #6fdc8c;
        font-size: 1.1vh;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.06em;
    }

    .lsc__buy {
        padding: 1.4vh 1.6vh 1.6vh;
        border-top: 1px solid rgba(255, 255, 255, 0.06);
        background: rgba(0, 0, 0, 0.18);
    }
    .lsc__sel span {
        display: block;
        font-size: 1.1vh;
        text-transform: uppercase;
        letter-spacing: 0.1em;
        color: rgba(255, 255, 255, 0.45);
    }
    .lsc__sel b {
        display: block;
        font-size: 1.6vh;
        margin: 0.3vh 0 1.1vh;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
    }
    .lsc__btn {
        padding: 1.3vh;
        border-radius: 1vh;
        text-align: center;
        font-size: 1.6vh;
        font-weight: 800;
        color: #1a1405;
        background: var(--acc);
        cursor: pointer;
    }
    .lsc__btn:hover {
        filter: brightness(1.08);
    }
    .lsc__btn.disabled {
        background: rgba(255, 255, 255, 0.08);
        color: rgba(255, 255, 255, 0.5);
        cursor: default;
        filter: none;
    }
    .lsc__keys, .lsc__hint {
        margin-top: 0.9vh;
        font-size: 1.15vh;
        color: rgba(255, 255, 255, 0.45);
        text-align: center;
    }
    .lsc__hint {
        margin: 0.6vh 0;
    }

    .lsc__foot {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 1.2vh 1.6vh;
        border-top: 1px solid rgba(255, 255, 255, 0.06);
    }
    .lsc__exit {
        padding: 0.9vh 2.4vh;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.07);
        border: 1px solid rgba(255, 255, 255, 0.1);
        font-size: 1.4vh;
        font-weight: 700;
        cursor: pointer;
    }
    .lsc__exit:hover {
        background: rgba(255, 107, 107, 0.18);
        border-color: rgba(255, 107, 107, 0.5);
    }
    .lsc__esc {
        font-size: 1.2vh;
        color: rgba(255, 255, 255, 0.5);
    }

    /* Правая колонка: характеристики и палитра */
    .lsc__right {
        width: 40vh;
        display: flex;
        flex-direction: column;
        gap: 1.6vh;
    }
    .lsc__card {
        padding: 1.8vh 2vh;
        background: rgba(20, 23, 28, 0.94);
        border: 1px solid rgba(255, 255, 255, 0.08);
        border-radius: 1.6vh;
        box-shadow: 0 2.4vh 6vh rgba(0, 0, 0, 0.45);
    }
    .lsc__cardhead {
        font-size: 1.8vh;
        font-weight: 700;
        margin-bottom: 1.4vh;
    }
    .lsc__stat {
        margin-bottom: 1.4vh;
    }
    .lsc__statrow {
        display: flex;
        align-items: center;
        gap: 0.9vh;
        font-size: 1.4vh;
        font-weight: 600;
        color: rgba(255, 255, 255, 0.8);
        margin-bottom: 0.6vh;
    }
    .lsc__staticon {
        width: 1.8vh;
        height: 1.8vh;
        background-size: contain;
        background-repeat: no-repeat;
        background-position: center;
    }
    .lsc__delta {
        margin-left: auto;
        font-size: 1.4vh;
        color: #6fdc8c;
    }
    .lsc__delta.down {
        color: #ff6b6b;
    }
    /* Шкала из 10 делений: заливка + «окна» между делениями */
    .lsc__bar {
        position: relative;
        height: 0.8vh;
        border-radius: 0.4vh;
        background: rgba(255, 255, 255, 0.08);
        overflow: hidden;
    }
    .lsc__bar::after {
        content: "";
        position: absolute;
        inset: 0;
        background: repeating-linear-gradient(90deg, transparent 0, transparent calc(10% - 0.4vh), #14171c calc(10% - 0.4vh), #14171c 10%);
    }
    .lsc__fill {
        position: absolute;
        top: 0;
        bottom: 0;
        left: 0;
        background: #e9ecf1;
        transition: width 0.2s, left 0.2s;
    }
    .lsc__fill--up {
        background: #6fdc8c;
    }
    .lsc__fill--down {
        background: #ff6b6b;
    }
    .lsc__legend {
        font-size: 1.1vh;
        color: rgba(255, 255, 255, 0.4);
        line-height: 1.5;
    }
    .lsc__legend i {
        display: inline-block;
        width: 1vh;
        height: 1vh;
        border-radius: 0.2vh;
        vertical-align: middle;
    }
    .lsc__legend i.up { background: #6fdc8c; }
    .lsc__legend i.down { background: #ff6b6b; margin-left: 0.6vh; }

    /* Палитра (color.svelte) — под общий стиль */
    .lsc__color :global(.color-picker-panel) {
        width: 100%;
        margin-top: 0;
        background: rgba(20, 23, 28, 0.94);
        border: 1px solid rgba(255, 255, 255, 0.08);
        border-radius: 1.6vh;
        overflow: hidden;
    }
    .lsc__color :global(.color-picker-panel .panel-header) {
        background: rgba(var(--acc-rgb), 0.12);
        border-radius: 0;
    }
</style>
