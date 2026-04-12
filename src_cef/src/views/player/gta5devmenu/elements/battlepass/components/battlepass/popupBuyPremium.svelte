<script>
    import { translateText } from 'lang'
    import { isPopupBuyOpened } from '../../stores.js'
    import { format } from 'api/formatter'
    import { accountRedbucks } from 'store/account'
    import { setGroup, executeClient, executeClientToGroup, executeClientAsyncToGroup} from 'api/rage'
    import { serverDonatMultiplier, serverId } from 'store/server'
    
    setGroup (".battlepass.");

    const pricePremium = 19999;

    let isPremium = 0;
    executeClientAsyncToGroup("getPremium").then((result) => {
        isPremium = result;
    });

    export let popupData;

    if (!popupData) popupData = {
        id: 0,
        title: "Покупка випр",
        data: [
            1000,
            100
        ]
    }


    let selectType = 0;


    const config = {
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded'
        }
    }

    const onDonate1 = () => {
        if (!selectType) {
            executeClient("client.donatepack.rb", 0);
            executeClient ("client.donatepack.close");
        } else {
            
            window.notificationAdd(4, 9, `Вы начали процесс покупки Battle Pass. Совершите донат через личный кабинет (acirpgta5.myarena.site) в размере 1999 руб, и премиум-доступ автоматически станет доступен.`, 25000);
            executeClient ("client.donatepack.donate", 0);
            executeClient ("client.donatepack.close");
        }
    }

    const onDonate2 = () => {
        if (!selectType) {
            executeClient("client.donatepack.rb", 1);
            executeClient ("client.donatepack.close");
        } else {
            
            window.notificationAdd(4, 9, `Вы начали процесс покупки Battle Pass. Совершите донат через личный кабинет (acirpgta5.myarena.site) в размере 1999 руб, и премиум-доступ автоматически станет доступен.`, 25000);
            executeClient ("client.donatepack.donate", 1);
            executeClient ("client.donatepack.close");
        }
    }

    const getPrice = (price, type) => {
        if (type === 1)
            return price;

        return Math.floor (price / $serverDonatMultiplier);
    }

    const onBuyPremium = () => {
        if (isPremium)
            return;
        else if (!window.loaderData.delay ("battlePass.onBuyPremium", 1))
            return;

        executeClientToGroup ("buyPremium");

        onClose ();
    }

    const onClose = () => isPopupBuyOpened.set(false);
</script>

