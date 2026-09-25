<script>
    import './main.css';
    import ImgAvatar from '../hudevo/phonenew/assets/images/person.png';
    import './fonts/inv/style.css';
    import './fonts/items/style.css';
    import './fonts/gamemenu/style.css';
    import './fonts/Gilroy/stylesheet.css';
    import './fonts/SFPro/stylesheet.css';
    import { serverDateTime, isEvent } from 'store/server'
    import { charData } from 'store/chars';
    import { accountRedbucks } from 'store/account'
    import { format } from 'api/formatter'
    import { executeClient } from 'api/rage'
    import { otherStatsData } from 'store/account'
    import { hasJsonStructure } from "api/functions";
    import { addListernEvent } from 'api/functions';
    export let visible = false;

    const handleKeyDown = (event) => {
        const { keyCode } = event;

        if (keyCode === 27)
            onClose ();
    }

    const onClose = () => {
        executeClient ("client.stockitemsexit");
    }

    let replacements = [
        { word: 'Osnovnoe', replacement: 'Основное' },
        { word: 'Statiskic', replacement: 'Статистика' },
        { word: 'Carsuser', replacement: 'Транспорт' },
        { word: 'Housebiz', replacement: 'Имущество' },
        { word: 'Skills', replacement: 'Навыки' },
        { word: 'Anim0', replacement: '' },
        { word: 'Anim1', replacement: '' },
        { word: 'Anim2', replacement: '' },
        { word: 'Anim3', replacement: '' },
        { word: 'Anim4', replacement: '' },
        { word: 'Anim5', replacement: '' },
        { word: 'Anim6', replacement: '' },
        { word: 'Anim7', replacement: '' },
        { word: 'Q1', replacement: 'Квесты' },
        { word: 'Q2', replacement: 'Ежедневные' },
        { word: 'Q3', replacement: 'Бонусы' },
        { word: 'Report', replacement: 'Обращения' },
        { word: 'BP1', replacement: 'Главная' },
        { word: 'BP2', replacement: 'Купить опыт' },
        { word: 'BP3', replacement: 'Ежедневные задания' },
        { word: 'BP4', replacement: 'Квест' },
        { word: 'Shop', replacement: 'Персонаж' },
        { word: 'Shopcl', replacement: '' },
        { word: 'Shopcar', replacement: '' },
        { word: 'Money', replacement: 'Валюта' },
        { word: 'Personal', replacement: 'Стартовые наборы' },
        { word: 'Roulette', replacement: 'Кейс' },
        { word: 'Cases', replacement: 'Кейсы' },
        { word: 'Premium', replacement: 'помогу' },
        { word: 'MiniGames', replacement: 'МиниИгры' },
        { word: 'General', replacement: 'Общие' },
        { word: 'State', replacement: 'Государственные орг.' },
        { word: 'Crime', replacement: 'Криминальные орг.' },
        { word: 'FAQ', replacement: 'FAQ' },
        { word: 'Settings', replacement: 'Настройки' },
        { word: 'Stock', replacement: 'Хранилище' },
        { word: 0, replacement: 'Все' },
        { word: 1, replacement: 'Маски' },
        { word: 2, replacement: 'Головные уборы' },
        { word: 3, replacement: 'Аксессуары' },
        { word: 4, replacement: 'Верхняя одежда' },
        { word: 5, replacement: 'Бронежилеты' },
        { word: 6, replacement: 'Нижняя одежда' },
        { word: 7, replacement: 'Штаны' },
        { word: 8, replacement: 'Обувь' },
        { word: 10, replacement: 'Цена. По убыванию' },
        { word: 11, replacement: 'Цена. По возрастанию' },
        { word: 12, replacement: 'Редкость. По убыванию' },
        { word: 13, replacement: 'Редкость. По возрастанию' },
    ];

    executeClient ("client.stockitems");
    executeClient ("client.phone.cars.load");
    executeClient ("client.gta5devmenucars");
    executeClient ("client.donate.load");

    window.gta5devmenustockitems = function (itemstock) {
        itemliststock = itemstock;
    };

    window.gta5devmenucars = function (vehiclesJson) {
        if (hasJsonStructure(vehiclesJson))
            carsList = JSON.parse(vehiclesJson);
    };

    let itemliststock;

    let carsList = [];

    addListernEvent("phoneCarsLoad", true);

    let useVisible = -1;

    $: {
        if (useVisible != visible) {
            if (visible && $otherStatsData.Name/* && $otherStatsData.UUID !== selectCharData.UUID*/) {
                selectCharData = $otherStatsData;
            } else if (visible && !$otherStatsData.Name && selectCharData !== $charData) {
                selectCharData = $charData;
            } else if (!visible && $otherStatsData.Name) {
                selectCharData = $charData;
                window.accountStore.otherStatsData ('{}');
            }
            useVisible = visible;
        }
    }
    
    let selectCharData = $charData; 
    let selectView = "Menu";
    let pageload = 0;

    window.pageloadf2 = (wiew) => {
        pageload = wiew;
    }

    import User from "./elements/user/index.svelte";
    import Animation from "./elements/anim/index.svelte";
    import Quests from "./elements/quest/index.svelte";
    import Rewrad from "./elements/rewardslist/index.svelte";
    import Report from "./elements/report/index.svelte";
    import Roulette from "./elements/roulette/index.svelte";
    import BattlePass from "./elements/battlepass/index.svelte";
    import Shop from "./elements/shop/index.svelte";
    import MiniGames from "./elements/minigames/index.svelte";
    import State from "./elements/rules/state.svelte";
    import Crime from "./elements/rules/crime.svelte";
    import General from "./elements/rules/general.svelte";
    import FAQ from "./elements/faq/index.svelte";
    import Settings from "./elements/settings/index.svelte";
    import Stock from "./elements/stock/stock.svelte";


    const Views = {
        Osnovnoe: User,
        Statiskic: User,
        Carsuser: User,
        Housebiz: User,
        Skills: User,
        Anim0: Animation,
        Anim1: Animation,
        Anim2: Animation,
        Anim3: Animation,
        Anim4: Animation,
        Anim5: Animation,
        Anim6: Animation,
        Anim7: Animation,
        Q1: Quests,
        Q2: Rewrad,
        Q3: Rewrad,
        Report,
        BP1: BattlePass,
        BP2: BattlePass,
        BP3: BattlePass,
        BP4: BattlePass,
        Shop: Shop,
        Shopcl: Shop,
        Shopcar: Shop,
        Money: Shop,
        Personal: Shop,
        Roulette,
        Cases: Shop,
        Premium: Shop,
        MiniGames,
        State,
        Crime,
        General,
        FAQ,
        Settings,
        Stock,
    }
 
    
    window.gameMenuView = (wiew) => {
        executeClient ("client.stockitems");
        executeClient ("client.phone.cars.load");
        executeClient ("client.gta5devmenucars");
        selecttypecat = false;
        catlist = false;
        selectgender = true;
        genderlist = false;
        selectView = "";
        pageload = 0;
        pagefilter = 10;
        setTimeout(() => {
            selectView = wiew;
        }, 10);
    }

    let selecttypecat;
    let catlist;
    let selectgender;
    let genderlist;
    let pricefilter;
    let catFilter;
    let pagefilter;

    const onSelectTypeCat = (type) => {
        pageload = type;
        catlist = false;
    }

    const onSelectTypeFilter = (type) => {
        pagefilter = type;
        catFilter = false;
    }

    const onSelectGender = (type) => {
        selectgender = type;
        genderlist = false;
    }

    function opencatlisttype() {
        catlist = !catlist;
    }
    
    function togglePriceFilter() {
        catFilter = !catFilter;
    }

    const opengenerlist = () => {
        genderlist = true;
    }

    let searchText = "";

    const defaultSorted = ["User", "Animation", "Quests", "Report", "BattlePass", "Shop", "MiniGames", "Rules", "FAQ", "Settings"];
    let _pagesSorted = ["User", "Animation", "Quests", "MiniGames", "Rules", "FAQ", "Settings"];
    let PagesSorted = ["User", "Animation", "Quests", "MiniGames", "Rules", "FAQ", "Settings"];

    const updatePage = (name, value) => {

        const index = _pagesSorted.indexOf(name)

        if (index !== -1 && !value)
            _pagesSorted.splice(index, 1);
        else if (index === -1 && value)
            _pagesSorted.push(name)

        let sorted = [];

        defaultSorted.forEach((value) => {
            if (_pagesSorted.includes (value))
                sorted.push(value)
        })

        PagesSorted = sorted;
    }

    import { charFractionID, charOrganizationID } from 'store/chars'


    isEvent.subscribe(value => {
        updatePage ("Animation", value);
    });
    charFractionID.subscribe(value => {
        updatePage ("MiniGames", value > 0);
    });
    charOrganizationID.subscribe(value => {
        updatePage ("User", value > 0);
    });


    let UseVisible = visible;

    $: {
        if (UseVisible != visible) {
            UseVisible = visible;
            if (!visible) {
                selectView = "Menu";
                onInputBlur ();
            }
        }
    }

    let isFocusInput = false;

    window.onFocusInput = (value) => isFocusInput = value;

    let timerId = null;

    const onClickPage = () => {
        setTimeout(() => {
            executeClient ("client:binder", "get", "all");
        }, 100);
    }
