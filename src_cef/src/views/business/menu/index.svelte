<script>
    import { translateText } from 'lang'
    import { executeClient } from 'api/rage'
    import './css/main.sass'
    import './css/main.css'
    import './fonts/Gilroy/stylesheet.css';
    import './fonts/SFPro/stylesheet.css';
    import { ItemType, itemsInfo, ItemId } from 'json/itemsInfo.js'
    import { charUUID, charWanted, charMoney, charBankMoney } from 'store/chars'
    import { format } from 'api/formatter'
    import { getPng } from './getPng.js'
    
    let userData = {
        targetMoney: 0,
        changeMoney: 0,
        timerIdMoney: 0,
        Money: 0,
        targetBank: 0,
        changeBank: 0,
        timerIdBank: 0,
        Bank: 0,
    };

    import { onMount } from 'svelte';
    onMount(async () => {
        //bar.animate(1.0);

        charMoney.subscribe(value => {
            if (userData.Money !== value) {
                CounterUpdate ("Money", value);
            }
        });
        charBankMoney.subscribe(value => {
            if (userData.Bank !== value) {
                CounterUpdate ("Bank", value);
            }
        });
    });

    const CounterUpdate = (args, value) => {
        if (userData["timerId" + args])
            clearTimeout (userData["timerId" + args]);
        userData["change" + args] = userData[args] > value ? (0 - (userData[args] - value)) : (value - userData[args]);
        userData[args] = value;
        userData["timerId" + args] = setTimeout (() => {
            userData["timerId" + args] = 0;
            userData["change" + args] = 0;
            if (!userData["target" + args]) {
                userData["target" + args] = new CountUp("target" + args, value);
                //userData["target" + args].start();
                //userData["target" + args].update(value);
            }
            else
                userData["target" + args].update(value);
        }, !userData["target" + args] ? 0 : 5000)
    }

    let
        title = '',
        titleIcon = '',
        btn = '',
        elements = [],
        type = 0;

    window.smOpen = (_title, _titleIcon, _btn, _json, _type = 0) => {
        title = _title;
        titleIcon = _titleIcon;
        btn = _btn;
        elements = JSON.parse (_json);
        type = _type;
    }

    const configImages = [
        { name: 'Сим-карта', url: 'inventoryItems/items/sm-icon-sim.png' },
        { name: 'Рабочий топор', url: 'inventoryItems/items/244.png' },
        { name: 'Обычная кирка', url: 'inventoryItems/items/234.png' },
        { name: 'Усиленная кирка', url: 'inventoryItems/items/235.png' },
        { name: 'Профессиональная кирка', url: 'inventoryItems/items/236.png' },
        { name: 'Сумка', url: 'inventoryItems/clothes/male/bags/40_0.png' },
        { name: 'Сумка с дрелью', url: 'inventoryItems/items/15.png' },
        { name: 'Стяжки', url: 'inventoryItems/items/18.png' },
        { name: 'Мешок', url: 'inventoryItems/items/17.png' },
        { name: 'Бронежилет', url: 'inventoryItems/items/-9.png' },
        { name: 'Отмычка для замков', url: 'inventoryItems/items/11.png' },
        { name: 'Военная отмычка', url: 'inventoryItems/items/16-war.png' },
        { name: 'Радиоперехватчик', url: 'inventoryItems/items/279.png' },
        { name: 'QR-код', url: 'inventoryItems/items/270.png' },
        { name: 'Услуга по отмыву денег', url: 'inventoryItems/items/otmyv.png' },
        { name: 'Понизить розыск', url: 'inventoryItems/items/rozysk.png' },
        { name: 'Взломать наручники', url: 'inventoryItems/items/naruchniki.png' },
        { name: 'Мед. карта', url: 'inventoryItems/items/med.png' },
        { name: 'Лотерейный билет', url: 'inventoryItems/items/lottery.png' },
        { name: 'Лицензия на оружие', url: 'inventoryItems/items/litsenzia.png' },
        { name: 'Угон автотранспорта', url: 'inventoryItems/items/ugon.png' },
        { name: 'Перевозка автотранспорта', url: 'inventoryItems/items/perevozkaa.png' },
        { name: 'Перевозка оружия', url: 'inventoryItems/items/perevozkaw.png' },
        { name: 'Перевозка денег', url: 'inventoryItems/items/perevozkam.png' },
        { name: 'Перевозка трупов', url: 'inventoryItems/items/perevozkat.png' },
        { name: 'Сдать бронежилет', url: 'inventoryItems/items/-9.png' },
        { name: 'Дубинка', url: 'inventoryItems/items/181.png' },
        { name: 'Stun Gun', url: 'inventoryItems/items/109.png' },
        { name: 'Combat Pistol', url: 'inventoryItems/items/101.png' },
        { name: 'Heavy Pistol', url: 'inventoryItems/items/104.png' },
        { name: 'Pistol Mk2', url: 'inventoryItems/items/112.png' },
        { name: 'Pistol 50', url: 'inventoryItems/items/102.png' },
        { name: 'Ceramic Pistol', url: 'inventoryItems/items/151.png' },
        { name: 'Pump Shotgun Mk2', url: 'inventoryItems/items/149.png' },
        { name: 'Carbine Rifle Mk2', url: 'inventoryItems/items/133.png' },
        { name: 'Special Carbine', url: 'inventoryItems/items/129.png' },
        { name: 'Special Carbine Mk2', url: 'inventoryItems/items/134.png' },
        { name: 'SMG', url: 'inventoryItems/items/117.png' },
        { name: 'Bullpup Shotgun', url: 'inventoryItems/items/143.png' },
        { name: 'SawnOff Shotgun', url: 'inventoryItems/items/142.png' },
        { name: 'Heavy Shotgun', url: 'inventoryItems/items/146.png' },
        { name: 'Carbine Rifle', url: 'inventoryItems/items/127.png' },
        { name: 'Sniper Rifle', url: 'inventoryItems/items/136.png' },
        { name: 'Combat PDW', url: 'inventoryItems/items/119.png' },
        { name: 'Combat MG', url: 'inventoryItems/items/121.png' },
        { name: 'Combat MG Mk2', url: 'inventoryItems/items/125.png' },
        { name: 'Bullpup Rifle', url: 'inventoryItems/items/130.png' },
        { name: 'Аптечка', url: 'inventoryItems/items/1.png' },
        { name: 'Дробь', url: 'inventoryItems/items/204.png' },
        { name: 'Малый калибр', url: 'inventoryItems/items/201.png' },
        { name: 'Автоматный калибр', url: 'inventoryItems/items/202.png' },
        { name: 'Снайперский калибр', url: 'inventoryItems/items/203.png' },
        { name: 'Пистолетный калибр', url: 'inventoryItems/items/200.png' },
        { name: 'AP Pistol', url: 'inventoryItems/items/108.png' },
        { name: 'Assault SMG', url: 'inventoryItems/items/118.png' },
        { name: 'Sweeper Shotgun', url: 'inventoryItems/items/148.png' },
        { name: 'Assault Shotgun', url: 'inventoryItems/items/144.png' },
        { name: 'Advanced Rifle', url: 'inventoryItems/items/128.png' },
        { name: 'Bullpup Rifle Mk2', url: 'inventoryItems/items/135.png' },
        { name: 'Heavy Sniper', url: 'inventoryItems/items/137.png' },
        { name: 'Marksman Rifle Mk2', url: 'inventoryItems/items/140.png' },
        { name: 'Бейдж', url: 'inventoryItems/items/-7.png' },
        { name: 'Фейерверк обычный', url: 'inventoryItems/items/216.png' },
        { name: 'Фейерверк звезда', url: 'inventoryItems/items/217.png' },
        { name: 'Фейерверк взрывной', url: 'inventoryItems/items/218.png' },
        { name: 'Фейерверк фонтан', url: 'inventoryItems/items/219.png' },
        { name: 'Материалы', url: 'inventoryItems/items/13.png' },
        { name: 'Наркотики', url: 'inventoryItems/items/14.png' },
        { name: 'Эксклюзивный кейс', url: 'inventoryItems/items/281.png' },
        { name: 'Стул', url: 'inventoryItems/items/313.png' },
        { name: 'Конус', url: 'inventoryItems/items/371.png' },
        { name: 'Светящийся конус', url: 'inventoryItems/items/372.png' },
        { name: 'Отбойник', url: 'inventoryItems/items/373.png' },
        { name: 'Отбойник', url: 'inventoryItems/items/374.png' },
        { name: 'Перекрытие', url: 'inventoryItems/items/375.png' },
        { name: 'Знак STOP', url: 'inventoryItems/items/376.png' },
        { name: 'Знак НЕТ ПРОЕЗДА', url: 'inventoryItems/items/377.png' },
        { name: 'КПП', url: 'inventoryItems/items/378.png' },
        { name: 'Большой забор', url: 'inventoryItems/items/379.png' },
        { name: 'Маленький забор', url: 'inventoryItems/items/380.png' },
        { name: 'Ночной свет', url: 'inventoryItems/items/381.png' },
        { name: 'Камера видеонаблюдения', url: 'inventoryItems/items/382.png' },
        { name: 'Камера видеонаблюдения', url: 'inventoryItems/items/383.png' },


    ];

    const getOtherImageUrl = (name) => {
        let ind = configImages.findIndex(x => x.name === name);
        let url = document.cloud + configImages[ind].url;
        return url;
    }

    const getTypeName = (type) => {
        if(!type) return `Купить`;
        else if(type == 1) return `Взять`;
        else return `Сдать`;
    }

    const HandleKeyDown = (event) => {
        const { keyCode } = event;
        if (keyCode !== 27) return;
        executeClient ('client.sm.exit')
    }

    const categories = [
        {
    	   Name: "Продукты",
   	   Type: "products",
    	   Icon: "Продукты",
           Items: [1, 3, 4, 5, 6, 7, 8, 9, 10, 225, 229, 228, 233]
    	   // Аптечка, Чипсы, Пиво, Пицца, Бургер, ХотДог, Сэндвич, Кола, Спрайт, Вейп, Бонг, Бинокль, Гитара
	},
        {
    	   Name: "Электроника",
    	   Type: "elect",
    	   Icon: "Электроника",
    	   Items: [2, 271, 230, 224, 226, 231, 232, 248, 243]
    	   // Канистра, СимКарта, Зонтик, LoveNote, Роза, Камера, Микрофон, Бумбокс, Радио
	},
        {
    	   Name: "Инструменты",
    	   Type: "tools",
    	   Icon: "Инструменты",
    	   Items: [191, 194, 182, 184, 19, 41, 234, 235, 236, 244]
    	   // Фонарик, Ключ, Молоток, Лом, Ключи от машины, Связка ключей, Кирка1/2/3, Рабочий топор
	},
        {
           Name: "Разное",
    	   Type: "other",
    	   Icon: "Разное",
    	   Items: [17, 18, 11, 16, 279, 270, -9, 223, 249, 181, 225]
    	   // Мешок, Стяжки, Отмычка, Военная отмычка, Радиоперехватчик, QR-код, Броник, Записка, Кальян, Дубинка, Вейп
	}
    ];
    
    let currentCategory = categories[0].Type;

    const selectCategory = (type) => {
        currentCategory = type;
    };
