<script>
    import { translateText } from 'lang'
    import Days from './elements/days.svelte';
    export let onSetLoad;
    import { executeClient } from "api/rage";
    executeClient ("client.rewardslist.day.load");

    let rewardList = [];

    onSetLoad (true);
    const updateLoad = (json) => {
        onSetLoad (false);

        rewardList = JSON.parse(json)
    }

    import { addListernEvent } from 'api/functions';
    addListernEvent("rewardList.day.init", updateLoad);

    const onTake = (item) => {
        executeClient ("client.rewardslist.day.take", item.day);
    }
</script>

<div class="list1">
    {#each rewardList as item}
        <Days {item} on:click={() => onTake (item)}/>
    {/each}
</div>