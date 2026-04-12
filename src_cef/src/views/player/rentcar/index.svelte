<script>
    import './assets/css/iconsarenda.css'
    import './assets/css/main.sass'
    import './assets/css/main.css'
	import { fade } from 'svelte/transition';
    import { accountVip } from 'store/account'
    import { charMoney, charLVL, charBankMoney } from 'store/chars'
    import rangeslider from 'components/rangeslider/index'
    import { format } from 'api/formatter'
    import { executeClient } from 'api/rage'
    export let viewData;
    
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


    if (!viewData) viewData = '[]';

    let HourValue = 0;

    const VehicleArray = JSON.parse (viewData);

    let SelectVehicle = -1;

    const authColors =[
        "#000",
        "#fff",
        "#e60000",
        "#ff7300",
        "#f0f000",
        "#00e600",
        "#00cdff",
        "#0000e6",
        "#be3ca5",
    ];
    let colorId = 0;

    const setColor = (index) => {
        if (index === colorId) return;
        colorId = index;
    }

    const setHourValue = (value) => {
        HourValue = Number(value);
    }

    const rangeslidercreate = () => {
        const max = 8;
        HourValue = 1;
        setTimeout(() => {
            rangeslider.create(document.getElementById("rangeslider"), {min: 1, max: max, value: 1, step: 1, onSlide: (value, percent, position) => {
                HourValue = Number(value);
            }});
        }, 0);
    }

    const onBuy = () => {
        if ($charMoney < GetRentCarCash (VehicleArray [SelectVehicle].Price * HourValue)) {            
            window.notificationAdd(1, 9, `У Вас не достаточно средств!`, 3000);
            return;
        }
        executeClient ('client.rentcar.buy', VehicleArray [SelectVehicle].Id, colorId, HourValue);
    }

    const onExit = () => {        
        executeClient ('client.rentcar.exit');
    }

    const GetRentCarCashToLevel = (Price) => {
        const level = $charLVL;

        if (level <= 2) Price = Math.round(Price * 1.0);
        else if (level <= 4) Price = Math.round(Price * 1.5);
        else if (level <= 6) Price = Math.round(Price * 2.0);
        else if (level <= 9) Price = Math.round(Price * 4.5);
        else if (level <= 19) Price = Math.round(Price * 6.0);
        else Price = Math.round(Price * 8.0);
        return Price;
    }

    const GetRentCarCash = (Price) => {

        switch ($accountVip)
        {
            case 1:
                Price = Math.round(Price * 0.95);
                break;
            case 2:
                Price = Math.round(Price * 0.9);
                break;
            case 3:
                Price = Math.round(Price * 0.85);
                break;
            case 4:
            case 5:
                Price = Math.round(Price * 0.8);
                break;
        }
        return GetRentCarCashToLevel (Price);
    }

    const handleKeyDown = (event) => {
        const { keyCode } = event;
        if (keyCode !== 27) return;

        if (SelectVehicle !== -1) SelectVehicle = -1;
        else onExit ();
    }

</script>

<svelte:window on:keyup={handleKeyDown}/>


