<script>
    import { translateText } from 'lang'
    import { daysName, getPngUrl, getTypeToTitle, statusType, getTimeFromMins } from './data'
    import { itemsInfo } from 'json/itemsInfo.js'
    export let item;

</script>


<div class="blockrewrad">
    <div class="headrewrad">
        <div class="line"></div>
        <span>{daysName [item.day]}</span>
    </div>
    <img src="{getPngUrl (item)}" alt=""/>
    <p>{itemsInfo[item.itemId].Name} {item.desc}</p>
    {#if item.status === statusType.received}
        <div class="btnuse">{translateText('player1', 'Получено')}</div>
        {:else if item.status === statusType.skip}
            <div class="btnerror">{translateText('player1', 'Пропущено')}</div>
        {:else if item.status === statusType.receive}
            <div class="btnuse" on:keypress={() => {}} on:click>{translateText('player1', 'Забрать награду')}</div>
        {:else if item.status === statusType.time}
            <div class="btntime">{getTimeFromMins (item.time)}</div>
    {/if}
</div>