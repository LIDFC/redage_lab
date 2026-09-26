<script>
    // Меню подъезда многоквартирного дома (сервер: Houses/Apartments/ApartmentManager.cs, клиент: src_client/house/apartments.js)
    import { executeClient } from 'api/rage'
    import { houseType } from 'json/realEstate.js'
    import { format } from 'api/formatter'
    import interiorImage from '@/views/player/help/images/interior3.jpg'

    export let viewData;

    let data = { id: 0, name: "", address: "", flats: [] };
    $: if (viewData && typeof viewData === "string")
        data = JSON.parse(viewData);
    else if (viewData && typeof viewData === "object")
        data = viewData;

    let selected = null;
    let onlyFree = false;

    $: myFlat = data.flats.find(f => f.isMine);
    $: shown = data.flats.filter(f => !onlyFree || f.isFree);
    $: floors = [...new Set(shown.map(f => f.floor))].sort((a, b) => b - a);
    $: freeCount = data.flats.filter(f => f.isFree).length;
    $: if (selected)
        selected = data.flats.find(f => f.id === selected.id) || null;

    const action = (flat, name) => executeClient("client.apartments.action", data.id, flat.id, name);
    const close = () => executeClient("client.apartments.close");

    const onKey = (e) => {
        if (e.keyCode === 27) {
            if (selected)
                selected = null;
            else
                close();
        }
    }

    const status = (f) => {
        if (f.isMine) return "Ваша квартира";
        if (f.isFree) return "Продаётся";
        if (f.isAuction) return "На аукционе";
        return f.locked ? "Закрыто" : "Открыто";
    }
</script>

<svelte:window on:keyup={onKey} />

