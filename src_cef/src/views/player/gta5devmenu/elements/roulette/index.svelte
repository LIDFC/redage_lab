<script>
    import { translateText } from 'lang'
    export let popupData;
    import { ItemId } from 'json/itemsInfo.js'
    import { onDestroy } from 'svelte';
    import { selectCase } from '../shop/elements/roulette/state.js'
    import { executeClient, executeClientAsync } from 'api/rage'
    import { accountRedbucks, accountUnique } from 'store/account'
    import { format } from 'api/formatter'
    
    import './main.sass';

    let width;

    window.addEventListener('resize', () => {
        width = window.innerWidth;
    });

    width = window.innerWidth;

    let caseData = {}
    const getData = () => {
        executeClientAsync("donate.roulette.getCaseOne", $selectCase).then((result) => {
            if (result && typeof result === "string") {
                caseData = JSON.parse(result);
                selectCaseToItems = caseData.items;
                casesData = GetRouletteData ()
                isLoad = true;
                checkitem()
            }
        });
    }

    function updateCaseuses() {
        setTimeout(() => {
        caseuses = window.getItemToCount(ItemId["Case" + caseData.index]);
        }, 1000);
    }

    // вызов функции при инициализации или при изменении caseData.index
    updateCaseuses();

    let value = 1;

    let roulletcase = false;

    let faststart = false
    
    executeClient ("client.donate.roulette.loadCase", $selectCase);

    let isLoad = false;

    import { addListernEvent } from 'api/functions';
    addListernEvent ("donate.roulette.initCase", getData)
    
    import PoputWin from './popupprise.svelte'
    let dataPopup;
    let isPopup;

    const maxCount = 8;

    const SetPopup = (toggled = false, data = null) => {
        dataPopup = data;
        isPopup = toggled;
        roulletcase = false;
    } 

    let antiFlud = 0;

    const getRndInteger = (min, max) => {
        return Math.floor(Math.random() * (max - min + 1) ) + min;
    }

    const GetRouletteData = () => {
        let _casesData = [];
        for(let i = 0; i < maxCount; i++) {
            let newItems = [];
            const sCase = caseData.items;
            let randToIndex;

            for (let i = 0; i < 50; i++) {
                randToIndex = getRndInteger (0, sCase.length - 1);
                newItems = [
                    ...newItems,
                    sCase[randToIndex]
                ]
            }

            _casesData.push({
                randomBlocks: newItems,
                startRandomBlocks: newItems.slice(0, 9),
                winBlock: {},
                carousel: 0,
                carouselStart: 0,
                fixСarousel: true,
                IntervalId: null,
            });
        }
        return _casesData;
    }

    let currentCount = 1;
    let
        casesData = {},
        toggledFast = false,
        selectCaseToItems = [];

    let isConfirm = false;
    const Confirm = (data) => {
        if (isConfirm)
            return;

        isConfirm = true;
        data.forEach((caseItem, caseindex) => {
            const elemWidth = document.querySelector(`#popuponate__roulette1 .newdonate__roulette-main:nth-child(${caseindex + 1}) .newdonate__roulette-element:first-child`);
            let newItems = casesData [caseindex].startRandomBlocks;
            let randToIndex;
            for (let index = newItems.length; index < 50; index++) {
                
                if (index === caseItem.Index) {
                    newItems = [
                        ...newItems,
                        selectCaseToItems[caseItem.ItemIndex]
                    ];
                } else {
                    randToIndex = getRndInteger (0, selectCaseToItems.length - 1);

                    newItems = [
                        ...newItems,
                        selectCaseToItems[randToIndex]
                    ];
                }                    
            }

            const randomCarousel = Math.round(getRndInteger (0 - (elemWidth.clientWidth / 2) + 180, elemWidth.clientWidth / 2) - 180);

            casesData [caseindex] = {
                fixСarousel: false,
                //carousel: (elemWidth.clientWidth * (caseItem.Index - 1) + randomCarousel),
                winBlock: caseItem,
                randomBlocks: newItems,
                startRandomBlocks: newItems.slice(caseItem.Index - 3, caseItem.Index + 6),
                carouselStart: randomCarousel
            }
            setTimeout(() => {
                const first = document.querySelector(`#popuponate__roulette1 .newdonate__roulette-main:nth-child(${caseindex + 1}) .newdonate__roulette-element:nth-child(4)`);
                const realCarousel = document.querySelector(`#popuponate__roulette1 .newdonate__roulette-main:nth-child(${caseindex + 1}) .newdonate__roulette-element:nth-child(${caseItem.Index + 1})`);
                
                casesData [caseindex].carousel = (realCarousel.getBoundingClientRect().x - first.getBoundingClientRect().x + randomCarousel);

                if (!toggledFast) {
                    let stopToCord = -1;
                    casesData [caseindex].IntervalId = setInterval(() => {
                        if (stopToCord === elemWidth.getBoundingClientRect().left) {
                            clearInterval (casesData [caseindex].IntervalId);
                            casesData [caseindex].IntervalId = null;
                            casesData [caseindex].fixСarousel = true;
                            openPopup ();
                        } else
                            stopToCord = elemWidth.getBoundingClientRect().left;
                    }, 500);
                } else {
                    casesData [caseindex].fixСarousel = true;
                    openPopup ();
                }
            }, 0)
        });
        return;
    }

    const openPopup = () => {
        let toggled = false;
        
        casesData.forEach((caseItem) => {
            if (!caseItem.fixСarousel && caseItem.winBlock && caseItem.winBlock.Item) {
                toggled = true;
            }
        })
        
        if (!toggled)
            SetPopup (true, casesData);
    }
    
    window.events.addEvent("cef.roullete.confirm", Confirm);

    onDestroy(() => {
        for(let i = 0; i < maxCount; i++) {
            if (casesData [i].IntervalId !== null) {
                clearInterval (casesData [i].IntervalId);
                casesData [i].IntervalId = null;
            }
        }
        window.events.removeEvent("cef.roullete.confirm", Confirm);
        executeClient ("client.roullete.confirm", false, -1);      
    });

    let caseuses = window.getItemToCount(ItemId["Case" + caseData.index])

    const onOpen = (_toggledFast = false) => {
        if (isConfirm)
            return;
        else if (antiFlud > new Date().getTime())
            return;
        else if (window.getItemToCount(ItemId["Case" + caseData.index]) < currentCount)
            return window.notificationAdd(4, 9, `Недостаточно Кейсов!`, 3000);
        antiFlud = new Date().getTime() + 2500;
        toggledFast = _toggledFast;
        roulletcase = true;
        executeClient ("client.roullete.open", caseData.index, currentCount);
        updateCaseuses();
    }

    const onOpen1 = (_toggledFast = false) => {
        let value1 = value
        if (antiFlud > new Date().getTime())
            return;
        else if ($accountRedbucks < getPrice (caseData.price * value1, caseData.index, $accountUnique))
            return window.notificationAdd(4, 9, `Недостаточно Redbucks!`, 3000);
        antiFlud = new Date().getTime() + 2500;
        toggledFast = _toggledFast;
        executeClient ("client.roullete.buy", caseData.index, value1);
        updateCaseuses();
    }

    function checkitem() {
        caseuses = window.getItemToCount(ItemId["Case" + caseData.index]);
    }

    const onCurrentCount = (count) => {
        if (isConfirm)
            return;
        else if (antiFlud > new Date().getTime())
            return;
            
        currentCount  = count;
    }

    let isEndPopup = isPopup;
    $: {
        if (isPopup !== isEndPopup) {
            isEndPopup = isPopup;
            if (!isPopup && isConfirm) {                
                for(let i = 0; i < maxCount; i++) {
                    casesData [i].winBlock = {};
                }
                isConfirm = false;
            }
        }
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

    const onfast = () => {
        faststart = true;
    }

    const offfast = () => {
        faststart = false;
    }

</script>

{#if isLoad}
<div id="app" class="caseopenmenu">
        {#if isPopup}
                <PoputWin {SetPopup} popupData={dataPopup} {roulletcase} /> 
        {:else if roulletcase}
        <div class="headcasemenu">
            <div id="popuponate__roulette1">
                <div class="newdonate__roulette-container">
                    {#each casesData as caseData, indexCase}
                        {#if currentCount > indexCase}
                        <div class="newdonate__roulette-main">
                            <div class="linecenter"></div>
                            <div class="newdonate__roulette-elements" style={`transition: ${caseData.fixСarousel ? "none" : "all 10000ms cubic-bezier(0.32, 0.64, 0.45, 1) 0s"};transform: translate3d(${caseData.fixСarousel ? (0 - (caseData.carouselStart + 350 ) / width * 100) : (0 - (caseData.carousel + 350 ) / width * 100)}vw, 0px, 0px)`}>
                                {#each (!caseData.fixСarousel ? caseData.randomBlocks : caseData.startRandomBlocks) as item, index}
                                <div class="newdonate__roulette-element margin-22">
                                    <div class="cases_content_prizes_list-item-line {item.color}"></div>
                                    <img src="{document.cloud + `img/roulette/${item.image}.png`}" alt="">
                                    <span>{item.title}</span>
                                </div>
                                {/each}
                            </div>
                        </div>
                        {/if}
                    {/each}
                </div>
            </div>
        </div>
        {:else}
        <div class="headcasemenu">
            <div class="infocasename">
                <h1>{caseData.name}</h1>
                <p>{caseData.desc}</p>
            </div>
            <div class="caseblockuse">
                <img src="{document.cloud + `img/roulette/${caseData.image}.png`}" alt=""/>
                <div class="caseuseb">
                    <svg width="35" height="35" viewBox="0 0 35 35" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <g filter="url(#filter0_d_712_2779)">
                        <rect x="10" y="10" width="15" height="15" rx="2" fill="#E41958"/>
                        </g>
                        <path d="M16.3175 21C16.1118 20.9999 15.9145 20.9182 15.7691 20.7727L14.2177 19.2214C14.0764 19.0751 13.9983 18.8792 14 18.6758C14.0018 18.4724 14.0834 18.2779 14.2272 18.134C14.371 17.9902 14.5655 17.9086 14.7689 17.9069C14.9723 17.9051 15.1682 17.9833 15.3145 18.1246L16.2736 19.0837L20.3823 14.2905C20.4474 14.2094 20.528 14.1421 20.6196 14.0928C20.7111 14.0435 20.8116 14.0131 20.9151 14.0034C21.0186 13.9937 21.123 14.0049 21.2221 14.0363C21.3212 14.0678 21.413 14.1188 21.492 14.1864C21.5709 14.254 21.6355 14.3368 21.6819 14.4299C21.7282 14.523 21.7554 14.6244 21.7617 14.7282C21.7681 14.832 21.7535 14.936 21.7189 15.034C21.6843 15.132 21.6303 15.2221 21.5602 15.2988L16.9062 20.7285C16.834 20.8142 16.7439 20.883 16.6421 20.9299C16.5404 20.9769 16.4295 21.0008 16.3175 21Z" fill="white"/>
                        <defs>
                        <filter id="filter0_d_712_2779" x="0" y="0" width="35" height="35" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                        <feFlood flood-opacity="0" result="BackgroundImageFix"/>
                        <feColorMatrix in="SourceAlpha" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 127 0" result="hardAlpha"/>
                        <feOffset/>
                        <feGaussianBlur stdDeviation="5"/>
                        <feComposite in2="hardAlpha" operator="out"/>
                        <feColorMatrix type="matrix" values="0 0 0 0 0.894118 0 0 0 0 0.0980392 0 0 0 0 0.345098 0 0 0 0.5 0"/>
                        <feBlend mode="normal" in2="BackgroundImageFix" result="effect1_dropShadow_712_2779"/>
                        <feBlend mode="normal" in="SourceGraphic" in2="effect1_dropShadow_712_2779" result="shape"/>
                        </filter>
                        </defs>
                    </svg>
                    <p>У вас <b>{caseuses}</b> кейсов</p>                                        
                </div>
            </div>
            <div class="casebuyblock" class:none={caseData.price <= 0}>
                <h1>ПРИОБРЕСТИ КЕЙСЫ</h1>
                <span>Количество кейсов к покупке</span>
                <div class="bginput">
                    <input placeholder="1" maxlength="3" bind:value={value}>
                    <div class="pricinput">
                        <p>{format("money", getPrice (caseData.price * value, caseData.index, $accountUnique))}</p>
                        <svg width="28" height="28" viewBox="0 0 28 28" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <g filter="url(#filter0_d_712_2769)">
                            <rect x="5" y="5" width="18" height="18" rx="9" fill="#CFF80B"/>
                            </g>
                            <path d="M9.14146 16.578C9.14146 16.466 9.21613 16.41 9.36546 16.41C9.5148 16.41 9.9768 16.5127 10.7515 16.718C10.9288 15.1873 11.4281 13.526 12.2495 11.734C12.5388 11.0807 12.8841 10.754 13.2855 10.754C13.3601 10.754 13.4301 10.8333 13.4955 10.992C13.5701 11.1507 13.6075 11.3467 13.6075 11.58C13.6075 11.86 13.4581 12.63 13.1595 13.89C12.8701 15.15 12.6788 16.2467 12.5855 17.18C13.2015 17.32 13.7428 17.39 14.2095 17.39C14.6855 17.39 15.1195 17.2687 15.5115 17.026C15.9035 16.774 16.2395 16.452 16.5195 16.06C17.0981 15.2293 17.3875 14.2307 17.3875 13.064C17.3781 10.908 16.5195 9.83 14.8115 9.83C14.2048 9.83933 13.1315 10.11 11.5915 10.642C11.2928 10.7447 11.0315 10.796 10.8075 10.796C10.5835 10.796 10.4715 10.7307 10.4715 10.6C10.4715 10.4227 10.6021 10.2127 10.8635 9.97C11.1248 9.72733 11.4608 9.494 11.8715 9.27C12.8795 8.72867 13.8595 8.458 14.8115 8.458C16.1181 8.458 17.1915 8.808 18.0315 9.508C19.0208 10.3387 19.5108 11.5987 19.5015 13.288C19.5015 14.0067 19.3895 14.716 19.1655 15.416C18.9415 16.116 18.6008 16.7413 18.1435 17.292C17.6861 17.8333 17.1075 18.272 16.4075 18.608C15.7075 18.944 14.8815 19.112 13.9295 19.112C13.5468 19.112 13.0615 19.028 12.4735 18.86C12.4081 19.0373 12.2448 19.126 11.9835 19.126C11.4981 19.126 11.1668 19.0467 10.9895 18.888C10.8215 18.72 10.7235 18.4493 10.6955 18.076C10.2475 17.824 9.87413 17.5627 9.57546 17.292C9.28613 17.0213 9.14146 16.7833 9.14146 16.578Z" fill="#1E1E1E" fill-opacity="0.5"/>
                            <defs>
                            <filter id="filter0_d_712_2769" x="0" y="0" width="28" height="28" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                            <feFlood flood-opacity="0" result="BackgroundImageFix"/>
                            <feColorMatrix in="SourceAlpha" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 127 0" result="hardAlpha"/>
                            <feOffset/>
                            <feGaussianBlur stdDeviation="2.5"/>
                            <feComposite in2="hardAlpha" operator="out"/>
                            <feColorMatrix type="matrix" values="0 0 0 0 0.811765 0 0 0 0 0.972549 0 0 0 0 0.0431373 0 0 0 0.25 0"/>
                            <feBlend mode="normal" in2="BackgroundImageFix" result="effect1_dropShadow_712_2769"/>
                            <feBlend mode="normal" in="SourceGraphic" in2="effect1_dropShadow_712_2769" result="shape"/>
                            </filter>
                            </defs>
                        </svg>                                            
                    </div>
                </div>
                <div class="btnbuycase" on:keypress on:click={() => onOpen1()}>
                    <p>Купить за</p>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <g filter="url(#filter0_d_712_2760)">
                        <rect x="5" y="5" width="14" height="14" rx="7" fill="#CFF80B"/>
                        </g>
                        <path d="M8.43871 14.27C8.43871 14.19 8.49204 14.15 8.59871 14.15C8.70537 14.15 9.03537 14.2233 9.58871 14.37C9.71537 13.2767 10.072 12.09 10.6587 10.81C10.8654 10.3433 11.112 10.11 11.3987 10.11C11.452 10.11 11.502 10.1667 11.5487 10.28C11.602 10.3933 11.6287 10.5333 11.6287 10.7C11.6287 10.9 11.522 11.45 11.3087 12.35C11.102 13.25 10.9654 14.0333 10.8987 14.7C11.3387 14.8 11.7254 14.85 12.0587 14.85C12.3987 14.85 12.7087 14.7633 12.9887 14.59C13.2687 14.41 13.5087 14.18 13.7087 13.9C14.122 13.3067 14.3287 12.5933 14.3287 11.76C14.322 10.22 13.7087 9.45 12.4887 9.45C12.0554 9.45667 11.2887 9.65 10.1887 10.03C9.97537 10.1033 9.78871 10.14 9.62871 10.14C9.46871 10.14 9.38871 10.0933 9.38871 10C9.38871 9.87333 9.48204 9.72333 9.66871 9.55C9.85537 9.37667 10.0954 9.21 10.3887 9.05C11.1087 8.66333 11.8087 8.47 12.4887 8.47C13.422 8.47 14.1887 8.72 14.7887 9.22C15.4954 9.81333 15.8454 10.7133 15.8387 11.92C15.8387 12.4333 15.7587 12.94 15.5987 13.44C15.4387 13.94 15.1954 14.3867 14.8687 14.78C14.542 15.1667 14.1287 15.48 13.6287 15.72C13.1287 15.96 12.5387 16.08 11.8587 16.08C11.5854 16.08 11.2387 16.02 10.8187 15.9C10.772 16.0267 10.6554 16.09 10.4687 16.09C10.122 16.09 9.88537 16.0333 9.75871 15.92C9.63871 15.8 9.56871 15.6067 9.54871 15.34C9.22871 15.16 8.96204 14.9733 8.74871 14.78C8.54204 14.5867 8.43871 14.4167 8.43871 14.27Z" fill="#1E1E1E" fill-opacity="0.5"/>
                        <defs>
                        <filter id="filter0_d_712_2760" x="0" y="0" width="24" height="24" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                        <feFlood flood-opacity="0" result="BackgroundImageFix"/>
                        <feColorMatrix in="SourceAlpha" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 127 0" result="hardAlpha"/>
                        <feOffset/>
                        <feGaussianBlur stdDeviation="2.5"/>
                        <feComposite in2="hardAlpha" operator="out"/>
                        <feColorMatrix type="matrix" values="0 0 0 0 0.811765 0 0 0 0 0.972549 0 0 0 0 0.0431373 0 0 0 0.25 0"/>
                        <feBlend mode="normal" in2="BackgroundImageFix" result="effect1_dropShadow_712_2760"/>
                        <feBlend mode="normal" in="SourceGraphic" in2="effect1_dropShadow_712_2760" result="shape"/>
                        </filter>
                        </defs>
                    </svg>                                        
                    <p>{format("money", getPrice (caseData.price * value, caseData.index, $accountUnique))}</p>
                </div>
            </div>
        </div>
        {/if}
    <div class="opencasebtns" class:none={isPopup}>
        <div class="numberopencase none">
            <p>ОТКРЫТЬ КЕЙСОВ</p>
            <div class="listbtnnum">
                <div class="btnnum">1</div>
                <div class="btnnum">2</div>
                <div class="btnnum">3</div>
                <div class="btnnum">4</div>
                <div class="btnnum">5</div>
            </div>
        </div>
        {#if roulletcase}
            <div class="openbtncaseon">ОТКРЫВАЕМ...</div>
            {:else}
            {#if faststart}
                <div class="openbtncase" on:keypress on:click={() => onOpen(true)}>ОТКРЫТЬ КЕЙС</div>
                {:else}
                <div class="openbtncase" on:keypress on:click={() => onOpen()}>ОТКРЫТЬ КЕЙС</div>
            {/if}
        {/if}
        <div class="openbtncasefast" class:none={roulletcase}>
            <p>БЫСТРОЕ ОТКРЫТИЕ</p>
            {#if faststart}
                <svg width="48" height="26" viewBox="0 0 48 26" fill="none" xmlns="http://www.w3.org/2000/svg" on:keypress on:click={() => offfast()}>
                    <path fill-rule="evenodd" clip-rule="evenodd" d="M14.9242 0.0772886C16.5619 -0.00658199 18.1976 0.000127651 19.8353 0.000127651C19.8466 0.000127651 28.1336 0.000127651 28.1336 0.000127651C29.8033 0.000127651 31.4391 -0.00658199 33.0758 0.0772886C34.5638 0.152772 36.0132 0.313804 37.456 0.673608C40.4932 1.43012 43.1454 3.01024 45.0626 5.25042C46.9685 7.47635 48 10.2013 48 12.9992C48 15.8005 46.9685 18.5238 45.0626 20.7497C43.1454 22.989 40.4932 24.57 37.456 25.3265C36.0132 25.6863 34.5638 25.8465 33.0758 25.9228C31.4391 26.0067 29.8033 25.9992 28.1656 25.9992C28.1544 25.9992 19.8654 26 19.8654 26C18.1976 25.9992 16.5619 26.0067 14.9242 25.9228C13.4372 25.8465 11.9878 25.6863 10.5449 25.3265C7.50777 24.57 4.85553 22.989 2.93835 20.7497C1.03247 18.5238 0 15.8005 0 13.0001C0 10.2013 1.03247 7.47635 2.93835 5.25042C4.85553 3.01024 7.50777 1.43012 10.5449 0.673608C11.9878 0.313804 13.4372 0.152772 14.9242 0.0772886Z" fill="#E81C5A"/>
                    <path fill-rule="evenodd" clip-rule="evenodd" d="M34 24C40.0751 24 45 19.0751 45 13C45 6.92487 40.0751 2 34 2C27.9249 2 23 6.92487 23 13C23 19.0751 27.9249 24 34 24Z" fill="white"/>
                </svg>                                    
                {:else}
                <svg width="48" height="26" viewBox="0 0 48 26" fill="none" xmlns="http://www.w3.org/2000/svg" on:keypress on:click={() => onfast()}>
                    <path fill-rule="evenodd" clip-rule="evenodd" d="M14.9242 0.0772886C16.5619 -0.00658199 18.1976 0.000127651 19.8353 0.000127651C19.8466 0.000127651 28.1336 0.000127651 28.1336 0.000127651C29.8033 0.000127651 31.4391 -0.00658199 33.0758 0.0772886C34.5638 0.152772 36.0132 0.313804 37.456 0.673608C40.4932 1.43012 43.1454 3.01024 45.0626 5.25042C46.9685 7.47635 48 10.2013 48 12.9992C48 15.8005 46.9685 18.5238 45.0626 20.7497C43.1454 22.989 40.4932 24.57 37.456 25.3265C36.0132 25.6863 34.5638 25.8465 33.0758 25.9228C31.4391 26.0067 29.8033 25.9992 28.1656 25.9992C28.1544 25.9992 19.8654 26 19.8654 26C18.1976 25.9992 16.5619 26.0067 14.9242 25.9228C13.4372 25.8465 11.9878 25.6863 10.5449 25.3265C7.50777 24.57 4.85553 22.989 2.93835 20.7497C1.03247 18.5238 0 15.8005 0 13.0001C0 10.2013 1.03247 7.47635 2.93835 5.25042C4.85553 3.01024 7.50777 1.43012 10.5449 0.673608C11.9878 0.313804 13.4372 0.152772 14.9242 0.0772886Z" fill="#272727"/>
                    <path fill-rule="evenodd" clip-rule="evenodd" d="M14 24C20.0751 24 25 19.0751 25 13C25 6.92487 20.0751 2 14 2C7.92487 2 3 6.92487 3 13C3 19.0751 7.92487 24 14 24Z" fill="white"/>
                </svg> 
            {/if}                                  
        </div>
    </div>
    <div class="infocl">СОДЕРЖИМОЕ КЕЙСА</div>
    <div class="listblockitems">
        {#each caseData.items as value, index}
            <div class="blockitemcase">
                <div class="linebic {value.color}"></div>
                <img src="{document.cloud + `img/roulette/${value.image}.png`}" alt="">
                <p>{value.title}</p>
            </div>
        {/each}
    </div>
</div>
{/if}