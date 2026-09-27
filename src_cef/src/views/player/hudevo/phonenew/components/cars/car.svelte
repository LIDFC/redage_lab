<script>
    import { sound, playSound } from 'api/uiSound'
    import { vehicleName } from '@/api/vehicleName';
    import { format } from 'api/formatter'
    import { executeClient, executeClientToGroup } from 'api/rage'
    import { fade } from 'svelte/transition'
    import { carState } from './data'

    export let selectedCar;
    export let OnUpdatePage;
    export let garage = null;
    export let reload;

    // Всё работает из любого места: находиться у гаража не нужно (сервер проверяет владельца, деньги и состояние машины)
    const functionData = [
        { name: 'Отметить в GPS', icon: '◎', func: "gps", isCarGarage: false, isPark: true },
        { name: 'Эвакуировать в гараж', icon: '⤓', func: "evac", isCarGarage: false, isPark: true, spawned: true },
        { name: 'Восстановить', icon: '✚', func: "repair", isPark: true, spawned: true },
        { name: 'Дубликат ключа', icon: '⚿', func: "key", sell: true },
        { name: 'Сменить замки', icon: '⟲', func: "changekey", sell: true, price: 100 },
        { name: 'Продать государству', icon: '$', func: "sell", sell: true, danger: true },
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

    const onEnterRent = (func) => {
        if (!window.loaderData.delay ("onVehicleAction", 1))
            return;
        executeClient ("client.rentcar.func", func);
        executeClientToGroup ("close")
    }

    $: state = carState(selectedCar, garage);
    $: actions = functionData.filter(f => isVisible(f, selectedCar));
</script>

<div class="auto__scroll" in:fade>
    <div class="auto__back" on:click={() => isSlots ? (isSlots = false) : OnUpdatePage ("List")}>‹ {isSlots ? 'К машине' : 'Все машины'}</div>

    <div class="auto__hero car">
        <div class="auto__hero_img" style="background-image: url('{document.cloud}inventoryItems/vehicle/{selectedCar.model.toLowerCase()}.png')"></div>
        <div class="auto__hero_name">{vehicleName(selectedCar.model)}</div>
        <div class="auto__vehicle_row">
            <span class="auto__plate">{selectedCar.number}</span>
            <span class="auto__owner">{selectedCar.header}</span>
        </div>
        <div class="auto__status {state.cls}"><i></i>{state.text}</div>
    </div>

    {#if selectedCar.isRent}
        <div class="auto__card list">
            <div class="auto__row" use:sound={"tap"} on:click={() => onEnterRent ("gpstrack")}><i>◎</i><span>Показать на карте</span></div>
            {#if !selectedCar.isJob}
                <div class="auto__row" use:sound={"tap"} on:click={() => onEnterRent ("datetime")}><i>⟲</i><span>Продлить аренду</span><em>${format("money", selectedCar.rentPrice)}</em></div>
            {/if}
            <div class="auto__row danger" on:click={() => onEnterRent ("stoprent")}><i>✕</i><span>Отказаться от аренды</span></div>
        </div>
    {:else if isSlots}
        <div class="auto__card">
            <div class="auto__card_title">Выберите место в гараже</div>
            <div class="auto__slots">
                {#each Array(garage.maxCars) as _, place}
                    <div class="auto__slot" use:sound={"tap"} class:current={place === selectedCar.place} class:busy={garage.slots[place] && place !== selectedCar.place} on:click={() => onPark (place)}>
                        <b>{place + 1}</b>
                        <span>{place === selectedCar.place ? 'эта машина' : garage.slots[place] ? vehicleName(garage.slots[place]) : 'свободно'}</span>
                    </div>
                {/each}
            </div>
            <div class="auto__hint">Если место занято, машины поменяются местами.</div>
        </div>
    {:else}
        <div class="auto__card list">
            {#if canPark}
                <div class="auto__row" use:sound={"tap"} on:click={() => isSlots = true}><i>P</i><span>Место в гараже</span><em>№{selectedCar.place + 1}</em></div>
            {/if}
            {#each actions as func}
                <div class="auto__row" use:sound={"tap"} class:danger={func.danger} on:click={() => onEnter (func, selectedCar)}>
                    <i>{func.icon}</i>
                    <span>{func.name}</span>
                    {#if func.func === "sell"}
                        <em>${format("money", selectedCar.sell)}</em>
                    {:else if func.price}
                        <em>${func.price}</em>
                    {/if}
                </div>
            {:else}
                {#if !canPark}
                    <div class="auto__empty">Для этой машины сейчас нет действий</div>
                {/if}
            {/each}
        </div>
    {/if}
</div>
