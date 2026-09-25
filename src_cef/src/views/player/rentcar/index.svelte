<script>
    import { fade, fly } from 'svelte/transition';
    import { charMoney, charBankMoney } from 'store/chars'
    import { format } from 'api/formatter'
    import { executeClient } from 'api/rage'
    import authInfo from '@/views/business/autoshop/authInfo';

    export let viewData;

    // Список приходит с сервера (Rentcar.OpenRentMenu → client.rentcar.open):
    // FinalPrice — цена за час (для рабочего транспорта — за смену) уже с учётом VIP и уровня.
    let vehicles = [];
    try {
        vehicles = JSON.parse(viewData || '[]');
    } catch (e) {
        vehicles = [];
    }

    const authInfoLower = {};
    Object.keys(authInfo).forEach((key) => { authInfoLower[key.toLowerCase()] = authInfo[key]; });

    const getName = (model) => {
        const info = authInfo[model] || authInfoLower[String(model).toLowerCase()];
        if (info && info.name)
            return `${info.name} ${info.model || ""}`.trim();
        return String(model).charAt(0).toUpperCase() + String(model).slice(1);
    };

    const getImage = (model) => `${document.cloud}inventoryItems/vehicle/${String(model).toLowerCase()}.png`;
    const getPrice = (item) => item.FinalPrice !== undefined ? item.FinalPrice : item.Price;

    const colors = ["#111111", "#f2f2f2", "#e60000", "#ff7300", "#f0f000", "#00e600", "#00cdff", "#0000e6", "#be3ca5"];
    const HOURS = [1, 2, 3, 4, 5, 6, 7, 8];

    const isJobMenu = vehicles.length > 0 && vehicles.every((v) => v.IsJob);

    let selectedIndex = vehicles.length ? 0 : -1;
    let hours = 1;
    let colorId = 0;

    $: selected = selectedIndex >= 0 ? vehicles[selectedIndex] : null;
    $: total = selected ? getPrice(selected) * (selected.IsJob ? 1 : hours) : 0;
    $: canPay = total <= $charMoney;

    const onRent = () => {
        if (!selected)
            return;
        if (!canPay) {
            window.notificationAdd(4, 9, "Недостаточно наличных для аренды", 3000);
            return;
        }
        executeClient('client.rentcar.buy', selected.Id, colorId, selected.IsJob ? 1 : hours);
    };

    const onExit = () => executeClient('client.rentcar.exit');

    const onKeyDown = (event) => {
        if (event.key === "Escape") {
            onExit();
            return;
        }
        if (!vehicles.length)
            return;
        if (event.key === "ArrowDown" || event.key === "ArrowUp" || event.key === "ArrowLeft" || event.key === "ArrowRight") {
            const step = event.key === "ArrowDown" ? 2 : event.key === "ArrowUp" ? -2 : event.key === "ArrowRight" ? 1 : -1;
            selectedIndex = (selectedIndex + step + vehicles.length) % vehicles.length;
            event.preventDefault();
        }
        if (event.key === "Enter")
            onRent();
    };
</script>

<svelte:window on:keydown={onKeyDown} />

