<script>
    import { getPng } from '@/views/player/hudevo/phonenew/components/property/business/data'
    import { money, markup, isPercent } from './util'

    export let data;
    export let action;
    export let openModal;

    $: ordered = data.products.filter(p => p.ordered);
    $: canOrderAll = data.products.some(p => !p.ordered && p.count < p.max);
    let showOrders = false;
</script>

<div class="biz__body">
    <div class="biz__stockbar">
        <div class="biz__stock_info">
            Товаров на складе: <b>{data.whCount.toLocaleString('ru-RU')}</b>
            <span>Объём склада: <b>{data.whMax.toLocaleString('ru-RU')}</b></span>
            <div class="biz__meter"><div style="width: {Math.min(100, data.whCount / Math.max(1, data.whMax) * 100)}%"></div></div>
        </div>
        {#if ordered.length}
            <div class="biz__btn ghost" on:click={() => showOrders = !showOrders}>Активные заказы ({ordered.length})</div>
        {/if}
        <div class="biz__btn primary" class:disabled={!canOrderAll} on:click={() => canOrderAll && action("maxProducts")}>
            Заказать всё{data.orderAllPrice > 0 ? ` · ${money(data.orderAllPrice)}` : ""}
        </div>
    </div>

    {#if showOrders}
        <div class="biz__table orders">
            <div class="biz__tr head"><span class="grow">Товар</span><span class="r w">Количество</span><span class="r w">Номер заказа</span><span class="r w"></span></div>
            {#each ordered as p}
                <div class="biz__tr">
                    <span class="grow">{p.name}</span>
                    <span class="r w">{p.orderAmount}</span>
                    <span class="r w muted">#{p.orderUid}</span>
                    <span class="r w"><span class="biz__link danger" on:click={() => action("cancelOrder", { uid: p.orderUid })}>Отменить</span></span>
                </div>
            {/each}
        </div>
    {/if}

    <div class="biz__products">
        {#each data.products as p}
            <div class="biz__product">
                <div class="biz__product_img" style="background-image: url({getPng(p.productType, p.name, p.itemId)})"></div>
                <div class="biz__product_name">{p.name}</div>
                <div class="biz__product_sub">
                    {#if isPercent(p.name, data.type)}Наценка{:else}От закупочной{/if}
                </div>
                <div class="biz__product_markup">{markup(p, data.type)}%
                    {#if !isPercent(p.name, data.type)}<span>{money(p.price)}</span>{/if}
                </div>
                <div class="biz__meter small"><div style="width: {Math.min(100, p.count / Math.max(1, p.max) * 100)}%"></div></div>
                <div class="biz__product_stock">На складе <b>{p.count}</b> / {p.max}</div>
                <div class="biz__product_actions">
                    {#if !p.fixedPrice}
                        <div class="biz__mini" on:click={() => openModal({ type: "price", product: p, bizType: data.type })}>Цена</div>
                    {/if}
                    {#if p.ordered}
                        <div class="biz__mini danger" on:click={() => action("cancelOrder", { uid: p.orderUid })}>Отменить заказ</div>
                    {:else if p.count < p.max}
                        <div class="biz__mini accent" on:click={() => openModal({ type: "order", product: p, bank: data.bank })}>Заказать</div>
                    {:else}
                        <div class="biz__mini disabled">Склад полон</div>
                    {/if}
                </div>
            </div>
        {/each}
    </div>
</div>
