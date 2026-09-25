<script>
    import { onDestroy } from 'svelte';
    import { executeClient } from 'api/rage'
    import List from './cars/list.svelte';
    export let pageload;
    export let searchText;

    const categories = [
        { page: 1, title: "Автомобили", isHeli: false },
        { page: 2, title: "Вертолёты", isHeli: true },
    ];

    let vehicles = [];
    window.gta5devmenuDonateVehicles = (json) => {
        vehicles = JSON.parse(json);
    };
    onDestroy(() => {
        window.gta5devmenuDonateVehicles = () => {};
    });
    executeClient ("client.donate.vehicles.load");
</script>
{#each categories as category (category.page)}
    {#if pageload === 0 || pageload === category.page}
        <List {vehicles} {category} {searchText} {pageload}/>
    {/if}
{/each}
