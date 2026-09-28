<script>
    // Приложение «Чёрный рынок» (открывается из телефона при включённом VPN).
    // Клиент: src_client/phone/blackmarket.js, сервер: dotnet/resources/NeptuneEvo/BlackMarket
    import { executeClient } from 'api/rage'
    import { onDestroy } from 'svelte'
    import { btc } from './api'
    import Market from './pages/Market.svelte'
    import MyLots from './pages/MyLots.svelte'
    import Orders from './pages/Orders.svelte'
    import P2P from './pages/P2P.svelte'
    import Transfer from './pages/Transfer.svelte'
    import History from './pages/History.svelte'
    import Wallets from './pages/Wallets.svelte'
    import CashOut from './pages/CashOut.svelte'
    import Fence from './pages/Fence.svelte'

    export let viewData;

    let page = "market";
    let data = { wallet: { balance: 0, reserved: 0 }, fraction: null, config: {}, lots: [], inventory: [], p2p: [], drops: [] };
    const parse = (value) => {
        if (!value) return;
        try {
            data = typeof value === "string" ? JSON.parse(value) : value;
            // Стартовая вкладка (у Мавра приложение открывается сразу на «Обнале»)
            if (data.page)
                page = data.page;
        } catch (e) {}
    }
    $: parse(viewData);

    let lastResult = null;

    const pages = [
        { key: "market", name: "Рынок" },
        { key: "mylots", name: "Мои лоты" },
        { key: "orders", name: "Заказы" },
        { key: "p2p", name: "P2P" },
        { key: "transfer", name: "Перевод" },
        { key: "history", name: "История" },
        { key: "wallets", name: "Кошельки" },
        { key: "cashout", name: "Обнал" },
        { key: "fence", name: "Скупка" },
    ];

    window.events.addEvent("blackmarket.result", (actionName, ok, message, json) => {
        parse(json);
        lastResult = { action: actionName, ok, message, time: Date.now() };
    });
    onDestroy(() => window.events.removeEvent("blackmarket.result"));

    const close = () => executeClient("client.blackmarket.close");
    const onKey = (e) => {
        if (e.keyCode === 27)
            close();
    }
    $: available = (data.wallet?.balance || 0) - (data.wallet?.reserved || 0);
    $: ordersCount = (data.drops || []).length;
</script>

<svelte:window on:keyup={onKey} />

