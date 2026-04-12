<script>
    export let itemliststock;
    import { executeClient } from "api/rage";
    import { itemsInfo } from 'json/itemsInfo.js'
    import { getPng } from './getPng.js'

    let sortedItems = [];

    $: {
        sortedItems = itemliststock.slice().sort((a, b) => a.SqlId - b.SqlId);
    }

    const userstock = (item) => {
        executeClient ("client.gamemenu.inventory.stack", "other", item.Index, 2, 1);
        executeClient ("client.stockitems");
        executeClient ("client.stockitems");
        executeClient ("client.stockitems");
    }

</script>

<div class="list1">
    {#each sortedItems as item}
        <div class="blockrewrad1">
            <div class="headrewrad">
                <div class="line"></div>
                <span>{item.Count}</span>
            </div>
            <img src="{getPng(item, itemsInfo[item.ItemId])}" alt=""/>
            <p>{itemsInfo [item.ItemId].Name}</p>
            <div class="btnuse" on:keypress={() => {}} on:click={() => userstock(item)}>Забрать</div>
        </div>
    {/each}
</div>