<script>
    import { executeClient } from 'api/rage'
    import { format } from 'api/formatter'
    import './main.sass'
    import './fonts/style.css'
    import { ItemType, itemsInfo } from 'json/itemsInfo.js'
    import { furnitureImage } from './props/index.js'

    export let viewData;

    if (!viewData)
        viewData = [];

    $: if (viewData && typeof viewData === "string") {
        viewData = JSON.parse (viewData)
    }

    const onBuy = (furniture, type = 0) => {
        if (!window.loaderData.delay ("furniture.buy", 1))
            return;

        executeClient ("client.furniture.buy", furniture.name, type);
    }

    // Предпросмотр: модель ставится перед игроком, камера облетает её (src_client/house/index.js)
    const onPreview = (furniture) => {
        if (!window.loaderData.delay ("furniture.preview", 1))
            return;
        executeClient ("client.furniture.preview", furniture.model, furniture.name);
    }

    // Счётчик мебели в доме игрока
    import { addListernEvent } from 'api/functions'
    let furnitureCount = -1;
    let furnitureMax = 100;
    addListernEvent ("furniture.count", (count, max) => {
        furnitureCount = Number(count);
        furnitureMax = Number(max) || 100;
    });
    executeClient ("client.furniture.getCount");

    const onExit = () => {        
        executeClient ("client.furniture.close");      
    }

    // Категории — из поля type с сервера (Houses/HouseFurniture.cs), в порядке появления
    const categoryIcons = {
        "Хранилища": "🗄", "Диваны": "🛋", "Кресла и стулья": "🪑", "Столы": "🍽", "Кровати": "🛏", "Свет": "💡",
        "Техника": "📺", "Шкафы и полки": "📚", "Декор": "🕯", "Картины": "🖼", "Кухня и ванная": "🍳", "Досуг": "🎱",
        "Статуи": "🗿", "Фигурки": "🏆", "Растения": "🌿", "Ёлки": "🎄", "Драгоценности": "💰", "Алкоголь": "🍾", "Вазы": "🏺",
    };
    const iconOf = (type) => categoryIcons[type] || "🏠";

    let category = "all";
    let search = "";
    let brokenImages = {};

    $: categories = [...new Set((Array.isArray(viewData) ? viewData : []).map((f) => f.type))];
    $: shown = (Array.isArray(viewData) ? viewData : []).filter((f) =>
        (category === "all" || f.type === category) &&
        (!search || String(f.name).toLowerCase().includes(search.toLowerCase())));
    $: countOf = (type) => (Array.isArray(viewData) ? viewData : []).filter((f) => f.type === type).length;

    const onKeyUp = (event) => {
        switch(event.which) {
            case 27:
                onExit ();
                break;
        }
    }
</script>
<svelte:window on:keyup={onKeyUp} />
<!--
    <HousePopup/>
