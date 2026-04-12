<script>
    import { translateText } from 'lang'
    import { addListernEvent } from 'api/functions'
    import { getPngToItemId } from '@/views/player/menu/elements/inventory/getPng.js'
    import { setGroup, executeClientToGroup, executeClientAsyncToGroup } from 'api/rage'
    setGroup (".battlepass.");

    export let selectPage = 0;
    export let currentLvl = 0;

    let awardsList = [];
    const getAwards = (index) => {
        executeClientAsyncToGroup("getAwards", index).then((result) => {
            if (result && typeof result === "string") {
                const _awardsList = JSON.parse(result);
                let data = [];

                _awardsList.forEach((item) => {
                    data.push({
                        index: item.index,
                        usual: {
                            ...item.usual,
                            ...getPngToItemId (item.usual),
                        },
                        premium: {
                            ...item.premium,
                            ...getPngToItemId (item.premium),
                        },
                    })
                });

                awardsList = data;
            }
        });
    }

    $: if (selectPage >= 0)
        getAwards (selectPage);

    //

    let isPremium = 0;
    const getPremium = () => {
        executeClientAsyncToGroup("getPremium").then((result) => {
            isPremium = result;
        });
    }
    getPremium ();

    addListernEvent ("battlePassBuyPremiumSuccess", () => {
        getPremium ();
    })

    //


    const onTake = (taked, index, isPrem) => {
        if (taked)
            return;
        if (!window.loaderData.delay ("battlePass.onTake", 1))
            return;

        executeClientToGroup ("take", index, isPrem)
    }

    addListernEvent ("battlePassTakeSuccess", () => {
        getAwards (selectPage);
    })

    const getCount = (item) => {
        if (typeof item === "undefined" || !item)
            return "";

        if (item.Count == undefined)
            return "";

        let count = item.Count.toString();

        switch (item.Type) {
            case 0:
                count += translateText('player', ' шт.')
                break;
            case 1:
                count += translateText('player', ' д.')
                break;
            case 2:
                count += "$"
                break;
            case 3:
                count += " RB"
                break;
        }

        return count;
    }
</script>

