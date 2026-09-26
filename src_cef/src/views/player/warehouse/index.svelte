<script>
    import { fade, fly } from 'svelte/transition';
    import { executeClient } from 'api/rage'
    import { format } from 'api/formatter'
    import img0 from './images/0.jpg';
    import img1 from './images/1.jpg';
    import img2 from './images/2.jpg';
    import img3 from './images/3.jpg';
    import img4 from './images/4.jpg';
    import img5 from './images/5.jpg';
    import img7 from './images/7.jpg';
    import img8 from './images/8.jpg';
    import img9 from './images/9.jpg';

    export let viewData;

    // Данные присылает сервер (Warehouses/WarehouseManager.cs → OpenMenu)
    let data = { units: [] };
    try {
        data = typeof viewData === 'string' ? JSON.parse(viewData) : (viewData || data);
    } catch (e) {}

    const photos = { 0: img0, 1: img1, 2: img2, 3: img3, 4: img4, 5: img5, 7: img7, 8: img8, 9: img9 };
    const photo = photos[data.id] || img1;

    let selected = data.units.find((u) => u.isMine) || data.units.find((u) => u.isFree) || null;

    $: freeCount = data.units.filter((u) => u.isFree).length;

    const send = (act, payload = {}) => executeClient('warehouse', act, JSON.stringify(payload));

    const onBuy = (family) => {
        if (!selected || !selected.isFree) return;
        send('buy', { buildingId: data.id, slot: selected.slot, family });
    };
    const onEnter = () => selected && selected.isMine && send('enter', { unitId: selected.id });
    const onSell = () => selected && selected.canSell && send('sell', { unitId: selected.id });
    const onClose = () => send('close');

    const status = (u) => {
        if (u.isFree) return "Свободна";
        if (u.isMine) return u.isFamily ? "Семейная" : "Моя";
        return "Занята";
    };

    const onKeyDown = (event) => {
        if (event.key === "Escape") onClose();
    };
</script>

<svelte:window on:keydown={onKeyDown} />

