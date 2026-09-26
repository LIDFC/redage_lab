<script>
    // Вкладка «Многоквартирные дома»: список домов → квартиры по этажам → выбор квартиры (покупка — кнопкой Enter / «Купить»)
    import { houseType } from 'json/realEstate.js'
    import { format } from 'api/formatter'
    import interiorImage from '@/views/player/help/images/interior3.jpg'

    export let apartmentsData = [];
    export let onSelectData;
    export let selectedBuildingId = null;

    let classFilter = -1;

    $: building = apartmentsData.find(b => b.id === selectedBuildingId) || null;
    $: flats = building ? building.flats.filter(f => classFilter === -1 || f.type === classFilter) : [];
    $: floors = [...new Set(flats.map(f => f.floor))].sort((a, b) => b - a);
    $: classes = building ? [...new Set(building.flats.map(f => f.type))].sort((a, b) => a - b) : [];

    const freeCount = (b) => b.flats.filter(f => f.isFree).length;
    const minPrice = (b) => {
        const free = b.flats.filter(f => f.isFree);
        return free.length ? Math.min(...free.map(f => f.price)) : 0;
    }

    const openBuilding = (b) => {
        selectedBuildingId = b.id;
        classFilter = -1;
    }

    const selectFlat = (flat) => {
        if (!flat.isFree)
            return;
        onSelectData({ ...flat, buildingName: building.name, address: building.address }, "apartment");
    }
</script>