<div class="battlepass1">
    <div class="head">
		<img src="{document.cloud}inventoryItems/items/bp.png" alt=""/>
        <span>Встречайте летний боевой пропуск! Выполняйте задания и получайте награды. Вы можете приобрести премиум пропуск, а также бонус к уровням, чтобы получать ещё больше наград!</span>
    </div>
    <div class="selectexp">
        <div class="blockupgradebp">
            <h1>Стартовый пропуск</h1>
            <span>Возможность получить обычные награды из верхней ленты призов за очки опыта.</span>
            <span>Доступ к премиум наградам из нижней ленты призов можно купить в любое время.</span>
            <div class="btnlist">
                <div class="btnbpbuy" on:keypress={() => {}} on:click={onClose}>
                    <p>Оставить Стартовый пропуск</p>
                </div>
            </div>
        </div>
        <div class="blockupgradebp">
            <h1>Премиум пропуск</h1>
            <span>Возможность получить обычные награды + премиум награды из нижней ленты призов до 132 уровня за очки опыта.</span>
            <div class="btnlist">
                <div class="btnbpbuy" on:keypress on:click={onDonate2}>
                    <p>Приобрести за</p>
                    <svg width="21" height="20" viewBox="0 0 21 20" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <g clip-path="url(#clip0_498_3111)">
                        <rect x="0.203125" width="20.9091" height="20" rx="2" fill="#AA8ED6"></rect>
                        <path d="M6.72412 4.43115H10.6865C11.7119 4.43115 12.5469 4.75342 13.1914 5.39795C13.8408 6.04248 14.1655 6.87744 14.1655 7.90283C14.1655 8.93311 13.8359 9.77295 13.1768 10.4224C12.5225 11.0669 11.6753 11.3892 10.6353 11.3892H8.36475V15H6.72412V4.43115ZM8.36475 5.81543V10.0122H10.2471C10.96 10.0122 11.5117 9.8291 11.9023 9.46289C12.293 9.09668 12.4883 8.5791 12.4883 7.91016C12.4883 7.24609 12.293 6.73096 11.9023 6.36475C11.5166 5.99854 10.9673 5.81543 10.2544 5.81543H8.36475Z" fill="white"></path>
                        </g>
                        <defs>
                        <clipPath id="clip0_498_3111">
                        <rect width="20.9091" height="20" fill="white"></rect>
                        </clipPath>
                        </defs>
                    </svg>                                
                    <p>{popupData.data[1]}</p>
                </div>
                <div class="btnbpbuy" on:keypress on:click={onDonate1}>
                    <p>Приобрести за</p>
                    <svg width="24" height="22" viewBox="0 0 24 22" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <g clip-path="url(#clip0_498_3105)">
                        <path d="M12.2969 22C18.6481 22 23.7969 17.0751 23.7969 11C23.7969 4.92487 18.6481 0 12.2969 0C5.9456 0 0.796875 4.92487 0.796875 11C0.796875 17.0751 5.9456 22 12.2969 22Z" fill="#AA8ED6"></path>
                        <path d="M10.5429 13.3192C10.2589 13.604 10.0691 13.9627 9.99722 14.3502C9.99722 14.7282 10.1885 15.6682 10.7207 15.6682C11.0615 15.6682 11.717 14.9242 11.9209 14.6762C13.4187 12.7759 15.1725 11.0732 17.1366 9.61223L17.1638 9.63823C16.2083 10.8262 14.4759 12.7832 14.4759 14.3492C14.4759 15.1062 14.776 15.9282 15.6761 15.9282C16.8491 15.9282 19.7827 13.7092 20.1099 12.8092L20.0555 12.3092C18.2134 14.0192 17.1492 14.6092 16.7676 14.6092C16.4404 14.6092 16.2898 14.2312 16.2898 13.9702C16.4966 12.8568 17.042 11.8265 17.858 11.0082L19.9729 8.54123C20.1371 8.34123 20.5594 7.88823 20.5594 7.62723C20.5594 7.36623 19.9865 6.50523 19.6185 6.50523C19.3729 6.50523 19.0958 6.77923 18.9223 6.92323C17.1366 8.28123 15.5684 9.70423 13.8863 11.0872L13.8581 11.0612C14.4174 10.2392 15.8915 8.30723 15.8915 7.34223C15.8817 7.09731 15.7718 6.86584 15.5855 6.69756C15.3992 6.52927 15.1514 6.4376 14.8952 6.44223C14.3359 6.45523 13.1764 7.43423 12.6715 7.89123L10.5022 9.89923L10.475 9.87323C10.8146 9.13702 11.0972 8.37798 11.3208 7.60223C11.3152 7.42429 11.2584 7.25127 11.1566 7.10223L10.8294 6.55423C10.6924 6.33223 10.6381 6.15423 10.3067 6.13623C9.41393 6.34951 8.54887 6.65741 7.72753 7.05423L6.19072 7.73123L6.15935 7.70723C6.69149 7.26323 6.88281 7.02823 6.88281 6.95023C6.88281 6.87223 6.78767 6.81923 6.71867 6.81923C6.5917 6.85398 6.47499 6.91664 6.37785 7.00223C5.16408 7.82423 5.24562 7.80223 5.12331 7.95523C4.94977 8.45545 4.831 8.97155 4.7689 9.49523C4.70094 9.80823 4.87344 9.97823 5.09612 9.97823C5.7924 9.97823 5.1369 9.95223 9.33962 8.07323C9.46895 7.99907 9.61027 7.94601 9.75781 7.91623C9.9899 7.91623 10.2356 8.17723 8.80226 10.6302L5.95862 14.0762C6.10917 14.4762 6.37681 15.5122 6.95494 15.5122C7.39194 15.5122 7.73276 15.0122 7.96485 14.7122L10.0108 12.0792L13.2852 8.68623C13.3803 8.58623 13.5444 8.41223 13.7034 8.41223C13.8623 8.41223 13.9124 8.55623 13.6762 8.97323C13.0525 10.1013 12.314 11.1678 11.4713 12.1572L10.5429 13.3192Z" fill="white"></path>
                        </g>
                        <defs>
                        <clipPath id="clip0_498_3105">
                        <rect width="23" height="22" fill="white" transform="translate(0.796875)"></rect>
                        </clipPath>
                        </defs>
                    </svg>                                
                    <p>{popupData.data[0]}</p>
                </div>
            </div>
        </div>
    </div>
</div>