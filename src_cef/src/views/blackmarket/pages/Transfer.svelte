<script>
    import { action, btc, toInt } from '../api'

    export let data;
    export let lastResult;

    let phone = "";
    let amount = "";
    $: available = (data.wallet?.balance || 0) - (data.wallet?.reserved || 0);
    $: n = toInt(amount);
    $: can = toInt(phone) > 0 && n > 0 && n <= available;

    const send = () => can && action("transfer", { phone: toInt(phone), amount: n });
    $: if (lastResult && lastResult.action === "transfer" && lastResult.ok) amount = "";
</script>

<div class="bm__h1">Перевод BTC</div>
<div class="bm__card box">
    <div class="bm__muted small" style="margin-bottom: 1.6vh">
        Перевод на кошелёк по номеру телефона получателя. Получатель увидит только сумму, без вашего имени.
    </div>
    <div class="bm__field">
        <div class="bm__label">Номер телефона получателя</div>
        <input class="bm__input" type="number" bind:value={phone} placeholder="Например, 5551234" />
    </div>
    <div class="bm__field">
        <div class="bm__label">Сумма, BTC (свободно {btc(available)})</div>
        <div class="bm__row">
            <input class="bm__input" style="flex: 1" type="number" min="1" bind:value={amount} />
            <div class="bm__btn" on:click={() => amount = String(available)}>Всё</div>
        </div>
    </div>
    <div class="bm__btn primary" class:disabled={!can} on:click={send}>Перевести</div>
    {#if lastResult && lastResult.action === "transfer"}
        <div class={lastResult.ok ? "bm__ok" : "bm__error"}>{lastResult.message}</div>
    {/if}
</div>

<style>
    .box { max-width: 60vh; padding: 2.4vh; }
</style>
