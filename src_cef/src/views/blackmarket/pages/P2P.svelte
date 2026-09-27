<script>
    import { action, btc, usd, timeLeft, toInt } from '../api'

    export let data;
    export let lastResult;

    $: fee = Number(data.config?.p2pFee) || 0;
    $: minHours = data.config?.minHours || 5;
    $: maxHours = data.config?.maxHours || 120;
    $: offers = (data.p2p || []).slice().sort((a, b) => a.price - b.price);
    $: available = (data.wallet?.balance || 0) - (data.wallet?.reserved || 0);

    const priceText = (p) => `$${Number(p).toLocaleString('ru-RU', { maximumFractionDigits: 4 })}`;
    const costOf = (amount, price) => Math.ceil(amount * Number(price));

    // Покупка
    let buying = null;
    let buyAmount = "";
    $: buyN = buying ? Math.min(Math.max(1, toInt(buyAmount)), buying.amount) : 0;
    $: buyFee = Math.ceil(buyN * fee / 100);

    // Своя заявка
    let creating = false;
    let sellAmount = "";
    let sellPrice = "";
    let hours = 24;
    $: sellN = Math.min(Math.max(0, toInt(sellAmount)), available);
    $: sellP = Number(String(sellPrice).replace(",", ".")) || 0;

    const buy = () => buying && buyN > 0 && action("p2pBuy", { id: buying.id, amount: buyN });
    const create = () => sellN > 0 && sellP > 0 && action("p2pCreate", { amount: sellN, price: sellP, hours: toInt(hours) });

    $: if (lastResult && lastResult.ok) {
        if (lastResult.action === "p2pBuy") buying = null;
        if (lastResult.action === "p2pCreate") creating = false;
    }
</script>

<div class="bm__row" style="justify-content: space-between; margin-bottom: 1.6vh">
    <div class="bm__h1" style="margin: 0">P2P: BTC за наличные</div>
    <div class="bm__btn primary" on:click={() => { creating = true; sellAmount = ""; sellPrice = ""; hours = Math.min(Math.max(24, minHours), maxHours); }}>+ Продать BTC</div>
</div>
<div class="bm__muted small" style="margin-bottom: 1.4vh">
    Покупатель платит наличными, продавец получает деньги сразу. Комиссия сети {fee}% удерживается в BTC с покупателя. Участники анонимны.
</div>

<div class="bm__scroll">
    {#each offers as o (o.id)}
        <div class="bm__card bm__row offer">
            <div style="flex: 1">
                <div class="bm__price">{btc(o.amount)}</div>
                <div class="bm__muted small">{o.mine ? "Ваша заявка" : "Аноним"} · {timeLeft(o.minutesLeft)}</div>
            </div>
            <div style="text-align: right">
                <div>{priceText(o.price)} <span class="bm__muted small">за 1 BTC</span></div>
                <div class="bm__muted small">всё за {usd(costOf(o.amount, o.price))}</div>
            </div>
            {#if o.mine}
                <div class="bm__btn" on:click={() => action("p2pCancel", { id: o.id })}>Снять</div>
            {:else}
                <div class="bm__btn gold" on:click={() => { buying = o; buyAmount = String(o.amount); }}>Купить</div>
            {/if}
        </div>
    {:else}
        <div class="bm__empty">Заявок нет</div>
    {/each}
</div>

{#if buying}
    <div class="bm__modal-bg" on:click|self={() => buying = null}>
        <div class="bm__modal">
            <div class="bm__h1">Купить BTC</div>
            <div class="bm__line"><span class="bm__muted">Цена</span><span>{priceText(buying.price)} за 1 BTC</span></div>
            <div class="bm__line"><span class="bm__muted">Доступно</span><span>{btc(buying.amount)}</span></div>
            <div class="bm__field" style="margin-top: 1.4vh">
                <div class="bm__label">Сколько BTC купить</div>
                <input class="bm__input" type="number" min="1" max={buying.amount} bind:value={buyAmount} />
            </div>
            <div class="bm__line"><span class="bm__muted">Заплатите наличными</span><span class="bm__price">{usd(costOf(buyN, buying.price))}</span></div>
            <div class="bm__line"><span class="bm__muted">Комиссия {fee}%</span><span>{btc(buyFee)}</span></div>
            <div class="bm__line"><span>Получите</span><span class="bm__price">{btc(Math.max(0, buyN - buyFee))}</span></div>
            <div class="bm__row" style="margin-top: 1.6vh">
                <div class="bm__btn" style="flex: 1" on:click={() => buying = null}>Отмена</div>
                <div class="bm__btn gold" style="flex: 2" class:disabled={buyN - buyFee <= 0} on:click={buy}>Купить</div>
            </div>
        </div>
    </div>
{/if}

{#if creating}
    <div class="bm__modal-bg" on:click|self={() => creating = false}>
        <div class="bm__modal">
            <div class="bm__h1">Продать BTC</div>
            <div class="bm__muted small" style="margin-bottom: 1.2vh">BTC заявки блокируются в кошельке до продажи или снятия. Свободно: {btc(available)}</div>
            <div class="bm__row">
                <div class="bm__field" style="flex: 1">
                    <div class="bm__label">Сколько BTC</div>
                    <input class="bm__input" type="number" min="1" bind:value={sellAmount} />
                </div>
                <div class="bm__field" style="flex: 1">
                    <div class="bm__label">Цена за 1 BTC, $</div>
                    <input class="bm__input" type="number" min="0" step="0.01" bind:value={sellPrice} />
                </div>
            </div>
            <div class="bm__field">
                <div class="bm__label">Срок заявки: {hours} ч</div>
                <input class="range" type="range" min={minHours} max={maxHours} bind:value={hours} />
            </div>
            <div class="bm__line"><span class="bm__muted">Получите за всё</span><span class="bm__price">{usd(costOf(sellN, sellP))}</span></div>
            <div class="bm__row" style="margin-top: 1.6vh">
                <div class="bm__btn" style="flex: 1" on:click={() => creating = false}>Отмена</div>
                <div class="bm__btn primary" style="flex: 2" class:disabled={sellN <= 0 || sellP <= 0} on:click={create}>Выставить</div>
            </div>
        </div>
    </div>
{/if}

<style>
    .offer { margin-bottom: 1vh; gap: 1.6vh; }
    .range { width: 100%; accent-color: #d8394b; }
</style>
