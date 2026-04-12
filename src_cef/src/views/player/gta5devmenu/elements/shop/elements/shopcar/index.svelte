<script>
    import { translateText } from 'lang'
    import { format } from 'api/formatter'
    import { serverDonatMultiplier } from 'store/server'
    import { validate } from 'api/validation';
    import Cars from './cars/cars.svelte';
    import Helicopter from './cars/helicopter.svelte';
    export let pageload;
    export let pagenameitem;
    export let SetPopup;
    export let searchText;

</script>
{#if pageload === 0 || pageload === 1 }
    <Cars {searchText} {pagenameitem} {pageload} {SetPopup}/>
{/if}
{#if pageload === 0 || pageload === 2 }
    <Helicopter {searchText} {pagenameitem} {pageload} {SetPopup}/>
{/if}