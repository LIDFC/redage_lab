<script>
    import { money, isPercent } from './util'

    export let modal;
    export let action;
    export let onClose;

    // payTax: сумма пополнения налогового счёта; price: цена/наценка товара; order: количество для заказа
    let value = "";
    const p = modal.product;

    if (modal.type === "price")
        value = p.price;
    else if (modal.type === "order")
        value = Math.max(p.minOrder, p.max - p.count);
    else if (modal.type === "payTax")
        value = Math.min(modal.max, modal.bank);

    $: num = Math.max(0, Math.floor(Number(String(value).replace(/\D/g, "")) || 0));
    $: orderMax = p ? p.max - p.count : 0;
    $: valid = modal.type === "payTax" ? num > 0 && num <= modal.max
        : modal.type === "price" ? num >= p.minPrice && num <= p.maxPrice
        : num >= p.minOrder && num <= orderMax;

    const submit = () => {
        if (!valid) return;
        if (modal.type === "payTax") action("payTax", { amount: num });
        else if (modal.type === "price") action("extraCharge", { name: p.name, value: num });
        else action("addOrder", { name: p.name, value: num });
        onClose();
    };

    const onKey = (e) => {
        if (e.key === "Enter") submit();
        if (e.key === "Escape") onClose();
    };
</script>

<div class="biz__modal_bg" on:click|self={onClose}>
    <div class="biz__modal">
        {#if modal.type === "payTax"}
            <h3>Пополнить налоговый счёт</h3>
            <p>Налог списывается каждый час: {money(modal.taxHour)}. Можно внести до {money(modal.max)}.</p>
            <div class="biz__input"><span>$</span><input bind:value on:keydown={onKey} autofocus /></div>
            <div class="biz__quick">
                {#each [24, 72, 168] as h}
                    {#if modal.taxHour * h <= modal.max}
                        <div on:click={() => value = modal.taxHour * h}>{h === 24 ? "Сутки" : h === 72 ? "3 дня" : "Неделя"}</div>
                    {/if}
                {/each}
                <div on:click={() => value = modal.max}>Максимум</div>
            </div>
            <p class="muted">Спишется с банковского счёта (на счету {money(modal.bank)})</p>
        {:else if modal.type === "price"}
            <h3>Цена: {p.name}</h3>
            <p>{isPercent(p.name, modal.bizType) ? "Наценка в процентах" : "Цена за единицу"}: от {p.minPrice} до {p.maxPrice}</p>
            <input class="biz__range" type="range" min={p.minPrice} max={p.maxPrice} bind:value />
            <div class="biz__input"><span>{isPercent(p.name, modal.bizType) ? "%" : "$"}</span><input bind:value on:keydown={onKey} autofocus /></div>
            {#if !isPercent(p.name, modal.bizType)}
                <p class="muted">Это {Math.round(num * 100 / Math.max(1, p.defaultPrice))}% от закупочной цены ({money(p.defaultPrice)})</p>
            {/if}
        {:else}
            <h3>Заказ: {p.name}</h3>
            <p>Можно заказать от {p.minOrder} до {orderMax} шт. Цена за единицу {money(p.unitPrice)}.</p>
            <div class="biz__input"><span>шт</span><input bind:value on:keydown={onKey} autofocus /></div>
            <p class="muted">К оплате с банковского счёта: <b>{money(num * p.unitPrice)}</b> (на счету {money(modal.bank)})</p>
        {/if}
        <div class="biz__modal_buttons">
            <div class="biz__btn ghost" on:click={onClose}>Отмена</div>
            <div class="biz__btn primary" class:disabled={!valid} on:click={submit}>Подтвердить</div>
        </div>
    </div>
</div>