<div class="apt">
    {#if !building}
        <div class="apt__title">Квартиры в многоквартирных домах</div>
        <div class="apt__subtitle">Квартира — как дом: свой интерьер, гараж, сожители и налог. Вход общий, через подъезд.</div>
        <div class="apt__buildings">
            {#each apartmentsData as b}
                <div class="apt__building" on:click={() => openBuilding(b)}>
                    <div class="apt__building-image" style="background-image: url({interiorImage})">
                        <div class="apt__badge" class:empty={!freeCount(b)}>
                            {freeCount(b) ? `Свободно ${freeCount(b)} из ${b.flats.length}` : "Все квартиры проданы"}
                        </div>
                    </div>
                    <div class="apt__building-body">
                        <div class="apt__building-name">{b.name}</div>
                        <div class="apt__gray">{b.address}</div>
                        <div class="apt__building-footer">
                            <span class="apt__gray">Этажей: <span class="apt__white">{b.floors}</span></span>
                            {#if freeCount(b)}
                                <span class="apt__price">от ${format("money", minPrice(b))}</span>
                            {/if}
                        </div>
                    </div>
                </div>
            {:else}
                <div class="apt__empty">Многоквартирных домов пока нет</div>
            {/each}
        </div>
    {:else}
        <div class="apt__head">
            <div class="apt__back" on:click={() => selectedBuildingId = null}>← Все дома</div>
            <div>
                <div class="apt__title left">{building.name}</div>
                <div class="apt__gray">{building.address} · свободно {freeCount(building)} из {building.flats.length}</div>
            </div>
        </div>
        <div class="apt__filters">
            <div class="apt__chip" class:active={classFilter === -1} on:click={() => classFilter = -1}>Все</div>
            {#each classes as c}
                <div class="apt__chip" class:active={classFilter === c} on:click={() => classFilter = c}>{houseType[c]}</div>
            {/each}
        </div>
        <div class="apt__floors">
            {#each floors as floor}
                <div class="apt__floor">
                    <div class="apt__floor-label">{floor}<span>этаж</span></div>
                    <div class="apt__flats">
                        {#each flats.filter(f => f.floor === floor) as flat}
                            <div class="apt__flat" class:sold={!flat.isFree} on:click={() => selectFlat(flat)}>
                                <div class="apt__flat-top">
                                    <span class="apt__flat-number">Кв. {flat.number}</span>
                                    <span class="apt__flat-class">{houseType[flat.type]}</span>
                                </div>
                                {#if flat.isFree}
                                    <div class="apt__price">${format("money", flat.price)}</div>
                                {:else}
                                    <div class="apt__gray">{flat.isAuction ? "На аукционе" : "Продана"}</div>
                                {/if}
                                <div class="apt__flat-meta">
                                    <span>Гараж: {flat.cars}</span>
                                    <span>Жильцов: до {flat.maxRoommates + 1}</span>
                                </div>
                            </div>
                        {/each}
                    </div>
                </div>
            {/each}
        </div>
    {/if}
</div>

<style>
    .apt {
        position: relative;
        z-index: 1;
        width: 100%;
        height: 100%;
        display: flex;
        flex-direction: column;
        overflow: hidden;
    }
    .apt__title {
        font-family: 'TTNorms-Bold';
        font-size: 2.6vh;
        text-align: center;
    }
    .apt__title.left {
        text-align: left;
        line-height: 1.2;
        margin-bottom: 0.3vh;
    }
    .apt__subtitle {
        text-align: center;
        color: rgba(255, 255, 255, 0.6);
        font-size: 1.5vh;
        margin: 0.8vh 0 2.4vh;
    }
    .apt__gray {
        color: rgba(255, 255, 255, 0.55);
        font-size: 1.4vh;
    }
    .apt__white {
        color: white;
    }
    .apt__price {
        font-family: 'TTNorms-Bold';
        color: #2BB6A8;
        font-size: 1.8vh;
    }
    .apt__buildings {
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: 1.6vh;
        overflow-y: auto;
        padding-right: 0.6vh;
    }
    .apt__building {
        background: rgba(255, 255, 255, 0.06);
        border: 1px solid rgba(255, 255, 255, 0.08);
        border-radius: 1vh;
        overflow: hidden;
        cursor: pointer;
        transition: transform 0.15s, border-color 0.15s, background 0.15s;
    }
    .apt__building:hover {
        transform: translateY(-0.3vh);
        border-color: rgba(43, 182, 168, 0.8);
        background: rgba(255, 255, 255, 0.09);
    }
    .apt__building-image {
        height: 12vh;
        background-size: cover;
        background-position: center;
        position: relative;
    }
    .apt__building-image::after {
        content: "";
        position: absolute;
        inset: 0;
        background: linear-gradient(180deg, rgba(1, 22, 39, 0) 30%, rgba(1, 22, 39, 0.85) 100%);
    }
    .apt__badge {
        position: absolute;
        z-index: 1;
        left: 1vh;
        bottom: 1vh;
        padding: 0.4vh 0.9vh;
        border-radius: 0.5vh;
        font-size: 1.3vh;
        background: rgba(43, 182, 168, 0.9);
    }
    .apt__badge.empty {
        background: rgba(255, 255, 255, 0.2);
    }
    .apt__building-body {
        padding: 1.2vh 1.4vh 1.4vh;
    }
    .apt__building-name {
        font-family: 'TTNorms-Bold';
        font-size: 2vh;
        margin-bottom: 0.3vh;
    }
    .apt__building-footer {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-top: 1vh;
    }
    .apt__empty {
        grid-column: 1 / -1;
        text-align: center;
        color: rgba(255, 255, 255, 0.6);
        font-size: 2vh;
        margin-top: 10vh;
    }
    .apt__head {
        display: flex;
        align-items: center;
        gap: 2vh;
        margin-bottom: 1.4vh;
    }
    .apt__back {
        padding: 0.8vh 1.4vh;
        border-radius: 0.6vh;
        background: rgba(255, 255, 255, 0.1);
        cursor: pointer;
        white-space: nowrap;
    }
    .apt__back:hover {
        background: rgba(255, 255, 255, 0.18);
    }
    .apt__filters {
        display: flex;
        flex-wrap: wrap;
        gap: 0.8vh;
        margin-bottom: 1.4vh;
    }
    .apt__chip {
        padding: 0.6vh 1.4vh;
        border-radius: 2vh;
        border: 1px solid rgba(255, 255, 255, 0.25);
        font-size: 1.4vh;
        cursor: pointer;
    }
    .apt__chip.active {
        background: white;
        color: #011627;
    }
    .apt__floors {
        overflow-y: auto;
        padding-right: 0.6vh;
    }
    .apt__floors::-webkit-scrollbar, .apt__buildings::-webkit-scrollbar {
        width: 0.4vh;
    }
    .apt__floors::-webkit-scrollbar-thumb, .apt__buildings::-webkit-scrollbar-thumb {
        background: rgba(255, 255, 255, 0.6);
        border-radius: 0.4vh;
    }
    .apt__floor {
        display: flex;
        align-items: stretch;
        gap: 1.2vh;
        margin-bottom: 1.2vh;
    }
    .apt__floor-label {
        width: 5.5vh;
        flex-shrink: 0;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        font-family: 'TTNorms-Bold';
        font-size: 2.4vh;
        border-right: 2px solid rgba(255, 255, 255, 0.15);
    }
    .apt__floor-label span {
        font-family: 'TTNorms-Regular';
        font-size: 1.2vh;
        color: rgba(255, 255, 255, 0.5);
    }
    .apt__flats {
        display: grid;
        grid-template-columns: repeat(4, 1fr);
        gap: 1vh;
        flex: 1;
    }
    .apt__flat {
        background: rgba(255, 255, 255, 0.07);
        border: 1px solid rgba(255, 255, 255, 0.08);
        border-radius: 0.8vh;
        padding: 1vh 1.2vh;
        cursor: pointer;
        display: flex;
        flex-direction: column;
        gap: 0.5vh;
        transition: border-color 0.15s, background 0.15s;
    }
    .apt__flat:hover {
        border-color: rgba(43, 182, 168, 0.8);
        background: rgba(43, 182, 168, 0.12);
    }
    .apt__flat.sold {
        opacity: 0.45;
        cursor: default;
    }
    .apt__flat.sold:hover {
        border-color: rgba(255, 255, 255, 0.08);
        background: rgba(255, 255, 255, 0.07);
    }
    .apt__flat-top {
        display: flex;
        justify-content: space-between;
        align-items: baseline;
    }
    .apt__flat-number {
        font-family: 'TTNorms-Bold';
        font-size: 1.7vh;
    }
    .apt__flat-class {
        font-size: 1.2vh;
        color: rgba(255, 255, 255, 0.6);
    }
    .apt__flat-meta {
        display: flex;
        gap: 1.2vh;
        font-size: 1.3vh;
        color: rgba(255, 255, 255, 0.7);
    }
</style>
