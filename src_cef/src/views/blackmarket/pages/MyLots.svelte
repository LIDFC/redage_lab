<script>
    import { action, btc, itemIcon, timeLeft, toInt } from '../api'

    export let data;
    export let lastResult;

    $: myLots = (data.lots || []).filter(l => l.mine);
    $: inventory = data.inventory || [];
    $: minHours = data.config?.minHours || 5;
    $: maxHours = data.config?.maxHours || 120;

    let creating = false;
    let itemId = null;
    let count = 1;
    let price = "";
    let hours = 24;

    $: item = inventory.find(i => i.itemId === itemId) || null;
    $: amount = item ? Math.min(Math.max(1, toInt(count)), item.count) : 0;
    $: priceUnit = toInt(price);
    $: canCreate = item && amount > 0 && priceUnit > 0;

    const startCreate = () => {
        creating = true;
        itemId = inventory.length ? inventory[0].itemId : null;
        count = 1;
        price = "";
        hours = Math.min(Math.max(24, minHours), maxHours);
    }

    const create = () => {
        if (!canCreate) return;
        action("createLot", { itemId, count: amount, price: priceUnit, hours: toInt(hours) });
    }

    let editing = null;
    let newPrice = "";
    const saveEdit = () => {
        if (!editing || toInt(newPrice) <= 0) return;
        action("editLot", { id: editing.id, price: toInt(newPrice) });
    }
    const cancelLot = (lot) => action("cancelLot", { id: lot.id });

    $: if (lastResult && lastResult.ok) {
        if (lastResult.action === "createLot") creating = false;
        if (lastResult.action === "editLot") editing = null;
    }
</script>

<div class="bm__row" style="justify-content: space-between; margin-bottom: 1.6vh">
    <div class="bm__h1" style="margin: 0">Мои лоты</div>
    <div class="bm__btn primary" on:click={startCreate}>+ Выставить товар</div>
</div>
<div class="bm__muted small" style="margin-bottom: 1.4vh">
    Выставленный товар хранится у рынка: его нельзя использовать или передать, пока лот не снят. Оплата приходит сразу после покупки.
</div>

<div class="bm__scroll">
    {#each myLots as lot (lot.id)}
        <div class="bm__card bm__row mylot">
            <div class="bm__icon" style="background-image: url({itemIcon(lot.itemId)})"></div>
            <div style="flex: 1; min-width: 0">
                <div class="name">{lot.name}</div>
                <div class="bm__muted small">Осталось {lot.count} шт. · до снятия {timeLeft(lot.minutesLeft)}</div>
            </div>
            <div class="bm__price">{btc(lot.price)} <span class="bm__muted small">/ шт.</span></div>
            <div class="bm__btn" on:click={() => { editing = lot; newPrice = String(lot.price); }}>Цена</div>
            <div class="bm__btn" on:click={() => cancelLot(lot)}>Снять</div>
        </div>
    {:else}
        <div class="bm__empty">У вас нет активных лотов</div>
    {/each}
</div>

{#if creating}
    <div class="bm__modal-bg" on:click|self={() => creating = false}>
        <div class="bm__modal">
            <div class="bm__h1">Новый лот</div>
            {#if !inventory.length}
                <div class="bm__empty">В инвентаре нет товаров, которые принимает рынок</div>
                <div class="bm__btn" on:click={() => creating = false}>Закрыть</div>
            {:else}
                <div class="bm__label">Товар из инвентаря</div>
                <div class="bm-items">
                    {#each inventory as i (i.itemId)}
                        <div class="bm__card bm-inv" class:active={i.itemId === itemId} on:click={() => { itemId = i.itemId; count = 1; }}>
                            <div class="bm__icon small" style="background-image: url({itemIcon(i.itemId)})"></div>
                            <div class="bm-inv-name">{i.name}</div>
                            <div class="bm__muted small">{i.count} шт.</div>
                        </div>
                    {/each}
                </div>
                <div class="bm__row">
                    <div class="bm__field" style="flex: 1">
                        <div class="bm__label">Количество (из {item ? item.count : 0})</div>
                        <input class="bm__input" type="number" min="1" max={item ? item.count : 1} bind:value={count} />
                    </div>
                    <div class="bm__field" style="flex: 1">
                        <div class="bm__label">Цена за штуку, BTC</div>
                        <input class="bm__input" type="number" min="1" bind:value={price} placeholder="0" />
                    </div>
                </div>
                <div class="bm__field">
                    <div class="bm__label">Срок объявления: {hours} ч</div>
                    <input class="range" type="range" min={minHours} max={maxHours} bind:value={hours} />
                </div>
                <div class="bm__line"><span class="bm__muted">Выручка при продаже всего</span><span class="bm__price">{btc(priceUnit * amount)}</span></div>
                <div class="bm__row" style="margin-top: 1.6vh">
                    <div class="bm__btn" style="flex: 1" on:click={() => creating = false}>Отмена</div>
                    <div class="bm__btn primary" style="flex: 2" class:disabled={!canCreate} on:click={create}>Выставить</div>
                </div>
            {/if}
        </div>
    </div>
{/if}

{#if editing}
    <div class="bm__modal-bg" on:click|self={() => editing = null}>
        <div class="bm__modal">
            <div class="bm__h1">Цена: {editing.name}</div>
            <div class="bm__field">
                <div class="bm__label">Новая цена за штуку, BTC</div>
                <input class="bm__input" type="number" min="1" bind:value={newPrice} />
            </div>
            <div class="bm__row">
                <div class="bm__btn" style="flex: 1" on:click={() => editing = null}>Отмена</div>
                <div class="bm__btn primary" style="flex: 2" class:disabled={toInt(newPrice) <= 0} on:click={saveEdit}>Сохранить</div>
            </div>
        </div>
    </div>
{/if}

<style>
    .mylot { margin-bottom: 1vh; gap: 1.4vh; }
    .name {
        font-family: 'TTNorms-Bold';
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
    }
    .bm-items {
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: 0.8vh;
        max-height: 24vh;
        overflow-y: auto;
        margin-bottom: 1.4vh;
    }
    .bm-inv {
        cursor: pointer;
        display: flex;
        flex-direction: column;
        align-items: center;
        text-align: center;
        gap: 0.4vh;
        padding: 1vh;
    }
    .bm-inv.active { border-color: #d8394b; background: rgba(216, 57, 75, 0.1); }
    .bm-inv-name { font-family: "TTNorms-Regular"; font-size: 1.25vh; line-height: 1.2; }
    :global(.bm .bm__icon.small) { width: 4.4vh; height: 4.4vh; }
    .range { width: 100%; accent-color: #d8394b; }
</style>
