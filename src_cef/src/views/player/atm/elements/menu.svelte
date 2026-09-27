<script>
    export let onSelectMain;
    export let menuItem;
</script>
{#each menuItem as item, index}
    <div class="atm__tile" on:click={() => onSelectMain (index)}>
        <i class="atm__icon {item.icon}"></i>
        <p>{item.title}</p>
    </div>
{/each}
<div class="atm__tile exit" on:click={() => onSelectMain (-1)}>
    <i class="atm__icon exit"></i>
    <p>Забрать карту</p>
</div>