</script>

<svelte:window on:keyup={handleKeyDown} />

<!-- svelte-ignore a11y-no-noninteractive-tabindex -->
<div class="gta5devmenuf2">
    <div class="leftmenu">
        <div class="headplayr">
            <img src={ImgAvatar} alt=""/>
            <div class="infoplayer">
                <p>Логин</p>
                <span>{selectCharData.Login}</span>
            </div>
        </div>
        <div class="menulist">
            <li class="namenav" tabindex="0" class:active={selectView === "Osnovnoe" || selectView === "Statiskic" || selectView === "Carsuser" || selectView === "Housebiz" || selectView === "Skills"}>
                <div class="navblock">
                    <svg class="icon" width="19" height="19" viewBox="0 0 19 19" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M9.5 0C6.73762 0 4.49023 2.24738 4.49023 5.00977C4.49023 7.77215 6.73762 10.0195 9.5 10.0195C12.2624 10.0195 14.5098 7.77215 14.5098 5.00977C14.5098 2.24738 12.2624 0 9.5 0ZM15.7331 13.2924C14.3616 11.8998 12.5433 11.1328 10.6133 11.1328H8.38672C6.45673 11.1328 4.63845 11.8998 3.26689 13.2924C1.90204 14.6782 1.15039 16.5075 1.15039 18.4434C1.15039 18.7508 1.39962 19 1.70703 19H17.293C17.6004 19 17.8496 18.7508 17.8496 18.4434C17.8496 16.5075 17.098 14.6782 15.7331 13.2924Z"/>
                    </svg>       
                    <p>Персонаж</p>
                    <svg class="arrow1" width="11" height="7" viewBox="0 0 11 7" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M1 1L5.5 5.5L10 1" stroke-linecap="round"/>
                    </svg> 
                </div>
                <ul class="dropdown">
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Osnovnoe")} class:active={selectView === "Osnovnoe"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Основное</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Statiskic")} class:active={selectView === "Statiskic"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Статистика</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Carsuser")} class:active={selectView === "Carsuser"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Транспорт</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Housebiz")} class:active={selectView === "Housebiz"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Имущество</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Skills")} class:active={selectView === "Skills"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21H11" stroke-width="2"/>
                        </svg>                                                                       
                        <p>Навыки</p>                                    
                    </li>
                </ul>                                                
            </li>
            <li tabindex="0" class="namenav" class:active={selectView === "Anim0" || selectView === "Anim1" || selectView === "Anim2" || selectView === "Anim3" || selectView === "Anim4" || selectView === "Anim5" || selectView === "Anim6" || selectView === "Anim7"}>
                <div class="navblock">
                    <svg class="icon" width="19" height="19" viewBox="0 0 19 19" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M9.30208 9.49992C9.30208 11.5733 7.61504 13.2603 5.54167 13.2603C3.46829 13.2603 1.78125 11.5733 1.78125 9.49992C1.78125 7.42654 3.46829 5.7395 5.54167 5.7395C7.61504 5.7395 9.30208 7.42654 9.30208 9.49992ZM9.5 5.7395C9.27833 5.7395 9.04875 5.76325 8.82708 5.80284C9.84833 6.70534 10.4896 8.02742 10.4896 9.49992C10.4896 10.9724 9.84833 12.2945 8.82708 13.197C9.04875 13.2366 9.27833 13.2603 9.5 13.2603C11.5742 13.2603 13.2604 11.5741 13.2604 9.49992C13.2604 7.42575 11.5742 5.7395 9.5 5.7395ZM13.8542 5.95325C13.585 5.95325 13.3158 5.98492 13.0546 6.06409C13.9175 6.95075 14.4479 8.16992 14.4479 9.49992C14.4479 10.8299 13.9175 12.0491 13.0546 12.9358C13.3158 13.0149 13.585 13.0466 13.8542 13.0466C15.7067 13.0466 17.2188 11.4553 17.2188 9.49992C17.2188 7.5445 15.7067 5.95325 13.8542 5.95325Z"/>
                    </svg>     
                    <p>Анимации</p>
                    <svg class="arrow1" width="11" height="7" viewBox="0 0 11 7" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M1 1L5.5 5.5L10 1" stroke-linecap="round"/>
                    </svg> 
                </div>
                <ul class="dropdown">
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Anim0")} class:active={selectView === "Anim0"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Избранное</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Anim1")} class:active={selectView === "Anim1"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Сесть/Лечь</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Anim2")} class:active={selectView === "Anim2"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Социальные</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Anim3")} class:active={selectView === "Anim3"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Позы</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Anim4")} class:active={selectView === "Anim4"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Неприличное</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Anim5")} class:active={selectView === "Anim5"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Физ. упражнения</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Anim6")} class:active={selectView === "Anim6"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Танцы</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Anim7")} class:active={selectView === "Anim7"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21H11" stroke-width="2"/>
                        </svg> 
                        <p>Прочее</p>                                    
                    </li>
                </ul>    
            </li>
            <li tabindex="0" class="namenav" class:active={selectView === "Q1" || selectView === "Q2" || selectView === "Q3"}>
                <div class="navblock">
                    <svg class="icon" width="19" height="19" viewBox="0 0 19 19" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <g clip-path="url(#clip0_465_136)">
                        <path d="M14.7669 18.5143L9.49997 15.2185L4.23308 18.5143C4.04174 18.6355 3.79493 18.6263 3.61068 18.4926C3.52085 18.4275 3.45259 18.337 3.4147 18.2327C3.37681 18.1284 3.37103 18.0152 3.39812 17.9076L4.90494 11.8797L0.199027 7.88979C0.113993 7.81852 0.0522049 7.72346 0.021596 7.61681C-0.00901292 7.51017 -0.00704472 7.3968 0.0272479 7.29129C0.0614997 7.18568 0.126533 7.09271 0.214 7.02433C0.301467 6.95594 0.407376 6.91526 0.518131 6.9075L6.66025 6.47859L8.98356 0.71595C9.15426 0.295204 9.84568 0.295204 10.0164 0.71595L12.3397 6.47859L18.4818 6.9075C18.5926 6.91526 18.6985 6.95594 18.7859 7.02433C18.8734 7.09271 18.9384 7.18568 18.9727 7.29129C19.007 7.3968 19.009 7.51017 18.9783 7.61681C18.9477 7.72346 18.886 7.81852 18.8009 7.88979L14.095 11.8798L15.6018 17.9077C15.6289 18.0153 15.6231 18.1285 15.5852 18.2328C15.5473 18.337 15.4791 18.4276 15.3892 18.4926C15.2995 18.558 15.1923 18.595 15.0814 18.5988C14.9705 18.6027 14.8609 18.5733 14.7669 18.5143Z"/>
                        </g>
                        <defs>
                        <clipPath id="clip0_465_136">
                        <rect width="19" height="19" fill="white"/>
                        </clipPath>
                        </defs>
                    </svg>                                      
                    <p>Задания</p>
                    <svg class="arrow1" width="11" height="7" viewBox="0 0 11 7" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M1 1L5.5 5.5L10 1" stroke-linecap="round"/>
                    </svg> 
                </div>
                <ul class="dropdown">
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Q1")} class:active={selectView === "Q1"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Квесты</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Q2")} class:active={selectView === "Q2"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Ежедневные</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Q3")} class:active={selectView === "Q3"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21H11" stroke-width="2"/>
                        </svg>                                                                       
                        <p>Бонусы</p>                                    
                    </li>
                </ul> 
            </li>
            <li tabindex="0" class="namenav" on:keypress={() => {}} on:click={() => window.gameMenuView ("Report")} class:active={selectView === "Report"}>
                <div class="navblock">
                    <svg class="icon" width="19" height="19" viewBox="0 0 19 19" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M17.1748 3.58195V17.7714C17.1737 18.0971 17.0437 18.4091 16.8134 18.6393C16.583 18.8695 16.2708 18.9992 15.9451 19H5.40904C5.08335 18.9992 4.77122 18.8695 4.54082 18.6393C4.31042 18.4091 4.18047 18.0971 4.17935 17.7714V17.1809H13.5918C14.0591 17.1801 14.5072 16.9942 14.8378 16.6638C15.1684 16.3335 15.3546 15.8856 15.3557 15.4182V2.35337H15.9451C16.2708 2.3542 16.5829 2.48387 16.8133 2.71406C17.0437 2.94425 17.1737 3.25626 17.1748 3.58195ZM5.38418 0.267188L2.09314 3.55816H5.38418V0.267188ZM13.5918 16.6477H3.05564C2.72988 16.6466 2.41779 16.5167 2.18746 16.2863C1.95713 16.0559 1.82726 15.7438 1.82617 15.4181V4.09242H5.65129C5.72216 4.09242 5.79012 4.06427 5.84022 4.01416C5.89033 3.96406 5.91848 3.8961 5.91848 3.82523V0H13.5918C13.9176 0.00090095 14.2299 0.130743 14.4603 0.361153C14.6907 0.591564 14.8205 0.903808 14.8214 1.22966V15.4181C14.8203 15.7438 14.6904 16.056 14.4601 16.2864C14.2297 16.5167 13.9176 16.6466 13.5918 16.6477ZM13.4771 14.9258C13.4769 14.8551 13.4487 14.7874 13.3986 14.7375C13.3485 14.6876 13.2806 14.6597 13.2099 14.6598H3.43742C3.40215 14.6593 3.36714 14.6659 3.33443 14.6791C3.30171 14.6924 3.27195 14.7119 3.24686 14.7367C3.22176 14.7615 3.20184 14.7911 3.18825 14.8236C3.17465 14.8562 3.16765 14.8911 3.16765 14.9264C3.16765 14.9617 3.17465 14.9966 3.18825 15.0291C3.20184 15.0617 3.22176 15.0912 3.24686 15.116C3.27195 15.1408 3.30171 15.1604 3.33443 15.1736C3.36714 15.1868 3.40215 15.1934 3.43742 15.193H13.21C13.2808 15.193 13.3487 15.1648 13.3988 15.1148C13.4489 15.0647 13.4771 14.9968 13.4771 14.9259V14.9258ZM13.4771 13.0397C13.477 12.9689 13.4488 12.901 13.3987 12.8509C13.3486 12.8008 13.2807 12.7726 13.2099 12.7725H3.43742C3.36656 12.7725 3.2986 12.8006 3.24849 12.8507C3.19839 12.9009 3.17024 12.9688 3.17024 13.0397C3.17024 13.1105 3.19839 13.1785 3.24849 13.2286C3.2986 13.2787 3.36656 13.3069 3.43742 13.3069H13.21C13.2808 13.3068 13.3487 13.2787 13.3988 13.2286C13.4489 13.1785 13.4771 13.1106 13.4771 13.0398V13.0397ZM13.4771 11.1535C13.4771 11.0827 13.449 11.0147 13.3988 10.9646C13.3487 10.9145 13.2808 10.8863 13.2099 10.8863H3.43742C3.36656 10.8863 3.2986 10.9145 3.24849 10.9646C3.19839 11.0147 3.17024 11.0827 3.17024 11.1535C3.17024 11.2244 3.19839 11.2923 3.24849 11.3424C3.2986 11.3926 3.36656 11.4207 3.43742 11.4207H13.21C13.2808 11.4206 13.3486 11.3924 13.3987 11.3423C13.4487 11.2923 13.4769 11.2244 13.4771 11.1536V11.1535ZM13.4771 9.26628C13.477 9.19547 13.4488 9.12758 13.3987 9.07751C13.3486 9.02743 13.2807 8.99924 13.2099 8.9991H3.43742C3.36656 8.9991 3.2986 9.02725 3.24849 9.07735C3.19839 9.12746 3.17024 9.19542 3.17024 9.26628C3.17024 9.33715 3.19839 9.40511 3.24849 9.45522C3.2986 9.50532 3.36656 9.53347 3.43742 9.53347H13.21C13.2808 9.53345 13.3487 9.50531 13.3988 9.45523C13.4489 9.40515 13.4771 9.33723 13.4771 9.2664V9.26628ZM13.4771 7.38013C13.477 7.30931 13.4488 7.24143 13.3987 7.19135C13.3486 7.14127 13.2807 7.11308 13.2099 7.11294H3.43742C3.36656 7.11294 3.2986 7.14109 3.24849 7.1912C3.19839 7.2413 3.17024 7.30926 3.17024 7.38013C3.17024 7.45099 3.19839 7.51895 3.24849 7.56906C3.2986 7.61916 3.36656 7.64731 3.43742 7.64731H13.21C13.2808 7.64716 13.3486 7.61897 13.3987 7.56892C13.4487 7.51887 13.4769 7.45103 13.4771 7.38024V7.38013ZM13.4771 5.49397C13.4771 5.42311 13.449 5.35515 13.3988 5.30504C13.3487 5.25493 13.2808 5.22678 13.2099 5.22678H3.43742C3.36656 5.22678 3.2986 5.25493 3.24849 5.30504C3.19839 5.35515 3.17024 5.42311 3.17024 5.49397C3.17024 5.56483 3.19839 5.63279 3.24849 5.6829C3.2986 5.73301 3.36656 5.76116 3.43742 5.76116H13.21C13.2808 5.761 13.3486 5.73281 13.3987 5.68276C13.4487 5.63271 13.4769 5.56487 13.4771 5.49408V5.49397ZM13.4771 3.60673C13.477 3.53591 13.4488 3.46803 13.3987 3.41796C13.3486 3.36788 13.2807 3.33968 13.2099 3.33955H6.77174C6.70088 3.33955 6.63292 3.3677 6.58281 3.4178C6.5327 3.46791 6.50455 3.53587 6.50455 3.60673C6.50455 3.6776 6.5327 3.74556 6.58281 3.79566C6.63292 3.84577 6.70088 3.87392 6.77174 3.87392H13.21C13.2808 3.8739 13.3487 3.84578 13.3988 3.79574C13.4489 3.7457 13.477 3.67783 13.4771 3.60703V3.60673ZM13.4771 1.72058C13.477 1.64976 13.4488 1.58188 13.3987 1.5318C13.3486 1.48172 13.2807 1.45353 13.2099 1.45339H6.77174C6.70088 1.45339 6.63292 1.48154 6.58281 1.53165C6.5327 1.58175 6.50455 1.64971 6.50455 1.72058C6.50455 1.79144 6.5327 1.8594 6.58281 1.90951C6.63292 1.95961 6.70088 1.98776 6.77174 1.98776H13.21C13.2808 1.98761 13.3486 1.95942 13.3987 1.90937C13.4487 1.85932 13.4769 1.79148 13.4771 1.72069V1.72058Z"/>
                    </svg>     
                    <p>Обращения</p>
                    <svg class="arrow2" width="7" height="11" viewBox="0 0 7 11" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M1 10L5.5 5.5L1 1" stroke-linecap="round"/>
                    </svg> 
                </div>
            </li>
            <li tabindex="0" class="namenav" class:active={selectView === "BP1" || selectView === "BP2" || selectView === "BP3" || selectView === "BP4"}>
                <div class="navblock">
                    <svg class="icon" width="19" height="19" viewBox="0 0 19 19" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <g clip-path="url(#clip0_1141_95)">
                        <path d="M17.611 18.2592C16.4746 16.9147 14.4385 15.8895 11.9776 15.4711C11.7369 14.0299 11.3072 13.3566 11.296 13.3379C11.4833 13.25 11.7094 13.2201 11.9676 13.2771C12.2824 13.3479 12.4973 13.5149 12.6276 13.7264C12.6655 13.788 12.7621 13.7672 12.7733 13.6956C12.7904 13.5797 12.782 13.4615 12.7488 13.3491C12.6588 13.0331 12.4127 12.7704 12.0921 12.7012C11.8907 12.6582 11.6805 12.6877 11.4987 12.7845C11.6783 12.6326 11.8866 12.5183 12.1113 12.4485C12.6442 12.2778 13.1089 12.361 13.4704 12.5917C13.4837 12.6002 13.4993 12.6045 13.5152 12.6039C13.531 12.6032 13.5463 12.5978 13.5589 12.5883C13.5716 12.5788 13.5811 12.5657 13.5861 12.5507C13.5911 12.5357 13.5914 12.5195 13.587 12.5043C13.5266 12.2873 13.4071 12.0746 13.1856 11.8793C12.8125 11.5512 12.2932 11.4087 11.8215 11.5649C11.6733 11.614 11.5345 11.6878 11.4109 11.7831L11.438 11.7494C11.7856 11.3409 12.2033 11.1643 12.6151 11.1543C12.7046 11.1523 12.7446 11.0361 12.6759 10.9782C12.5143 10.8442 12.3221 10.7523 12.1163 10.7109C11.5908 10.5989 11.0378 10.7563 10.7039 11.1768C10.5875 11.323 10.5019 11.4913 10.4524 11.6715C10.3071 11.5537 10.1393 11.4667 9.95934 11.4158C9.44302 11.2693 8.90212 11.4641 8.54069 11.861C8.38455 12.0321 8.29419 12.2107 8.2463 12.3894C8.22132 12.4814 8.32709 12.5559 8.40662 12.5034C8.74514 12.2769 9.18277 12.1803 9.69785 12.3077C9.71201 12.3119 9.72575 12.3157 9.73991 12.319C9.58371 12.3132 9.42768 12.3342 9.27854 12.381C8.80427 12.5288 8.46533 12.9469 8.35332 13.4312C8.28919 13.7102 8.31251 13.9471 8.38496 14.1536C8.4112 14.2286 8.51446 14.2302 8.54527 14.1569C8.70808 13.7664 9.03412 13.4362 9.55877 13.2675C9.78164 13.1928 10.0173 13.1639 10.2517 13.1826C10.0473 13.2094 9.85927 13.3082 9.72117 13.4611C9.50048 13.7043 9.45426 14.0603 9.56544 14.3701C9.60999 14.4951 9.67412 14.5917 9.74907 14.6679C9.79903 14.7191 9.88897 14.6799 9.88356 14.6087C9.86482 14.3601 9.94269 14.102 10.1571 13.8605C10.3224 13.6752 10.5106 13.5561 10.7014 13.5294C10.7009 13.5294 11.0032 14.2719 11.0453 15.3432C10.5308 15.2901 10.014 15.2633 9.49673 15.2629C8.20383 15.2629 6.97422 15.424 5.86162 15.7159C6.09231 10.6888 9.1861 7.3585 9.18985 7.34184C9.57085 7.59376 9.89272 7.97934 10.1009 8.52107C10.372 9.22477 10.3254 9.86227 10.0801 10.3886C10.0097 10.5393 10.1746 10.6988 10.3262 10.6284C10.5753 10.5113 10.7958 10.3411 10.972 10.1296C11.4688 9.54622 11.6616 8.72926 11.3797 8.01723C11.2027 7.57019 10.8736 7.20003 10.4503 6.97208C10.9333 7.10616 11.42 7.35059 11.8943 7.72659C12.9012 8.52648 13.3413 9.51749 13.3758 10.5023C13.3825 10.6871 13.6094 10.77 13.7281 10.6276C14.0558 10.2362 14.3027 9.73735 14.3914 9.07487C14.5455 7.92521 14.1499 6.73391 13.2359 6.01979C12.9489 5.79457 12.6254 5.62009 12.2795 5.50388L12.3807 5.51429C13.6099 5.65795 14.4868 6.22841 15.0398 7.00165C15.1688 7.18195 15.4616 7.10491 15.4824 6.88464C15.5253 6.45493 15.4736 5.99106 15.2738 5.48806C14.8116 4.32383 13.7906 3.4544 12.5418 3.34864C12.1069 3.31168 11.669 3.36353 11.2547 3.50104C11.2952 3.06707 11.2467 2.62941 11.1123 2.2148C10.728 1.02225 9.6508 0.224025 8.41245 0.0353989C7.86322 -0.0478796 7.38895 0.0120809 6.97173 0.158651C6.77436 0.227772 6.7652 0.514667 6.95882 0.592949C7.85073 0.952712 8.61731 1.68057 9.04037 2.85979C9.05202 2.89185 9.06285 2.92391 9.07326 2.95556C8.88193 2.64493 8.63886 2.36932 8.35457 2.14068C7.45224 1.41158 6.20223 1.2954 5.11753 1.70472C4.47295 1.94789 4.03657 2.31598 3.72719 2.73904C3.70481 2.76853 3.69201 2.80418 3.69051 2.84118C3.68901 2.87817 3.69888 2.91474 3.7188 2.94595C3.73871 2.97717 3.76772 3.00153 3.8019 3.01575C3.83609 3.02998 3.87381 3.03339 3.90999 3.02551C4.89018 2.82107 5.97197 3.01885 6.99463 3.83248C7.46848 4.2089 7.817 4.62738 8.05685 5.06834C7.73953 4.70689 7.30425 4.46977 6.82849 4.39919C6.07107 4.28552 5.31865 4.65736 4.8627 5.27404C4.69763 5.49187 4.5821 5.74311 4.52417 6.01022C4.48919 6.17511 4.68365 6.30086 4.81689 6.19718C5.27409 5.84158 5.88244 5.65379 6.62737 5.75872C7.23905 5.84491 7.70832 6.09599 8.04477 6.44202C7.98481 6.49323 4.25227 9.27848 1.80679 17.8062C1.65772 17.9478 1.51906 18.0952 1.39165 18.2459C1.14098 18.5428 1.35292 19 1.74059 19H17.2579C17.6397 19.0021 17.8579 18.5515 17.611 18.2592Z"/>
                        </g>
                        <defs>
                        <clipPath id="clip0_1141_95">
                        <rect width="19" height="19" fill="white"/>
                        </clipPath>
                        </defs>
                    </svg>                                                              
                    <p>Сезонный пропуск</p>
                    <svg class="arrow1" width="11" height="7" viewBox="0 0 11 7" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M1 1L5.5 5.5L10 1" stroke-linecap="round"/>
                    </svg> 
                </div>
                <ul class="dropdown">
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("BP1")} class:active={selectView === "BP1"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Главная</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("BP2")} class:active={selectView === "BP2"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Купить опыт</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("BP3")} class:active={selectView === "BP3"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Ежедневные задания</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("BP4")} class:active={selectView === "BP4"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21H11" stroke-width="2"/>
                        </svg>                                                                       
                        <p>Квест</p>                                    
                    </li>
                </ul>   
            </li>
            <li tabindex="0" class="namenav" class:active={selectView === "Shop" || selectView === "Shopcl" || selectView === "Stock" || selectView === "Shopcar" || selectView === "Roulette" || selectView === "Money" || selectView === "Premium" || selectView === "Cases" || selectView === "Personal"}>
                <div class="navblock">
                    <svg class="icon" width="19" height="19" viewBox="0 0 19 19" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M16.625 16.8333C16.15 16.8333 15.8333 16.5167 15.8333 16.0417V11.2917H11.0833V16.0417C11.0833 16.5167 10.7667 16.8333 10.2917 16.8333H2.375C1.9 16.8333 1.58333 16.5167 1.58333 16.0417V11.2917H0.791667C0.316667 11.2917 0 10.975 0 10.5V8.91667C0 8.8375 -7.66789e-08 8.67917 0.0791666 8.6L1.6625 4.64167C1.74167 4.325 2.05833 4.16667 2.375 4.16667H16.625C16.9417 4.16667 17.2583 4.325 17.3375 4.64167L18.9208 8.6C18.9208 8.67917 19 8.75833 19 8.91667V10.5C19 10.975 18.6833 11.2917 18.2083 11.2917H17.4167V16.0417C17.4167 16.5167 17.1 16.8333 16.625 16.8333ZM3.16667 15.25H9.5V11.2917H3.16667V15.25ZM16.625 2.58333H2.375C1.9 2.58333 1.58333 2.26667 1.58333 1.79167C1.58333 1.31667 1.9 1 2.375 1H16.625C17.1 1 17.4167 1.31667 17.4167 1.79167C17.4167 2.26667 17.1 2.58333 16.625 2.58333Z"/>
                    </svg>                                      
                    <p>Магазин</p>
                    <svg class="arrow1" width="11" height="7" viewBox="0 0 11 7" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M1 1L5.5 5.5L10 1" stroke-linecap="round"/>
                    </svg> 
                </div>
                <ul class="dropdown">
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Cases")} class:active={selectView === "Cases" || selectView === "Roulette"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Кейсы</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Money")} class:active={selectView === "Money"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Валюта</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Premium")} class:active={selectView === "Premium"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Премиум аккаунт</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Shop")} class:active={selectView === "Shop"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Персонаж</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Shopcl")} class:active={selectView === "Shopcl"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Одежда</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Shopcar")} class:active={selectView === "Shopcar"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Транспорт</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Stock")} class:active={selectView === "Stock"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Хранилище</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Personal")} class:active={selectView === "Personal"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21H11" stroke-width="2"/>
                        </svg>                                                                       
                        <p>Стартовые наборы</p>                                    
                    </li>
                </ul>
            </li>
            <li tabindex="0" class="namenav" class:active={selectView === "MiniGames"}>
                <div class="navblock">
                    <svg class="icon"width="19" height="19" viewBox="0 0 19 19" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M18.9589 12.0813L17.9396 5.97451C17.7574 4.86291 17.1853 3.85249 16.326 3.12423C15.4666 2.39597 14.376 1.99744 13.2496 2.00001H5.74906C4.62286 1.99776 3.53262 2.39644 2.67353 3.12467C1.81445 3.8529 1.24261 4.86314 1.0604 5.97451L0.0411206 12.0813C-0.0744178 12.7736 0.0552922 13.4845 0.407872 14.0914C0.760453 14.6982 1.31379 15.163 1.97242 15.4055C2.63105 15.648 3.35366 15.653 4.01559 15.4197C4.67752 15.1863 5.23725 14.7293 5.59821 14.1274L6.75747 12.1928H12.2412L13.4018 14.1274C13.7628 14.7293 14.3225 15.1863 14.9844 15.4197C15.6463 15.653 16.369 15.648 17.0276 15.4055C17.6862 15.163 18.2395 14.6982 18.5921 14.0914C18.9447 13.4845 19.0744 12.7736 18.9589 12.0813ZM13.5764 5.3976C13.7108 5.3976 13.8422 5.43745 13.9539 5.51212C14.0657 5.58678 14.1528 5.69291 14.2042 5.81708C14.2556 5.94124 14.2691 6.07787 14.2429 6.20968C14.2167 6.3415 14.1519 6.46257 14.0569 6.55761C13.9619 6.65264 13.8408 6.71736 13.709 6.74358C13.5772 6.7698 13.4405 6.75634 13.3164 6.70491C13.1922 6.65348 13.0861 6.56638 13.0114 6.45464C12.9368 6.34289 12.8969 6.21151 12.8969 6.07712C12.8969 5.8969 12.9685 5.72406 13.0959 5.59662C13.2234 5.46919 13.3962 5.3976 13.5764 5.3976ZM6.78125 8.11567H6.10173V8.79519C6.10173 8.9754 6.03014 9.14824 5.90271 9.27568C5.77527 9.40311 5.60244 9.4747 5.42222 9.4747C5.242 9.4747 5.06916 9.40311 4.94173 9.27568C4.81429 9.14824 4.7427 8.9754 4.7427 8.79519V8.11567H4.06318C3.88296 8.11567 3.71013 8.04408 3.58269 7.91664C3.45526 7.78921 3.38367 7.61637 3.38367 7.43615C3.38367 7.25593 3.45526 7.08309 3.58269 6.95566C3.71013 6.82823 3.88296 6.75663 4.06318 6.75663H4.7427V6.07712C4.7427 5.8969 4.81429 5.72406 4.94173 5.59662C5.06916 5.46919 5.242 5.3976 5.42222 5.3976C5.60244 5.3976 5.77527 5.46919 5.90271 5.59662C6.03014 5.72406 6.10173 5.8969 6.10173 6.07712V6.75663H6.78125C6.96147 6.75663 7.13431 6.82823 7.26174 6.95566C7.38918 7.08309 7.46077 7.25593 7.46077 7.43615C7.46077 7.61637 7.38918 7.78921 7.26174 7.91664C7.13431 8.04408 6.96147 8.11567 6.78125 8.11567ZM12.2174 8.11567C12.083 8.11567 11.9516 8.07581 11.8399 8.00115C11.7281 7.92648 11.641 7.82036 11.5896 7.69619C11.5382 7.57203 11.5247 7.4354 11.5509 7.30358C11.5771 7.17177 11.6419 7.05069 11.7369 6.95566C11.8319 6.86063 11.953 6.79591 12.0848 6.76969C12.2166 6.74347 12.3533 6.75693 12.4774 6.80836C12.6016 6.85979 12.7077 6.94688 12.7824 7.05863C12.8571 7.17038 12.8969 7.30175 12.8969 7.43615C12.8969 7.61637 12.8253 7.78921 12.6979 7.91664C12.5704 8.04408 12.3976 8.11567 12.2174 8.11567ZM13.5764 9.4747C13.442 9.4747 13.3107 9.43485 13.1989 9.36018C13.0872 9.28552 13.0001 9.17939 12.9486 9.05522C12.8972 8.93106 12.8837 8.79443 12.91 8.66262C12.9362 8.5308 13.0009 8.40973 13.0959 8.31469C13.191 8.21966 13.312 8.15494 13.4439 8.12872C13.5757 8.10251 13.7123 8.11596 13.8365 8.16739C13.9606 8.21882 14.0668 8.30592 14.1414 8.41767C14.2161 8.52941 14.2559 8.66079 14.2559 8.79519C14.2559 8.9754 14.1844 9.14824 14.0569 9.27568C13.9295 9.40311 13.7566 9.4747 13.5764 9.4747ZM14.9355 8.11567C14.8011 8.11567 14.6697 8.07581 14.5579 8.00115C14.4462 7.92648 14.3591 7.82036 14.3077 7.69619C14.2562 7.57203 14.2428 7.4354 14.269 7.30358C14.2952 7.17177 14.3599 7.05069 14.455 6.95566C14.55 6.86063 14.6711 6.79591 14.8029 6.76969C14.9347 6.74347 15.0713 6.75693 15.1955 6.80836C15.3197 6.85979 15.4258 6.94688 15.5005 7.05863C15.5751 7.17038 15.615 7.30175 15.615 7.43615C15.615 7.61637 15.5434 7.78921 15.416 7.91664C15.2885 8.04408 15.1157 8.11567 14.9355 8.11567Z"/>
                    </svg>                                      
                    <p>Мини-игры</p>
                    <svg class="arrow1" width="11" height="7" viewBox="0 0 11 7" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M1 1L5.5 5.5L10 1" stroke-linecap="round"/>
                    </svg> 
                </div>
                <ul class="dropdown">
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("MiniGames")} class:active={selectView === "MiniGames"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21H11" stroke-width="2"/>
                        </svg>                                                                       
                        <p>Сапёр</p>                                    
                    </li>
                </ul>
            </li>
            <li tabindex="0" class="namenav"  class:active={selectView === "General" || selectView === "State" || selectView === "Crime"}>
                <div class="navblock">
                    <svg class="icon" width="19" height="19" viewBox="0 0 19 19" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M13.5762 3.06319H16.4572L13.795 0.401C13.722 0.364532 13.6491 0.291595 13.5762 0.255127V2.29735V3.06319Z"/>
                        <path d="M12.8464 4.52207C12.4453 4.52207 12.1171 4.19386 12.1171 3.79271V0H4.3858C4.02111 0 3.65643 0.145873 3.40115 0.401152C3.14587 0.65643 3 1.02111 3 1.3858V17.6142C3 18.38 3.61996 19 4.3858 19H15.5816C16.3474 19 16.9674 18.38 16.9674 17.6142V4.52207H12.8464ZM13.8311 6.34549C14.2322 6.34549 14.5605 6.6737 14.5605 7.07486C14.5605 7.47601 14.1958 7.80422 13.8311 7.80422H9.01727C8.61612 7.80422 8.28791 7.47601 8.28791 7.07486C8.28791 6.6737 8.65259 6.34549 9.01727 6.34549H9.60077H13.8311ZM6.42802 8.8618C6.97505 8.8618 7.41267 9.29942 7.41267 9.84645C7.41267 10.3935 6.97505 10.8311 6.42802 10.8311C5.881 10.8311 5.44338 10.3935 5.44338 9.84645C5.44338 9.29942 5.91747 8.8618 6.42802 8.8618ZM5.44338 7.07486C5.44338 6.52783 5.881 6.09021 6.42802 6.09021C6.97505 6.09021 7.41267 6.52783 7.41267 7.07486C7.41267 7.62188 6.97505 8.0595 6.42802 8.0595C5.881 8.0595 5.44338 7.62188 5.44338 7.07486ZM6.42802 11.6334C6.97505 11.6334 7.41267 12.071 7.41267 12.618C7.41267 13.1651 6.97505 13.6027 6.42802 13.6027C5.881 13.6027 5.44338 13.1651 5.44338 12.618C5.44338 12.071 5.91747 11.6334 6.42802 11.6334ZM6.42802 14.3685C6.97505 14.3685 7.41267 14.8061 7.41267 15.3532C7.41267 15.9002 6.97505 16.3378 6.42802 16.3378C5.881 16.3378 5.44338 15.9002 5.44338 15.3532C5.44338 14.8061 5.91747 14.3685 6.42802 14.3685ZM8.98081 11.8887H13.7946C14.1958 11.8887 14.524 12.2169 14.524 12.618C14.524 13.0192 14.1593 13.3474 13.7946 13.3474H8.98081C8.57966 13.3474 8.25144 13.0192 8.25144 12.618C8.25144 12.2169 8.61612 11.8887 8.98081 11.8887ZM8.25144 9.84645C8.25144 9.4453 8.61612 9.11708 8.98081 9.11708H13.7946C14.1958 9.11708 14.524 9.4453 14.524 9.84645C14.524 10.2476 14.1593 10.5758 13.7946 10.5758H8.98081C8.57966 10.5758 8.28791 10.2476 8.25144 9.84645ZM8.98081 14.6238H13.7946C14.1958 14.6238 14.524 14.952 14.524 15.3532C14.524 15.7543 14.1593 16.0825 13.7946 16.0825H8.98081C8.57966 16.0825 8.25144 15.7543 8.25144 15.3532C8.25144 14.952 8.61612 14.6238 8.98081 14.6238Z"/>
                    </svg>                                     
                    <p>Правила сервера</p>
                    <svg class="arrow1" width="11" height="7" viewBox="0 0 11 7" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M1 1L5.5 5.5L10 1" stroke-linecap="round"/>
                    </svg> 
                </div>
                <ul class="dropdown">
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("General")} class:active={selectView === "General"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Общие</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("State")} class:active={selectView === "State"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21M1 21H11M1 21V42" stroke-width="2"/>
                        </svg>
                        <p>Государственные орг.</p>                                    
                    </li>
                    <li on:keypress={() => {}} on:click={() => window.gameMenuView ("Crime")} class:active={selectView === "Crime"}>
                        <svg width="11" height="42" viewBox="0 0 11 42" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M1 0V21H11" stroke-width="2"/>
                        </svg>                                                                       
                        <p>Криминальные орг.</p>                                    
                    </li>
                </ul>
            </li>
            <li tabindex="0" class="namenav" on:keypress={() => {}} on:click={() => window.gameMenuView ("FAQ")} class:active={selectView === "FAQ"}>
                <div class="navblock">
                    <svg class="icon" width="19" height="19" viewBox="0 0 19 19" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M13.5762 3.06319H16.4572L13.795 0.401C13.722 0.364532 13.6491 0.291595 13.5762 0.255127V2.29735V3.06319Z"/>
                        <path d="M12.8464 4.52207C12.4453 4.52207 12.1171 4.19386 12.1171 3.79271V0H4.3858C4.02111 0 3.65643 0.145873 3.40115 0.401152C3.14587 0.65643 3 1.02111 3 1.3858V17.6142C3 18.38 3.61996 19 4.3858 19H15.5816C16.3474 19 16.9674 18.38 16.9674 17.6142V4.52207H12.8464ZM13.8311 6.34549C14.2322 6.34549 14.5605 6.6737 14.5605 7.07486C14.5605 7.47601 14.1958 7.80422 13.8311 7.80422H9.01727C8.61612 7.80422 8.28791 7.47601 8.28791 7.07486C8.28791 6.6737 8.65259 6.34549 9.01727 6.34549H9.60077H13.8311ZM6.42802 8.8618C6.97505 8.8618 7.41267 9.29942 7.41267 9.84645C7.41267 10.3935 6.97505 10.8311 6.42802 10.8311C5.881 10.8311 5.44338 10.3935 5.44338 9.84645C5.44338 9.29942 5.91747 8.8618 6.42802 8.8618ZM5.44338 7.07486C5.44338 6.52783 5.881 6.09021 6.42802 6.09021C6.97505 6.09021 7.41267 6.52783 7.41267 7.07486C7.41267 7.62188 6.97505 8.0595 6.42802 8.0595C5.881 8.0595 5.44338 7.62188 5.44338 7.07486ZM6.42802 11.6334C6.97505 11.6334 7.41267 12.071 7.41267 12.618C7.41267 13.1651 6.97505 13.6027 6.42802 13.6027C5.881 13.6027 5.44338 13.1651 5.44338 12.618C5.44338 12.071 5.91747 11.6334 6.42802 11.6334ZM6.42802 14.3685C6.97505 14.3685 7.41267 14.8061 7.41267 15.3532C7.41267 15.9002 6.97505 16.3378 6.42802 16.3378C5.881 16.3378 5.44338 15.9002 5.44338 15.3532C5.44338 14.8061 5.91747 14.3685 6.42802 14.3685ZM8.98081 11.8887H13.7946C14.1958 11.8887 14.524 12.2169 14.524 12.618C14.524 13.0192 14.1593 13.3474 13.7946 13.3474H8.98081C8.57966 13.3474 8.25144 13.0192 8.25144 12.618C8.25144 12.2169 8.61612 11.8887 8.98081 11.8887ZM8.25144 9.84645C8.25144 9.4453 8.61612 9.11708 8.98081 9.11708H13.7946C14.1958 9.11708 14.524 9.4453 14.524 9.84645C14.524 10.2476 14.1593 10.5758 13.7946 10.5758H8.98081C8.57966 10.5758 8.28791 10.2476 8.25144 9.84645ZM8.98081 14.6238H13.7946C14.1958 14.6238 14.524 14.952 14.524 15.3532C14.524 15.7543 14.1593 16.0825 13.7946 16.0825H8.98081C8.57966 16.0825 8.25144 15.7543 8.25144 15.3532C8.25144 14.952 8.61612 14.6238 8.98081 14.6238Z"/>
                    </svg>      
                    <p>FAQ</p>
                    <svg class="arrow2" width="7" height="11" viewBox="0 0 7 11" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M1 10L5.5 5.5L1 1" stroke-linecap="round"/>
                    </svg>  
                </div>
            </li>
            <li tabindex="0" class="namenav" on:keypress={() => {}} on:click={() => { window.gameMenuView ("Settings"); onClickPage(); }} class:active={selectView === "Settings"}>
                <div class="navblock">
                    <svg class="icon" width="19" height="19" viewBox="0 0 19 19" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <g clip-path="url(#clip0_729_7482)">
                        <path d="M18.577 7.88945L16.3541 7.34395C16.2316 6.9543 16.0758 6.57578 15.8865 6.2084L17.0221 4.31582C17.1557 4.09316 17.1223 3.81484 16.9441 3.63672L15.3633 2.05586C15.1852 1.87773 14.9068 1.84434 14.6842 1.97793L12.7916 3.11348C12.4242 2.92422 12.0457 2.76836 11.6561 2.6459L11.1217 0.423047C11.0549 0.178125 10.8322 0 10.5762 0H8.34961C8.09355 0 7.8709 0.178125 7.8041 0.423047L7.26973 2.6459C6.88008 2.76836 6.50156 2.92422 6.13418 3.11348L4.2416 1.97793C4.01895 1.84434 3.74062 1.87773 3.5625 2.05586L1.98164 3.63672C1.80352 3.81484 1.77012 4.09316 1.90371 4.31582L3.03926 6.2084C2.85 6.57578 2.69414 6.9543 2.57168 7.34395L0.423047 7.88945C0.178125 7.94512 0 8.16777 0 8.42383V10.6504C0 10.9064 0.178125 11.1291 0.423047 11.1848L2.57168 11.7303C2.69414 12.1199 2.85 12.4984 3.03926 12.8658L1.90371 14.7584C1.77012 14.9811 1.80352 15.2594 1.98164 15.4375L3.5625 17.0184C3.74062 17.1965 4.01895 17.2299 4.2416 17.0963L6.13418 15.9607C6.50156 16.15 6.88008 16.3059 7.26973 16.4283L7.8041 18.577C7.8709 18.8219 8.09355 19 8.34961 19H10.5762C10.8322 19 11.0549 18.8219 11.1217 18.577L11.6561 16.4283C12.0457 16.3059 12.4242 16.15 12.7916 15.9607L14.6842 17.0963C14.9068 17.2299 15.1852 17.1965 15.3633 17.0184L16.9441 15.4375C17.1223 15.2594 17.1557 14.9811 17.0221 14.7584L15.8865 12.8658C16.0758 12.4984 16.2316 12.1199 16.3541 11.7303L18.577 11.1848C18.8219 11.1291 19 10.9064 19 10.6504V8.42383C19 8.16777 18.8219 7.94512 18.577 7.88945ZM9.46289 12.3203C7.92656 12.3203 6.67969 11.0734 6.67969 9.53711C6.67969 8.00078 7.92656 6.75391 9.46289 6.75391C10.9992 6.75391 12.2461 8.00078 12.2461 9.53711C12.2461 11.0734 10.9992 12.3203 9.46289 12.3203Z"/>
                        </g>
                        <defs>
                        <clipPath id="clip0_729_7482">
                        <rect width="19" height="19" fill="white"/>
                        </clipPath>
                        </defs>
                    </svg>                                     
                    <p>Настройки</p>
                    <svg class="arrow2" width="7" height="11" viewBox="0 0 7 11" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M1 10L5.5 5.5L1 1" stroke-linecap="round"/>
                    </svg> 
                </div>
            </li>
        </div>
    </div>
    <div class="rightmenu" class:active={selectView != "Menu"}>
        <div class="topnav">
            {#if selectView === "Shopcl" || selectView === "Shopcar" || selectView === "Anim0" || selectView === "Anim1" || selectView === "Anim2" || selectView === "Anim3" || selectView === "Anim4" || selectView === "Anim5" || selectView === "Anim6" || selectView === "Anim7"}
            <div class="bginput">
                <svg width="17" height="17" viewBox="0 0 17 17" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <path d="M14.5637 13.0613L11.8665 10.3642C12.4434 9.49716 12.7508 8.47886 12.75 7.4375C12.75 4.50819 10.3668 2.125 7.4375 2.125C4.50819 2.125 2.125 4.50819 2.125 7.4375C2.125 10.3668 4.50819 12.75 7.4375 12.75C8.47886 12.7508 9.49716 12.4434 10.3642 11.8665L13.0613 14.5637C13.1593 14.6652 13.2766 14.7461 13.4062 14.8018C13.5358 14.8575 13.6752 14.8868 13.8163 14.888C13.9574 14.8892 14.0973 14.8624 14.2279 14.8089C14.3585 14.7555 14.4771 14.6766 14.5769 14.5769C14.6766 14.4771 14.7555 14.3585 14.8089 14.2279C14.8624 14.0973 14.8892 13.9574 14.888 13.8163C14.8868 13.6752 14.8575 13.5358 14.8018 13.4062C14.7461 13.2766 14.6652 13.1593 14.5637 13.0613ZM3.71875 7.4375C3.71875 5.38688 5.38688 3.71875 7.4375 3.71875C9.48812 3.71875 11.1562 5.38688 11.1562 7.4375C11.1562 9.48812 9.48812 11.1562 7.4375 11.1562C5.38688 11.1562 3.71875 9.48812 3.71875 7.4375Z" fill="white" fill-opacity="0.4"/>
                </svg>                        
                <input bind:value={searchText} placeholder="Введите значение для поиска"> 
            </div>
            {:else}
            <h1>{replacements.find(r => selectView === r.word)?.replacement ?? selectView}</h1>
            {/if}
            {#if selectView ==="Shopcl"}
                <div class="cataloglisttype">
                    <div class="blocktype" class:active={catlist} on:keypress on:click={opencatlisttype}>
                        <svg width="12" height="12" viewBox="0 0 12 12" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <g clip-path="url(#clip0_122_2)">
                            <path d="M10.8525 10.3534L11.8966 11.3975C12.0345 11.5353 12.0345 11.7588 11.8966 11.8966C11.7588 12.0345 11.5353 12.0345 11.3975 11.8966L10.3534 10.8525C9.97402 11.1302 9.50616 11.2941 9 11.2941C7.73299 11.2941 6.70589 10.267 6.70589 9C6.70589 7.73299 7.73299 6.70589 9 6.70589C10.267 6.70589 11.2941 7.73299 11.2941 9C11.2941 9.50616 11.1302 9.97402 10.8525 10.3534ZM9 7.41176C8.12283 7.41176 7.41176 8.12283 7.41176 9C7.41176 9.87717 8.12283 10.5882 9 10.5882C9.87717 10.5882 10.5882 9.87717 10.5882 9C10.5882 8.12283 9.87717 7.41176 9 7.41176ZM0.705891 0H4.2353C4.62516 0 4.94119 0.316031 4.94119 0.705891V4.2353C4.94119 4.62516 4.62516 4.94119 4.2353 4.94119H0.705891C0.316031 4.94119 0 4.62516 0 4.2353V0.705891C0 0.316031 0.316031 0 0.705891 0ZM0.705891 6.70589H4.2353C4.62516 6.70589 4.94119 7.02192 4.94119 7.41178V10.9412C4.94119 11.331 4.62516 11.6471 4.2353 11.6471H0.705891C0.316031 11.6471 0 11.331 0 10.9412V7.41176C0 7.02192 0.316031 6.70589 0.705891 6.70589ZM7.41176 0H10.9412C11.331 0 11.6471 0.316031 11.6471 0.705891V4.2353C11.6471 4.62516 11.331 4.94119 10.9412 4.94119H7.41176C7.0219 4.94119 6.70587 4.62516 6.70587 4.2353V0.705891C6.70589 0.316031 7.02192 0 7.41176 0Z"/>
                            </g>
                            <defs>
                            <clipPath id="clip0_122_2">
                            <rect width="12" height="12" fill="white"/>
                            </clipPath>
                            </defs>
                        </svg>
                        {#if pageload}                            
                            <p>{replacements.find(r => pageload === r.word)?.replacement ?? pageload}</p>
                            {:else}
                            <p>Все</p>
                        {/if}
                        <svg width="12" height="12" viewBox="0 0 12 12" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <g clip-path="url(#clip0_122_6)">
                            <path d="M11.6894 2.56167C11.2753 2.1466 10.6035 2.1466 10.1889 2.56167L6.00017 7.18501L1.81141 2.56167C1.39683 2.1466 0.725501 2.1466 0.310938 2.56167C-0.103646 2.97675 -0.103646 3.64907 0.310938 4.06414L5.18351 9.44271C5.40778 9.66698 5.70648 9.76588 6.00019 9.74741C6.29338 9.76588 6.59259 9.66698 6.81684 9.44271L11.6894 4.06414C12.1035 3.64907 12.1035 2.97676 11.6894 2.56167Z"/>
                            </g>
                            <defs>
                            <clipPath id="clip0_122_6">
                            <rect width="12" height="12" fill="white"/>
                            </clipPath>
                            </defs>
                        </svg>                            
                    </div>
                    <ul class:active={catlist}>
                        <li on:keypress={() => {}} on:click={()=> onSelectTypeCat (0)}>Все</li>
                        <li on:keypress={() => {}} on:click={()=> onSelectTypeCat (1)}>Маски</li>
                        <li on:keypress={() => {}} on:click={()=> onSelectTypeCat (2)}>Головные уборы</li>
                        <li on:keypress={() => {}} on:click={()=> onSelectTypeCat (3)}>Аксессуары</li>
                        <li on:keypress={() => {}} on:click={()=> onSelectTypeCat (4)}>Верхняя одежда</li>
                        <li on:keypress={() => {}} on:click={()=> onSelectTypeCat (5)}>Бронежилеты</li>
                        <li on:keypress={() => {}} on:click={()=> onSelectTypeCat (6)}>Нижняя одежда</li>
                        <li on:keypress={() => {}} on:click={()=> onSelectTypeCat (7)}>Штаны</li>
                        <li on:keypress={() => {}} on:click={()=> onSelectTypeCat (8)}>Обувь</li>
                    </ul>
                </div>
                <div class="cataloglisttype">
                    <div class="blocktype" class:active={catFilter} on:keypress on:click={togglePriceFilter}>
                        <svg width="12" height="12" viewBox="0 0 12 12" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <g clip-path="url(#clip0_130_15)">
                            <path d="M10.8621 2.338H1.13788C0.988418 2.33813 0.840397 2.30878 0.702288 2.25164C0.564179 2.1945 0.438691 2.11069 0.333005 2.00501C0.227319 1.89932 0.143509 1.77383 0.0863692 1.63572C0.0292296 1.49761 -0.000117729 1.34959 6.25548e-06 1.20013C-0.000117729 1.05067 0.0292296 0.902647 0.0863692 0.764538C0.143509 0.626429 0.227319 0.500941 0.333005 0.395255C0.438691 0.289569 0.564179 0.205759 0.702288 0.148619C0.840397 0.0914796 0.988418 0.0621323 1.13788 0.0622563H10.8621C11.0116 0.0621323 11.1596 0.0914796 11.2977 0.148619C11.4358 0.205759 11.5613 0.289569 11.667 0.395255C11.7727 0.500941 11.8565 0.626429 11.9136 0.764538C11.9708 0.902647 12.0001 1.05067 12 1.20013C12.0001 1.34959 11.9708 1.49761 11.9136 1.63572C11.8565 1.77383 11.7727 1.89932 11.667 2.00501C11.5613 2.11069 11.4358 2.1945 11.2977 2.25164C11.1596 2.30878 11.0116 2.33813 10.8621 2.338ZM8.30922 6.00145C8.30947 5.85186 8.28022 5.7037 8.22315 5.56543C8.16607 5.42715 8.0823 5.30149 7.97661 5.19563C7.87093 5.08977 7.7454 5.00579 7.60723 4.94849C7.46905 4.89118 7.32093 4.86169 7.17135 4.86169H1.13788C0.988294 4.86169 0.840175 4.89118 0.701999 4.94849C0.563823 5.00579 0.438301 5.08977 0.332614 5.19563C0.226928 5.30149 0.143153 5.42715 0.08608 5.56543C0.0290075 5.7037 -0.00024161 5.85186 6.25548e-06 6.00145C0.000256148 6.30307 0.120249 6.59225 0.333615 6.80544C0.546981 7.01863 0.836261 7.13838 1.13788 7.13838H7.17135C7.47297 7.13838 7.76225 7.01863 7.97561 6.80544C8.18898 6.59225 8.30897 6.30307 8.30922 6.00145ZM8.30922 10.8009C8.30934 10.6514 8.28 10.5034 8.22286 10.3653C8.16572 10.2272 8.08191 10.1017 7.97622 9.99601C7.87054 9.89032 7.74505 9.80651 7.60694 9.74937C7.46883 9.69223 7.32081 9.66289 7.17135 9.66301H1.13788C0.988497 9.66289 0.840554 9.6922 0.702506 9.74928C0.564458 9.80636 0.439013 9.89009 0.333339 9.99568C0.227665 10.1013 0.143835 10.2266 0.0866401 10.3646C0.029445 10.5026 6.20412e-06 10.6506 6.25548e-06 10.7999C-0.000490318 10.9496 0.0285821 11.098 0.0855548 11.2364C0.142528 11.3748 0.226279 11.5007 0.332002 11.6066C0.437725 11.7126 0.563339 11.7967 0.701633 11.854C0.839927 11.9113 0.988179 11.9408 1.13788 11.9406H7.17135C7.32105 11.9408 7.4693 11.9113 7.60759 11.854C7.74589 11.7967 7.8715 11.7126 7.97723 11.6066C8.08295 11.5007 8.1667 11.3748 8.22367 11.2364C8.28065 11.098 8.30972 10.9496 8.30922 10.7999V10.8009Z"/>
                            </g>
                            <defs>
                            <clipPath id="clip0_130_15">
                            <rect width="12" height="12" fill="white"/>
                            </clipPath>
                            </defs>
                        </svg>                                                          
                        <p>{replacements.find(r => pagefilter === r.word)?.replacement ?? pagefilter}</p>
                        <svg width="12" height="12" viewBox="0 0 12 12" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <g clip-path="url(#clip0_122_6)">
                            <path d="M11.6894 2.56167C11.2753 2.1466 10.6035 2.1466 10.1889 2.56167L6.00017 7.18501L1.81141 2.56167C1.39683 2.1466 0.725501 2.1466 0.310938 2.56167C-0.103646 2.97675 -0.103646 3.64907 0.310938 4.06414L5.18351 9.44271C5.40778 9.66698 5.70648 9.76588 6.00019 9.74741C6.29338 9.76588 6.59259 9.66698 6.81684 9.44271L11.6894 4.06414C12.1035 3.64907 12.1035 2.97676 11.6894 2.56167Z"/>
                            </g>
                            <defs>
                            <clipPath id="clip0_122_6">
                            <rect width="12" height="12" fill="white"/>
                            </clipPath>
                            </defs>
                        </svg>                            
                    </div>
                    <ul class:active={catFilter}>
                        <li on:keypress={() => {}} on:click={()=> onSelectTypeFilter (10)}>Цена. По убыванию</li>
                        <li on:keypress={() => {}} on:click={()=> onSelectTypeFilter (11)}>Цена. По возрастанию</li>
                        <li on:keypress={() => {}} on:click={()=> onSelectTypeFilter (12)}>Редкость. По убыванию</li>
                        <li on:keypress={() => {}} on:click={()=> onSelectTypeFilter (13)}>Редкость. По возрастанию</li>
                    </ul>
                </div>
            {/if}
            <div class="balance">
                <svg width="24" height="22" viewBox="0 0 24 22" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <g clip-path="url(#clip0_1121_79)">
                    <mask id="mask0_1121_79" style="mask-type:luminance" maskUnits="userSpaceOnUse" x="0" y="0" width="24" height="22">
                    <path d="M23.7969 0H0.796875V22H23.7969V0Z" fill="white"/>
                    </mask>
                    <g mask="url(#mask0_1121_79)">
                    <path d="M12.2969 22C18.6481 22 23.7969 17.0751 23.7969 11C23.7969 4.92487 18.6481 0 12.2969 0C5.9456 0 0.796875 4.92487 0.796875 11C0.796875 17.0751 5.9456 22 12.2969 22Z" fill="#AA8ED6"/>
                    <path d="M10.5429 13.3187C10.2589 13.6035 10.0691 13.9622 9.99722 14.3497C9.99722 14.7277 10.1885 15.6677 10.7207 15.6677C11.0615 15.6677 11.717 14.9237 11.9209 14.6757C13.4187 12.7754 15.1725 11.0727 17.1366 9.61174L17.1638 9.63774C16.2083 10.8257 14.4759 12.7827 14.4759 14.3487C14.4759 15.1057 14.776 15.9277 15.6761 15.9277C16.8491 15.9277 19.7827 13.7087 20.1099 12.8087L20.0555 12.3087C18.2134 14.0187 17.1492 14.6087 16.7676 14.6087C16.4404 14.6087 16.2898 14.2307 16.2898 13.9697C16.4966 12.8563 17.042 11.826 17.858 11.0077L19.9729 8.54074C20.1371 8.34074 20.5594 7.88774 20.5594 7.62674C20.5594 7.36574 19.9865 6.50474 19.6185 6.50474C19.3729 6.50474 19.0958 6.77874 18.9223 6.92274C17.1366 8.28074 15.5684 9.70374 13.8863 11.0867L13.8581 11.0607C14.4174 10.2387 15.8915 8.30674 15.8915 7.34174C15.8817 7.09682 15.7718 6.86535 15.5855 6.69707C15.3992 6.52878 15.1514 6.43711 14.8952 6.44174C14.3359 6.45474 13.1764 7.43374 12.6715 7.89074L10.5022 9.89874L10.475 9.87274C10.8146 9.13653 11.0972 8.37749 11.3208 7.60174C11.3152 7.4238 11.2584 7.25078 11.1566 7.10174L10.8294 6.55374C10.6924 6.33174 10.6381 6.15374 10.3067 6.13574C9.41393 6.34902 8.54887 6.65692 7.72753 7.05374L6.19072 7.73074L6.15935 7.70674C6.69149 7.26274 6.88281 7.02774 6.88281 6.94974C6.88281 6.87174 6.78767 6.81874 6.71867 6.81874C6.5917 6.85349 6.47499 6.91615 6.37785 7.00174C5.16408 7.82374 5.24562 7.80174 5.12331 7.95474C4.94977 8.45496 4.831 8.97106 4.7689 9.49474C4.70094 9.80774 4.87344 9.97774 5.09612 9.97774C5.7924 9.97774 5.1369 9.95174 9.33962 8.07274C9.46895 7.99858 9.61027 7.94552 9.75781 7.91574C9.9899 7.91574 10.2356 8.17674 8.80226 10.6297L5.95862 14.0757C6.10917 14.4757 6.37681 15.5117 6.95494 15.5117C7.39194 15.5117 7.73276 15.0117 7.96485 14.7117L10.0108 12.0787L13.2852 8.68574C13.3803 8.58574 13.5444 8.41174 13.7034 8.41174C13.8623 8.41174 13.9124 8.55574 13.6762 8.97274C13.0525 10.1008 12.314 11.1673 11.4713 12.1567L10.5429 13.3187Z" fill="white"/>
                    </g>
                    </g>
                    <defs>
                    <clipPath id="clip0_1121_79">
                    <rect width="24" height="22" fill="white"/>
                    </clipPath>
                    </defs>
                </svg>                    
                <p>{format("money", $accountRedbucks)}</p>                           
            </div>
            <div class="btnbuy">
                <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <g clip-path="url(#clip0_472_639)">
                    <path d="M7.99935 0.666626C6.54896 0.666626 5.13113 1.09672 3.92517 1.90252C2.71921 2.70831 1.77928 3.85362 1.22424 5.19361C0.669193 6.53361 0.523969 8.00809 0.806927 9.43062C1.08989 10.8531 1.78832 12.1598 2.8139 13.1854C3.83949 14.211 5.14616 14.9094 6.56869 15.1924C7.99122 15.4753 9.46571 15.3301 10.8057 14.7751C12.1457 14.22 13.291 13.2801 14.0968 12.0741C14.9026 10.8682 15.3327 9.45036 15.3327 7.99996C15.3327 6.05504 14.5601 4.18978 13.1848 2.81451C11.8095 1.43924 9.94427 0.666626 7.99935 0.666626ZM11.3327 8.66663H8.66602V11.3333C8.66602 11.5101 8.59578 11.6797 8.47076 11.8047C8.34573 11.9297 8.17616 12 7.99935 12C7.82254 12 7.65297 11.9297 7.52795 11.8047C7.40292 11.6797 7.33269 11.5101 7.33269 11.3333V8.66663H4.66602C4.48921 8.66663 4.31964 8.59639 4.19461 8.47136C4.06959 8.34634 3.99935 8.17677 3.99935 7.99996C3.99935 7.82315 4.06959 7.65358 4.19461 7.52855C4.31964 7.40353 4.48921 7.33329 4.66602 7.33329H7.33269V4.66663C7.33269 4.48981 7.40292 4.32025 7.52795 4.19522C7.65297 4.0702 7.82254 3.99996 7.99935 3.99996C8.17616 3.99996 8.34573 4.0702 8.47076 4.19522C8.59578 4.32025 8.66602 4.48981 8.66602 4.66663V7.33329H11.3327C11.5095 7.33329 11.6791 7.40353 11.8041 7.52855C11.9291 7.65358 11.9994 7.82315 11.9994 7.99996C11.9994 8.17677 11.9291 8.34634 11.8041 8.47136C11.6791 8.59639 11.5095 8.66663 11.3327 8.66663Z"/>
                    </g>
                    <defs>
                    <clipPath id="clip0_472_639">
                    <rect width="16" height="16" fill="white"/>
                    </clipPath>
                    </defs>
                </svg>                            
                <p>Пополнить</p>
            </div>
            <div class="esc">
                <p>Выход</p>
                <span>ESC</span>
            </div>
        </div>
        <div class="menu">
            <svelte:component this={Views[selectView]} {pagefilter} {pageload} {itemliststock} {carsList} {searchText} {visible} {selectView}/>
        </div>
    </div>
</div>