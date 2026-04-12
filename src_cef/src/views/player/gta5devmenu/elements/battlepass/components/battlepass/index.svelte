<script>
    import { translateText } from 'lang'
    import { isPopupBuyOpened } from '../../stores.js'
    import { setGroup, executeClientToGroup, executeClientAsyncToGroup } from 'api/rage'
    import { addListernEvent } from 'api/functions'
    import { GetTime } from 'api/moment'

    import Awards from './awards.svelte'

    setGroup (".battlepass.");

    let awardsCount = 0;
    let maxPage = 0;
    executeClientAsyncToGroup("getAwardsCount").then((result) => {
        awardsCount = result;
        maxPage = Math.floor ((awardsCount - 1) / maxCountAwards);
    });

    //

    const maxCountAwards = 6;
    let selectPage = 0;
    let currentLvl = 0;

    const getLvl = (isInit = false) => {
        executeClientAsyncToGroup("getLvl").then((result) => {
            currentLvl = result;

            if (isInit) {
                selectPage = Math.floor((currentLvl - 1) / maxCountAwards);
                if (selectPage < 0)
                    selectPage = 0;
            }
        });
    }

    getLvl (true);

    //

    const maxExp = 50;
    let currentExp = 0;
    const getExp = () => {
        executeClientAsyncToGroup("getExp").then((result) => {
            currentExp = result;
        });
    }

    getExp ();

    //

    addListernEvent ("battlePassUpdateLvlAndExp", () => {
        getLvl ();
        getExp ();
    })

    //

    const onTakeEverything = () => {
        if (!window.loaderData.delay ("battlePass.onTakeEverything", 2))
            return;

        executeClientToGroup ("takeAll")
    }

    const onPage = (number) => {

        number += selectPage;

        if (number >= 0 && number <= maxPage)
            selectPage = number;
    }

    //
    const maxMinutesAddExp = 60 * 3;

    let minutesAddExp = 0;

    import { serverDateTime } from 'store/server'

    let minutes = -1;

    serverDateTime.subscribe((dateTime) => {
        const moment = GetTime (dateTime);

        if (moment.minutes() !== minutes) {
            if (minutes !== -1)
                minutesAddExp++;

            minutes = moment.minutes();
        }
    });

    executeClientAsyncToGroup("getMinutesAddExp").then((result) => {
        minutesAddExp = result;
    });

    const getTimeFromMins = (mins) => {
        let hours = Math.trunc(mins / 60);
        let minutes = mins % 60;
        return hours + translateText('player', ' ч. ') + minutes + translateText('player', ' м.');
    }



    //

    let isAllAwardsTaked = false;

    const getIsAllAwardsTaked = () => {

        executeClientAsyncToGroup("isAllAwardsTaked").then((result) => {
            isAllAwardsTaked = result;
        });
    }

    getIsAllAwardsTaked ();

    addListernEvent ("isAllAwardsTaked", () => {
        getIsAllAwardsTaked ();
    })

    const pricePremium = 19999;

    let isPremium = 0;
    executeClientAsyncToGroup("getPremium").then((result) => {
        isPremium = result;
    });

    const onBuyPremium = () => {
        if (isPremium)
            return;
        else if (!window.loaderData.delay ("battlePass.onBuyPremium", 1))
            return;

        executeClientToGroup ("buyPremium");
    }

</script>

