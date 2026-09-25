<script>
    import List from './clothes/list.svelte';
    import { categories } from './catalog.js';
    export let pageload;
    export let pagefilter;
    export let searchText;
</script>
{#each categories as category (category.page)}
    {#if pageload === 0 || pageload === category.page}
        <List {category} {searchText} {pagefilter} {pageload}/>
    {/if}
{/each}
