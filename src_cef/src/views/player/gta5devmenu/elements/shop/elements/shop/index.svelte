<script>
    import { translateText } from 'lang'
    import { format } from 'api/formatter'
    import { serverDonatMultiplier } from 'store/server'
    import { validate } from 'api/validation';
    import ImgName from './img/p_1.svg';
    import ImgAppearance from './img/p_2.svg';
    import ImgWarn from './img/p_3.svg';
    import ImgSim from './img/sim.png';
    import ImgNumber from './img/number.png';
    let selectIndex = 0;

    export let SetPopup;


    const shopList = [
        {
            title: "Персонаж",
            desc: "",
            function: "onSelectP",
            img: "text",
            list: [
                {
                    id: 0,
                    isName: true,
                    name: "Смена имени",
                    desc: "",
                    text: `Позволяет один раз сменить имя одного
                            Вашего персонажа.
                            После смены имени персонаж забудет о совершенных ранее рукопожатиях,
                            при этом инвентарь и статистика персонажа никак не изменятся. Только никнейм.`,
                    img: ImgName,
                    price: 800,
                },
                {
                    id: 2,
                    name: "Снять предупреждение",
                    desc: "",
                    text: `Warn - предупреждение от Администрации. Эта донат-функция снимает только 1 warn.
                        В случае, если у вас 2 warn'a, то для полного снятия потребуется дважды оплатить "Снять предупреждение".
                        Если у вас накопится 3 warn'a одновременно - персонаж будет автоматически заблокирован на 30 дней.`,
                    img: ImgWarn,
                    price: 1000,
                },
                {
                    id: 1,
                    name: "Смена внешности",
                    desc: "",
                    text: `После оплаты этой функции,
                            Ваш персонаж будет отправлен в редактор внешности (как при создании персонажа),
                            где Вы сможете заново настроить его внешность.
                            Содержимое инвентаря и татуировки останутся.`,
                    img: ImgAppearance,
                    price: 1000,
                },
                {
                    id: 4,
                    isSim: true,
                    name: "Номер телефона",
                    desc: "",
                    text: `Долгожданная система покупки номера для телефона!
                        Соберите свой уникальный номер из любых цифр и пусть все вокруг завидуют...
                        Номер на авто выдаётся как предмет в инвентарь.`,
                    img: ImgSim,
                    btnName: "Купить"
                },
                {
                    id: 3,
                    isNumber: true,
                    name: "Номер автомобиля",
                    desc: "",
                    text: `Долгожданная система покупки номера для автомобиля!
                        Соберите свой уникальный номер из букв и/или цифр и пусть все вокруг завидуют...
                        Номер на авто выдаётся как предмет в инвентарь.`,
                    img: ImgNumber,
                    btnName: "Купить"
                },
            ]
        },
    ];

    const onToServer = (item) => {
        switch (shopList[selectIndex].function) {
            case "onSelectPrice":
                if (item.btnName === "Купить") SetPopup ("PopupPayment", 0);
                else if (item.btnName === "Конвертировать") SetPopup ("PopupChange");
                else window.router.setView('DonateSapper');
                break;
            case "onSelectP":
                if (item.isNumber) {
                    SetPopup ("PopupNomer");
                    return;
                }
                if (item.isSim) {
                    SetPopup ("PopupSim");
                    return;
                }
                SetPopup ("PopupPPopup", item);
                break;
            case "onSelectC":
                SetPopup ("PopupCPopup", item);
                break;
        }
    }

    const getDonate = (text) => {
        if (text < 0) text = 0;
        else if (text > 999999) text = 999999;
        let tallage = 0;
        if ($serverDonatMultiplier > 1) {
            text = text * $serverDonatMultiplier;
        } else {
            if (text >= 20000) {
                tallage = 50;
            } else if (text >= 10000) {
                tallage = 30;
            } else if (text >= 3000) {
                tallage = 20;
            } else if (text >= 1000) {
                tallage = 10;
            }
        }

        return `${Math.round(text) + Math.round(text / 100 * tallage)}`;
    }

    const onSelectPrice = (item) => {
        if (!item.price) SetPopup ("PopupPayment", 0);
        else SetPopup ("PopupPayment", item.priceReal);
    }

</script>

<div class="list1">
    {#each shopList[selectIndex].list as item, index}
    <div class="blockpersm" on:keypress on:click={() => onToServer (item)}>
        <img src={item.img} alt=""/>
        <h1>{item.name}</h1>
    </div>
    {/each}
</div>