<script>
    import { executeClient } from 'api/rage'
    import { format } from 'api/formatter'
    import { charBankMoney } from 'store/chars';

    import './css/main.css'

    import Business from './elements/business.svelte'
    import Input from './elements/input.svelte'
    import Menu from './elements/menu.svelte'

    export let viewData;

    // Протокол с сервером (MoneySystem/Bank.cs) прежний: window.atm.open([type, subdata, placeholder]),
    // atmCB(type, index) — выбор пункта, atmVal(value) — ввод суммы/счёта.

    let SelectViews = "Menu";

    const Views = {
        Menu,
        Input,
        Business
    }

    const menuItem = [
        { title: 'Внести наличные', icon: 'user-shared' },
        { title: 'Снять наличные', icon: 'user-received' },
        { title: 'Налог за дом', icon: 'home' },
        { title: 'Налог за бизнес', icon: 'store' },
        { title: 'Перевод на счёт', icon: 'article' },
    ];

    let
        type = 1,
        subdata = '',
        activeMain = 0,
        placeholder = "";

    // При выборе дома/бизнеса сервер присылает setatm с новыми данными в тот же компонент
    $: number = viewData ? String(viewData.number) : "";
    $: holder = viewData ? viewData.holder : "";
    $: isPersonal = /^\d+$/.test(number);

    window.atm = {
        open: (data) => {
            window.atm.reset();
            placeholder = data[2];
            subdata = data[1];
            type = data[0];

            if (type === 1) SelectViews = "Menu";
            else if (type === 2) SelectViews = "Input";
            else if (type === 3) SelectViews = "Business";
        },
        reset: () => {
            subdata = [];
            type = 1;
            SelectViews = "Menu";
        }
    }

    const onSelectMain = (index) => {
        if (index !== -1)
            activeMain = index;

        executeClient ("atmCB", type, index);
    }
</script>

<div class="atm">
    <div class="atm__screen">
        {#if SelectViews == "Menu"}
            <div class="atm__layout">
                <div class="atm__menu">
                    <svelte:component this={Views[SelectViews]} {menuItem} {onSelectMain} />
                </div>
                <div class="atm__side">
                    <div class="atm__side_title">Ваш счёт</div>
                    <div class="atm__info">
                        <p>Владелец счёта</p>
                        <span>{holder}</span>
                    </div>
                    <div class="atm__info">
                        <p>Баланс счёта</p>
                        <span class="money">${format("money", $charBankMoney)}</span>
                    </div>
                    <div class="atm__card">
                        <div class="atm__card_top">
                            <b>FLEECA</b>
                            <i class="atm__chip"></i>
                        </div>
                        <div class="atm__card_number">{isPersonal ? number.padStart(10, '•').replace(/(.{4})(?=.)/g, '$1 ') : number}</div>
                        <div class="atm__card_bottom">
                            <span>{holder}</span>
                            <span>12/30</span>
                        </div>
                    </div>
                    <div class="atm__note">Без комиссии · 24/7</div>
                </div>
            </div>
        {:else}
            <div class="atm__op">
                <svelte:component this={Views[SelectViews]} {menuItem} {type} {subdata} {activeMain} {placeholder} {holder} />
            </div>
        {/if}
    </div>
</div>
