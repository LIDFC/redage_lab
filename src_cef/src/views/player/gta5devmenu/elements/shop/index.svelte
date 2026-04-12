<script>
    import { translateText } from 'lang'
    import './assets/main.sass';
	import { fade } from 'svelte/transition';
    import { accountRedbucks } from 'store/account'
    import { serverDateTime, serverDonatMultiplier } from 'store/server'
    import { format } from 'api/formatter'
    import { executeClient } from 'api/rage'
    import { TimeFormat } from 'api/moment'
    import Shop from './elements/shop/index.svelte';
    import Shopcl from './elements/shopcl/index.svelte'
    import Shopcar from './elements/shopcar/index.svelte'
    import Money from './elements/money/index.svelte';
    import Personal from './elements/personal/index.svelte';

    import Roulette from './elements/roulette/index.svelte';
    import Cases from './elements/roulette/list.svelte';

    import Premium from './elements/premium/index.svelte';
    export let selectView;
    export let searchText;
    export let pageload;
    export let pagefilter;

    import PopupChange from './popup/popupchange.svelte';
    import PopupPayment from './popup/popuppayment.svelte';
    import PopupPPopup from './elements/shop/pPopup.svelte';
    import PopupCPopup from './elements/shop/cPopup.svelte';
    import PopupNomer from './popup/popupnomer.svelte';
    import PopupName from './elements/shop/popupname.svelte';
    import PopupSim from './popup/popupsim.svelte';
    import PopupInfo from './popup/popupinfo.svelte';

    let Popups = {
        PopupChange,
        PopupName,
        PopupPPopup,
        PopupInfo,
        PopupSim,
        PopupPayment,
        PopupCPopup,
        PopupNomer
    }

    let SelectPopup = "";
    let SelectPopupData = null;
    let AreaPopup = false;
    
    let pagenameitem = ""

    window.pagenameitemf2 = (wiew) => {
        pagenameitem = "";
        setTimeout(() => {
            pagenameitem = wiew;
        }, 1);
    }

    let onSelectedView1;
    if (selectView === "Shop") {
        onSelectedView1 = "Shop";
    } else if (selectView === "Personal") {
        onSelectedView1 = "Personal";
    } else if (selectView === "Shopcl") {
        onSelectedView1 = "Shopcl";
    } else if (selectView === "Shopcar") {
        onSelectedView1 = "Shopcar";
    } else if (selectView === "Money") {
        onSelectedView1 = "Money";
    } else if (selectView === "Roulette") {
        onSelectedView1 = "Roulette";
    } else if (selectView === "Cases") {
        onSelectedView1 = "Cases";
    } else if (selectView === "Premium") {
        onSelectedView1 = "Premium";
    }

    const SetPopup = (popup = null, data = null) => {
        if (popup === -1 && AreaPopup)
            return;
        SelectPopupData = data;
        SelectPopup = popup;
    }

    const SetView = (view) => {
        selectView = view;
    }

    let isLoad = false;
    const getData = () => {
        isLoad = true;
    }

    executeClient ("client.donate.load");
    import { addListernEvent } from 'api/functions';
    addListernEvent ("donate.init", getData)
</script>


{#if Popups[SelectPopup]}
    <svelte:component this={Popups[SelectPopup]} {SetPopup} popupData={SelectPopupData} on:mouseenter={e => AreaPopup = true} on:mouseleave={e => AreaPopup = false} />
{/if}
{#if onSelectedView1 === "Shop"}
<Shop {onSelectedView1} {SetPopup} {selectView} {SetView} isPopup={Popups[SelectPopup]} />
{/if}
{#if onSelectedView1 === "Shopcl"}
<Shopcl {searchText} {pagenameitem} {pagefilter} {pageload} {onSelectedView1} {SetPopup} {selectView} {SetView} isPopup={Popups[SelectPopup]} />
{/if}
{#if onSelectedView1 === "Shopcar"}
<Shopcar {searchText} {pagenameitem} {pageload} {onSelectedView1} {SetPopup} {selectView} {SetView} isPopup={Popups[SelectPopup]} />
{/if}
{#if onSelectedView1 === "Personal"}
<Personal {onSelectedView1} {SetPopup} {selectView} {SetView} isPopup={Popups[SelectPopup]} />
{/if}
{#if onSelectedView1 === "Roulette"}
<Roulette {onSelectedView1} {SetPopup} {selectView} {SetView} isPopup={Popups[SelectPopup]} />
{/if}
{#if onSelectedView1 === "Cases"}
<Cases {onSelectedView1} {SetPopup} {selectView} {SetView} isPopup={Popups[SelectPopup]} />
{/if}
{#if onSelectedView1 === "Money"}
<Money {onSelectedView1} {SetPopup} {selectView} {SetView} isPopup={Popups[SelectPopup]} />
{/if}
{#if onSelectedView1 === "Premium"}
<Premium {onSelectedView1} {SetPopup} {selectView} {SetView} isPopup={Popups[SelectPopup]} />
{/if}