</script>
<svelte:window on:keyup={HandleKeyDown} />

{#if title == "Магазин" }
    <div class="gta5dev24">
        <div class="shopmenu">
            <div class="smenuhead">
                <img class="logo24" src="http://u90228c5.beget.tech/heone1/247/logo.png" alt="">
                <div class="cate">
                    {#each categories as item, index}
                        <div class="category" class:active={ currentCategory == item.Type } on:keypress={() => {}} on:click={() => selectCategory(item.Type)}>
                                <img class="active" src="http://u90228c5.beget.tech/heone1/247/{item.Icon}1.png" alt=""/>
                                <img src="http://u90228c5.beget.tech/heone1/247/{item.Icon}.png" alt=""/>
                            <p>{ item.Name }</p>
                        </div>
                    {/each}
                </div>
                <div class="money">
                    <div class="nal">
                        <p id="targetMoney">${format("money", userData.Money)}</p>
                        <svg width="14" height="14" viewBox="0 0 14 14" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path fill-rule="evenodd" clip-rule="evenodd" d="M14 5.6592C14 5.46714 13.9237 5.28296 13.7879 5.14715C13.6521 5.01135 13.4679 4.93506 13.2759 4.93506H0.724138C0.532085 4.93506 0.347897 5.01135 0.212095 5.14715C0.0762929 5.28296 0 5.46714 0 5.6592V11.9351C0 12.1271 0.0762929 12.3113 0.212095 12.4471C0.347897 12.5829 0.532085 12.6592 0.724138 12.6592H13.2759C13.4679 12.6592 13.6521 12.5829 13.7879 12.4471C13.9237 12.3113 14 12.1271 14 11.9351V5.6592ZM0.965517 10.4868C0.965517 10.5508 0.990948 10.6122 1.03622 10.6575C1.08148 10.7027 1.14288 10.7282 1.2069 10.7282C1.39895 10.7282 1.58314 10.8045 1.71894 10.9403C1.85474 11.0761 1.93103 11.2602 1.93103 11.4523C1.93103 11.5163 1.95647 11.5777 2.00173 11.623C2.047 11.6682 2.1084 11.6937 2.17241 11.6937H11.8276C11.8916 11.6937 11.953 11.6682 11.9983 11.623C12.0435 11.5777 12.069 11.5163 12.069 11.4523C12.069 11.2602 12.1453 11.0761 12.2811 10.9403C12.4169 10.8045 12.601 10.7282 12.7931 10.7282C12.8571 10.7282 12.9185 10.7027 12.9638 10.6575C13.0091 10.6122 13.0345 10.5508 13.0345 10.4868V7.10747C13.0345 7.04345 13.0091 6.98206 12.9638 6.93679C12.9185 6.89152 12.8571 6.86609 12.7931 6.86609C12.601 6.86609 12.4169 6.7898 12.2811 6.654C12.1453 6.5182 12.069 6.33401 12.069 6.14196C12.069 6.07794 12.0435 6.01654 11.9983 5.97127C11.953 5.92601 11.8916 5.90058 11.8276 5.90058H2.17241C2.1084 5.90058 2.047 5.92601 2.00173 5.97127C1.95647 6.01654 1.93103 6.07794 1.93103 6.14196C1.93103 6.33401 1.85474 6.5182 1.71894 6.654C1.58314 6.7898 1.39895 6.86609 1.2069 6.86609C1.14288 6.86609 1.08148 6.89152 1.03622 6.93679C0.990948 6.98206 0.965517 7.04345 0.965517 7.10747V10.4868Z" fill="#1b1b1b"></path>
                        <path fill-rule="evenodd" clip-rule="evenodd" d="M1.44824 10.2695V7.32468C1.68009 7.27717 1.89289 7.16264 2.06024 6.99529C2.22758 6.82795 2.34211 6.61515 2.38962 6.3833H11.6103C11.6578 6.61515 11.7723 6.82795 11.9397 6.99529C12.107 7.16264 12.3198 7.27717 12.5517 7.32468V10.2695C12.3198 10.317 12.107 10.4315 11.9397 10.5989C11.7723 10.7662 11.6578 10.979 11.6103 11.2109H2.38962C2.34211 10.979 2.22758 10.7662 2.06024 10.5989C1.89289 10.4315 1.68009 10.317 1.44824 10.2695ZM7.00045 6.86485C6.48823 6.86517 5.9971 7.06881 5.63493 7.43102C5.27276 7.79323 5.06919 8.2844 5.06893 8.79661C5.06919 9.3088 5.27277 9.79994 5.63494 10.1621C5.99712 10.5243 6.48826 10.7279 7.00045 10.7281C7.51266 10.7279 8.00383 10.5243 8.36604 10.1621C8.72825 9.79996 8.93189 9.30883 8.93221 8.79661C8.93195 8.28436 8.72835 7.79315 8.36613 7.43093C8.00391 7.06871 7.5127 6.86511 7.00045 6.86485Z" fill="#1b1b1b"></path>
                        <path d="M7.00052 10.2454C7.80064 10.2454 8.44928 9.59679 8.44928 8.79666C8.44928 7.99653 7.80064 7.3479 7.00052 7.3479C6.20039 7.3479 5.55176 7.99653 5.55176 8.79666C5.55176 9.59679 6.20039 10.2454 7.00052 10.2454Z" fill="#1b1b1b"></path>
                        <path fill-rule="evenodd" clip-rule="evenodd" d="M4.47122 4.45208L10.6102 2.80708C10.6408 2.79877 10.6728 2.79659 10.7043 2.80068C10.7357 2.80477 10.7661 2.81504 10.7936 2.8309C10.821 2.84676 10.8451 2.8679 10.8644 2.8931C10.8837 2.9183 10.8978 2.94706 10.9059 2.97773C10.9305 3.0696 10.973 3.15572 11.0308 3.23118C11.0887 3.30663 11.1609 3.36995 11.2432 3.41751C11.3256 3.46508 11.4165 3.49595 11.5108 3.50838C11.6051 3.52081 11.7009 3.51454 11.7927 3.48994C11.8546 3.47349 11.9204 3.48221 11.9758 3.51419C12.0313 3.54618 12.0717 3.59882 12.0884 3.6606L12.3006 4.45208H13.2318L12.5997 2.09236C12.575 2.00049 12.5326 1.91437 12.4747 1.83892C12.4168 1.76347 12.3446 1.70017 12.2622 1.65262C12.1798 1.60508 12.0889 1.57423 11.9946 1.56184C11.9003 1.54944 11.8045 1.55575 11.7126 1.58039L0.995117 4.45208H4.47122Z" fill="#1b1b1b"></path>
                        <path fill-rule="evenodd" clip-rule="evenodd" d="M6.33618 4.45249L10.5253 3.33008C10.6311 3.54176 10.7968 3.71768 11.0018 3.836C11.2067 3.95431 11.4419 4.0098 11.6781 3.99556L11.8005 4.45249H6.33618Z" fill="#1b1b1b"></path>
                        </svg>
                    </div>
                    <div class="bank">
                        <p>${format("money", userData.Bank)}</p>
                        <svg width="14" height="14" viewBox="0 0 14 14" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <g clip-path="url(#clip0_21_344)">
                        <g clip-path="url(#clip1_21_344)">
                        <path d="M0 3.1501C0 2.87162 0.110625 2.60455 0.307538 2.40764C0.504451 2.21072 0.771523 2.1001 1.05 2.1001H12.95C13.2285 2.1001 13.4955 2.21072 13.6925 2.40764C13.8894 2.60455 14 2.87162 14 3.1501V4.2001H0V3.1501Z" fill="#1b1b1b" fill-opacity="0.3"></path>
                        <path fill-rule="evenodd" clip-rule="evenodd" d="M0 5.6001V10.8501C0 11.1286 0.110625 11.3956 0.307538 11.5926C0.504451 11.7895 0.771523 11.9001 1.05 11.9001H12.95C13.2285 11.9001 13.4955 11.7895 13.6925 11.5926C13.8894 11.3956 14 11.1286 14 10.8501V5.6001H0ZM4.9 8.4001H1.4V7.0001H4.9V8.4001Z" fill="#1b1b1b" fill-opacity="0.3"></path>
                        </g>
                        </g>
                        <defs>
                        <clipPath id="clip0_21_344">
                        <rect width="14" height="14" fill="white"></rect>
                        </clipPath>
                        <clipPath id="clip1_21_344">
                        <rect width="14" height="14" fill="white"></rect>
                        </clipPath>
                        </defs>
                        </svg>
                    </div>
                </div>
                <div class="closed">
                    <p>Выход</p>
                    <b>ESC</b>
                </div>
            </div>
            <div class="smitemlist">
                {#each elements.filter(x => categories.find(x => x.Type == currentCategory).Items.includes(Number(x.ItemId))) as value, index}
                    <div class="itemblock" id={value.id} key={index}>
                        <div class="headblock">
                            <span>1шт</span>
                            {#if value.ItemId == 0 || value.ItemId == -5}
                                    <img class="item" alt="" src="{getOtherImageUrl(value.Name)}">
                                {:else}
                                    <img class="item" alt="" src="{getPng(value, itemsInfo[value.ItemId])}">
                            {/if}
                            <div class="btnlist">
                                <div class="btnbuy" on:keypress={() => {}} on:click={() => executeClient ('client.sm.click', value.Id, 1)}><svg xmlns="http://www.w3.org/2000/svg" version="1.1" xmlns:xlink="http://www.w3.org/1999/xlink" width="512" height="512" x="0" y="0" viewBox="0 0 20 20" style="enable-background:new 0 0 512 512" xml:space="preserve" class=""><g><g><path d="M0 4.5A1.5 1.5 0 0 1 1.5 3h17A1.5 1.5 0 0 1 20 4.5V6H0z" opacity="1" data-original="#000000" class=""></path><path fill-rule="evenodd" d="M0 8v7.5A1.5 1.5 0 0 0 1.5 17h17a1.5 1.5 0 0 0 1.5-1.5V8zm7 4H2v-2h5z" clip-rule="evenodd" opacity="1" data-original="#000000" class=""></path></g></g></svg> Картой</div>
                                <div class="btnbuy2" on:keypress={() => {}} on:click={() => executeClient ('client.sm.click', value.Id)}><svg xmlns="http://www.w3.org/2000/svg" version="1.1" xmlns:xlink="http://www.w3.org/1999/xlink" width="512" height="512" x="0" y="0" viewBox="0 0 24 24" style="enable-background:new 0 0 512 512" xml:space="preserve" class=""><g><path d="m17.27 5.009-2.123-3.637a.752.752 0 0 0-1.033-.266L7.624 5z" opacity="1" data-original="#000000" class=""></path><path d="M3 6c-.55 0-1-.45-1-1s.45-1 1-1h3.37l3.34-2H3C1.52 2 .29 3.08.05 4.5c-.02.08-.05.16-.05.25V20c0 1.65 1.35 3 3 3h17c1.1 0 2-.9 2-2v-2h-2.5c-2.48 0-4.5-2.02-4.5-4.5s2.02-4.5 4.5-4.5H22V8c0-1.1-.9-2-2-2zm16-2c0-1.01-.75-1.85-1.73-1.98L19 5z" opacity="1" data-original="#000000" class=""></path><path d="M23.25 11.5H19.5c-1.654 0-3 1.346-3 3s1.346 3 3 3h3.75a.75.75 0 0 0 .75-.75v-4.5a.75.75 0 0 0-.75-.75zm-3.75 4a1 1 0 1 1 0-2 1 1 0 0 1 0 2z"  opacity="1" data-original="#000000" class=""></path></g></svg>Наличными</div>
                            </div>
                        </div>
                        <div class="infoblock">
                            <p>{@html value.Name}</p>
                            <span>{itemsInfo [value.ItemId].Description}</span>
                            <b>{value.Price.replace(/[0-9]+/,'')}{value.Price.replace(/[^\d]+/g,'')}</b>
                        </div>
                    </div>
                {/each}
            </div>
        </div>
    </div>
    {:else}
            <div id='shop'>
                <div class="box-ch">
                    <div class="box-info">
                        <div class="l">
                            <div class="title"><span class="i-title {titleIcon}" />{title}</div>
                        </div>
                        <div class="button-box">
                            <div class="btn red" on:keypress={() => {}} on:click={() => executeClient ('client.sm.exit')}>{translateText('business', 'Выйти')}</div>
                        </div>
                    </div>
                    <div class="item-info">
            
                        <ul class="items">
                            {#each elements as value, index}
                            <li id={value.id} key={index} class="block" on:keypress={() => {}} on:click={() => executeClient ('client.sm.click', value.Id)}>
                                <div class="box">
                                    <div class="name">{@html value.Name}</div>
                                <!--<div class="name">{@html getPng(value, itemsInfo[value.ItemId])}</div>
                                    <span class="item-img {value.Icon}" />  
                                    <img src="{getPng(value, itemsInfo[value.ItemId])}">
                                    <span class="item-img" style="background-image: url({getPng(value, itemsInfo[value.ItemId])})" />-->
            
                                    {#if value.ItemId == 0}
                                        <div class="item-img"><img alt="" src="{getOtherImageUrl(value.Name)}"></div>
                                        {:else}
                                        <div class="item-img"><img alt="" src="{getPng(value, itemsInfo[value.ItemId])}"></div>
                                     {/if}
            
                                    {#if value.Price}
                                        <div class="price">
                                            {value.Price.replace(/[^\d]+/g,'')}
                                            <span class="green"> {value.Price.replace(/[0-9]+/,'')}</span>
                                        </div>
                                    {/if}
                                </div>
                                <div class="btn {btn}">{getTypeName(value.Name.match(/Сдать/g) ? 2 : type)}</div>
                             </li>
                             {/each}
                         </ul>
            
                    </div>
                    
                    
                </div>
            </div>
{/if}