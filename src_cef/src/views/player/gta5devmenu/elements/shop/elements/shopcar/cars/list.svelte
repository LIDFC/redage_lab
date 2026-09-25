<script>
    import { accountRedbucks } from 'store/account'
    import { executeClient } from 'api/rage'
    import { format } from 'api/formatter'

    export let vehicles = [];
    export let category;
    export let searchText;
    export let pageload;

    const getRarity = (price) => {
        if (price >= 50000) return 5;
        if (price >= 30000) return 4;
        if (price >= 15000) return 3;
        return 2;
    }

    $: search = (searchText || "").toLowerCase().trim();
    $: items = vehicles
        .filter((item) => item.isHeli === category.isHeli)
        .filter((item) => !search.length || item.name.toLowerCase().includes(search) || item.model.toLowerCase().includes(search));

    let selectedModel = null;
    $: currentIndex = items.findIndex((item) => item.model === selectedModel);
    $: selected = currentIndex > -1 ? items[currentIndex] : null;
    $: others = items.filter((item) => item.model !== selectedModel).slice(0, 4);
    $: if (pageload === 0) selectedModel = null;

    const getImage = (item) => `${document.cloud}inventoryItems/vehicle/${item.model.toLowerCase()}.png`;

    const select = (item) => {
        selectedModel = item.model;
        window.pageloadf2 (category.page);
        window.pagenameitemf2 (item.name);
    }

    const step = (dir) => {
        const next = items[currentIndex + dir];
        if (next) select(next);
    }

    // Покупка идёт в донат-автосалоне (выбор цвета, тест-драйв), из меню ставим туда метку
    const onBuy = (item) => {
        if ($accountRedbucks < item.price)
            return window.notificationAdd(4, 9, `Недостаточно Redbucks!`, 3000);
        executeClient ("client.donate.vehicles.route");
    }
