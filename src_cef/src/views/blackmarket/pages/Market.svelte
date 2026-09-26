<script>
    import { action, btc, itemIcon, timeLeft, categories, toInt } from '../api'

    export let data;
    export let lastResult;

    let category = "all";
    let search = "";
    let sort = "asc";
    let selected = null;
    let count = 1;
    let source = "personal";

    $: query = search.trim().toLowerCase();
    $: lots = (data.lots || [])
        .filter(l => !l.mine)
        .filter(l => category === "all" || l.category === category)
        .filter(l => !query.length || l.name.toLowerCase().includes(query))
        .sort((a, b) => sort === "asc" ? a.price - b.price : b.price - a.price);

    // Лот мог измениться, пока открыто окно покупки
    $: if (selected) {
        const fresh = (data.lots || []).find(l => l.id === selected.id);
        selected = fresh || null;
    }
    $: max = selected ? selected.count : 1;
    $: amount = Math.min(Math.max(1, toInt(count)), max);
    $: total = selected ? selected.price * amount : 0;
    $: fee = source === "fraction" ? Math.ceil(total * (Number(data.config?.fractionFee) || 0) / 100) : 0;
    $: available = (data.wallet?.balance || 0) - (data.wallet?.reserved || 0);
    $: canFraction = !!data.fraction && data.fraction.canPay;
    $: enough = source === "fraction" ? (data.fraction?.balance || 0) >= total + fee : available >= total;

    const open = (lot) => {
        selected = lot;
        count = 1;
        source = "personal";
    }

    let waiting = false;
    const buy = () => {
        if (!selected || !enough || waiting) return;
        waiting = true;
        action("buy", { id: selected.id, count: amount, source });
    }
    $: if (lastResult && lastResult.action === "buy") {
        waiting = false;
        if (lastResult.ok) selected = null;
    }
</script>

<div class="bm__h1">Рынок</div>
<div class="bm__row" style="margin-bottom: 1.4vh; flex-wrap: wrap">
    {#each categories as c}
        <div class="bm__chip" class:active={category === c.key} on:click={() => category = c.key}>{c.name}</div>
    {/each}
</div>
<div class="bm__row" style="margin-bottom: 1.6vh">
    <input class="bm__input" style="flex: 1" bind:value={search} placeholder="Поиск по названию" />
    <div class="bm__chip" class:active={sort === "asc"} on:click={() => sort = "asc"}>Сначала дешёвые</div>
    <div class="bm__chip" class:active={sort === "desc"} on:click={() => sort = "desc"}>Сначала дорогие</div>
</div>

<div class="bm__scroll">
    <div class="grid">
        {#each lots as lot (lot.id)}
            <div class="bm__card lot" on:click={() => open(lot)}>
                <div class="bm__row">
                    <div class="bm__icon" style="background-image: url({itemIcon(lot.itemId)})"></div>
                    <div class="info">
                        <div class="name">{lot.name}</div>
                        <div class="bm__muted small">Продавец: аноним · {timeLeft(lot.minutesLeft)}</div>
                    </div>
                </div>
                <div class="bm__row between">
                    <div><span class="bm__price">{btc(lot.price)}</span> <span class="bm__muted small">/ шт.</span></div>
                    <div class="bm__muted">{lot.count} шт.</div>
                </div>
            </div>
        {:else}
            <div class="bm__empty">Предложений нет</div>
        {/each}
    </div>
</div>

{#if selected}
    <div class="bm__modal-bg" on:click|self={() => selected = null}>
        <div class="bm__modal">
            <div class="bm__row" style="margin-bottom: 1.6vh">
                <div class="bm__icon" style="background-image: url({itemIcon(selected.itemId)})"></div>
                <div>
                    <div class="name big">{selected.name}</div>
                    <div class="bm__muted small">Продавец: аноним · осталось {timeLeft(selected.minutesLeft)}</div>
                </div>
            </div>
            <div class="bm__line"><span class="bm__muted">Цена за штуку</span><span class="bm__price">{btc(selected.price)}</span></div>
            <div class="bm__line"><span class="bm__muted">В наличии</span><span>{selected.count} шт.</span></div>

            <div class="bm__field" style="margin-top: 1.4vh">
                <div class="bm__label">Количество</div>
                <div class="bm__row">
                    <input class="bm__input" style="flex: 1" type="number" min="1" max={max} bind:value={count} />
                    <div class="bm__btn" on:click={() => count = max}>Всё</div>
                </div>
                {#if max > 1}
                    <input class="range" type="range" min="1" max={max} bind:value={count} />
                {/if}
            </div>

            <div class="bm__label">Оплата</div>
            <div class="bm__row" style="margin-bottom: 1.2vh">
                <div class="bm__chip" class:active={source === "personal"} on:click={() => source = "personal"}>Личный кошелёк</div>
                {#if data.fraction}
                    <div class="bm__chip" class:active={source === "fraction"} class:locked={!canFraction}
                         on:click={() => canFraction && (source = "fraction")}>Кошелёк банды</div>
                {/if}
            </div>
            {#if data.fraction && !canFraction}
                <div class="bm__muted small" style="margin-bottom: 1vh">Платить с кошелька банды можно с {data.fraction.needRank} ранга</div>
            {/if}

            <div class="bm__line"><span class="bm__muted">Сумма</span><span>{btc(total)}</span></div>
            {#if source === "fraction"}
                <div class="bm__line"><span class="bm__muted">Комиссия банды {data.config?.fractionFee}%</span><span>{btc(fee)}</span></div>
            {/if}
            <div class="bm__line"><span>Итого</span><span class="bm__price">{btc(total + fee)}</span></div>
            {#if !enough}
                <div class="bm__error">Не хватает BTC</div>
            {/if}
            <div class="bm__muted small" style="margin: 1.2vh 0">
                После оплаты товар оставят в закладке — точка появится на карте только у вас. Кто первым заберёт, тому и достанется.
            </div>
            <div class="bm__row">
                <div class="bm__btn" style="flex: 1" on:click={() => selected = null}>Отмена</div>
                <div class="bm__btn primary" style="flex: 2" class:disabled={!enough || waiting} on:click={buy}>Купить</div>
            </div>
        </div>
    </div>
{/if}

<style>
    .grid {
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: 1.2vh;
    }
    .lot {
        cursor: pointer;
        display: flex;
        flex-direction: column;
        gap: 1.2vh;
        transition: border-color 0.15s, background 0.15s;
    }
    .lot:hover {
        border-color: rgba(216, 57, 75, 0.5);
        background: rgba(216, 57, 75, 0.06);
    }
    .info { min-width: 0; }
    .name {
        font-family: 'TTNorms-Bold';
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
    }
    .name.big { font-size: 2vh; white-space: normal; }
    .between { justify-content: space-between; }
    .range { width: 100%; margin-top: 1vh; accent-color: #d8394b; }
    .locked { opacity: 0.45; cursor: default; }
</style>
