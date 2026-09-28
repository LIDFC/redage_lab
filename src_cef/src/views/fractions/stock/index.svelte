<script>
    import { executeClient } from 'api/rage'
    import { sound } from 'api/uiSound'
    import { fade, scale } from 'svelte/transition'

    // Склад организации / фракции: деньги, аптечки, наркотики, материалы и склад предметов.
    // Данные — массив из 5 строк (Organizations.Manager.OpenOrgStock / Fractions.Manager.OpenFracStockMenu),
    // события прежние: stockTake(index), stockPut(index), stockExit (src_client/fractions/stock.js).
    export let viewData;

    let count = [];
    $: {
        try {
            count = typeof viewData === "string" ? JSON.parse(viewData) : viewData || [];
        } catch (e) {
            count = [];
        }
    }

    const items = [
        { index: 0, title: "Деньги", unit: "$", color: "#7ED321",
            icon: '<rect x="3" y="6" width="18" height="12" rx="2"/><circle cx="12" cy="12" r="2.6"/><path d="M6 9.5v5M18 9.5v5"/>' },
        { index: 1, title: "Аптечки", unit: "шт", color: "#FF6B6B",
            icon: '<rect x="4" y="6" width="16" height="13" rx="2"/><path d="M9 6V4.5h6V6M12 9.5v6M9 12.5h6"/>' },
        { index: 2, title: "Наркотики", unit: "г", color: "#A78BFA",
            icon: '<path d="M12 21c0-5 1-9 0-17M12 12c-3-1-6-4-6-8 3 1 5 3 6 6M12 12c3-1 6-4 6-8-3 1-5 3-6 6M12 16c-2 0-5-1-7-4 3-1 5 0 7 2M12 16c2 0 5-1 7-4-3-1-5 0-7 2"/>' },
        { index: 3, title: "Материалы", unit: "шт", color: "#F5A524",
            icon: '<path d="M4 8l8-4 8 4-8 4z"/><path d="M4 12l8 4 8-4M4 16l8 4 8-4"/>' },
    ];

    const format = (value) => {
        const number = Number(value);
        if (!isFinite(number)) return value ?? "—";
        return Math.round(number).toString().replace(/\B(?=(\d{3})+(?!\d))/g, " ");
    };

    const take = (index) => executeClient('stockTake', index);
    const put = (index) => executeClient('stockPut', index);
    const exit = () => executeClient('stockExit');

    const onKey = (e) => {
        if (e.keyCode === 27) exit();
    };
</script>

<svelte:window on:keyup={onKey} />

