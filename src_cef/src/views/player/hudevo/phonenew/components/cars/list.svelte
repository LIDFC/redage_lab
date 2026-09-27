<script>
    import { vehicleName } from '@/api/vehicleName';
    import { carState } from './data'
    import { currentPage } from '../../stores'
    import { executeClient, executeClientToGroup } from 'api/rage'
    import { onInputFocus, onInputBlur } from "@/views/player/hudevo/phonenew/data";
    import { onDestroy } from 'svelte'
    import { fade } from 'svelte/transition'

    export let OnUpdatePage;
    export let carsList = [];
    export let garage = null;

    let inputText = "";
    let category = "";

    $: categories = [...new Set(carsList.map(c => c.header).filter(Boolean))];
    $: filtered = carsList.filter(c => {
        if (category && c.header !== category)
            return false;
        const text = inputText.trim().toLowerCase();
        if (!text)
            return true;
        return c.number.toLowerCase().includes(text) || c.model.toLowerCase().includes(text) || vehicleName(c.model).toLowerCase().includes(text);
    });
    $: used = garage ? Object.keys(garage.slots || {}).length : 0;

    const setPointArenda = () => {
        executeClient ("gps.name", 'Ближайшая аренда авто');
        executeClientToGroup ("close");
    }

    onDestroy(() => onInputBlur ());
</script>

<div class="auto__scroll" in:fade>
    {#if garage}
        <div class="auto__garage">
            <div class="auto__garage_icon">P</div>
            <div class="auto__garage_info">
                <b>{garage.parking ? 'Парковочное место' : 'Гараж'}</b>
                <span>{garage.parking ? 'Машина стоит у дома' : `Занято ${used} из ${garage.maxCars}${garage.apartment ? ' · по классу квартиры' : ''}`}</span>
            </div>
            {#if !garage.parking}
                <div class="auto__meter"><div style="width: {Math.min(100, used / Math.max(1, garage.maxCars) * 100)}%"></div></div>
            {/if}
        </div>
    {/if}

    {#if carsList.length}
        <input type="text" class="auto__search" placeholder="Поиск по модели или номеру" bind:value={inputText} on:focus={onInputFocus} on:blur={onInputBlur}>
        {#if categories.length > 1}
            <div class="auto__chips">
                <div class:active={!category} on:click={() => category = ""}>Все</div>
                {#each categories as item}
                    <div class:active={category === item} on:click={() => category = item}>{item}</div>
                {/each}
            </div>
        {/if}

        {#each filtered as item}
            <div class="auto__vehicle" on:click={() => OnUpdatePage("Car", item)}>
                <div class="auto__vehicle_img" style="background-image: url('{document.cloud}inventoryItems/vehicle/{item.model.toLowerCase()}.png')"></div>
                <div class="auto__vehicle_info">
                    <b>{vehicleName(item.model)}</b>
                    <div class="auto__vehicle_row">
                        <span class="auto__plate small">{item.number}</span>
                        <span class="auto__owner">{item.header}</span>
                    </div>
                    <div class="auto__status {carState(item, garage).cls}"><i></i>{carState(item, garage).text}</div>
                </div>
                <div class="auto__chevron">›</div>
            </div>
        {:else}
            <div class="auto__empty">Ничего не найдено</div>
        {/each}
    {:else}
        <div class="auto__empty big">
            <b>Машин нет</b>
            <span>Можно взять машину в аренду или вызвать такси.</span>
        </div>
        <div class="auto__action primary" on:click={setPointArenda}>Найти аренду</div>
        <div class="auto__action" on:click={() => currentPage.set("taxi")}>Вызвать такси</div>
    {/if}
</div>