<div class="bm">
    <div class="bm__panel">
        <div class="bm__side">
            <div class="bm__logo">
                <div class="bm__logo-mark">◈</div>
                <div>
                    <div class="bm__logo-title">Чёрный рынок</div>
                    <div class="bm__logo-sub">защищённое соединение · VPN</div>
                </div>
            </div>
            <div class="bm__nav">
                {#each pages as p}
                    <div class="bm__nav-item" class:active={page === p.key} on:click={() => page = p.key}>
                        {p.name}
                        {#if p.key === "orders" && ordersCount > 0}<span class="bm__badge">{ordersCount}</span>{/if}
                    </div>
                {/each}
            </div>
            <div class="bm__wallet">
                <div class="bm__muted">Мой кошелёк</div>
                <div class="bm__wallet-value">{btc(available)}</div>
                {#if data.wallet?.reserved > 0}
                    <div class="bm__muted small">в P2P-заявках: {btc(data.wallet.reserved)}</div>
                {/if}
                {#if data.fraction}
                    <div class="bm__muted" style="margin-top: 1vh">{data.fraction.name}</div>
                    <div class="bm__wallet-value small">{btc(data.fraction.balance)}</div>
                {/if}
            </div>
            <div class="bm__close" on:click={close}>Отключиться <span>ESC</span></div>
        </div>

        <div class="bm__content">
            {#if page === "market"}
                <Market {data} {lastResult} />
            {:else if page === "mylots"}
                <MyLots {data} {lastResult} />
            {:else if page === "orders"}
                <Orders {data} />
            {:else if page === "p2p"}
                <P2P {data} {lastResult} />
            {:else if page === "transfer"}
                <Transfer {data} {lastResult} />
            {:else if page === "history"}
                <History {data} />
            {:else if page === "wallets"}
                <Wallets {data} {lastResult} />
            {:else if page === "cashout"}
                <CashOut {data} {lastResult} />
            {:else if page === "fence"}
                <Fence {data} {lastResult} />
            {/if}
        </div>
    </div>
</div>

<style>
    .bm {
        position: absolute;
        inset: 0;
        z-index: 1000;
        display: flex;
        align-items: center;
        justify-content: center;
        background: radial-gradient(circle at 70% 10%, rgba(40, 12, 16, 0.94), rgba(4, 5, 8, 0.97) 60%);
        color: #e6e6e6;
        font-family: 'TTNorms-Regular';
        font-size: 1.5vh;
    }
    .bm__panel {
        width: 150vh;
        max-width: 96vw;
        height: 84vh;
        display: flex;
        border-radius: 1.4vh;
        overflow: hidden;
        background: rgba(10, 11, 14, 0.96);
        border: 1px solid rgba(255, 255, 255, 0.06);
        box-shadow: 0 2vh 7vh rgba(0, 0, 0, 0.6), inset 0 0 0 1px rgba(170, 30, 45, 0.08);
    }
    .bm__side {
        width: 27vh;
        flex-shrink: 0;
        display: flex;
        flex-direction: column;
        padding: 2.4vh 2vh;
        background: linear-gradient(180deg, rgba(20, 8, 11, 0.9), rgba(8, 8, 10, 0.9));
        border-right: 1px solid rgba(255, 255, 255, 0.05);
    }
    .bm__logo {
        display: flex;
        align-items: center;
        gap: 1.2vh;
        margin-bottom: 3vh;
    }
    .bm__logo-mark {
        width: 4.4vh;
        height: 4.4vh;
        border-radius: 1vh;
        display: flex;
        align-items: center;
        justify-content: center;
        background: #1a0b0e;
        border: 1px solid rgba(200, 40, 55, 0.5);
        color: #d8394b;
        font-size: 2.4vh;
    }
    .bm__logo-title {
        font-family: 'TTNorms-Bold';
        font-size: 2vh;
        letter-spacing: 0.05vh;
    }
    .bm__logo-sub {
        font-size: 1.1vh;
        color: #7fd36b;
        opacity: 0.8;
        font-family: monospace;
    }
    .bm__nav {
        display: flex;
        flex-direction: column;
        gap: 0.5vh;
    }
    .bm__nav-item {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 1.2vh 1.4vh;
        border-radius: 0.9vh;
        cursor: pointer;
        color: rgba(255, 255, 255, 0.6);
        transition: all 0.15s;
    }
    .bm__nav-item:hover { background: rgba(255, 255, 255, 0.04); color: white; }
    .bm__nav-item.active {
        background: rgba(200, 40, 55, 0.14);
        color: white;
        box-shadow: inset 0.3vh 0 0 #d8394b;
    }
    .bm__badge {
        min-width: 2.2vh;
        padding: 0.2vh 0.6vh;
        border-radius: 1vh;
        background: #d8394b;
        color: white;
        font-size: 1.1vh;
        text-align: center;
    }
    .bm__wallet {
        margin-top: auto;
        padding: 1.6vh;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.03);
        border: 1px solid rgba(255, 255, 255, 0.05);
    }
    .bm__wallet-value {
        font-family: 'TTNorms-Bold';
        font-size: 2.2vh;
        color: #f2b544;
    }
    .bm__wallet-value.small { font-size: 1.7vh; }
    .bm__close {
        margin-top: 1.4vh;
        cursor: pointer;
        text-align: center;
        padding: 1vh;
        border-radius: 0.9vh;
        color: rgba(255, 255, 255, 0.6);
        background: rgba(255, 255, 255, 0.04);
    }
    .bm__close span {
        margin-left: 0.6vh;
        padding: 0.1vh 0.5vh;
        border-radius: 0.4vh;
        background: rgba(255, 255, 255, 0.08);
        font-size: 1.1vh;
    }
    .bm__content {
        flex: 1;
        min-width: 0;
        display: flex;
        flex-direction: column;
        padding: 2.4vh 2.8vh;
    }

    /* Общие элементы страниц */
    :global(.bm .bm__muted) { color: rgba(255, 255, 255, 0.45); }
    :global(.bm .bm__muted.small) { font-size: 1.2vh; }
    :global(.bm .bm__h1) { font-family: 'TTNorms-Bold'; font-size: 2.6vh; margin-bottom: 1.6vh; }
    :global(.bm .bm__row) { display: flex; align-items: center; gap: 1vh; }
    :global(.bm .bm__scroll) { flex: 1; min-height: 0; overflow-y: auto; padding-right: 0.6vh; }
    :global(.bm .bm__scroll::-webkit-scrollbar) { width: 0.4vh; }
    :global(.bm .bm__scroll::-webkit-scrollbar-thumb) { background: rgba(255, 255, 255, 0.12); border-radius: 0.4vh; }
    :global(.bm .bm__input) {
        padding: 1.1vh 1.3vh;
        border-radius: 0.9vh;
        border: 1px solid rgba(255, 255, 255, 0.08);
        background: rgba(255, 255, 255, 0.04);
        color: white;
        font-family: inherit;
        font-size: 1.5vh;
        outline: none;
    }
    :global(.bm .bm__input:focus) { border-color: rgba(216, 57, 75, 0.6); }
    :global(.bm .bm__btn) {
        cursor: pointer;
        padding: 1.1vh 1.8vh;
        border-radius: 0.9vh;
        background: rgba(255, 255, 255, 0.07);
        text-align: center;
        transition: all 0.15s;
        user-select: none;
    }
    :global(.bm .bm__btn:hover) { background: rgba(255, 255, 255, 0.12); }
    :global(.bm .bm__btn.primary) { background: #b3283a; color: white; font-family: 'TTNorms-Bold'; }
    :global(.bm .bm__btn.primary:hover) { background: #cc3044; }
    :global(.bm .bm__btn.gold) { background: #f2b544; color: #140f05; font-family: 'TTNorms-Bold'; }
    :global(.bm .bm__btn.disabled) { opacity: 0.4; pointer-events: none; }
    :global(.bm .bm__chip) {
        cursor: pointer;
        padding: 0.7vh 1.3vh;
        border-radius: 2vh;
        background: rgba(255, 255, 255, 0.05);
        color: rgba(255, 255, 255, 0.65);
        white-space: nowrap;
    }
    :global(.bm .bm__chip.active) { background: rgba(216, 57, 75, 0.2); color: white; box-shadow: inset 0 0 0 1px rgba(216, 57, 75, 0.6); }
    :global(.bm .bm__card) {
        padding: 1.4vh;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.035);
        border: 1px solid rgba(255, 255, 255, 0.05);
    }
    :global(.bm .bm__empty) { margin: 5vh auto; text-align: center; color: rgba(255, 255, 255, 0.35); }
    :global(.bm .bm__price) { font-family: 'TTNorms-Bold'; color: #f2b544; }
    :global(.bm .bm__icon) {
        width: 6vh;
        height: 6vh;
        flex-shrink: 0;
        border-radius: 0.9vh;
        background-color: rgba(0, 0, 0, 0.35);
        background-size: 80%;
        background-position: center;
        background-repeat: no-repeat;
    }
    :global(.bm .bm__label) { font-size: 1.2vh; color: rgba(255, 255, 255, 0.45); margin-bottom: 0.5vh; }
    :global(.bm .bm__field) { display: flex; flex-direction: column; margin-bottom: 1.4vh; }
    :global(.bm .bm__modal-bg) {
        position: absolute;
        inset: 0;
        display: flex;
        align-items: center;
        justify-content: center;
        background: rgba(0, 0, 0, 0.55);
        z-index: 5;
    }
    :global(.bm .bm__modal) {
        width: 52vh;
        padding: 2.4vh;
        border-radius: 1.2vh;
        background: #111217;
        border: 1px solid rgba(216, 57, 75, 0.35);
        box-shadow: 0 2vh 5vh rgba(0, 0, 0, 0.6);
    }
    :global(.bm .bm__line) { display: flex; justify-content: space-between; padding: 0.6vh 0; border-bottom: 1px solid rgba(255, 255, 255, 0.05); }
    :global(.bm .bm__error) { color: #ff6b6b; margin-top: 0.8vh; }
    :global(.bm .bm__ok) { color: #7fd36b; margin-top: 0.8vh; }
</style>
