<script>
    import { action, btc, usd, toInt } from '../api'

    export let data;
    export let lastResult;

    $: rate = Number(data.config?.rate) || 0;
    $: launderFee = Number(data.config?.launderFee) || 0;
    $: cashoutFee = Number(data.config?.cashoutFee) || 0;
    $: wantedChance = Number(data.config?.wantedChance) || 0;
    $: atPoint = !!data.cashout?.atPoint;
    $: bag = Number(data.cashout?.bag) || 0;
    $: available = (data.wallet?.balance || 0) - (data.wallet?.reserved || 0);

    // Сумка → BTC
    $: bagGross = rate > 0 ? Math.floor(bag / rate) : 0;
    $: bagFee = Math.ceil(bagGross * launderFee / 100);

    // BTC → $
    let amount = "";
    $: n = Math.min(Math.max(0, toInt(amount)), available);
    $: outFee = Math.ceil(n * cashoutFee / 100);
    $: outUsd = Math.floor((n - outFee) * rate);

    $: if (lastResult && lastResult.ok && lastResult.action === "cashout") amount = "";
    $: launderHint = !atPoint ? "Подойдите к Мавру" : bag <= 0 ? "Наденьте сумку с деньгами после ограбления" : "";
    $: cashoutHint = !atPoint ? "Подойдите к Мавру" : available <= 0 ? "Нет свободных BTC" : n <= 0 ? "Введите сумму BTC" : outUsd <= 0 ? "Слишком маленькая сумма" : "";
</script>

<div class="bm__h1">Обнал</div>
<div class="bm__card bm__row where" class:ok={atPoint}>
    <div style="flex: 1">
        {#if atPoint}
            Вы у Мавра — можно работать.
        {:else}
            Обнал проводит только Мавр. Приезжайте к нему лично — приложение подскажет дорогу.
        {/if}
    </div>
    <div class="bm__btn" on:click={() => action("refresh")}>Обновить</div>
    {#if !atPoint}
        <div class="bm__btn" on:click={() => action("cashoutGps")}>Метка на карте</div>
    {/if}
</div>

<div class="grid">
    <div class="bm__card box">
        <div class="bm__muted">Грязные деньги → BTC</div>
        <div class="bm__muted small" style="margin: 0.6vh 0 1.4vh">
            Сумка с деньгами после ограбления (на спине) превращается в крипту. Комиссия {launderFee}%.
        </div>
        <div class="bm__line"><span class="bm__muted">В сумке</span><span>{bag > 0 ? usd(bag) : "сумки нет"}</span></div>
        <div class="bm__line"><span class="bm__muted">Курс</span><span>1 BTC = {usd(rate)}</span></div>
        <div class="bm__line"><span class="bm__muted">Комиссия</span><span>{btc(bagFee)}</span></div>
        <div class="bm__line"><span>Получите</span><span class="bm__price">{btc(Math.max(0, bagGross - bagFee))}</span></div>
        <div class="bm__btn primary" style="margin-top: 1.6vh" class:disabled={!atPoint || bag <= 0 || bagGross - bagFee <= 0} on:click={() => action("launder")}>Отмыть в крипту</div>
        {#if launderHint}<div class="bm__muted small hint">{launderHint}</div>{/if}
    </div>

    <div class="bm__card box">
        <div class="bm__muted">BTC → наличные</div>
        <div class="bm__muted small" style="margin: 0.6vh 0 1.4vh">
            Комиссия {cashoutFee}%. Риск: с вероятностью {wantedChance}% вас заметят и объявят в розыск.
        </div>
        <div class="bm__field">
            <div class="bm__label">Сколько BTC обналичить (свободно {btc(available)})</div>
            <div class="bm__row">
                <input class="bm__input" style="flex: 1" type="number" min="1" bind:value={amount} />
                <div class="bm__btn" on:click={() => amount = String(available)}>Всё</div>
            </div>
        </div>
        <div class="bm__line"><span class="bm__muted">Комиссия</span><span>{btc(outFee)}</span></div>
        <div class="bm__line"><span>Получите наличными</span><span class="bm__price">{usd(Math.max(0, outUsd))}</span></div>
        <div class="bm__btn gold" style="margin-top: 1.6vh" class:disabled={!atPoint || n <= 0 || outUsd <= 0} on:click={() => action("cashout", { btc: n })}>Обналичить</div>
        {#if cashoutHint}<div class="bm__muted small hint">{cashoutHint}</div>{/if}
    </div>
</div>

<style>
    .where {
        margin-bottom: 1.4vh;
        border-color: rgba(216, 57, 75, 0.35);
        color: rgba(255, 255, 255, 0.8);
    }
    .where.ok { border-color: rgba(127, 211, 107, 0.45); color: #9fe08f; }
    .grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 1.4vh;
    }
    .box { padding: 2vh; }
    .hint { margin-top: 0.8vh; text-align: center; }
</style>
