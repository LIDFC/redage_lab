<script>
    import { translateText } from 'lang'
    import {executeClient, executeClientAsync} from 'api/rage'
    import { selectCase } from './state.js'
    import { format } from 'api/formatter'
    import { accountUnique } from 'store/account'
    import { ItemId } from 'json/itemsInfo.js'

    const onOpenCase = (index) => {
        selectCase.set (index);
        window.gameMenuView ( "Roulette", index )
    }

    let selectIndex = 0;
    let caseData = [];
    const onSelectCases = (index) => {
        selectIndex = index;
        caseData = [];
        shopList[selectIndex].cases.forEach((index) => {
            const cs = casesData.find(c => c.index === index);
            if (cs)
                caseData.push(cs);
        })
    }

    const getPrice = (price, index, unique) => {
        if (unique && unique.split("_")) {
            let getData = unique.split("_");
            if (getData[0] === "cases" && Number (getData[1]) === index && Number (getData[2]) === 0) {
                price = Math.round (price * 0.7);
            }
        }
        return price;
    }

    let shopList = [];
    let casesData = [];

    executeClient ("client.donate.load");

    executeClientAsync("donate.roulette.getList").then((result) => {
        if (result && typeof result === "string") {
            result = JSON.parse(result);

            shopList = result.shopList;
            casesData = result.caseData;
            onSelectCases(0);
            isLoad = true;
        }
    });

    let isLoad = true;

</script>