<div class="stk" in:fade={{ duration: 150 }}>
    <div class="stk__box" in:scale={{ start: 0.97, duration: 200 }}>
        <div class="stk__head">
            <div class="stk__logo">
                <svg viewBox="0 0 24 24"><path d="M3 9l9-5 9 5v11H3z"/><path d="M7 20v-7h10v7M7 16h10"/></svg>
            </div>
            <div class="stk__title">
                <span>Хранилище</span>
                <h1>Склад</h1>
            </div>
            <div class="stk__close" use:sound={"tap"} on:click={exit} title="Закрыть (Esc)">✕</div>
        </div>

        <div class="stk__grid">
            {#each items as item}
                <div class="stk__card" style="--c:{item.color}">
                    <div class="stk__card_top">
                        <div class="stk__icon"><svg viewBox="0 0 24 24">{@html item.icon}</svg></div>
                        <span>{item.title}</span>
                    </div>
                    <div class="stk__value">
                        {#if item.unit === "$"}${format(count[item.index])}{:else}{format(count[item.index])}<small>{item.unit}</small>{/if}
                    </div>
                    <div class="stk__actions">
                        <div class="stk__btn" use:sound={"tap"} on:click={() => take(item.index)}>Взять</div>
                        <div class="stk__btn put" use:sound={"tap"} on:click={() => put(item.index)}>Положить</div>
                    </div>
                </div>
            {/each}
        </div>

        <div class="stk__items" use:sound={"tap"} on:click={() => take(4)}>
            <div class="stk__icon big"><svg viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="16" rx="2"/><path d="M3 10h18M3 15h18M9 7h6M9 12.5h6M9 17.5h6"/></svg></div>
            <div class="stk__items_info">
                <b>Склад предметов</b>
                <span>Оружие, снаряжение и вещи · предметов: {format(count[4])}</span>
            </div>
            <div class="stk__items_open">Открыть ›</div>
        </div>

        <div class="stk__foot">Все операции записываются в логи организации</div>
    </div>
</div>

<style>
    .stk {
        position: absolute;
        top: 0;
        left: 0;
        right: 0;
        bottom: 0;
        display: flex;
        align-items: center;
        justify-content: center;
        background: radial-gradient(circle at 50% 40%, rgba(16, 18, 24, 0.7), rgba(5, 6, 9, 0.9));
        font-family: "Gilroy", "Montserrat", sans-serif;
        color: #e9edf3;
    }
    .stk__box {
        width: 84vh;
        max-width: 94vw;
        padding: 2.4vh;
        background: #12151b;
        border: 1px solid rgba(255, 255, 255, 0.07);
        border-radius: 1.6vh;
        box-shadow: 0 3vh 8vh rgba(0, 0, 0, 0.55);
        display: flex;
        flex-direction: column;
        gap: 1.6vh;
    }
    .stk__head {
        display: flex;
        align-items: center;
        gap: 1.4vh;
    }
    .stk__logo {
        width: 5vh;
        height: 5vh;
        border-radius: 1.2vh;
        background: rgba(245, 165, 36, 0.14);
        color: #f5a524;
        display: flex;
        align-items: center;
        justify-content: center;
    }
    svg {
        width: 58%;
        height: 58%;
        fill: none;
        stroke: currentColor;
        stroke-width: 1.7;
        stroke-linecap: round;
        stroke-linejoin: round;
    }
    .stk__title {
        flex: 1;
    }
    .stk__title span {
        font-size: 1.2vh;
        letter-spacing: 0.12vh;
        text-transform: uppercase;
        color: #8b93a1;
    }
    .stk__title h1 {
        margin: 0.2vh 0 0;
        font-size: 2.6vh;
        font-weight: 700;
    }
    .stk__close {
        width: 4.2vh;
        height: 4.2vh;
        border-radius: 1vh;
        display: flex;
        align-items: center;
        justify-content: center;
        color: #8b93a1;
        background: rgba(255, 255, 255, 0.04);
        cursor: pointer;
        font-size: 1.8vh;
    }
    .stk__close:hover {
        color: #fff;
        background: rgba(255, 255, 255, 0.1);
    }
    .stk__grid {
        display: grid;
        grid-template-columns: repeat(2, 1fr);
        gap: 1.2vh;
    }
    .stk__card {
        padding: 1.6vh;
        border-radius: 1.3vh;
        background: rgba(255, 255, 255, 0.03);
        border: 1px solid rgba(255, 255, 255, 0.05);
        display: flex;
        flex-direction: column;
        gap: 1.2vh;
    }
    .stk__card_top {
        display: flex;
        align-items: center;
        gap: 1vh;
        font-size: 1.5vh;
        color: #aab1bd;
    }
    .stk__icon {
        width: 4vh;
        height: 4vh;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.05);
        color: var(--c, #f5a524);
        display: flex;
        align-items: center;
        justify-content: center;
        flex: none;
    }
    .stk__icon.big {
        width: 5vh;
        height: 5vh;
        color: #7fb2e5;
    }
    .stk__value {
        font-size: 3vh;
        font-weight: 700;
        color: var(--c, #fff);
    }
    .stk__value small {
        margin-left: 0.6vh;
        font-size: 1.4vh;
        font-weight: 500;
        color: #8b93a1;
    }
    .stk__actions {
        display: flex;
        gap: 0.8vh;
    }
    .stk__btn {
        flex: 1;
        padding: 1vh;
        border-radius: 0.9vh;
        text-align: center;
        font-size: 1.4vh;
        font-weight: 700;
        cursor: pointer;
        background: rgba(255, 255, 255, 0.07);
        transition: filter 0.15s, background 0.15s;
    }
    .stk__btn:hover {
        background: rgba(255, 255, 255, 0.12);
    }
    .stk__btn.put {
        background: #f5a524;
        color: #16181d;
    }
    .stk__btn.put:hover {
        filter: brightness(1.1);
    }
    .stk__items {
        display: flex;
        align-items: center;
        gap: 1.4vh;
        padding: 1.4vh 1.6vh;
        border-radius: 1.3vh;
        background: rgba(127, 178, 229, 0.07);
        border: 1px solid rgba(127, 178, 229, 0.18);
        cursor: pointer;
        transition: background 0.15s;
    }
    .stk__items:hover {
        background: rgba(127, 178, 229, 0.12);
    }
    .stk__items_info {
        flex: 1;
        display: flex;
        flex-direction: column;
        gap: 0.3vh;
    }
    .stk__items_info b {
        font-size: 1.7vh;
    }
    .stk__items_info span {
        font-size: 1.3vh;
        color: #8b93a1;
    }
    .stk__items_open {
        font-size: 1.5vh;
        font-weight: 700;
        color: #7fb2e5;
    }
    .stk__foot {
        font-size: 1.2vh;
        color: #6b7280;
        text-align: center;
    }
</style>