<div class="aptm">
    <div class="aptm__panel">
        <div class="aptm__side">
            <div class="aptm__image" style="background-image: url({interiorImage})"></div>
            <div class="aptm__side-body">
                <div class="aptm__caption">Многоквартирный дом</div>
                <div class="aptm__name">{data.name}</div>
                <div class="aptm__gray">{data.address}</div>
                <div class="aptm__stats">
                    <div><span>{data.flats.length}</span>квартир</div>
                    <div><span>{freeCount}</span>свободно</div>
                    <div><span>{floors.length ? Math.max(...data.flats.map(f => f.floor)) : 0}</span>этажей</div>
                </div>

                {#if myFlat}
                    <div class="aptm__my">
                        <div class="aptm__gray">Ваша квартира</div>
                        <div class="aptm__my-title">№{myFlat.number}, {myFlat.floor} этаж · {houseType[myFlat.type]}</div>
                        <div class="aptm__btn primary" on:click={() => action(myFlat, "enter")}>Войти домой</div>
                    </div>
                {/if}

                {#if selected}
                    <div class="aptm__details">
                        <div class="aptm__details-title">Квартира №{selected.number}</div>
                        <div class="aptm__row"><span>Этаж</span><span>{selected.floor}</span></div>
                        <div class="aptm__row"><span>Класс</span><span>{houseType[selected.type]}</span></div>
                        <div class="aptm__row"><span>Гаражных мест</span><span>{selected.cars}</span></div>
                        <div class="aptm__row"><span>Сожители</span><span>{selected.roommates} / {selected.maxRoommates}</span></div>
                        {#if selected.isFree}
                            <div class="aptm__row"><span>Налог</span><span>${format("money", selected.tax)}</span></div>
                            <div class="aptm__price">${format("money", selected.price)}</div>
                            <div class="aptm__buttons">
                                <div class="aptm__btn" on:click={() => action(selected, "view")}>Осмотреть</div>
                                <div class="aptm__btn primary" on:click={() => action(selected, "buy")}>Купить</div>
                            </div>
                        {:else}
                            <div class="aptm__row"><span>Владелец</span><span>{selected.owner || "—"}</span></div>
                            <div class="aptm__buttons">
                                <div class="aptm__btn primary" on:click={() => action(selected, "enter")}>
                                    {selected.isMine || !selected.locked ? "Войти" : "Постучать / войти"}
                                </div>
                            </div>
                        {/if}
                    </div>
                {:else if !myFlat}
                    <div class="aptm__hint">Выберите квартиру справа. Свободную можно осмотреть и купить прямо здесь.</div>
                {/if}
            </div>
        </div>

        <div class="aptm__main">
            <div class="aptm__top">
                <div class="aptm__title">Квартиры</div>
                <div class="aptm__toggle" class:active={onlyFree} on:click={() => onlyFree = !onlyFree}>
                    <div class="aptm__check">{onlyFree ? "✓" : ""}</div>
                    Только свободные
                </div>
                <div class="aptm__close" on:click={close}>Закрыть <span>ESC</span></div>
            </div>
            <div class="aptm__floors">
                {#each floors as floor}
                    <div class="aptm__floor">
                        <div class="aptm__floor-label">{floor}<span>этаж</span></div>
                        <div class="aptm__flats">
                            {#each shown.filter(f => f.floor === floor) as flat}
                                <div class="aptm__flat"
                                     class:free={flat.isFree}
                                     class:mine={flat.isMine}
                                     class:active={selected && selected.id === flat.id}
                                     on:click={() => selected = flat}>
                                    <div class="aptm__flat-top">
                                        <span class="aptm__flat-number">№{flat.number}</span>
                                        <span class="aptm__flat-class">{houseType[flat.type]}</span>
                                    </div>
                                    <div class="aptm__flat-status">{status(flat)}</div>
                                    {#if flat.isFree}
                                        <div class="aptm__flat-price">${format("money", flat.price)}</div>
                                    {/if}
                                </div>
                            {/each}
                        </div>
                    </div>
                {:else}
                    <div class="aptm__empty">{onlyFree ? "Свободных квартир нет" : "В доме пока нет квартир"}</div>
                {/each}
            </div>
        </div>
    </div>
</div>

<style>
    .aptm {
        position: absolute;
        inset: 0;
        z-index: 1000;
        display: flex;
        align-items: center;
        justify-content: center;
        background: radial-gradient(circle at 30% 20%, rgba(18, 52, 78, 0.92), rgba(1, 14, 26, 0.96));
        color: white;
        font-family: 'TTNorms-Regular';
        font-size: 1.5vh;
    }
    .aptm__panel {
        width: 130vh;
        height: 76vh;
        display: flex;
        gap: 2.4vh;
    }
    .aptm__side {
        width: 38vh;
        flex-shrink: 0;
        display: flex;
        flex-direction: column;
        background: rgba(255, 255, 255, 0.05);
        border: 1px solid rgba(255, 255, 255, 0.08);
        border-radius: 1.4vh;
        overflow: hidden;
    }
    .aptm__image {
        height: 14vh;
        flex-shrink: 0;
        background-size: cover;
        background-position: center;
        position: relative;
    }
    .aptm__image::after {
        content: "";
        position: absolute;
        inset: 0;
        background: linear-gradient(180deg, rgba(1, 14, 26, 0) 40%, rgba(8, 27, 43, 1) 100%);
    }
    .aptm__side-body {
        padding: 0 2vh 2vh;
        display: flex;
        flex-direction: column;
        flex: 1;
        min-height: 0;
        overflow-y: auto;
    }
    .aptm__side-body::-webkit-scrollbar {
        width: 0.4vh;
    }
    .aptm__side-body::-webkit-scrollbar-thumb {
        background: rgba(255, 255, 255, 0.5);
    }
    .aptm__caption {
        text-transform: uppercase;
        letter-spacing: 0.15em;
        font-size: 1.1vh;
        color: #2BB6A8;
    }
    .aptm__name {
        font-family: 'TTNorms-Bold';
        font-size: 3vh;
        margin: 0.4vh 0 0.2vh;
    }
    .aptm__gray {
        color: rgba(255, 255, 255, 0.55);
        font-size: 1.4vh;
    }
    .aptm__stats {
        display: flex;
        justify-content: space-between;
        margin: 1.4vh 0;
        padding: 1vh 0;
        border-top: 1px solid rgba(255, 255, 255, 0.1);
        border-bottom: 1px solid rgba(255, 255, 255, 0.1);
    }
    .aptm__stats div {
        display: flex;
        flex-direction: column;
        align-items: center;
        color: rgba(255, 255, 255, 0.55);
        font-size: 1.3vh;
    }
    .aptm__stats span {
        font-family: 'TTNorms-Bold';
        font-size: 2.6vh;
        color: white;
    }
    .aptm__my {
        background: rgba(43, 182, 168, 0.12);
        border: 1px solid rgba(43, 182, 168, 0.5);
        border-radius: 1vh;
        padding: 1.4vh;
        margin-bottom: 1.6vh;
    }
    .aptm__my-title {
        font-family: 'TTNorms-Bold';
        margin: 0.4vh 0 1.2vh;
    }
    .aptm__details-title {
        font-family: 'TTNorms-Bold';
        font-size: 2.2vh;
        margin-bottom: 1vh;
        color: #FFC062;
    }
    .aptm__row {
        display: flex;
        justify-content: space-between;
        padding: 0.6vh 0;
        border-bottom: 1px dashed rgba(255, 255, 255, 0.08);
    }
    .aptm__row span:first-child {
        color: rgba(255, 255, 255, 0.55);
    }
    .aptm__price {
        font-family: 'TTNorms-Bold';
        font-size: 2.6vh;
        color: #2BB6A8;
        margin: 1vh 0 0.4vh;
    }
    .aptm__buttons {
        display: flex;
        gap: 1vh;
        margin-top: 1.2vh;
    }
    .aptm__btn {
        flex: 1;
        text-align: center;
        padding: 1.1vh 0;
        border-radius: 0.8vh;
        background: rgba(255, 255, 255, 0.1);
        cursor: pointer;
        transition: background 0.15s;
    }
    .aptm__btn:hover {
        background: rgba(255, 255, 255, 0.18);
    }
    .aptm__btn.primary {
        background: #2BB6A8;
        font-family: 'TTNorms-Bold';
    }
    .aptm__btn.primary:hover {
        background: #34cdbd;
    }
    .aptm__hint {
        color: rgba(255, 255, 255, 0.5);
        line-height: 1.4;
    }
    .aptm__main {
        flex: 1;
        display: flex;
        flex-direction: column;
        min-width: 0;
    }
    .aptm__top {
        display: flex;
        align-items: center;
        gap: 2vh;
        margin-bottom: 1.6vh;
    }
    .aptm__title {
        font-family: 'TTNorms-Bold';
        font-size: 2.8vh;
        flex: 1;
    }
    .aptm__toggle {
        display: flex;
        align-items: center;
        gap: 0.8vh;
        cursor: pointer;
        color: rgba(255, 255, 255, 0.75);
    }
    .aptm__check {
        width: 1.8vh;
        height: 1.8vh;
        border-radius: 0.4vh;
        border: 1px solid rgba(255, 255, 255, 0.5);
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 1.3vh;
    }
    .aptm__toggle.active .aptm__check {
        background: #2BB6A8;
        border-color: #2BB6A8;
    }
    .aptm__close {
        cursor: pointer;
        display: flex;
        align-items: center;
        gap: 0.8vh;
        color: rgba(255, 255, 255, 0.75);
    }
    .aptm__close span {
        background: white;
        color: #011627;
        padding: 0.5vh 0.9vh;
        border-radius: 0.4vh;
        font-size: 1.3vh;
    }
    .aptm__floors {
        flex: 1;
        overflow-y: auto;
        padding-right: 0.8vh;
    }
    .aptm__floors::-webkit-scrollbar {
        width: 0.4vh;
    }
    .aptm__floors::-webkit-scrollbar-thumb {
        background: rgba(255, 255, 255, 0.6);
        border-radius: 0.4vh;
    }
    .aptm__floor {
        display: flex;
        gap: 1.4vh;
        margin-bottom: 1.2vh;
    }
    .aptm__floor-label {
        width: 6vh;
        flex-shrink: 0;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        font-family: 'TTNorms-Bold';
        font-size: 2.6vh;
        background: rgba(255, 255, 255, 0.04);
        border-radius: 0.8vh;
    }
    .aptm__floor-label span {
        font-family: 'TTNorms-Regular';
        font-size: 1.2vh;
        color: rgba(255, 255, 255, 0.5);
    }
    .aptm__flats {
        flex: 1;
        display: grid;
        grid-template-columns: repeat(4, 1fr);
        gap: 1vh;
    }
    .aptm__flat {
        padding: 1.1vh 1.3vh;
        border-radius: 0.9vh;
        background: rgba(255, 255, 255, 0.05);
        border: 1px solid rgba(255, 255, 255, 0.08);
        cursor: pointer;
        transition: border-color 0.15s, background 0.15s;
        min-height: 7.4vh;
    }
    .aptm__flat:hover {
        background: rgba(255, 255, 255, 0.09);
    }
    .aptm__flat.free {
        border-color: rgba(43, 182, 168, 0.45);
    }
    .aptm__flat.mine {
        border-color: #FFC062;
        background: rgba(255, 192, 98, 0.1);
    }
    .aptm__flat.active {
        border-color: white;
        background: rgba(255, 255, 255, 0.14);
    }
    .aptm__flat-top {
        display: flex;
        justify-content: space-between;
        align-items: baseline;
    }
    .aptm__flat-number {
        font-family: 'TTNorms-Bold';
        font-size: 1.8vh;
    }
    .aptm__flat-class {
        font-size: 1.2vh;
        color: rgba(255, 255, 255, 0.6);
    }
    .aptm__flat-status {
        font-size: 1.3vh;
        color: rgba(255, 255, 255, 0.6);
        margin-top: 0.4vh;
    }
    .aptm__flat.free .aptm__flat-status {
        color: #2BB6A8;
    }
    .aptm__flat.mine .aptm__flat-status {
        color: #FFC062;
    }
    .aptm__flat-price {
        font-family: 'TTNorms-Bold';
        margin-top: 0.3vh;
    }
    .aptm__empty {
        text-align: center;
        margin-top: 20vh;
        color: rgba(255, 255, 255, 0.5);
        font-size: 2vh;
    }
</style>