<div class="battlepass">
    <div class="head">
		<img src="{document.cloud}inventoryItems/items/bp.png" alt=""/>
        <span>Встречайте летний боевой пропуск! Выполняйте задания и получайте награды. Вы можете приобрести премиум пропуск, а также бонус к уровням, чтобы получать ещё больше наград!</span>
    </div>
    <div class="battlenav">
        {#if !isPremium}
        <div class="btnnav" on:keypress={() => {}} on:click={() => isPopupBuyOpened.set(true)}>
            <p>Улучшить Летний пропуск</p>
        </div>
        {/if}
        {#if isAllAwardsTaked}
        <div class="btnrewrad" on:keypress={() => {}} on:click={onTakeEverything}>
            <p>Забрать награды</p>
        </div>
        {/if}
        <div class="leftslide" on:keypress={() => {}} on:click={() => onPage (-1)}>
            <svg width="9" height="17" viewBox="0 0 9 17" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M7.92308 1L1.6261 7.82172C1.27251 8.20478 1.27251 8.79522 1.6261 9.17828L7.92308 16" stroke-width="1.3" stroke-linecap="round"></path>
            </svg>                                    
        </div>
        <div class="rightslide" on:keypress={() => {}} on:click={() => onPage (1)}>
            <svg width="9" height="17" viewBox="0 0 9 17" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M0.999922 1L7.29425 7.82188C7.64764 8.2049 7.64764 8.7951 7.29425 9.17812L0.999922 16" stroke-width="1.3" stroke-linecap="round"></path>
            </svg>                                    
        </div>
        <div class="btnlvlyou">
            <p>{currentLvl} Уровень</p>
            <b>({currentExp}/{maxExp}<svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                <g clip-path="url(#clip0_1130_76)">
                <path d="M15.9701 14.6773C15.9698 14.6779 15.9695 14.6782 15.9692 14.6788C15.9463 14.7187 15.9122 14.751 15.8712 14.7718C15.8301 14.7925 15.7839 14.8007 15.7382 14.7954C8.08847 13.8948 0.339062 14.7863 0.261874 14.7954C0.216112 14.8007 0.169783 14.7925 0.128661 14.7717C0.0875391 14.751 0.0534435 14.7185 0.0306243 14.6785C0.00763091 14.638 -0.00280469 14.5917 0.000645528 14.5452C0.00409574 14.4988 0.0212758 14.4545 0.0499993 14.4179C0.0618743 14.4026 1.27062 12.8576 2.82159 10.4832C4.25159 8.29413 6.27319 4.93225 7.78412 1.34663C7.80199 1.3041 7.83201 1.2678 7.87043 1.24227C7.90884 1.21674 7.95394 1.20313 8.00006 1.20312C8.04619 1.20313 8.09128 1.21674 8.1297 1.24227C8.16811 1.2678 8.19814 1.3041 8.216 1.34663C9.72694 4.93225 11.7485 8.29413 13.1785 10.4832C14.7295 12.8576 15.9382 14.4026 15.9501 14.4179C15.9787 14.4543 15.9958 14.4984 15.9994 14.5445C16.0029 14.5907 15.9927 14.6369 15.9701 14.6773Z" fill="#D792FF"/>
                <path d="M15.9701 14.6772C15.9697 14.6778 15.9694 14.6781 15.9691 14.6788L12.4644 12.6728L8 7.45534V1.20312C8.09437 1.20312 8.17938 1.25969 8.21594 1.34656C9.72688 4.93219 11.7485 8.29406 13.1785 10.4831C14.7294 12.8575 15.9382 14.4025 15.9501 14.4178C15.9786 14.4542 15.9958 14.4983 15.9993 14.5445C16.0029 14.5906 15.9927 14.6368 15.9701 14.6772Z" fill="#B078FF"/>
                <path d="M15.9698 14.6788C15.9469 14.7187 15.9129 14.751 15.8718 14.7717C15.8308 14.7924 15.7845 14.8006 15.7388 14.7953C8.08909 13.8947 0.339687 14.7863 0.2625 14.7953C0.216738 14.8007 0.170409 14.7925 0.129287 14.7717C0.0881648 14.7509 0.0540692 14.7185 0.03125 14.6784L5.21472 11.7303L10.7297 11.6794L15.9698 14.6788ZM15.9707 14.6772C15.9704 14.6778 15.9701 14.6781 15.9698 14.6788C15.9698 14.6788 9.99944 10.2603 8.00066 1.20312C8.09503 1.20312 8.18003 1.25969 8.21659 1.34656C9.72753 4.93219 11.7491 8.29406 13.1791 10.4831C14.7301 12.8575 15.9388 14.4025 15.9507 14.4178C15.9793 14.4542 15.9964 14.4983 16 14.5445C16.0035 14.5906 15.9934 14.6368 15.9707 14.6772Z" fill="#9057EA"/>
                <path d="M11.6859 11.5062C10.6321 9.8931 9.2652 7.6591 7.99927 5.15723C6.73333 7.6591 5.36645 9.8931 4.31264 11.5062C4.05267 11.9041 3.78894 12.2996 3.52148 12.6926C4.02321 12.664 4.52518 12.6399 5.02733 12.6202C6.9658 12.5441 9.61114 12.5095 12.4635 12.6727C12.2007 12.2863 11.9415 11.8974 11.6859 11.5062Z" fill="#EBBBFF"/>
                <path d="M7.20011 12.3959C7.22538 12.3784 7.24694 12.356 7.26356 12.3301C7.28018 12.3042 7.29154 12.2752 7.29698 12.2449C7.30242 12.2147 7.30184 12.1836 7.29527 12.1535C7.28871 12.1234 7.27628 12.095 7.25871 12.0697C7.25151 12.0593 6.52986 11.0184 5.88218 9.80717C5.86221 9.76981 5.83249 9.73857 5.79618 9.71675C5.75987 9.69494 5.71833 9.68337 5.67597 9.68328C5.63361 9.6832 5.59202 9.69459 5.55562 9.71625C5.51922 9.73791 5.48937 9.76903 5.46925 9.80631C5.45083 9.84037 5.44115 9.87847 5.44108 9.91719C5.44101 9.95592 5.45054 9.99405 5.46883 10.0282C6.12994 11.2645 6.86655 12.3268 6.87392 12.3373C6.9094 12.3884 6.96371 12.4232 7.02488 12.4342C7.08605 12.4452 7.14908 12.4314 7.20011 12.3959ZM5.9126 12.2686C5.96364 12.2331 5.99848 12.1788 6.00948 12.1176C6.02048 12.0564 6.00673 11.9934 5.97125 11.9424C5.96812 11.9378 5.65459 11.4854 5.37348 10.9598C5.35351 10.9224 5.32379 10.8912 5.28748 10.8693C5.25118 10.8475 5.20963 10.836 5.16727 10.8359C5.12492 10.8358 5.08333 10.8472 5.04693 10.8688C5.01053 10.8905 4.98068 10.9216 4.96055 10.9589C4.94214 10.993 4.93246 11.0311 4.93239 11.0698C4.93231 11.1085 4.94185 11.1466 4.96014 11.1808C5.25467 11.7316 5.573 12.1907 5.58645 12.21C5.60401 12.2352 5.62638 12.2568 5.65228 12.2734C5.67817 12.29 5.70709 12.3014 5.73738 12.3068C5.76767 12.3123 5.79873 12.3117 5.8288 12.3051C5.85886 12.2986 5.88734 12.2862 5.9126 12.2686Z" fill="#CBA7FF"/>
                <path d="M7.81211 8.25381C7.78749 8.27229 7.76676 8.29543 7.75109 8.32193C7.73542 8.34842 7.72512 8.37774 7.72078 8.40821C7.71645 8.43868 7.71815 8.46971 7.72581 8.49952C7.73346 8.52933 7.74691 8.55735 7.76539 8.58196C7.77295 8.59206 8.5319 9.60611 9.2231 10.793C9.24441 10.8297 9.27525 10.8598 9.31232 10.8803C9.3494 10.9008 9.39134 10.9108 9.43367 10.9094C9.476 10.9079 9.51715 10.895 9.55274 10.8721C9.58833 10.8491 9.61703 10.8169 9.63579 10.7789C9.65296 10.7442 9.66125 10.7058 9.65992 10.6671C9.65858 10.6284 9.64767 10.5906 9.62816 10.5572C8.92263 9.34566 8.14796 8.31083 8.14022 8.30054C8.1029 8.25084 8.04737 8.21799 7.98584 8.20923C7.92431 8.20047 7.86181 8.2165 7.81211 8.25381ZM9.10339 8.33436C9.05368 8.37167 9.02082 8.4272 9.01205 8.48873C9.00328 8.55027 9.01931 8.61277 9.05662 8.66248C9.05991 8.66689 9.38965 9.10761 9.68964 9.62276C9.71096 9.65937 9.74179 9.68952 9.77887 9.71C9.81594 9.73048 9.85788 9.74053 9.90021 9.73908C9.94254 9.73764 9.98369 9.72474 10.0193 9.70177C10.0549 9.6788 10.0836 9.64662 10.1023 9.60864C10.1195 9.57393 10.1278 9.53551 10.1265 9.49681C10.1251 9.45811 10.1142 9.42034 10.0947 9.38689C9.78038 8.84716 9.4456 8.39984 9.43146 8.3811C9.41299 8.35649 9.38985 8.33575 9.36337 8.32008C9.33689 8.30441 9.30757 8.29411 9.27711 8.28977C9.24664 8.28543 9.21562 8.28713 9.18581 8.29478C9.15601 8.30243 9.128 8.31588 9.10339 8.33436Z" fill="#CBA7FF"/>
                </g>
                <defs>
                <clipPath id="clip0_1130_76">
                <rect width="16" height="16" fill="white"/>
                </clipPath>
                </defs>
                </svg>)</b>
        </div>
    </div>
    <Awards {selectPage} {currentLvl} />
</div>