-->
<div id="furniture">
    <div class="house__header">Мебельный магазин</div>
    {#if furnitureCount >= 0}
        <div class="fcount" class:full={furnitureCount >= furnitureMax}>Мебели в вашем доме: {furnitureCount} / {furnitureMax}</div>
    {:else}
        <div class="fcount">У вас нет дома — мебель ставится только в своём доме</div>
    {/if}
    <div class="fcat">
        <div class="fcat__tab" class:active={category === "all"} on:click={() => (category = "all")}>Всё <i>{Array.isArray(viewData) ? viewData.length : 0}</i></div>
        {#each categories as type}
            <div class="fcat__tab" class:active={category === type} on:click={() => (category = type)}>{iconOf(type)} {type} <i>{countOf(type)}</i></div>
        {/each}
        <input class="fcat__search" placeholder="Поиск…" bind:value={search} />
    </div>
    <div class="house__furniture fgrid">
        {#each shown as furniture (furniture.name)}
            <div class="house__furniture_element">
                <div class="box-between">
                    <div class="box-column">
                        <div class="house__furniture_title">{furniture.name}</div>
                        <div class="house__furniture_text money">${format("money", furniture.price)}</div>
                        {#if furniture.items}
                            {#each furniture.items as itemData}
                            <div class="box-flex">
                                <div class="house__furniture_smallimage" style="background-image: url({document.cloud + "inventoryItems/items" + `/${itemData.itemId}.png`})"></div>
                                <div class="house__furniture_text">{itemData.price} {window.getItem (itemData.itemId).Name}</div>
                            </div>
                            {/each}
                        {/if}
                        <!--<div class="box-flex">
                            <div class="house__furniture_smallimage"></div>
                            <div class="house__furniture_text">10 сосны</div>
                        </div>
                        <div class="box-flex">
                            <div class="house__furniture_smallimage"></div>
                            <div class="house__furniture_text">10 сосны</div>
                        </div>
                        <div class="box-flex">
                            <div class="house__furniture_smallimage"></div>
                            <div class="house__furniture_text">10 сосны</div>
                        </div>-->
                    </div>
                    <div class="house__furniture_image fimg">
                        {#if brokenImages[furniture.model]}
                            <span class="fimg__icon">{iconOf(furniture.type)}</span>
                        {:else}
                            <img src={furnitureImage(furniture.model)} alt="" on:error={() => (brokenImages = { ...brokenImages, [furniture.model]: true })} />
                        {/if}
                    </div>
                </div>
                <div class="box-between">
                    <div class="house__element_button" on:click={() => onBuy (furniture)}>
                        <div class="houseicon-safe house__furniture_icon"></div>
                        Купить
                    </div>
                    <div class="house__element_button fpreview" on:click={() => onPreview (furniture)}>👁 Посмотреть</div>
                    {#if furniture.items && furniture.items.length}
                    <div class="house__element_button" on:click={() => onBuy (furniture, 1)}>
                        <div class="houseicon-garage house__furniture_icon"></div>
                        Скрафтить самому
                    </div>
                    {/if}
                </div>
            </div>
        {:else}
            <div class="fcat__empty">Ничего не найдено</div>
        {/each}
    </div>
    <div class="box-between" style="margin-top: auto">
        <div class="house_bottom_buttons esc" on:click={onExit}>
            <div>Выйти</div>
            <div class="house_bottom_button">ESC</div>
        </div>
    </div>
</div>
<style>
    .fcat { display: flex; flex-wrap: wrap; gap: 0.8vh; align-items: center; width: 70.3vw; margin-top: 2.4vh; }
    .fcat__tab { padding: 0.8vh 1.4vh; border-radius: 1vh; background: rgba(255, 255, 255, 0.06); font-size: 1.4vh; cursor: pointer; white-space: nowrap; }
    .fcat__tab:hover { background: rgba(255, 255, 255, 0.12); }
    .fcat__tab.active { background: #2BB6A8; color: #fff; }
    .fcat__tab i { font-style: normal; opacity: 0.6; margin-left: 0.4vh; }
    .fcat__search { margin-left: auto; width: 22vh; padding: 0.8vh 1.2vh; border-radius: 1vh; border: 1px solid rgba(255, 255, 255, 0.15); background: rgba(0, 0, 0, 0.25); color: #fff; font-size: 1.4vh; outline: none; }
    .fcount { margin-top: 1vh; font-size: 1.5vh; color: #2BB6A8; }
    .fcount.full { color: #ff6b6b; }
    .fpreview { justify-content: center; }
    :global(#furniture .fgrid .house__furniture_element > .box-between:last-child) { gap: 0.8vh; }
    :global(#furniture .fgrid .house__furniture_element > .box-between:last-child .house__element_button) { flex: 1; width: auto; justify-content: center; text-align: center; font-size: 1.3vh; }
    .fcat__type { display: flex; align-items: center; font-size: 1.3vh; opacity: 0.6; }
    .fcat__empty { grid-column: 1 / -1; text-align: center; opacity: 0.6; font-size: 1.6vh; padding: 4vh 0; }
    :global(#furniture .house__furniture.fgrid) { margin-top: 2vh !important; grid-template-rows: none !important; grid-auto-rows: max-content; align-content: start; }
    .fimg { display: flex; align-items: center; justify-content: center; }
    .fimg img { max-width: 100%; max-height: 100%; object-fit: contain; }
    .fimg__icon { font-size: 7vh; opacity: 0.85; }
</style>