<div class="list1">
    {#each caseData as item}
        <div class="blockcase" on:keypress={() => {}} on:click={() => onOpenCase (item.index)}>
            <div class="usecase">
                <svg width="35" height="35" viewBox="0 0 35 35" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <g filter="url(#filter0_d_708_2325)">
                    <rect x="10" y="10" width="15" height="15" rx="2" fill="#E41958"/>
                    </g>
                    <path d="M16.3175 21C16.1118 20.9999 15.9145 20.9182 15.7691 20.7727L14.2177 19.2214C14.0764 19.0751 13.9983 18.8792 14 18.6758C14.0018 18.4724 14.0834 18.2779 14.2272 18.134C14.371 17.9902 14.5655 17.9086 14.7689 17.9069C14.9723 17.9051 15.1682 17.9833 15.3145 18.1246L16.2736 19.0837L20.3823 14.2905C20.4474 14.2094 20.528 14.1421 20.6196 14.0928C20.7111 14.0435 20.8116 14.0131 20.9151 14.0034C21.0186 13.9937 21.123 14.0049 21.2221 14.0363C21.3212 14.0678 21.413 14.1188 21.492 14.1864C21.5709 14.254 21.6355 14.3368 21.6819 14.4299C21.7282 14.523 21.7554 14.6244 21.7617 14.7282C21.7681 14.832 21.7535 14.936 21.7189 15.034C21.6843 15.132 21.6303 15.2221 21.5602 15.2988L16.9062 20.7285C16.834 20.8142 16.7439 20.883 16.6421 20.9299C16.5404 20.9769 16.4295 21.0008 16.3175 21Z" fill="white"/>
                    <defs>
                    <filter id="filter0_d_708_2325" x="0" y="0" width="35" height="35" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                    <feFlood flood-opacity="0" result="BackgroundImageFix"/>
                    <feColorMatrix in="SourceAlpha" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 127 0" result="hardAlpha"/>
                    <feOffset/>
                    <feGaussianBlur stdDeviation="5"/>
                    <feComposite in2="hardAlpha" operator="out"/>
                    <feColorMatrix type="matrix" values="0 0 0 0 0.894118 0 0 0 0 0.0980392 0 0 0 0 0.345098 0 0 0 0.5 0"/>
                    <feBlend mode="normal" in2="BackgroundImageFix" result="effect1_dropShadow_708_2325"/>
                    <feBlend mode="normal" in="SourceGraphic" in2="effect1_dropShadow_708_2325" result="shape"/>
                    </filter>
                    </defs>
                </svg>
                <p>У вас: <b>{window.getItemToCount(ItemId["Case" + item.index])}</b></p>                                 
            </div>
            <img src="{document.cloud + `img/roulette/${item.image}.png`}" alt=""/>
            <div class="namecase">{item.name}</div>
            <div class="pricecase" class:none={item.price <= 0}>
                <p>{format("money", getPrice (item.price, item.index, $accountUnique))}</p>
                <svg width="30" height="30" viewBox="0 0 30 30" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <g filter="url(#filter0_d_708_2317)">
                    <rect x="5" y="5" width="20" height="20" rx="10" fill="#CFF80B"/>
                    </g>
                    <path d="M9.36261 17.405C9.36261 17.285 9.44261 17.225 9.60261 17.225C9.76261 17.225 10.2576 17.335 11.0876 17.555C11.2776 15.915 11.8126 14.135 12.6926 12.215C13.0026 11.515 13.3726 11.165 13.8026 11.165C13.8826 11.165 13.9576 11.25 14.0276 11.42C14.1076 11.59 14.1476 11.8 14.1476 12.05C14.1476 12.35 13.9876 13.175 13.6676 14.525C13.3576 15.875 13.1526 17.05 13.0526 18.05C13.7126 18.2 14.2926 18.275 14.7926 18.275C15.3026 18.275 15.7676 18.145 16.1876 17.885C16.6076 17.615 16.9676 17.27 17.2676 16.85C17.8876 15.96 18.1976 14.89 18.1976 13.64C18.1876 11.33 17.2676 10.175 15.4376 10.175C14.7876 10.185 13.6376 10.475 11.9876 11.045C11.6676 11.155 11.3876 11.21 11.1476 11.21C10.9076 11.21 10.7876 11.14 10.7876 11C10.7876 10.81 10.9276 10.585 11.2076 10.325C11.4876 10.065 11.8476 9.815 12.2876 9.575C13.3676 8.995 14.4176 8.705 15.4376 8.705C16.8376 8.705 17.9876 9.08 18.8876 9.83C19.9476 10.72 20.4726 12.07 20.4626 13.88C20.4626 14.65 20.3426 15.41 20.1026 16.16C19.8626 16.91 19.4976 17.58 19.0076 18.17C18.5176 18.75 17.8976 19.22 17.1476 19.58C16.3976 19.94 15.5126 20.12 14.4926 20.12C14.0826 20.12 13.5626 20.03 12.9326 19.85C12.8626 20.04 12.6876 20.135 12.4076 20.135C11.8876 20.135 11.5326 20.05 11.3426 19.88C11.1626 19.7 11.0576 19.41 11.0276 19.01C10.5476 18.74 10.1476 18.46 9.82761 18.17C9.51761 17.88 9.36261 17.625 9.36261 17.405Z" fill="#1E1E1E" fill-opacity="0.5"/>
                    <defs>
                    <filter id="filter0_d_708_2317" x="0" y="0" width="30" height="30" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                    <feFlood flood-opacity="0" result="BackgroundImageFix"/>
                    <feColorMatrix in="SourceAlpha" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 127 0" result="hardAlpha"/>
                    <feOffset/>
                    <feGaussianBlur stdDeviation="2.5"/>
                    <feComposite in2="hardAlpha" operator="out"/>
                    <feColorMatrix type="matrix" values="0 0 0 0 0.811765 0 0 0 0 0.972549 0 0 0 0 0.0431373 0 0 0 0.25 0"/>
                    <feBlend mode="normal" in2="BackgroundImageFix" result="effect1_dropShadow_708_2317"/>
                    <feBlend mode="normal" in="SourceGraphic" in2="effect1_dropShadow_708_2317" result="shape"/>
                    </filter>
                    </defs>
                </svg>                                    
            </div>
        </div>
    {/each}
</div>