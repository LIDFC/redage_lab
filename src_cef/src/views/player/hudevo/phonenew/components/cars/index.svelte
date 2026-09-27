<script>
    import { translateText } from 'lang'
    import Header from '../header.svelte'
    import HomeButton from '../homebutton.svelte'
    import Loader from './../loader.svelte'

    import Car from './car.svelte'
    import List from './list.svelte'
    import Panel from './panel.svelte'
    import './auto.sass'

    import { executeClientToGroup, executeClientAsyncToGroup } from "api/rage";
    import { addListernEvent, hasJsonStructure } from 'api/functions';
    import { onDestroy } from 'svelte'
    import { fade } from 'svelte/transition'

    // «Авто»: панель машины, в которой сидит игрок, и управление всеми своими машинами и гаражом.
    // Раньше машины настраивались через меню дома — теперь только здесь.

    let isLoad = false;
    let carsList = [];
    let garage = null;
    let panel = null;

    const loadCars = () => {
        Promise.all([
            executeClientAsyncToGroup("cars.getCarsList"),
            executeClientAsyncToGroup("cars.garageInfo"),
        ]).then(([list, info]) => {
            carsList = hasJsonStructure(list) ? JSON.parse(list) : [];
            garage = hasJsonStructure(info) ? JSON.parse(info) : null;
            if (selectedCar) {
                const fresh = carsList.find(c => c.number === selectedCar.number && !!c.isRent === !!selectedCar.isRent);
                if (fresh) selectedCar = fresh;
                else if (SelectViews === "Car") SelectViews = "List";
            }
            isLoad = true;
        });
    };
    addListernEvent("phoneCarsLoad", loadCars);
    executeClientToGroup("cars.load");

    const reload = () => setTimeout(() => executeClientToGroup("cars.load"), 700);

    const loadPanel = () => {
        executeClientAsyncToGroup("cars.panelState").then((result) => {
            panel = hasJsonStructure(result) ? JSON.parse(result) : null;
            if (!panel && tab === "panel")
                tab = "cars";
        });
    };

    let tab = "panel";
    loadPanel();
    const timer = setInterval(loadPanel, 600);
    onDestroy(() => clearInterval(timer));

    let SelectViews = "List";
    let selectedCar = null;
    const OnUpdatePage = (page, item) => {
        SelectViews = page;
        selectedCar = item;
    };
</script>
{#if !isLoad}
    <Loader />
{:else}
    <div class="newphone__rent" in:fade>
        <Header />
        <div class="newphone__rent_content">
            <div class="box-flex newphone__project_padding20 p-top">
                <div class="newphone__maps_headerimage rent"></div>
                <div class="newphone__maps_headertitle"><span class="orange">Авто</span></div>
            </div>
            <div class="auto__tabs">
                <div class="auto__tab" class:active={tab === "panel"} class:disabled={!panel} on:click={() => panel && (tab = "panel")}>Панель</div>
                <div class="auto__tab" class:active={tab === "cars"} on:click={() => tab = "cars"}>Мои машины</div>
            </div>
            {#if tab === "panel" && panel}
                <Panel {panel} refresh={loadPanel} />
            {:else if SelectViews === "Car" && selectedCar}
                <Car {OnUpdatePage} {selectedCar} {garage} {reload} />
            {:else}
                <List {OnUpdatePage} {carsList} {garage} {reload} />
            {/if}
        </div>
        <HomeButton />
    </div>
{/if}
