<script>
    import { translateText } from 'lang'
    import { format } from 'api/formatter'
    import { serverDonatMultiplier } from 'store/server'
    import { validate } from 'api/validation';
    import Accessories from './clothes/accessories.svelte';
    import Armore from './clothes/armore.svelte';
    import Heat from './clothes/heat.svelte';
    import Outerwear from './clothes/outerwear.svelte';
    import Pants from './clothes/pants.svelte';
    import Shoes from './clothes/shoes.svelte';
    import Undergarments from './clothes/undergarments.svelte';
    import Mask from './clothes/mask.svelte';
    export let pageload;
    export let pagenameitem;
    export let SetPopup;
    export let pagefilter;
    export let searchText;

</script>
{#if pageload === 0 || pageload === 1 }
    <Mask {searchText} {pagefilter} {pagenameitem} {pageload} {SetPopup}/>
{/if}
{#if pageload === 0 || pageload === 2 }
    <Heat {searchText} {pagefilter} {pagenameitem} {pageload} {SetPopup}/>
{/if}
{#if pageload === 0 || pageload === 3 }
    <Accessories {searchText} {pagefilter} {pagenameitem} {pageload} {SetPopup}/>
{/if}
{#if pageload === 0 || pageload === 4 }
    <Outerwear {searchText} {pagefilter} {pagenameitem} {pageload} {SetPopup}/>
{/if}
{#if pageload === 0 || pageload === 5 }
    <Armore {searchText} {pagefilter} {pagenameitem} {pageload} {SetPopup}/>
{/if}
{#if pageload === 0 || pageload === 6 }
    <Undergarments {searchText} {pagefilter} {pagenameitem} {pageload} {SetPopup}/>
{/if}
{#if pageload === 0 || pageload === 7 }
    <Pants {searchText} {pagefilter} {pagenameitem} {pageload} {SetPopup}/>
{/if}
{#if pageload === 0 || pageload === 8 }
    <Shoes {searchText} {pagefilter} {pagenameitem} {pageload} {SetPopup}/>
{/if}