<script>
    // Окно АЗС. Данные присылает сервер (Core/Businesses.cs, OpenPetrolMenu): цена, бак, остаток на станции.
    // Клиент: src_client/vehicle/petrol.js — события petrol / petrol.full / petrol.gov / closePetrol.
    import { executeClient } from 'api/rage'
    import { format } from 'api/formatter'
    import { charMoney } from 'store/chars'

    export let viewData;

    let data = { id: 0, price: 0, stock: 0, fuel: 0, tank: 0, money: 0, canGov: false, govLeft: 0, noFuel: false };
    $: if (viewData && typeof viewData === "string")
        data = { ...data, ...JSON.parse(viewData) };
    else if (viewData && typeof viewData === "object")
        data = { ...data, ...viewData };

    $: free = Math.max(0, data.tank - data.fuel);
    let liters = 0;
    let initialized = false;
    $: if (!initialized && data.tank) {
        liters = free;
        initialized = true;
    }
    $: liters = Math.max(0, Math.min(free, Math.round(Number(liters) || 0)));
    $: cost = liters * data.price;
    $: money = $charMoney || data.money;
    $: notEnough = cost > money;
    $: fuelPercent = data.tank ? data.fuel / data.tank * 100 : 0;
    $: addPercent = data.tank ? liters / data.tank * 100 : 0;
    $: noVehicle = !data.tank;

    const setPart = (part) => liters = Math.round(free * part);

    const fill = () => {
        if (!liters || noVehicle) return;
        if (liters >= free)
            executeClient('petrol.full');
        else
            executeClient('petrol', liters);
    }
    const gov = () => executeClient('petrol.gov');
    const close = () => executeClient('closePetrol');

    const onKey = (e) => {
        if (e.keyCode === 27) close();
        else if (e.keyCode === 13) fill();
    }

    window.petrol = { reset: () => liters = 0 };
</script>

<svelte:window on:keyup={onKey} />

