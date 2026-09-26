<script>
    import { action, itemIcon, timeLeft } from '../api'

    export let data;
    $: drops = data.drops || [];
</script>

<div class="bm__h1">Заказы</div>
<div class="bm__muted small" style="margin-bottom: 1.6vh">
    Купленный товар ждёт в закладке. Метка на карте видна только вам, но забрать закладку может любой, кто окажется там первым.
    Если выйти из игры, закладка пролежит не больше 30 минут.
</div>

<div class="bm__scroll">
    {#each drops as drop (drop.id)}
        <div class="bm__card bm__row order">
            <div class="bm__icon" style="background-image: url({itemIcon(drop.itemId)})"></div>
            <div style="flex: 1">
                <div class="name">{drop.name} × {drop.count}</div>
                <div class="bm__muted small">Исчезнет через {timeLeft(drop.minutesLeft)}</div>
            </div>
            <div class="bm__btn primary" on:click={() => action("gps", { id: drop.id })}>Метка на карте</div>
        </div>
    {:else}
        <div class="bm__empty">Активных закладок нет</div>
    {/each}
</div>

<style>
    .order { margin-bottom: 1vh; gap: 1.4vh; }
    .name { font-family: 'TTNorms-Bold'; }
</style>