<div class="gta5devrent">
    <div class="rentmenu">
        {#if SelectVehicle !== -1}
            <div class="acceptcar">
                    <div class="content_block_main_selected">
                    <div class="close-block flex-block" on:keypress={() => {}} on:click={() => SelectVehicle = -1}>
                        <svg xmlns="http://www.w3.org/2000/svg" width="17.122" height="17.121" viewBox="0 0 17.122 17.121">
                            <g transform="translate(1.061 1.061)">
                                <path d="M0,0,15,15" fill="none" stroke-linecap="square" stroke-miterlimit="10" stroke-width="2"></path> 
                                <path d="M6.929,0l-15,15" transform="translate(8.071)" stroke-linecap="square" stroke-miterlimit="10" stroke-width="2"></path>
                            </g>
                        </svg>
                    </div> 
                    <div class="content_block_main_selected__text-title">Аренда транспорта:</div> 
                    <div class="content_block_main_selected__text-text">Название транспорта:</div> 
                    <div class="content_block_main_selected__text-value">{VehicleArray [SelectVehicle].Model}</div> 
                    {#if !VehicleArray [SelectVehicle].IsJob}
                    <div class="content_block_main_selected__text-text">Стоимость аренды:</div> 
                    <div class="content_block_main_selected__text-value">${format("money", GetRentCarCash (VehicleArray [SelectVehicle].Price))} <span class="content_block_main_selected__text-text per-hour">/ час</span></div> 
                    <div class="content_block_main_selected__text-text">Выберите кол-во часов:</div> 
                    <div class="content_block_main_selected__hours-wrapper row-block align-center justify-start">
                        <div class="content_block_main_selected__hours row-block align-center justify-start" class:selected={HourValue === 1} on:keypress={() => {}} on:click={() => setHourValue (1)}><span data-v-ef912ed0="">1</span></div> 
                        <div class="content_block_main_selected__hours row-block align-center justify-start" class:selected={HourValue === 2} on:keypress={() => {}} on:click={() => setHourValue (2)}><span data-v-ef912ed0="">2</span></div> 
                        <div class="content_block_main_selected__hours row-block align-center justify-start" class:selected={HourValue === 3} on:keypress={() => {}} on:click={() => setHourValue (3)}><span data-v-ef912ed0="">3</span></div> 
                        <div class="content_block_main_selected__hours row-block align-center justify-start" class:selected={HourValue === 4} on:keypress={() => {}} on:click={() => setHourValue (4)}><span data-v-ef912ed0="">4</span></div> 
                        <div class="content_block_main_selected__hours row-block align-center justify-start" class:selected={HourValue === 5} on:keypress={() => {}} on:click={() => setHourValue (5)}><span data-v-ef912ed0="">5</span></div> 
                        <div class="content_block_main_selected__hours row-block align-center justify-start" class:selected={HourValue === 6} on:keypress={() => {}} on:click={() => setHourValue (6)}><span data-v-ef912ed0="">6</span></div> 
                        <div class="content_block_main_selected__hours row-block align-center justify-start" class:selected={HourValue === 7} on:keypress={() => {}} on:click={() => setHourValue (7)}><span data-v-ef912ed0="">7</span></div> 
                        <div class="content_block_main_selected__hours row-block align-center justify-start" class:selected={HourValue === 8} on:keypress={() => {}} on:click={() => setHourValue (8)}><span data-v-ef912ed0="">8</span></div>
                    </div> 
                    {/if}
                    <div class="content_block_main_selected__text-text">Выберите цвет:</div> 
                    <div class="content_block_main_selected-colors">
                        {#each authColors as value, index}
                            <div key={index} class:selected={colorId === index} on:keypress={() => {}} on:click={() => setColor (index)} class="color-block row-block align-center justify-center">
                                <div class="color-block__back" style="background: {value}"></div>
                                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 11.414 8.534" style="stroke: rgb(255, 255, 255);">
                                    <path d="M5.864,7.815,9.452,11.4,15.864,4.99" transform="translate(-5.157 -4.283)" fill="none" stroke-width="2"></path>
                                </svg>
                            </div> 
                        {/each}
                    </div> 
                    <div class="content_block_main_selected__text-text">К оплате:</div> 
                    <div class="content_block_main_selected__text-value to-be-paid">${format("money", GetRentCarCash (VehicleArray [SelectVehicle].Price * HourValue))}</div> 
                    <div class="buttons_panel row-block justify-between">
                        <div class="panel_button" on:keypress={() => {}} on:click={onBuy}>Наличными</div> 
                        <div class="panel_button">Картой</div>
                    </div>
                </div>
            </div>
        {/if}
        <div class="headrent">
            <div class="name">
                <p>Аренда</p>
            </div>
            <div class="catologs">
                <div class="blockcatl">
                    <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="16" height="16" viewBox="0 0 16 16">
                        <path d="M10.566,7.223a.742.742,0,0,1-.736-.736V1.839a.742.742,0,0,1,.736-.736h4.748a.742.742,0,0,1,.736.736V6.487a.742.742,0,0,1-.736.736Z"></path> 
                        <path d="M12.606,9.964,9.864,12.94a.465.465,0,0,0,0,.635l2.742,2.976a.447.447,0,0,0,.669,0l2.742-2.976a.465.465,0,0,0,0-.635L13.275,9.964A.446.446,0,0,0,12.606,9.964Z"></path> 
                        <circle cx="3.11" cy="3.11" r="3.11" transform="translate(1.237 10.065)"></circle> 
                        <path d="M1.605,2.173,4.013.8a.664.664,0,0,1,.7,0L7.089,2.173a.717.717,0,0,1,.368.635V5.551a.717.717,0,0,1-.368.635L4.715,7.557a.664.664,0,0,1-.7,0L1.605,6.186a.717.717,0,0,1-.368-.635V2.809A.717.717,0,0,1,1.605,2.173Z"></path>
                    </svg>
                    <p>Весь транспорт</p>
                </div>
            </div>
            <div class="money">
                <div class="moneyl">
                    <p>${format("money", userData.Money)}</p>
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 14 12.981">
                        <g transform="translate(-1 -1.019)">
                            <path d="M13.5,5H3A1,1,0,0,1,3,3H5a.472.472,0,0,0,.5-.5A.472.472,0,0,0,5,2H3A2.006,2.006,0,0,0,1,4v8a2.006,2.006,0,0,0,2,2H13.5A1.473,1.473,0,0,0,15,12.5v-6A1.473,1.473,0,0,0,13.5,5ZM14,7.9v3.2a1.479,1.479,0,0,0-.5-.1h-2a1.5,1.5,0,0,1,0-3h2A1.479,1.479,0,0,0,14,7.9Z" fill="#212121"></path> 
                            <path d="M3.5,3.5A.472.472,0,0,0,3,4a.472.472,0,0,0,.5.5H13a.617.617,0,0,0,.4-.2.486.486,0,0,0,.05-.45l-1-2.5a.52.52,0,0,0-.65-.3L5.4,3.5Z" fill="#212121"></path> 
                            <path d="M12.5,9h-1a.5.5,0,0,0,0,1h1a.5.5,0,0,0,0-1Z" fill="#212121"></path>
                        </g>
                    </svg>
                </div>
                <div class="moneyl">
                    <p>${format("money", userData.Bank)}</p>
                    <svg viewBox="0 0 15 12" style="width: 1.5vh; margin-left: 0.5vh;">
                        <g id="credit-card" transform="translate(0 -54.821)">
                            <g id="Group_477" data-name="Group 477" transform="translate(0 54.821)">
                                <path xmlns="http://www.w3.org/2000/svg" id="Path_8530" data-name="Path 8530" d="M14.633,55.188a1.2,1.2,0,0,0-.883-.367H1.25a1.2,1.2,0,0,0-.883.367A1.2,1.2,0,0,0,0,56.071v9.5a1.2,1.2,0,0,0,.367.883,1.2,1.2,0,0,0,.883.367h12.5A1.254,1.254,0,0,0,15,65.571v-9.5A1.2,1.2,0,0,0,14.633,55.188ZM14,65.571a.253.253,0,0,1-.25.25H1.25a.253.253,0,0,1-.25-.25v-4.75H14v4.75Zm0-7.75H1v-1.75a.253.253,0,0,1,.25-.25h12.5a.253.253,0,0,1,.25.25v1.75Z" transform="translate(0 -54.821)" fill="#212121"></path> 
                                <rect id="Rectangle_1187" data-name="Rectangle 1187" width="2" height="1" transform="translate(2 9)" fill="#212121"></rect> 
                                <rect id="Rectangle_1188" data-name="Rectangle 1188" width="3" height="1" transform="translate(5 9)" fill="#212121"></rect>
                            </g>
                        </g>
                    </svg>
                </div>
            </div>
            <span>ESC</span>
        </div>
        <div class="rentlist">
            {#each VehicleArray as item, index}
                <div class="blockcar" on:keypress={() => {}} on:click={() => SelectVehicle = index}>
                    <h1>{item.Model}</h1>
                    <div class="carsimg" style="background-image: url({document.cloud}inventoryItems/vehicle/{item.Model.toLowerCase()}.png)"></div>
                    <div class="info">
                        <div class="rleft">
                            <p>Цена</p>
                            <span>${format("money", GetRentCarCash (item.Price))}<p>/ час</p></span>
                        </div>
                        <div class="rright">
                            <p>Тип топлива</p>
                            <span>Regular</span>
                        </div>
                    </div>
                </div>
            {/each}
        </div>
    </div>
</div>