<div class="gs">
    <div class="gs__panel">
        <div class="gs__head">
            <div class="gs__logo">
                <svg viewBox="0 0 24 24"><path d="M4 21V5a2 2 0 0 1 2-2h7a2 2 0 0 1 2 2v16M3 21h13M4 10h11M15 8h2a2 2 0 0 1 2 2v7a1.5 1.5 0 0 0 3 0V8l-3-3" /></svg>
            </div>
            <div class="gs__heading">
                <div class="gs__title">Заправочная станция</div>
                <div class="gs__subtitle">АЗС №{data.id} · в резервуаре {format("money", data.stock)} л</div>
            </div>
            <div class="gs__price">
                <span>${format("money", data.price)}</span>за литр
            </div>
        </div>

        {#if noVehicle}
            <div class="gs__empty">Подъедьте к колонке на машине и заглушите двигатель</div>
        {:else if data.noFuel}
            <div class="gs__empty">Этот транспорт не заправляется бензином</div>
        {:else}
            <div class="gs__tank">
                <div class="gs__tank-top">
                    <span>Бак</span>
                    <b>{data.fuel} <i>+ {liters}</i> / {data.tank} л</b>
                </div>
                <div class="gs__gauge">
                    <div class="gs__gauge-fuel" style="width: {fuelPercent}%"></div>
                    <div class="gs__gauge-add" style="left: {fuelPercent}%; width: {addPercent}%"></div>
                    {#each [25, 50, 75] as mark}
                        <div class="gs__gauge-mark" style="left: {mark}%"></div>
                    {/each}
                </div>
            </div>

            <div class="gs__amount">
                <div class="gs__label">Сколько литров залить</div>
                <div class="gs__amount-row">
                    <div class="gs__step" on:click={() => liters = liters - 1}>−</div>
                    <input class="gs__input" type="number" min="0" max={free} bind:value={liters} />
                    <div class="gs__step" on:click={() => liters = liters + 1}>+</div>
                </div>
                <input class="gs__range" type="range" min="0" max={free} bind:value={liters} style="--fill: {free ? liters / free * 100 : 0}%" />
                <div class="gs__quick">
                    <div on:click={() => setPart(0.25)}>25%</div>
                    <div on:click={() => setPart(0.5)}>50%</div>
                    <div on:click={() => setPart(0.75)}>75%</div>
                    <div class="accent" on:click={() => setPart(1)}>Полный бак</div>
                </div>
            </div>

            <div class="gs__total">
                <div>
                    <div class="gs__label">К оплате</div>
                    <div class="gs__cost" class:bad={notEnough}>${format("money", cost)}</div>
                </div>
                <div class="gs__balance">
                    <div class="gs__label">Наличные</div>
                    <div>${format("money", money)}</div>
                </div>
            </div>
            {#if notEnough}
                <div class="gs__warn">Не хватает наличных — уменьшите количество литров</div>
            {/if}
        {/if}

        <div class="gs__buttons">
            <div class="gs__btn" on:click={close}>Отмена <span>ESC</span></div>
            {#if data.canGov && !noVehicle}
                <div class="gs__btn gov" on:click={gov}>За счёт штата</div>
            {/if}
            <div class="gs__btn primary" class:disabled={!liters || notEnough || noVehicle || data.noFuel} on:click={fill}>Заправить <span>Enter</span></div>
        </div>
    </div>
</div>

<style>
    .gs {
        position: absolute;
        inset: 0;
        z-index: 1000;
        display: flex;
        align-items: center;
        justify-content: flex-end;
        padding-right: 8vh;
        background: linear-gradient(90deg, rgba(1, 14, 26, 0) 35%, rgba(1, 14, 26, 0.85) 100%);
        color: white;
        font-family: 'TTNorms-Regular';
        font-size: 1.5vh;
    }
    .gs__panel {
        width: 52vh;
        padding: 2.6vh;
        border-radius: 1.6vh;
        background: rgba(8, 24, 38, 0.94);
        border: 1px solid rgba(255, 255, 255, 0.08);
        box-shadow: 0 2vh 5vh rgba(0, 0, 0, 0.45);
    }
    .gs__head {
        display: flex;
        align-items: center;
        gap: 1.4vh;
        padding-bottom: 2vh;
        border-bottom: 1px solid rgba(255, 255, 255, 0.08);
        margin-bottom: 2vh;
    }
    .gs__logo {
        width: 5vh;
        height: 5vh;
        border-radius: 1.1vh;
        background: #F5A623;
        display: flex;
        align-items: center;
        justify-content: center;
        flex-shrink: 0;
    }
    .gs__logo svg {
        width: 60%;
        height: 60%;
        fill: none;
        stroke: #1b1206;
        stroke-width: 2;
        stroke-linecap: round;
        stroke-linejoin: round;
    }
    .gs__heading {
        flex: 1;
    }
    .gs__title {
        font-family: 'TTNorms-Bold';
        font-size: 2.3vh;
    }
    .gs__subtitle, .gs__label {
        color: rgba(255, 255, 255, 0.55);
        font-size: 1.35vh;
    }
    .gs__price {
        text-align: right;
        color: rgba(255, 255, 255, 0.55);
        font-size: 1.2vh;
        display: flex;
        flex-direction: column;
    }
    .gs__price span {
        font-family: 'TTNorms-Bold';
        font-size: 2.4vh;
        color: #F5A623;
    }
    .gs__empty {
        padding: 4vh 0;
        text-align: center;
        color: rgba(255, 255, 255, 0.65);
        font-size: 1.7vh;
    }
    .gs__tank-top {
        display: flex;
        justify-content: space-between;
        margin-bottom: 0.8vh;
        color: rgba(255, 255, 255, 0.6);
    }
    .gs__tank-top b {
        color: white;
        font-family: 'TTNorms-Bold';
    }
    .gs__tank-top i {
        font-style: normal;
        color: #F5A623;
    }
    .gs__gauge {
        position: relative;
        height: 2vh;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.08);
        overflow: hidden;
    }
    .gs__gauge-fuel {
        position: absolute;
        left: 0;
        top: 0;
        bottom: 0;
        background: linear-gradient(90deg, #2BB6A8, #37d3c2);
    }
    .gs__gauge-add {
        position: absolute;
        top: 0;
        bottom: 0;
        background: repeating-linear-gradient(135deg, #F5A623 0 0.8vh, #d98c12 0.8vh 1.6vh);
        transition: width 0.15s;
    }
    .gs__gauge-mark {
        position: absolute;
        top: 0;
        bottom: 0;
        width: 1px;
        background: rgba(255, 255, 255, 0.25);
    }
    .gs__amount {
        margin-top: 2.4vh;
    }
    .gs__amount-row {
        display: flex;
        gap: 1vh;
        margin: 0.8vh 0 1.2vh;
    }
    .gs__step {
        width: 5vh;
        display: flex;
        align-items: center;
        justify-content: center;
        border-radius: 0.8vh;
        background: rgba(255, 255, 255, 0.08);
        font-size: 2.4vh;
        cursor: pointer;
    }
    .gs__step:hover {
        background: rgba(255, 255, 255, 0.15);
    }
    .gs__input {
        flex: 1;
        height: 5vh;
        border-radius: 0.8vh;
        border: 1px solid rgba(255, 255, 255, 0.12);
        background: rgba(255, 255, 255, 0.04);
        color: white;
        text-align: center;
        font-family: 'TTNorms-Bold';
        font-size: 2.4vh;
        outline: none;
    }
    .gs__input::-webkit-inner-spin-button {
        -webkit-appearance: none;
    }
    .gs__range {
        -webkit-appearance: none;
        width: 100%;
        height: 0.6vh;
        border-radius: 0.3vh;
        background: linear-gradient(90deg, #F5A623 var(--fill), rgba(255, 255, 255, 0.12) var(--fill));
        outline: none;
        cursor: pointer;
    }
    .gs__range::-webkit-slider-thumb {
        -webkit-appearance: none;
        width: 2vh;
        height: 2vh;
        border-radius: 50%;
        background: white;
        border: 0.3vh solid #F5A623;
    }
    .gs__quick {
        display: grid;
        grid-template-columns: 1fr 1fr 1fr 1.6fr;
        gap: 0.8vh;
        margin-top: 1.4vh;
    }
    .gs__quick div {
        text-align: center;
        padding: 0.9vh 0;
        border-radius: 0.7vh;
        background: rgba(255, 255, 255, 0.07);
        cursor: pointer;
    }
    .gs__quick div:hover {
        background: rgba(255, 255, 255, 0.14);
    }
    .gs__quick .accent {
        border: 1px solid rgba(245, 166, 35, 0.6);
        color: #F5A623;
    }
    .gs__total {
        display: flex;
        justify-content: space-between;
        align-items: flex-end;
        margin-top: 2.4vh;
        padding: 1.6vh;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.04);
    }
    .gs__cost {
        font-family: 'TTNorms-Bold';
        font-size: 3.4vh;
        color: #2BB6A8;
    }
    .gs__cost.bad {
        color: #ff6b6b;
    }
    .gs__balance {
        text-align: right;
        font-family: 'TTNorms-Bold';
        font-size: 1.9vh;
    }
    .gs__warn {
        margin-top: 1vh;
        color: #ff8a8a;
        font-size: 1.35vh;
    }
    .gs__buttons {
        display: flex;
        gap: 1vh;
        margin-top: 2.2vh;
    }
    .gs__btn {
        flex: 1;
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 0.8vh;
        padding: 1.3vh 0;
        border-radius: 0.9vh;
        background: rgba(255, 255, 255, 0.08);
        cursor: pointer;
        white-space: nowrap;
    }
    .gs__btn:hover {
        background: rgba(255, 255, 255, 0.15);
    }
    .gs__btn span {
        font-size: 1.1vh;
        padding: 0.3vh 0.6vh;
        border-radius: 0.4vh;
        background: rgba(255, 255, 255, 0.15);
    }
    .gs__btn.primary {
        flex: 1.4;
        background: #F5A623;
        color: #1b1206;
        font-family: 'TTNorms-Bold';
    }
    .gs__btn.primary span {
        background: rgba(0, 0, 0, 0.15);
    }
    .gs__btn.primary:hover {
        background: #ffb940;
    }
    .gs__btn.gov {
        background: rgba(43, 182, 168, 0.2);
        border: 1px solid rgba(43, 182, 168, 0.6);
    }
    .gs__btn.disabled {
        opacity: 0.45;
        pointer-events: none;
    }
</style>