<div class="listitems">
    <div class="leftlitems">
        <div class="standartbp">
            <svg width="27" height="27" viewBox="0 0 27 27" fill="none" xmlns="http://www.w3.org/2000/svg">
                <g clip-path="url(#clip0_674_499)">
                <g filter="url(#filter0_d_674_499)">
                <path d="M14.0625 23.625C19.3437 23.625 23.625 19.3437 23.625 14.0625C23.625 8.78128 19.3437 4.5 14.0625 4.5C8.78128 4.5 4.5 8.78128 4.5 14.0625C4.5 19.3437 8.78128 23.625 14.0625 23.625Z" fill="#6464DB"></path>
                </g>
                <path d="M11.6982 18.15C11.4587 18.1499 11.229 18.0547 11.0597 17.8854L9.25351 16.0792C9.089 15.9089 8.99798 15.6807 9.00003 15.4439C9.00209 15.2072 9.09707 14.9806 9.26451 14.8132C9.43195 14.6458 9.65846 14.5508 9.89525 14.5487C10.132 14.5467 10.3602 14.6377 10.5305 14.8022L11.6472 15.9189L16.4309 10.3382C16.5066 10.2438 16.6005 10.1655 16.7071 10.1081C16.8136 10.0507 16.9306 10.0152 17.0512 10.0039C17.1717 9.99264 17.2932 10.0057 17.4086 10.0423C17.524 10.0789 17.6308 10.1383 17.7228 10.2171C17.8147 10.2958 17.8899 10.3922 17.9439 10.5005C17.9978 10.6089 18.0295 10.727 18.0369 10.8478C18.0443 10.9686 18.0273 11.0897 17.987 11.2039C17.9467 11.318 17.8839 11.4229 17.8022 11.5122L12.3836 17.8339C12.2996 17.9337 12.1947 18.0138 12.0762 18.0684C11.9577 18.123 11.8286 18.1509 11.6982 18.15Z" fill="white"></path>
                </g>
                <defs>
                <filter id="filter0_d_674_499" x="-1.5" y="-1.5" width="31.125" height="31.125" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                <feFlood flood-opacity="0" result="BackgroundImageFix"></feFlood>
                <feColorMatrix in="SourceAlpha" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 127 0" result="hardAlpha"></feColorMatrix>
                <feOffset></feOffset>
                <feGaussianBlur stdDeviation="3"></feGaussianBlur>
                <feComposite in2="hardAlpha" operator="out"></feComposite>
                <feColorMatrix type="matrix" values="0 0 0 0 0.392157 0 0 0 0 0.392157 0 0 0 0 0.858824 0 0 0 0.25 0"></feColorMatrix>
                <feBlend mode="normal" in2="BackgroundImageFix" result="effect1_dropShadow_674_499"></feBlend>
                <feBlend mode="normal" in="SourceGraphic" in2="effect1_dropShadow_674_499" result="shape"></feBlend>
                </filter>
                <clipPath id="clip0_674_499">
                <rect width="27" height="27" fill="white"></rect>
                </clipPath>
                </defs>
            </svg>                                        
            <p>Обычный</p>                           
        </div>
        <div class="premiumbp">
            <svg width="27" height="27" viewBox="0 0 27 27" fill="none" xmlns="http://www.w3.org/2000/svg">
                <g clip-path="url(#clip0_674_491)">
                <g filter="url(#filter0_d_674_491)">
                <path d="M14.0625 23.625C19.3437 23.625 23.625 19.3437 23.625 14.0625C23.625 8.78128 19.3437 4.5 14.0625 4.5C8.78128 4.5 4.5 8.78128 4.5 14.0625C4.5 19.3437 8.78128 23.625 14.0625 23.625Z" fill="white"></path>
                </g>
                <path d="M11.6982 18.15C11.4587 18.1499 11.229 18.0547 11.0597 17.8854L9.25351 16.0792C9.089 15.9089 8.99798 15.6807 9.00003 15.4439C9.00209 15.2072 9.09707 14.9806 9.26451 14.8132C9.43195 14.6458 9.65846 14.5508 9.89525 14.5487C10.132 14.5467 10.3602 14.6377 10.5305 14.8022L11.6472 15.9189L16.4309 10.3382C16.5066 10.2438 16.6005 10.1655 16.7071 10.1081C16.8136 10.0507 16.9306 10.0152 17.0512 10.0039C17.1717 9.99264 17.2932 10.0057 17.4086 10.0423C17.524 10.0789 17.6308 10.1383 17.7228 10.2171C17.8147 10.2958 17.8899 10.3922 17.9439 10.5005C17.9978 10.6089 18.0295 10.727 18.0369 10.8478C18.0443 10.9686 18.0273 11.0897 17.987 11.2039C17.9467 11.318 17.8839 11.4229 17.8022 11.5122L12.3836 17.8339C12.2996 17.9337 12.1947 18.0138 12.0762 18.0684C11.9577 18.123 11.8286 18.1509 11.6982 18.15Z" fill="#6464DB"></path>
                </g>
                <defs>
                <filter id="filter0_d_674_491" x="-1.5" y="-1.5" width="31.125" height="31.125" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                <feFlood flood-opacity="0" result="BackgroundImageFix"></feFlood>
                <feColorMatrix in="SourceAlpha" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 127 0" result="hardAlpha"></feColorMatrix>
                <feOffset></feOffset>
                <feGaussianBlur stdDeviation="3"></feGaussianBlur>
                <feComposite in2="hardAlpha" operator="out"></feComposite>
                <feColorMatrix type="matrix" values="0 0 0 0 1 0 0 0 0 1 0 0 0 0 1 0 0 0 0.25 0"></feColorMatrix>
                <feBlend mode="normal" in2="BackgroundImageFix" result="effect1_dropShadow_674_491"></feBlend>
                <feBlend mode="normal" in="SourceGraphic" in2="effect1_dropShadow_674_491" result="shape"></feBlend>
                </filter>
                <clipPath id="clip0_674_491">
                <rect width="27" height="27" fill="white"></rect>
                </clipPath>
                </defs>
            </svg>                                        
            <p>Премиум</p>                             
        </div>
    </div>
    <div class="rightlitems">
        {#each awardsList as item, index}
            <div class="blockrlitems">
                <div class="blockstandartitems" class:active={currentLvl > item.index}>
                    <div class="top">
                        <div class="lineitem {item.usual.Type}"></div>
                        <span>{getCount (item.usual)}</span>
                    </div>
                    <div class="butnitem">
                        <div class="butuse" on:keypress={() => {}} on:click={() => onTake (item.usual.taked, item.index, false)}>{item.usual.taked ? 'Получено' : 'Взять'}</div>
                    </div>
                    <div class="item">
                        <img src="{item.usual.png}" alt="">
                        <p>{item.usual.name}</p>
                    </div>
                </div>
                <div class="blockpremiumitems" class:active={currentLvl > item.index} class:buy={isPremium}>
                    <div class="top">
                        <div class="lineitem {item.premium.Type}"></div>
                        <span>{getCount (item.premium)}</span>
                    </div>
                    <div class="butnitem" class:active={isPremium}>
                        <div class="butuse"  on:keypress={() => {}} on:click={() => onTake (item.premium.taked, item.index, true)}>{item.premium.taked ? 'Получено' : 'Взять'}</div>
                        <div class="butdone">Недоступно</div>
                    </div>
                    <div class="item">
                        <img src="{item.premium.png}" alt="">
                        <p>{item.premium.name}</p>
                    </div>
                </div>
                <div class="blocklvlbp" class:active={currentLvl > item.index}>
                    <div class="linelvlbp"></div>
                    <div class="blvlbp">{item.index + 1}</div>
                </div>
            </div>
        {/each}
    </div>
</div>