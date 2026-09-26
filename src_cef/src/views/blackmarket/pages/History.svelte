<script>
    import { onDestroy } from 'svelte'
    import { action, btc, usd } from '../api'

    export let data;

    let scope = "me";
    let entries = [];
    let loading = true;

    window.events.addEvent("blackmarket.history", (s, json) => {
        if (s !== scope) return;
        try {
            entries = typeof json === "string" ? JSON.parse(json) : json;
        } catch (e) {
            entries = [];
        }
        loading = false;
    });
    onDestroy(() => window.events.removeEvent("blackmarket.history"));

    const load = (s) => {
        scope = s;
        loading = true;
        entries = [];
        action("history", { scope: s });
    }
    load("me");

    const date = (d) => {
        const t = new Date(d);
        const pad = (n) => String(n).padStart(2, "0");
        return `${pad(t.getDate())}.${pad(t.getMonth() + 1)}.${t.getFullYear()} ${pad(t.getHours())}:${pad(t.getMinutes())}`;
    }
    const amountText = (e) => {
        const sign = e.amount > 0 ? "+" : "−";
        const abs = Math.abs(e.amount);
        return sign + (e.currency === "$" ? usd(abs) : btc(abs));
    }
</script>

<div class="bm__row" style="justify-content: space-between; margin-bottom: 1.6vh">
    <div class="bm__h1" style="margin: 0">История</div>
    {#if data.fraction}
        <div class="bm__row">
            <div class="bm__chip" class:active={scope === "me"} on:click={() => load("me")}>Мои операции</div>
            <div class="bm__chip" class:active={scope === "fraction"} on:click={() => load("fraction")}>Кошелёк банды</div>
        </div>
    {/if}
</div>

<div class="bm__scroll">
    {#if loading}
        <div class="bm__empty">Загрузка…</div>
    {:else}
        {#each entries as e}
            <div class="entry">
                <div class="bm__muted small date">{date(e.date)}</div>
                <div class="title">{e.title}</div>
                <div class="amount" class:plus={e.amount > 0}>{amountText(e)}</div>
            </div>
        {:else}
            <div class="bm__empty">Операций пока нет</div>
        {/each}
    {/if}
</div>

<style>
    .entry {
        display: grid;
        grid-template-columns: 16vh 1fr auto;
        align-items: center;
        gap: 1.4vh;
        padding: 1.1vh 1.2vh;
        border-bottom: 1px solid rgba(255, 255, 255, 0.05);
    }
    .title { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .amount { font-family: 'TTNorms-Bold'; color: #ff7a7a; }
    .amount.plus { color: #7fd36b; }
</style>
