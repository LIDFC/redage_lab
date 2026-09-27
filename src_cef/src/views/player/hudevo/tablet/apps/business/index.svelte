<script>
    import { onDestroy } from 'svelte'
    import { fade } from 'svelte/transition'
    import { executeClient } from 'api/rage'
    import { hasJsonStructure } from 'api/functions'
    import Main from './main.svelte'
    import Transactions from './transactions.svelte'
    import Products from './products.svelte'
    import Modal from './modal.svelte'
    import './business.sass'

    export let onClose;

    // Данные приходят с сервера: Businesses/Tablet/Repository.cs (SendData / SendHistory)
    let data = undefined;     // undefined — загрузка, null — бизнеса нет
    let history = null;
    let tab = "main";
    let days = 1;
    let periodOpen = false;
    let modal = null;         // { type, ... } — окно ввода суммы/цены/количества

    const periods = [
        { days: 1, name: "Сегодня" },
        { days: 7, name: "7 дней" },
        { days: 30, name: "30 дней" },
    ];

    window.events.addEvent("cef.tablet.business.data", (json) => {
        data = hasJsonStructure(json) ? JSON.parse(json) : null;
    });
    window.events.addEvent("cef.tablet.business.history", (json) => {
        if (hasJsonStructure(json)) history = JSON.parse(json);
    });
    onDestroy(() => {
        window.events.removeEvent("cef.tablet.business.data");
        window.events.removeEvent("cef.tablet.business.history");
    });

    const load = (bizId = -1) => {
        history = null;
        executeClient("client.tablet.business.load", bizId, days);
    };
    load();

    const setPeriod = (value) => {
        periodOpen = false;
        if (days === value) return;
        days = value;
        history = null;
        executeClient("client.tablet.business.history", days);
    };

    const action = (name, args = {}) => {
        if (name === "sell" || name === "maxProducts") {
            executeClient("client.tablet.businessDialog", name);
            return;
        }
        executeClient("client.tablet.business.action", name, JSON.stringify(args));
    };

    const onSwitchBiz = (e) => load(Number(e.target.value));

    // Для превью без сервера: window.tabletBusinessMock(data, history)
    window.tabletBusinessMock = (d, h) => { data = d; history = h; };
</script>

<div class="biz" in:fade={{ duration: 150 }}>
    <div class="biz__header">
        <div class="biz__tabs">
            <div class="biz__tab" class:active={tab === "main"} on:click={() => tab = "main"}><i class="ic-grid"></i>Главная</div>
            <div class="biz__tab" class:active={tab === "transactions"} on:click={() => tab = "transactions"}><i class="ic-list"></i>Транзакции</div>
            <div class="biz__tab" class:active={tab === "products"} on:click={() => tab = "products"}><i class="ic-box"></i>Товары</div>
            {#if tab !== "products"}
                <div class="biz__period" on:click={() => periodOpen = !periodOpen}>
                    {periods.find(p => p.days === days).name}
                    <span class="biz__caret"></span>
                    {#if periodOpen}
                        <div class="biz__period_list">
                            {#each periods as p}
                                <div class:active={p.days === days} on:click|stopPropagation={() => setPeriod(p.days)}>{p.name}</div>
                            {/each}
                        </div>
                    {/if}
                </div>
            {/if}
        </div>
        <div class="biz__title">
            {#if data && data.businesses && data.businesses.length > 1}
                <select class="biz__select" value={data.id} on:change={onSwitchBiz}>
                    {#each data.businesses as b}
                        <option value={b.id}>{b.title}</option>
                    {/each}
                </select>
            {:else if data}
                <b>{data.title}</b>
            {/if}
            <div class="biz__avatar">$</div>
            <div class="biz__power" on:click={onClose} title="Закрыть планшет"></div>
        </div>
    </div>

    {#if data === undefined}
        <div class="biz__empty">Загрузка…</div>
    {:else if data === null}
        <div class="biz__empty">
            <b>У вас нет бизнеса</b>
            <span>Купить бизнес можно у риэлтора или на маркетплейсе.</span>
        </div>
    {:else if tab === "main"}
        <Main {data} {history} {action} openModal={(m) => modal = m} />
    {:else if tab === "transactions"}
        <Transactions {history} />
    {:else}
        <Products {data} {action} openModal={(m) => modal = m} />
    {/if}

    {#if modal}
        <Modal {modal} {action} onClose={() => modal = null} />
    {/if}
</div>