<div class="rent" in:fade={{ duration: 180 }}>
    <div class="rent__panel" in:fly={{ y: 24, duration: 260 }}>

        <section class="rent__board">
            <header class="rent__head">
                <div>
                    <div class="rent__eyebrow">{isJobMenu ? "Рабочий транспорт" : "Прокат транспорта"}</div>
                    <h1 class="rent__title">Аренда</h1>
                </div>
                <div class="rent__money">
                    <div>
                        <span class="rent__label">Наличные</span>
                        <span class="rent__value">${format("money", $charMoney)}</span>
                    </div>
                    <div>
                        <span class="rent__label">Банк</span>
                        <span class="rent__value small">${format("money", $charBankMoney)}</span>
                    </div>
                </div>
            </header>

            <div class="rent__grid">
                {#each vehicles as item, index (item.Id)}
                    <button class="tile" class:tile--active={index === selectedIndex} on:click={() => (selectedIndex = index)}>
                        <span class="tile__img" style="background-image: url('{getImage(item.Model)}')"></span>
                        <span class="tile__name">{getName(item.Model)}</span>
                        <span class="tile__price">
                            {#if getPrice(item) > 0}
                                ${format("money", getPrice(item))}<small>{item.IsJob ? " / смена" : " / час"}</small>
                            {:else}
                                Бесплатно
                            {/if}
                        </span>
                    </button>
                {/each}
                {#if !vehicles.length}
                    <div class="rent__empty">Здесь сейчас нечего арендовать</div>
                {/if}
            </div>

            <footer class="rent__hint">
                <span><b>↑↓←→</b> выбор</span>
                <span><b>Enter</b> арендовать</span>
                <span><b>Esc</b> закрыть</span>
            </footer>
        </section>

        {#if selected}
            {#key selected.Id}
                <section class="details" in:fade={{ duration: 160 }}>
                    <div class="details__hero">
                        <span class="details__img" style="background-image: url('{getImage(selected.Model)}')"></span>
                        <span class="details__hero-shade"></span>
                        <button class="details__close" on:click={onExit} aria-label="Закрыть">✕</button>
                        <h2 class="details__name">{getName(selected.Model)}</h2>
                    </div>

                    <div class="details__body">
                        <div class="row">
                            <div class="block">
                                <div class="block__title">{selected.IsJob ? "Цена за смену" : "Цена за час"}</div>
                                <div class="pay">{getPrice(selected) > 0 ? `$${format("money", getPrice(selected))}` : "Бесплатно"}</div>
                                <div class="block__note">с учётом VIP и уровня</div>
                            </div>
                            <div class="block">
                                <div class="block__title">Срок</div>
                                <div class="pay pay--light">{selected.IsJob ? "До конца смены" : `${hours} ч.`}</div>
                                <div class="block__note">{selected.IsJob ? "вернуть можно в любой момент" : "транспорт вернётся сам"}</div>
                            </div>
                        </div>

                        {#if !selected.IsJob}
                            <div class="block">
                                <div class="block__title">Количество часов</div>
                                <div class="hours">
                                    {#each HOURS as h}
                                        <button class="hours__item" class:hours__item--active={hours === h} on:click={() => (hours = h)}>{h}</button>
                                    {/each}
                                </div>
                            </div>
                        {/if}

                        <div class="block">
                            <div class="block__title">Цвет</div>
                            <div class="colors">
                                {#each colors as color, index}
                                    <button class="colors__item" class:colors__item--active={colorId === index} style="background: {color}" on:click={() => (colorId = index)} aria-label="Цвет {index + 1}"></button>
                                {/each}
                            </div>
                        </div>
                    </div>

                    <div class="details__total">
                        <span>К оплате</span>
                        <b class:bad={!canPay}>{total > 0 ? `$${format("money", total)}` : "Бесплатно"}</b>
                    </div>
                    <div class="details__actions">
                        <button class="btn btn--ghost" on:click={onExit}>Отмена</button>
                        <button class="btn" class:btn--disabled={!canPay} on:click={onRent}>Арендовать</button>
                    </div>
                    <div class="details__status" class:warn={!canPay}>
                        {canPay ? "Оплата наличными. Машина будет отмечена на карте" : "Не хватает наличных"}
                    </div>
                </section>
            {/key}
        {/if}
    </div>
</div>

<style>
    .rent {
        --bg: rgba(12, 13, 16, 0.94);
        --panel: rgba(255, 255, 255, 0.035);
        --line: rgba(255, 255, 255, 0.08);
        --text: #f3f1ec;
        --muted: rgba(243, 241, 236, 0.55);
        --accent: #ffb400;
        --accent-dark: #1a1405;
        --bad: #ff5d52;

        position: absolute;
        top: 0; right: 0; bottom: 0; left: 0;
        display: flex;
        align-items: center;
        justify-content: center;
        background: radial-gradient(ellipse at center, rgba(0, 0, 0, 0.35), rgba(0, 0, 0, 0.7));
        font-family: 'TTNorms-Medium', 'UniNeue', sans-serif;
        color: var(--text);
        user-select: none;
    }

    @media (max-width: 1400px) { .rent__panel { zoom: 80%; } }
    @media (min-width: 2400px) { .rent__panel { zoom: 125%; } }

    .rent__panel {
        position: relative;
        display: grid;
        grid-template-columns: 600px 420px;
        width: 1020px;
        height: 640px;
        background: var(--bg);
        border: 1px solid var(--line);
        border-radius: 14px;
        overflow: hidden;
        box-shadow: 0 30px 80px rgba(0, 0, 0, 0.55);
    }

    .rent__panel::before {
        content: "";
        position: absolute;
        left: 0; top: 0; right: 0;
        height: 4px;
        background: repeating-linear-gradient(-45deg, var(--accent) 0 14px, #111 14px 28px);
        opacity: 0.9;
    }

    .rent__board {
        display: flex;
        flex-direction: column;
        padding: 28px 24px 18px 28px;
        min-height: 0;
    }

    .rent__head {
        display: flex;
        align-items: flex-end;
        justify-content: space-between;
        margin-bottom: 20px;
    }

    .rent__eyebrow {
        font-size: 12px;
        letter-spacing: 0.12em;
        text-transform: uppercase;
        color: var(--accent);
        margin-bottom: 6px;
    }

    .rent__title {
        margin: 0;
        font-family: 'RF Dewi Expanded', 'TTNorms-Bold', sans-serif;
        font-weight: 800;
        font-size: 24px;
        line-height: 1;
        text-transform: uppercase;
    }

    .rent__money { display: flex; text-align: right; }
    .rent__money > div { display: flex; flex-direction: column; }
    .rent__money > div + div { margin-left: 18px; }
    .rent__label { font-size: 11px; color: var(--muted); text-transform: uppercase; letter-spacing: 0.08em; }
    .rent__value { font-family: 'RF Dewi Expanded', sans-serif; font-weight: 700; font-size: 20px; margin-top: 2px; }
    .rent__value.small { font-family: 'TTNorms-Bold', sans-serif; font-size: 15px; margin-top: 6px; }

    .rent__grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        grid-auto-rows: 132px;
        gap: 10px;
        flex: 1;
        min-height: 0;
        overflow-y: auto;
        padding-right: 4px;
    }

    .rent__grid::-webkit-scrollbar { width: 4px; }
    .rent__grid::-webkit-scrollbar-thumb { background: var(--line); border-radius: 2px; }

    .rent__empty { grid-column: 1 / -1; color: var(--muted); font-size: 14px; padding: 20px 0; }

    .tile {
        position: relative;
        border: 1px solid var(--line);
        border-radius: 10px;
        background: linear-gradient(160deg, #1c1d22, #121317);
        overflow: hidden;
        cursor: pointer;
        padding: 0;
        color: inherit;
        font: inherit;
        text-align: left;
        outline: none;
        transition: transform 0.15s ease, border-color 0.15s ease;
    }

    .tile:hover { transform: translateY(-2px); border-color: rgba(255, 180, 0, 0.45); }
    .tile--active { border-color: var(--accent); box-shadow: 0 0 0 1px var(--accent), 0 10px 24px rgba(255, 180, 0, 0.18); }

    .tile__img {
        position: absolute;
        left: 12px; right: 12px; top: 8px; bottom: 44px;
        background-size: contain;
        background-position: center;
        background-repeat: no-repeat;
    }

    .tile__name {
        position: absolute;
        left: 12px; bottom: 24px; right: 12px;
        font-family: 'TTNorms-Bold', sans-serif;
        font-size: 15px;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
    }

    .tile__price {
        position: absolute;
        left: 12px; bottom: 8px;
        font-size: 13px;
        color: var(--accent);
        font-family: 'TTNorms-Bold', sans-serif;
    }

    .tile__price small { color: var(--muted); font-family: 'TTNorms-Medium', sans-serif; }

    .rent__hint { display: flex; margin-top: 14px; font-size: 12px; color: var(--muted); }
    .rent__hint span + span { margin-left: 18px; }
    .rent__hint b {
        font-family: 'TTNorms-Bold', sans-serif;
        font-weight: normal;
        color: var(--text);
        padding: 1px 6px;
        margin-right: 4px;
        border: 1px solid var(--line);
        border-radius: 4px;
    }

    .details {
        display: flex;
        flex-direction: column;
        background: var(--panel);
        border-left: 1px solid var(--line);
        min-height: 0;
    }

    .details__hero {
        position: relative;
        height: 190px;
        flex-shrink: 0;
        background: radial-gradient(ellipse at 50% 60%, rgba(255, 180, 0, 0.12), rgba(0, 0, 0, 0) 70%);
    }

    .details__img {
        position: absolute;
        left: 40px; right: 40px; top: 24px; bottom: 40px;
        background-size: contain;
        background-position: center;
        background-repeat: no-repeat;
    }

    .details__hero-shade {
        position: absolute;
        top: 0; right: 0; bottom: 0; left: 0;
        background: linear-gradient(180deg, rgba(12, 13, 16, 0) 55%, rgba(12, 13, 16, 0.9) 100%);
    }

    .details__close {
        position: absolute;
        top: 14px; right: 14px;
        width: 32px; height: 32px;
        border-radius: 8px;
        border: 1px solid var(--line);
        background: rgba(0, 0, 0, 0.55);
        color: var(--text);
        font-size: 14px;
        cursor: pointer;
    }

    .details__close:hover { border-color: var(--accent); color: var(--accent); }

    .details__name {
        position: absolute;
        left: 24px; bottom: 10px; right: 24px;
        margin: 0;
        font-family: 'RF Dewi Expanded', 'TTNorms-Bold', sans-serif;
        font-weight: 800;
        font-size: 20px;
        text-transform: uppercase;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
    }

    .details__body { flex: 1; padding: 10px 24px 0; overflow-y: auto; min-height: 0; }

    .row { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }

    .block {
        padding: 12px 14px;
        margin-bottom: 10px;
        border: 1px solid var(--line);
        border-radius: 10px;
        background: rgba(0, 0, 0, 0.25);
    }

    .block__title { font-size: 11px; text-transform: uppercase; letter-spacing: 0.1em; color: var(--muted); margin-bottom: 8px; }
    .block__note { font-size: 11px; color: var(--muted); margin-top: 6px; }

    .pay { font-family: 'TTNorms-Bold', sans-serif; font-size: 18px; color: var(--accent); }
    .pay--light { color: var(--text); font-size: 15px; }

    .hours { display: grid; grid-template-columns: repeat(8, 1fr); gap: 6px; }

    .hours__item,
    .colors__item {
        height: 32px;
        border-radius: 7px;
        border: 1px solid var(--line);
        background: rgba(255, 255, 255, 0.04);
        color: var(--text);
        font-family: 'TTNorms-Bold', sans-serif;
        font-size: 13px;
        cursor: pointer;
        padding: 0;
    }

    .hours__item:hover { border-color: rgba(255, 180, 0, 0.45); }
    .hours__item--active { background: var(--accent); border-color: var(--accent); color: var(--accent-dark); }

    .colors { display: grid; grid-template-columns: repeat(9, 1fr); gap: 6px; }
    .colors__item { height: 26px; }
    .colors__item--active { box-shadow: 0 0 0 2px var(--bg), 0 0 0 4px var(--accent); }

    .details__total {
        display: flex;
        align-items: baseline;
        justify-content: space-between;
        padding: 12px 24px 0;
        font-size: 12px;
        text-transform: uppercase;
        letter-spacing: 0.1em;
        color: var(--muted);
    }

    .details__total b {
        font-family: 'RF Dewi Expanded', 'TTNorms-Bold', sans-serif;
        font-size: 24px;
        letter-spacing: 0;
        color: var(--text);
    }

    .details__total b.bad { color: var(--bad); }

    .details__actions {
        display: grid;
        grid-template-columns: 1fr 1.4fr;
        gap: 10px;
        padding: 12px 24px 6px;
    }

    .btn {
        height: 46px;
        border-radius: 10px;
        border: none;
        background: var(--accent);
        color: var(--accent-dark);
        font-family: 'TTNorms-Bold', sans-serif;
        font-size: 15px;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        cursor: pointer;
        transition: filter 0.15s ease, transform 0.1s ease;
    }

    .btn:hover { filter: brightness(1.1); }
    .btn:active { transform: scale(0.98); }

    .btn--ghost { background: transparent; color: var(--text); border: 1px solid var(--line); }
    .btn--ghost:hover { border-color: var(--accent); color: var(--accent); filter: none; }

    .btn--disabled,
    .btn--disabled:hover {
        background: rgba(255, 255, 255, 0.08);
        color: var(--muted);
        cursor: not-allowed;
        filter: none;
        transform: none;
    }

    .details__status { padding: 2px 24px 16px; font-size: 12px; color: var(--muted); text-align: right; }
    .details__status.warn { color: var(--bad); }
</style>
