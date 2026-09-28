<script>
    // Скупщик краденого у Мавра: цена падает с каждой продажей и восстанавливается со временем
    // (сервер: BlackMarket/Fence/FenceManager.cs). Оплата наличными или в BTC с бонусом.
    import { action, btc, usd, toInt, itemIcon } from '../api'

    export let data;
    export let lastResult;

    $: atPoint = !!data.cashout?.atPoint;
    $: fence = data.fence || { items: [], btcBonus: 0, rate: 0 };
    $: rate = Number(fence.rate) || 0;
    $: bonus = Number(fence.btcBonus) || 0;

    let amounts = {};
    $: if (lastResult && lastResult.ok && lastResult.action === "fenceSell") amounts = {};

    // Оценка как на сервере: каждая следующая штука дешевле на шаг насыщения
    const quote = (item, count) => {
        const minFactor = Number(fence.minFactor) || 0.35;
        let saturation = 1 - item.demand / 100;
        let total = 0;
        for (let i = 0; i < count; i++) {
            total += Math.round(item.basePrice * Math.max(minFactor, 1 - saturation));
            saturation = Math.min(1, saturation + (Number(item.step) || 0));
        }
        return total;
    };
    const count = (item) => Math.min(Math.max(0, toInt(amounts[item.itemId] ?? item.have)), item.have);
    const sell = (item, pay) => {
        const n = count(item);
        if (!atPoint || n <= 0) return;
        action("fenceSell", { itemId: item.itemId, count: n, pay });
    };
    const demandColor = (d) => (d >= 75 ? "#7fd36b" : d >= 50 ? "#f5a524" : "#ff6b6b");
</script>

<div class="bm__h1">Скупка краденого</div>
<div class="bm__card bm__row where" class:ok={atPoint}>
    <div style="flex: 1">
        {#if atPoint}
            Мавр смотрит товар. Чем больше сдают — тем ниже цена; спрос восстанавливается со временем.
        {:else}
            Краденое принимает только Мавр лично. Приезжайте к нему.
        {/if}
    </div>
    <div class="bm__btn" on:click={() => action("refresh")}>Обновить</div>
    {#if !atPoint}
        <div class="bm__btn" on:click={() => action("cashoutGps")}>Метка на карте</div>
    {/if}
</div>

{#if fence.payoutNote}
    <div class="bm__muted small note">{fence.payoutNote}</div>
{/if}
<div class="list">
    {#each fence.items as item (item.itemId)}
        {@const n = Math.min(Math.max(0, toInt(amounts[item.itemId] ?? item.have)), item.have)}
        {@const total = quote(item, n)}
        {@const totalBtc = rate > 0 ? Math.floor(total * (100 + bonus) / 100 / rate) : 0}
        <div class="bm__card item">
            <div class="icon"><img src={itemIcon(item.itemId)} alt="" /></div>
            <div class="info">
                <div class="name">{item.name}</div>
                <div class="bm__muted small">Сейчас {usd(item.price)} за шт. · база {usd(item.basePrice)}</div>
                <div class="demand">
                    <div class="bar"><div style="width:{item.demand}%; background:{demandColor(item.demand)}"></div></div>
                    <span class="bm__muted small">спрос {item.demand}%</span>
                </div>
            </div>
            <div class="sell">
                <div class="bm__row">
                    <input class="bm__input qty" type="number" min="0" max={item.have} value={n}
                        on:input={(e) => (amounts = { ...amounts, [item.itemId]: e.target.value })} />
                    <span class="bm__muted small">из {item.have}</span>
                </div>
                <div class="bm__row buttons">
                    <div class="bm__btn gold" class:disabled={!atPoint || n <= 0} on:click={() => sell(item, "cash")}>{usd(total)}</div>
                    <div class="bm__btn primary" class:disabled={!atPoint || n <= 0 || totalBtc <= 0} on:click={() => sell(item, "btc")}>{btc(totalBtc)}</div>
                </div>
            </div>
        </div>
    {:else}
        <div class="bm__muted">Мавр сейчас ничего не скупает</div>
    {/each}
</div>
<div class="bm__muted small" style="margin-top: 1.2vh">
    В BTC Мавр платит на {bonus}% больше (курс 1 BTC = {usd(rate)}). Краденое находят при ограблении домов, детали — при разборке угнанных машин у Мавра.
</div>

<style>
    .where {
        margin-bottom: 1.4vh;
        border-color: rgba(216, 57, 75, 0.35);
        color: rgba(255, 255, 255, 0.8);
    }
    .where.ok { border-color: rgba(127, 211, 107, 0.45); color: #9fe08f; }
    .note { margin: -0.4vh 0 1.2vh; color: #f5c060; }
    .list { display: flex; flex-direction: column; gap: 1vh; }
    .item { display: flex; align-items: center; gap: 1.6vh; padding: 1.4vh 1.8vh; }
    .icon { width: 6vh; height: 6vh; border-radius: 1vh; background: rgba(255, 255, 255, 0.05); display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
    .icon img { max-width: 80%; max-height: 80%; }
    .info { flex: 1; min-width: 0; }
    .name { font-size: 1.7vh; font-weight: 600; margin-bottom: 0.4vh; }
    .demand { display: flex; align-items: center; gap: 1vh; margin-top: 0.6vh; }
    .bar { width: 14vh; height: 0.6vh; border-radius: 0.3vh; background: rgba(255, 255, 255, 0.08); overflow: hidden; }
    .bar div { height: 100%; }
    .sell { display: flex; flex-direction: column; gap: 0.8vh; align-items: flex-end; }
    .qty { width: 9vh; }
    .buttons .bm__btn { min-width: 12vh; text-align: center; }
</style>