<div class="wh" in:fade={{ duration: 180 }}>
    <div class="wh__panel" in:fly={{ y: 24, duration: 260 }}>
        <section class="wh__board">
            <header class="wh__head">
                <div>
                    <div class="wh__eyebrow">Аренда ячеек · {data.address}</div>
                    <h1 class="wh__title">{data.name}</h1>
                </div>
                <div class="wh__stats">
                    <div><span class="wh__label">Свободно</span><span class="wh__value">{freeCount}/{data.units.length}</span></div>
                    <div><span class="wh__label">Слотов в ячейке</span><span class="wh__value small">{data.capacity}</span></div>
                </div>
            </header>

            <div class="wh__grid">
                {#each data.units as u (u.slot)}
                    <button class="cell" class:cell--free={u.isFree} class:cell--mine={u.isMine} class:cell--taken={!u.isFree && !u.isMine}
                        class:cell--active={selected && selected.slot === u.slot} on:click={() => (selected = u)}>
                        <b>{u.slot}</b>
                        <span>{status(u)}</span>
                    </button>
                {/each}
            </div>

            <footer class="wh__hint">
                <span><i class="dot dot--free"></i>свободна</span>
                <span><i class="dot dot--mine"></i>ваша / семьи</span>
                <span><i class="dot dot--taken"></i>занята</span>
                <span><b>Esc</b> закрыть</span>
            </footer>
        </section>

        <section class="details">
            <div class="details__hero" style="background-image: url('{photo}')">
                <span class="details__shade"></span>
                <button class="details__close" on:click={onClose} aria-label="Закрыть">✕</button>
                <h2 class="details__name">{selected ? `Ячейка №${selected.slot}` : "Выберите ячейку"}</h2>
            </div>

            <div class="details__body">
                {#if selected && selected.isFree}
                    <div class="block">
                        <div class="block__title">Стоимость</div>
                        <div class="pay">${format("money", data.price)}</div>
                        <div class="block__note">продажа обратно — ${format("money", data.sellPrice)}</div>
                    </div>
                    <p class="details__text">Личную ячейку открываете только вы. Семейную покупает владелец семьи, пользуются все, у кого есть доступ к складу семьи.</p>
                {:else if selected && selected.isMine}
                    <div class="block">
                        <div class="block__title">{selected.isFamily ? "Семейная ячейка" : "Личная ячейка"}</div>
                        <div class="pay pay--light">{data.capacity} слотов</div>
                        <div class="block__note">внутри — отметка «Склад» с вашими вещами</div>
                    </div>
                {:else if selected}
                    <p class="details__text">Эта ячейка уже арендована другим игроком или семьёй.</p>
                {:else}
                    <p class="details__text">Выберите ячейку слева.</p>
                {/if}
            </div>

            <div class="details__actions">
                {#if selected && selected.isFree}
                    <button class="btn" class:btn--disabled={data.hasPersonal} disabled={data.hasPersonal} on:click={() => onBuy(false)}>
                        {data.hasPersonal ? "Личная уже есть" : "Купить себе"}
                    </button>
                    <button class="btn btn--ghost" class:btn--disabled={!data.canBuyFamily} disabled={!data.canBuyFamily} on:click={() => onBuy(true)}>
                        Для семьи
                    </button>
                {:else if selected && selected.isMine}
                    <button class="btn" on:click={onEnter}>Войти</button>
                    <button class="btn btn--ghost" class:btn--disabled={!selected.canSell} disabled={!selected.canSell} on:click={onSell}>Продать</button>
                {:else}
                    <button class="btn btn--ghost" on:click={onClose}>Закрыть</button>
                {/if}
            </div>
            <div class="details__status">
                {#if selected && selected.isFree && !data.canBuyFamily}
                    {data.familyName ? "Для семьи покупает владелец, у семьи — одна ячейка" : "Вы не состоите в семье"}
                {:else if selected && selected.isMine && !selected.canSell}
                    Продать семейную ячейку может владелец семьи
                {/if}
            </div>
        </section>
    </div>
</div>

<style>
    .wh {
        --bg: rgba(12, 13, 16, 0.94);
        --panel: rgba(255, 255, 255, 0.035);
        --line: rgba(255, 255, 255, 0.08);
        --text: #f3f1ec;
        --muted: rgba(243, 241, 236, 0.55);
        --accent: #ffb400;
        --accent-dark: #1a1405;
        --mine: #58d68d;
        --bad: #ff5d52;
        position: absolute; top: 0; right: 0; bottom: 0; left: 0;
        display: flex; align-items: center; justify-content: center;
        background: radial-gradient(ellipse at center, rgba(0, 0, 0, 0.35), rgba(0, 0, 0, 0.7));
        font-family: 'TTNorms-Medium', 'UniNeue', sans-serif;
        color: var(--text);
        user-select: none;
    }
    @media (max-width: 1400px) { .wh__panel { zoom: 80%; } }
    @media (min-width: 2400px) { .wh__panel { zoom: 125%; } }
    .wh__panel {
        position: relative; display: grid; grid-template-columns: 600px 420px;
        width: 1020px; height: 600px;
        background: var(--bg); border: 1px solid var(--line); border-radius: 14px; overflow: hidden;
        box-shadow: 0 30px 80px rgba(0, 0, 0, 0.55);
    }
    .wh__panel::before {
        content: ""; position: absolute; left: 0; top: 0; right: 0; height: 4px;
        background: repeating-linear-gradient(-45deg, var(--accent) 0 14px, #111 14px 28px); opacity: 0.9;
    }
    .wh__board { display: flex; flex-direction: column; padding: 28px 24px 18px 28px; min-height: 0; }
    .wh__head { display: flex; align-items: flex-end; justify-content: space-between; margin-bottom: 20px; }
    .wh__eyebrow { font-size: 12px; letter-spacing: 0.12em; text-transform: uppercase; color: var(--accent); margin-bottom: 6px; }
    .wh__title { margin: 0; font-family: 'RF Dewi Expanded', 'TTNorms-Bold', sans-serif; font-weight: 800; font-size: 22px; line-height: 1.1; text-transform: uppercase; }
    .wh__stats { display: flex; text-align: right; }
    .wh__stats > div { display: flex; flex-direction: column; }
    .wh__stats > div + div { margin-left: 18px; }
    .wh__label { font-size: 11px; color: var(--muted); text-transform: uppercase; letter-spacing: 0.08em; white-space: nowrap; }
    .wh__value { font-family: 'RF Dewi Expanded', sans-serif; font-weight: 700; font-size: 20px; margin-top: 2px; }
    .wh__value.small { font-family: 'TTNorms-Bold', sans-serif; font-size: 15px; margin-top: 6px; }
    .wh__grid { display: grid; grid-template-columns: repeat(5, 1fr); grid-auto-rows: 74px; gap: 8px; flex: 1; min-height: 0; overflow-y: auto; }
    .cell {
        display: flex; flex-direction: column; align-items: center; justify-content: center;
        border: 1px solid var(--line); border-radius: 10px; background: #16171b;
        color: inherit; font: inherit; cursor: pointer; padding: 0; outline: none;
        transition: transform 0.15s ease, border-color 0.15s ease;
    }
    .cell:hover { transform: translateY(-2px); border-color: rgba(255, 180, 0, 0.45); }
    .cell b { font-family: 'RF Dewi Expanded', 'TTNorms-Bold', sans-serif; font-size: 20px; }
    .cell span { font-size: 11px; color: var(--muted); margin-top: 4px; }
    .cell--free b { color: var(--accent); }
    .cell--mine { background: rgba(88, 214, 141, 0.08); }
    .cell--mine b, .cell--mine span { color: var(--mine); }
    .cell--taken { opacity: 0.45; }
    .cell--active { border-color: var(--accent); box-shadow: 0 0 0 1px var(--accent), 0 10px 24px rgba(255, 180, 0, 0.18); }
    .wh__hint { display: flex; align-items: center; margin-top: 14px; font-size: 12px; color: var(--muted); }
    .wh__hint span { display: flex; align-items: center; }
    .wh__hint span + span { margin-left: 16px; }
    .wh__hint b { font-family: 'TTNorms-Bold', sans-serif; font-weight: normal; color: var(--text); padding: 1px 6px; margin-right: 4px; border: 1px solid var(--line); border-radius: 4px; }
    .dot { width: 8px; height: 8px; border-radius: 50%; margin-right: 6px; display: inline-block; }
    .dot--free { background: var(--accent); }
    .dot--mine { background: var(--mine); }
    .dot--taken { background: rgba(255, 255, 255, 0.3); }
    .details { display: flex; flex-direction: column; background: var(--panel); border-left: 1px solid var(--line); min-height: 0; }
    .details__hero { position: relative; height: 200px; flex-shrink: 0; background-size: cover; background-position: center; }
    .details__shade { position: absolute; top: 0; right: 0; bottom: 0; left: 0; background: linear-gradient(180deg, rgba(12, 13, 16, 0) 30%, rgba(12, 13, 16, 0.96) 100%); }
    .details__close {
        position: absolute; top: 14px; right: 14px; width: 32px; height: 32px; border-radius: 8px;
        border: 1px solid var(--line); background: rgba(0, 0, 0, 0.55); color: var(--text); font-size: 14px; cursor: pointer;
    }
    .details__close:hover { border-color: var(--accent); color: var(--accent); }
    .details__name { position: absolute; left: 24px; bottom: 12px; margin: 0; font-family: 'RF Dewi Expanded', 'TTNorms-Bold', sans-serif; font-weight: 800; font-size: 22px; text-transform: uppercase; }
    .details__body { flex: 1; padding: 12px 24px 0; overflow-y: auto; min-height: 0; }
    .details__text { margin: 0 0 12px; font-size: 13px; line-height: 1.45; color: rgba(243, 241, 236, 0.75); }
    .block { padding: 12px 14px; margin-bottom: 10px; border: 1px solid var(--line); border-radius: 10px; background: rgba(0, 0, 0, 0.25); }
    .block__title { font-size: 11px; text-transform: uppercase; letter-spacing: 0.1em; color: var(--muted); margin-bottom: 8px; }
    .block__note { font-size: 11px; color: var(--muted); margin-top: 6px; }
    .pay { font-family: 'TTNorms-Bold', sans-serif; font-size: 20px; color: var(--accent); }
    .pay--light { color: var(--text); font-size: 16px; }
    .details__actions { display: grid; grid-template-columns: 1.3fr 1fr; gap: 10px; padding: 12px 24px 6px; }
    .btn {
        height: 46px; border-radius: 10px; border: none; background: var(--accent); color: var(--accent-dark);
        font-family: 'TTNorms-Bold', sans-serif; font-size: 14px; text-transform: uppercase; letter-spacing: 0.06em; cursor: pointer;
    }
    .btn:hover { filter: brightness(1.1); }
    .btn--ghost { background: transparent; color: var(--text); border: 1px solid var(--line); }
    .btn--ghost:hover { border-color: var(--accent); color: var(--accent); filter: none; }
    .btn--disabled, .btn--disabled:hover { background: rgba(255, 255, 255, 0.08); color: var(--muted); border-color: transparent; cursor: not-allowed; filter: none; }
    .details__status { min-height: 16px; padding: 2px 24px 16px; font-size: 12px; color: var(--muted); text-align: right; }
</style>