</script>
{#if selected}
    <div class="selectblockcl">
        <div class="blgf act">
            <div class="headsbcl">
                <div class="shopback" on:keypress on:click={() => selectedModel = null}>&larr; Назад к списку</div>
                <h1>{selected.name}</h1>
                <span>{format("money", selected.price)}
                    <svg width="17" height="17" viewBox="0 0 17 17" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <g clip-path="url(#clip0_1_40)">
                    <path d="M16.5 8.25C16.5 3.69365 12.8063 0 8.25 0C3.69365 0 0 3.69365 0 8.25C0 12.8063 3.69365 16.5 8.25 16.5C12.8063 16.5 16.5 12.8063 16.5 8.25Z" fill="#CFF80B"/>
                    <path d="M3.91016 10.5434C3.91016 10.4414 3.97818 10.3904 4.11416 10.3904C4.25013 10.3904 4.67088 10.484 5.37641 10.6709C5.53788 9.27691 5.99268 7.76394 6.74066 6.13194C7.00413 5.53696 7.31868 5.23944 7.68416 5.23944C7.75218 5.23944 7.81593 5.31166 7.87541 5.45619C7.94343 5.60071 7.97741 5.77921 7.97741 5.99169C7.97741 6.24669 7.84143 6.94794 7.56941 8.09544C7.30593 9.24294 7.13163 10.2417 7.04666 11.0917C7.60766 11.2192 8.10063 11.2829 8.52566 11.2829C8.95916 11.2829 9.35441 11.1725 9.71141 10.9514C10.0684 10.7219 10.3744 10.4287 10.6294 10.0717C11.1564 9.31516 11.4199 8.40571 11.4199 7.34319C11.4114 5.37969 10.6294 4.39794 9.07391 4.39794C8.52138 4.40641 7.54391 4.65294 6.14141 5.13744C5.86938 5.23096 5.63141 5.27769 5.42741 5.27769C5.22341 5.27769 5.12141 5.21821 5.12141 5.09919C5.12141 4.93771 5.24043 4.74646 5.47841 4.52544C5.71638 4.30441 6.02238 4.09194 6.39641 3.88794C7.31441 3.39494 8.20691 3.14844 9.07391 3.14844C10.2639 3.14844 11.2414 3.46719 12.0064 4.10469C12.9074 4.86121 13.3536 6.00871 13.3452 7.54719C13.3452 8.20171 13.2432 8.84769 13.0392 9.48519C12.8352 10.1227 12.5249 10.6922 12.1084 11.1937C11.6919 11.6867 11.1649 12.0862 10.5274 12.3922C9.88991 12.6982 9.13766 12.8512 8.27066 12.8512C7.92213 12.8512 7.48016 12.7747 6.94466 12.6217C6.88518 12.7832 6.73638 12.8639 6.49841 12.8639C6.05643 12.8639 5.75463 12.7917 5.59316 12.6472C5.44016 12.4942 5.35091 12.2477 5.32541 11.9077C4.91741 11.6782 4.57743 11.4402 4.30541 11.1937C4.04193 10.9472 3.91016 10.7304 3.91016 10.5434Z" fill="#1E1E1E" fill-opacity="0.5"/>
                    </g>
                    <defs>
                    <clipPath id="clip0_1_40">
                    <rect width="16.5" height="16.5" fill="white"/>
                    </clipPath>
                    </defs>
                    </svg>
                </span>
                <div class="btnbuy" on:keypress on:click={() => onBuy (selected)}>Купить в автосалоне</div>
            </div>
            <div class="bgblocksbcl">
                <div class="linesbcl line{getRarity(selected.price)}"></div>
                <div class="centersbcl">
                    <span class="rligh" class:none={currentIndex <= 0} on:keypress on:click={() => step (-1)}>
                        <svg width="30" height="30" viewBox="0 0 30 30" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <rect x="30" y="30" width="30" height="30" rx="15" transform="rotate(-180 30 30)" fill="#262626"/>
                        <path d="M18 10L11.4 15.5L18 21" stroke="white" stroke-opacity="0.5" stroke-linecap="round"/>
                        </svg>
                    </span>
                    <img src="{getImage(selected)}" alt=""/>
                    <span class="rligh" class:none={currentIndex >= items.length - 1} on:keypress on:click={() => step (1)}>
                        <svg width="30" height="30" viewBox="0 0 30 30" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <rect width="30" height="30" rx="15" fill="#262626"/>
                        <path d="M12 20L18.6 14.5L12 9" stroke="white" stroke-opacity="0.5" stroke-linecap="round"/>
                        </svg>
                    </span>
                </div>
            </div>
        </div>
        {#if others.length}
        <div class="catalogitems">
            <h1>Другие модели</h1>
            <div class="clilist">
                {#each others as item (item.model)}
                    <div class="clotblock" on:keypress on:click={() => select (item)}>
                        <div class="headcbl">
                            <div class="linesbcl line{getRarity(item.price)}"></div>
                            <span>{format("money", item.price)}
                                <svg width="17" height="17" viewBox="0 0 17 17" fill="none" xmlns="http://www.w3.org/2000/svg">
                                <g clip-path="url(#clip0_1_40)">
                                <path d="M16.5 8.25C16.5 3.69365 12.8063 0 8.25 0C3.69365 0 0 3.69365 0 8.25C0 12.8063 3.69365 16.5 8.25 16.5C12.8063 16.5 16.5 12.8063 16.5 8.25Z" fill="#CFF80B"/>
                                <path d="M3.91016 10.5434C3.91016 10.4414 3.97818 10.3904 4.11416 10.3904C4.25013 10.3904 4.67088 10.484 5.37641 10.6709C5.53788 9.27691 5.99268 7.76394 6.74066 6.13194C7.00413 5.53696 7.31868 5.23944 7.68416 5.23944C7.75218 5.23944 7.81593 5.31166 7.87541 5.45619C7.94343 5.60071 7.97741 5.77921 7.97741 5.99169C7.97741 6.24669 7.84143 6.94794 7.56941 8.09544C7.30593 9.24294 7.13163 10.2417 7.04666 11.0917C7.60766 11.2192 8.10063 11.2829 8.52566 11.2829C8.95916 11.2829 9.35441 11.1725 9.71141 10.9514C10.0684 10.7219 10.3744 10.4287 10.6294 10.0717C11.1564 9.31516 11.4199 8.40571 11.4199 7.34319C11.4114 5.37969 10.6294 4.39794 9.07391 4.39794C8.52138 4.40641 7.54391 4.65294 6.14141 5.13744C5.86938 5.23096 5.63141 5.27769 5.42741 5.27769C5.22341 5.27769 5.12141 5.21821 5.12141 5.09919C5.12141 4.93771 5.24043 4.74646 5.47841 4.52544C5.71638 4.30441 6.02238 4.09194 6.39641 3.88794C7.31441 3.39494 8.20691 3.14844 9.07391 3.14844C10.2639 3.14844 11.2414 3.46719 12.0064 4.10469C12.9074 4.86121 13.3536 6.00871 13.3452 7.54719C13.3452 8.20171 13.2432 8.84769 13.0392 9.48519C12.8352 10.1227 12.5249 10.6922 12.1084 11.1937C11.6919 11.6867 11.1649 12.0862 10.5274 12.3922C9.88991 12.6982 9.13766 12.8512 8.27066 12.8512C7.92213 12.8512 7.48016 12.7747 6.94466 12.6217C6.88518 12.7832 6.73638 12.8639 6.49841 12.8639C6.05643 12.8639 5.75463 12.7917 5.59316 12.6472C5.44016 12.4942 5.35091 12.2477 5.32541 11.9077C4.91741 11.6782 4.57743 11.4402 4.30541 11.1937C4.04193 10.9472 3.91016 10.7304 3.91016 10.5434Z" fill="#1E1E1E" fill-opacity="0.5"/>
                                </g>
                                <defs>
                                <clipPath id="clip0_1_40">
                                <rect width="16.5" height="16.5" fill="white"/>
                                </clipPath>
                                </defs>
                                </svg>
                            </span>
                        </div>
                        <img src="{getImage(item)}" alt=""/>
                        <p>{item.name}</p>
                    </div>
                {/each}
            </div>
        </div>
        {/if}
    </div>
{:else if items.length || pageload === category.page}
    <h1 class="shop">{category.title}</h1>
    {#if !items.length}
        <p class="shopempty">{search.length ? "Ничего не найдено" : "В этом разделе пока нет транспорта"}</p>
    {/if}
    <div class="list1">
        {#each items as item (item.model)}
            <div class="blockrewrad" on:keypress on:click={() => select (item)}>
                <div class="headrewrad">
                    <div class="line line{getRarity(item.price)}"></div>
                    <span>{format("money", item.price)}
                        <svg width="20" height="20" viewBox="0 0 20 20" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <g clip-path="url(#clip0_951_28)">
                        <g filter="url(#filter0_d_951_28)">
                        <path d="M16.875 10C16.875 6.20304 13.797 3.125 10 3.125C6.20304 3.125 3.125 6.20304 3.125 10C3.125 13.797 6.20304 16.875 10 16.875C13.797 16.875 16.875 13.797 16.875 10Z" fill="#CFF80B"/>
                        </g>
                        <path d="M6.38281 11.9125C6.38281 11.8275 6.4395 11.785 6.55281 11.785C6.66613 11.785 7.01675 11.8629 7.60469 12.0187C7.73925 10.8571 8.11825 9.59625 8.74156 8.23625C8.96113 7.74044 9.22325 7.4925 9.52781 7.4925C9.5845 7.4925 9.63763 7.55269 9.68719 7.67312C9.74388 7.79356 9.77219 7.94231 9.77219 8.11937C9.77219 8.33187 9.65888 8.91625 9.43219 9.8725C9.21263 10.8287 9.06738 11.6611 8.99656 12.3694C9.46406 12.4756 9.87488 12.5287 10.2291 12.5287C10.5903 12.5287 10.9197 12.4367 11.2172 12.2525C11.5147 12.0613 11.7697 11.8169 11.9822 11.5194C12.4214 10.8889 12.6409 10.1311 12.6409 9.24562C12.6339 7.60937 11.9822 6.79125 10.6859 6.79125C10.2255 6.79831 9.41094 7.00375 8.24219 7.4075C8.0155 7.48544 7.81719 7.52437 7.64719 7.52437C7.47719 7.52437 7.39219 7.47481 7.39219 7.37562C7.39219 7.24106 7.49138 7.08169 7.68969 6.8975C7.888 6.71331 8.143 6.53625 8.45469 6.36625C9.21969 5.95542 9.96344 5.75 10.6859 5.75C11.6776 5.75 12.4922 6.01562 13.1297 6.54687C13.8805 7.17731 14.2524 8.13356 14.2453 9.41562C14.2453 9.96106 14.1603 10.4994 13.9903 11.0306C13.8203 11.5619 13.5618 12.0364 13.2147 12.4544C12.8676 12.8652 12.4284 13.1981 11.8972 13.4531C11.3659 13.7081 10.7391 13.8356 10.0166 13.8356C9.72613 13.8356 9.35781 13.7719 8.91156 13.6444C8.862 13.7789 8.738 13.8462 8.53969 13.8462C8.17138 13.8462 7.91988 13.7861 7.78531 13.6656C7.65781 13.5381 7.58344 13.3327 7.56219 13.0494C7.22219 12.8581 6.93888 12.6598 6.71219 12.4544C6.49263 12.2489 6.38281 12.0683 6.38281 11.9125Z" fill="#1E1E1E" fill-opacity="0.5"/>
                        </g>
                        <defs>
                        <filter id="filter0_d_951_28" x="-1.875" y="-1.875" width="23.75" height="23.75" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                        <feFlood flood-opacity="0" result="BackgroundImageFix"/>
                        <feColorMatrix in="SourceAlpha" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 127 0" result="hardAlpha"/>
                        <feOffset/>
                        <feGaussianBlur stdDeviation="2.5"/>
                        <feComposite in2="hardAlpha" operator="out"/>
                        <feColorMatrix type="matrix" values="0 0 0 0 0.811765 0 0 0 0 0.972549 0 0 0 0 0.0431373 0 0 0 0.25 0"/>
                        <feBlend mode="normal" in2="BackgroundImageFix" result="effect1_dropShadow_951_28"/>
                        <feBlend mode="normal" in="SourceGraphic" in2="effect1_dropShadow_951_28" result="shape"/>
                        </filter>
                        <clipPath id="clip0_951_28">
                        <rect width="20" height="20" fill="white"/>
                        </clipPath>
                        </defs>
                        </svg>
                    </span>
                </div>
                <img class="n" src="{getImage(item)}" alt=""/>
                <p>{item.name}</p>
            </div>
        {/each}
    </div>
{/if}

<style>
    .shopback {
        font-family: SF Pro Display;
        font-weight: 500;
        color: rgba(255, 255, 255, 0.6);
        font-size: 1.2963vh;
        line-height: 1.2963vh;
        padding: 0.9259vh 1.2963vh;
        margin-right: 1.8519vh;
        border-radius: 0.463vh;
        background: rgba(255, 255, 255, 0.06);
        white-space: nowrap;
        cursor: pointer;
    }
    .shopback:hover {
        color: #fff;
        background: rgba(255, 255, 255, 0.12);
    }
    .shopempty {
        margin: 1.8519vh 2.7778vh 0;
        color: rgba(255, 255, 255, 0.5);
        font-size: 1.4815vh;
    }
</style>
