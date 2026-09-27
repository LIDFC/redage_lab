<script>
    import { action, btc, usd, toInt } from '../api'

    export let data;
    export let lastResult;

    $: available = (data.wallet?.balance || 0) - (data.wallet?.reserved || 0);
    $: rate = Number(data.config?.rate) || 0;

    let exchangeBtc = "";
    $: exN = toInt(exchangeBtc);
    const exchange = () => exN > 0 && action("exchange", { btc: exN });

    let depositAmount = "";
    let withdrawAmount = "";
    const deposit = () => toInt(depositAmount) > 0 && action("fDeposit", { amount: toInt(depositAmount) });
    const withdraw = () => toInt(withdrawAmount) > 0 && action("fWithdraw", { amount: toInt(withdrawAmount) });

    $: if (lastResult && lastResult.ok) {
        if (lastResult.action === "exchange") exchangeBtc = "";
        if (lastResult.action === "fDeposit") depositAmount = "";
        if (lastResult.action === "fWithdraw") withdrawAmount = "";
    }
</script>

<div class="bm__h1">Кошельки</div>
<div class="grid">
    <div class="bm__card box">
        <div class="bm__muted">Личный кошелёк</div>
        <div class="big">{btc(data.wallet?.balance || 0)}</div>
        <div class="bm__line"><span class="bm__muted">Свободно</span><span>{btc(available)}</span></div>
        <div class="bm__line"><span class="bm__muted">В P2P-заявках</span><span>{btc(data.wallet?.reserved || 0)}</span></div>
    </div>

    <div class="bm__card box">
        <div class="bm__muted">Обменник</div>
        {#if rate > 0}
            <div class="big small">1 BTC = {usd(rate)}</div>
            <div class="bm__field" style="margin-top: 1.2vh">
                <div class="bm__label">Купить BTC за наличные</div>
                <input class="bm__input" type="number" min="1" bind:value={exchangeBtc} placeholder="Сколько BTC" />
            </div>
            <div class="bm__line"><span class="bm__muted">К оплате</span><span class="bm__price">{usd(Math.ceil(exN * rate))}</span></div>
            <div class="bm__btn gold" style="margin-top: 1.2vh" class:disabled={exN <= 0} on:click={exchange}>Купить BTC</div>
        {:else}
            <div class="bm__empty">Обменник сейчас закрыт</div>
        {/if}
    </div>

    {#if data.fraction}
        <div class="bm__card box wide">
            <div class="bm__row" style="justify-content: space-between">
                <div>
                    <div class="bm__muted">Кошелёк банды · {data.fraction.name}</div>
                    <div class="big">{btc(data.fraction.balance)}</div>
                </div>
                <div class="bm__muted small" style="text-align: right">
                    Ваш ранг: {data.fraction.rank}<br />
                    Оплата и вывод — с {data.fraction.needRank} ранга<br />
                    Комиссия при оплате: {data.config?.fractionFee}%
                </div>
            </div>
            <div class="bm__row" style="margin-top: 1.4vh; align-items: flex-end">
                <div class="bm__field" style="flex: 1; margin: 0">
                    <div class="bm__label">Внести из личного кошелька</div>
                    <input class="bm__input" type="number" min="1" bind:value={depositAmount} />
                </div>
                <div class="bm__btn primary" class:disabled={toInt(depositAmount) <= 0} on:click={deposit}>Внести</div>
                <div class="bm__field" style="flex: 1; margin: 0 0 0 2vh">
                    <div class="bm__label">Вывести себе</div>
                    <input class="bm__input" type="number" min="1" bind:value={withdrawAmount} />
                </div>
                <div class="bm__btn" class:disabled={!data.fraction.canPay || toInt(withdrawAmount) <= 0} on:click={withdraw}>Вывести</div>
            </div>
        </div>
    {/if}
</div>

<style>
    .grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 1.4vh;
    }
    .box { padding: 2vh; }
    .wide { grid-column: 1 / -1; }
    .big {
        font-family: 'TTNorms-Bold';
        font-size: 3vh;
        color: #f2b544;
        margin: 0.6vh 0 1.2vh;
    }
    .big.small { font-size: 2vh; color: white; }
</style>
