<script>
    import { vehicleName } from '@/api/vehicleName';
    import { TimeFormat } from 'api/moment'
    import { translateText } from 'lang'
    export let selectedCar;
    export let OnUpdatePage;

    import { format } from 'api/formatter'
    import {executeClient, executeClientAsyncToGroup, executeClientToGroup} from 'api/rage'

    export let garage = null;
    export let reload;

    // Всё работает из любого места: находиться у гаража больше не нужно (сервер проверяет владельца, деньги и состояние машины)
    const functionData = [
        {
            name: translateText('player2', 'Отметить в GPS'),
            func: "gps",
            isCarGarage: false,
            isPark: true
        },
        {
            name: 'Эвакуировать в гараж',
            func: "evac",
            isCarGarage: false,
            isPark: true,
            spawned: true
        },
        {
            name: translateText('player2', 'Восстановить'),
            func: "repair",
            isPark: true,
            spawned: true
        },
        {
            name: translateText('player2', 'Получить дубликат ключа'),
            func: "key",
            sell: true
        },
        {
            name: 'Сменить замки ($100)',
            func: "changekey",
            sell: true
        },
        {
            name: translateText('player2', 'Продать за $'),
            func: "sell",
            sell: true
        },
    ];

    const isVisible = (func, car) => {
        if (!func)
            return false;

        if (func.spawned && !car.isCreate)
            return false;

        if (func.sell && !car.sell)
            return false;

        if (!car.isAir) {
            if (func.isCarGarage != undefined && func.isCarGarage !== car.isCarGarage)
                return false;

            if (func.isPark != undefined && -1 === car.place)
                return false;

            if (func.func !== "gps" && func.func !== "key" && !!car.ticket)
                return false;
        }

        return true;
    }

    const onEnter = (func, car) => {
        if (!window.loaderData.delay ("onVehicleAction", 1))
            return;
        if (!car || !isVisible (func, car))
            return;

        executeClient ("client.vehicle.action", car.number, func.func);

        if (func.func === "sell")
            executeClientToGroup ("close")
        else
            reload ();
    }

    // Место в гараже (раньше — схема «Парковка» в меню дома)
    $: canPark = garage && !garage.parking && !selectedCar.isAir && !selectedCar.isRent && selectedCar.isCarGarage && selectedCar.place !== -1 && garage.maxCars > 1;
    let isSlots = false;
    const onPark = (place) => {
        if (place === selectedCar.place || !window.loaderData.delay ("onVehicleParking", 1))
            return;
        executeClientToGroup ("cars.parking", selectedCar.sqlId, place);
        isSlots = false;
    }
    import { fade } from 'svelte/transition'


    const onEnterRent = (func) => {
        if (!window.loaderData.delay ("onVehicleAction", 1))
            return;

        executeClient ("client.rentcar.func", func);

        executeClientToGroup ("close")
    }
</script>
<div class="newphone__rent_list" in:fade>
    <div class="newphone__rent_none vehicle">
        <div class="box-column">
            <div class="box-flex">
                <div class="orange">{vehicleName(selectedCar.model)}</div>
                <div class="newphone__rent_status">{selectedCar.header}</div>
            </div>
            {#if selectedCar.isRent && !selectedCar.isJob}
                <div class="gray">{translateText('player2', 'Продлено до')}:</div>
                <div class="date">
                    {TimeFormat (selectedCar.date, "H:mm DD.MM.YYYY")}
                </div>
            {:else}
                <div class="gray">{translateText('player2', 'Номер')}:</div>
                <div class="date">
                    {selectedCar.number}
                </div>
            {/if}
        </div>
        <div class="newphone__rent_noneimage rent" style="background-image: url('{document.cloud}inventoryItems/vehicle/{selectedCar.model.toLowerCase()}.png')"></div>
    </div>
   <!--{#if selectedCar.time == "Аренда"}
        <div class="newphone__project_button rent">Продлить аренду</div>
        <div class="newphone__project_button rent">Отказаться от аренды</div>
    {/if}
    <div class="newphone__project_button rent">Показать на карте</div>
    <div class="newphone__project_button rent">Продлить аренду</div>
    <div class="newphone__project_button rent">Отказаться от аренды</div>
    <div class="newphone__project_button rent">Показать на карте</div>-->
    {#if selectedCar.isRent}
        <div on:click={() => onEnterRent ("gpstrack")} class="newphone__project_button rent">{translateText('player2', 'Показать на карте')}</div>
        {#if !selectedCar.isJob}
        <div on:click={() => onEnterRent ("datetime")} class="newphone__project_button rent">{translateText('player2', 'Продлить время аренды за')} ${format("money", selectedCar.rentPrice)}</div>
        {/if}
        <div on:click={() => onEnterRent ("stoprent")} class="newphone__project_button rent">{translateText('player2', 'Отказаться от аренды')}</div>
    {:else}
        {#if isSlots}
            <div class="auto__title w-100">Выберите место в гараже</div>
            <div class="auto__slots">
                {#each Array(garage.maxCars) as _, place}
                    <div class="auto__slot" class:current={place === selectedCar.place} class:busy={garage.slots[place] && place !== selectedCar.place} on:click={() => onPark (place)}>
                        <b>{place + 1}</b>
                        <span>{place === selectedCar.place ? 'здесь' : garage.slots[place] ? vehicleName(garage.slots[place]) : 'свободно'}</span>
                    </div>
                {/each}
            </div>
            <div class="orange box-center m-top10" on:click={() => isSlots = false}>Отмена</div>
        {:else if canPark}
            <div on:click={() => isSlots = true} class="newphone__project_button rent">Место в гараже: {selectedCar.place + 1}</div>
        {/if}
        {#each functionData as func}
            {#if !isSlots && isVisible (func, selectedCar)}
                <div on:click={() => onEnter (func, selectedCar)} class="newphone__project_button rent">
                    {func.name}
                    {#if func.func == "sell"}
                        {format("money", selectedCar.sell)}
                    {/if}
                </div>
            {/if}
        {/each}
    {/if}
    <div class="orange box-center m-top10" on:click={() => OnUpdatePage ("List")}>{translateText('player2', 'Назад')}</div>
</div>