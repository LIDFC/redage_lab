<script>
    import { otherStatsData } from 'store/account'
    import { charData } from 'store/chars';
    import { format } from 'api/formatter'
    import { onMount } from 'svelte';
    import moment from 'moment'
    import fraction from 'json/fraction.js'
    import jobs from 'json/jobs.js'
    import vipinfo from 'json/vipinfo.js'
    import ImgHouse from '../../../hudevo/phonenew/assets/images/house.png';
    import ImgBiz from '../../../hudevo/phonenew/assets/images/shop.png';
    export let visible;
    export let selectView;
    export let carsList;
    import { authInfo } from './authInfo.js';

    let onSelectedView2 = "Page1";

    let selectCharData = $charData;   

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

    const Bool = (text) => {
        return String(text).toLowerCase() === "true";
    }

    let targetDate = new Date(selectCharData.CreateDate)

    let now = new Date();
    let diffInMilliseconds = now - targetDate;

    let diff = {
        years: 0,
        months: 0,
        days: 0,
        hours: 0,
        minutes: 0,
    };

    onMount(() => {
        diff.years = Math.floor(diffInMilliseconds / 31536000000);
        diffInMilliseconds = diffInMilliseconds % 31536000000;

        diff.months = Math.floor(diffInMilliseconds / 2628000000);
        diffInMilliseconds = diffInMilliseconds % 2628000000;

        diff.days = Math.floor(diffInMilliseconds / 86400000);
        diffInMilliseconds = diffInMilliseconds % 86400000;

        diff.hours = Math.floor(diffInMilliseconds / 3600000);
        diffInMilliseconds = diffInMilliseconds % 3600000;

        diff.minutes = Math.floor(diffInMilliseconds / 60000);
    });
</script>
{#if selectView === "Osnovnoe"}
    <div class="list1">
        <div class="block">
            <h1>Денежные средства</h1>
            <p>${selectCharData.BankMoney}</p>
            <span>Наличные<b>${format("money", selectCharData.Money)}</b></span>
        </div>
        <div class="block">
            <h1>Возраст</h1>
            <p>18 лет</p>
            <span></span>
        </div>
        <div class="block">
            <h1>Пол</h1>
            <p>{Bool(selectCharData.Gender) ? "Мужской" : "Женский"}</p>
            <span></span>
        </div>
        <div class="block">
            <h1>Работа</h1>
            <p>{jobs[selectCharData.WorkID]}</p>
            <span></span>
        </div>
        <div class="block">
            <h1>Семейное положение</h1>
            <p>{selectCharData.WeddingName}</p>
            <span></span>
        </div>
        {#if selectCharData.FractionID > 0}
        <div class="block">
            <h1>Организация</h1>
            <p>{fraction[selectCharData.FractionID]}</p>
            <span>Должность<b>{selectCharData.FractionLVL}</b></span>
        </div>
        {/if}
        {#if selectCharData.OrganizationID > 0}
        <div class="block">
            <h1>Семья</h1>
            <p>{selectCharData.OrganizationID}</p>
            <span>Должность<b>{selectCharData.OrganizationLVL}</b></span>
        </div>
        {/if}
        <div class="block">
            <h1>Премиум статус</h1>
            <p>{selectCharData.VipLvl > 0 ? `${vipinfo[selectCharData.VipLvl]}` : vipinfo[selectCharData.VipLvl]}</p>
            <span>Истикает:<b>{selectCharData.VipLvl > 0 ? `${moment(selectCharData.VipDate).format('DD.MM.YYYY')}` : vipinfo[selectCharData.VipLvl]}</b></span>
        </div>
        <div class="block">
            <h1>Паспорт</h1>
            <p>ID {selectCharData.UUID}</p>
            <span></span>
        </div>
        <div class="block">
            <h1>Телефон</h1>
            <p>{selectCharData.Sim == -1 ? "Нет Сим-Карты" : selectCharData.Sim}</p>
            <span></span>
        </div>
        <div class="block">
            <h1>Номер счета</h1>
            <p>XXXXXXXXXX{selectCharData.Bank}</p>
            <span></span>
        </div>
    </div>
{/if}

{#if selectView === "Statiskic"}
    <div class="list1">
        <div class="block">
            <h1>Дата регистрации</h1>
            <p>
                {#if diff.years > 0}
                    {diff.years} год(а) назад
                    {:else if diff.months > 0}
                        {diff.months} месяц(а) назад
                    {:else if diff.days > 0}
                        {diff.days} д. назад
                    {:else if diff.hours > 0}
                        {diff.hours} ч. назад
                    {:else}
                        {diff.minutes} мин. назад
                {/if}
            </p>
            <span>Дата регистрации:<b>{moment(selectCharData.CreateDate).format('DD.MM.YYYY HH:mm')}</b></span>
        </div>
        <div class="block">
            <h1>Уровень</h1>
            <p>{selectCharData.LVL}</p>
            <span>Респекты<b>{selectCharData.EXP} / {(3 + selectCharData.LVL * 3)}</b></span>
        </div>
        <div class="block">
            <h1>Время в игре</h1>
            <p>
                {#if onSelectedView2 === "Page1"}
                    {moment.duration(selectCharData.TodayTime, "minutes").format("h[ч.] m[м.]")}
                {/if}
                {#if onSelectedView2 === "Page2"}
                    {moment.duration(selectCharData.MonthTime, "minutes").format("w[нед.] d[д.] h[ч.] m[м.]")}
                {/if}
                {#if onSelectedView2 === "Page3"}
                    {moment.duration(selectCharData.TotalTime, "minutes").format("y[г.] M[мес.] w[nнед.] d[д.] h[ч.] m[м.]")}
                {/if}
            </p>
            <span>
                <div class="timeblock" on:keypress={() => {}} on:click={() => onSelectedView2 = "Page1"} class:active={onSelectedView2 == "Page1"}>День</div>
                <div class="timeblock" on:keypress={() => {}} on:click={() => onSelectedView2 = "Page2"} class:active={onSelectedView2 == "Page2"}>Месяц</div>
                <div class="timeblock" on:keypress={() => {}} on:click={() => onSelectedView2 = "Page3"} class:active={onSelectedView2 == "Page3"}>Всего</div>
            </span>
        </div>
        <div class="block">
            <h1>Предупреждения</h1>
            <p>{selectCharData.Warns > 0 ? `${selectCharData.Warns}` : 0}</p>
            <span>Активные<b>{selectCharData.Warns > 0 ? `${selectCharData.Warns}` : 0} / 3</b></span>
        </div>
        <div class="block" class:active={Boolean(selectCharData.Licenses[0]) == true}>
            <h1>Лицензия на мотоцикл</h1>
            <p class="active" >Получено</p>
            <p>Нет</p>
            <span></span>
        </div>
        <div class="block" class:active={Boolean(selectCharData.Licenses[1]) == true}>
            <h1>Лицензия на легковой транспорт</h1>
            <p class="active" >Получено</p>
            <p>Нет</p>
            <span></span>
        </div>
        <div class="block" class:active={Boolean(selectCharData.Licenses[2]) == true}>
            <h1>Лицензия на грузовой транспорт</h1>
            <p class="active" >Получено</p>
            <p>Нет</p>
            <span></span>
        </div>
        <div class="block" class:active={Boolean(selectCharData.Licenses[3]) == true}>
            <h1>Лицензия на водный транспорт</h1>
            <p class="active" >Получено</p>
            <p>Нет</p>
            <span></span>
        </div>
        <div class="block" class:active={Boolean(selectCharData.Licenses[4]) == true}>
            <h1>Лицензия на вертолёт</h1>
            <p class="active" >Получено</p>
            <p>Нет</p>
            <span></span>
        </div>
        <div class="block" class:active={Boolean(selectCharData.Licenses[5]) == true}>
            <h1>Лицензия на самолёт</h1>
            <p class="active" >Получено</p>
            <p>Нет</p>
            <span></span>
        </div>
        <div class="block" class:active={Boolean(selectCharData.Licenses[6]) == true}>
            <h1>Лицензия на оружие</h1>
            <p class="active" >Получено</p>
            <p>Нет</p>
            <span></span>
        </div>
        <div class="block" class:active={Boolean(selectCharData.Licenses[7]) == true}>
            <h1>Мед. карта</h1>
            <p class="active" >Получено</p>
            <p>Нет</p>
            <span></span>
        </div>
        <div class="block" class:active={Boolean(selectCharData.Licenses[8]) == true}>
            <h1>Лицензия парамедика</h1>
            <p class="active" >Получено</p>
            <p>Нет</p>
            <span></span>
        </div>
    </div>
{/if}

{#if selectView === "Skills"}
    {#if selectCharData.jobSkillsInfo}
        <div class="list1">
            {#each selectCharData.jobSkillsInfo as job, index}
                <div class="blockskill">
                    <h1>{job.name}</h1>
                    <p>{job.currentLevel} ранг</p>
                    <div class="progskill">
                        <div class="headskill">
                            <p>Прогресс:</p>
                            <b>{job.current}<p>/ {job.nextLevel}</p></b>
                        </div>
                        <div class="bgprog">
                            <div class="progbar" style="width: {job.currentLevel >= 5 ? 100 : (((job.currentLevel / 5) * 100) + ((job.current / job.nextLevel) * 20))}%"></div>
                        </div>
                    </div>
                </div>
            {/each}
        </div>
    {/if}
{/if}

{#if selectView === "Carsuser"}
    <div class="list1">
        {#each (carsList || []).filter((item) => Array.isArray(item) && item[1]) as item}
            <div class="blockcars">
                <div class="headblock">
                    <img src="{document.cloud}inventoryItems/vehicle/{item[1].toLowerCase()}.png" alt=""/>
                </div>
                <h1>{item[1]}</h1>
                <div class="infocar">
                    <p>Рег. номер: <b>{item[2]}</b></p>
                    <div class="fuel">
                        <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <g clip-path="url(#clip0_40_22490)">
                            <path d="M14.5692 2.09766L13.2358 0.764313C13.1056 0.634094 12.8947 0.634094 12.7645 0.764313C12.6343 0.894531 12.6343 1.10547 12.7645 1.23566L13.8622 2.33331L12.7645 3.43097C12.702 3.49347 12.6668 3.57809 12.6668 3.66666V4.66666C12.6668 5.40203 13.2648 6 14.0002 6V11.6667C14.0002 11.8506 13.8508 12 13.6668 12C13.4829 12 13.3335 11.8506 13.3335 11.6667V11C13.3335 10.4486 12.8849 10 12.3335 10H12.0002V1.33334C12.0002 0.597969 11.4022 0 10.6668 0H4.00018C3.26484 0 2.66684 0.597969 2.66684 1.33334V13.3333C1.9315 13.3333 1.3335 13.9313 1.3335 14.6667V15.6667C1.33347 15.7105 1.34207 15.7538 1.35881 15.7943C1.37555 15.8347 1.40011 15.8715 1.43107 15.9025C1.46203 15.9334 1.49878 15.958 1.53924 15.9747C1.5797 15.9915 1.62306 16.0001 1.66684 16H13.0002C13.044 16.0001 13.0873 15.9915 13.1278 15.9747C13.1682 15.958 13.205 15.9334 13.236 15.9025C13.2669 15.8715 13.2915 15.8347 13.3082 15.7943C13.325 15.7538 13.3336 15.7105 13.3335 15.6667V14.6667C13.3335 13.9313 12.7356 13.3333 12.0002 13.3333V10.6667H12.3335C12.5174 10.6667 12.6669 10.8161 12.6669 11V11.6667C12.6669 12.2181 13.1154 12.6667 13.6669 12.6667C14.2183 12.6667 14.6669 12.2181 14.6669 11.6667V2.33334C14.6669 2.28957 14.6582 2.24622 14.6415 2.20578C14.6247 2.16534 14.6002 2.1286 14.5692 2.09766ZM10.6668 5.67284C10.6669 5.71663 10.6583 5.75999 10.6415 5.80044C10.6248 5.8409 10.6002 5.87766 10.5693 5.90862C10.5383 5.93958 10.5016 5.96413 10.4611 5.98087C10.4206 5.99761 10.3773 6.00622 10.3335 6.00619H4.3335C4.28971 6.00622 4.24635 5.99761 4.2059 5.98087C4.16544 5.96413 4.12868 5.93958 4.09772 5.90862C4.06676 5.87766 4.04221 5.8409 4.02547 5.80044C4.00873 5.75999 4.00012 5.71663 4.00015 5.67284V1.66666C4.00012 1.62287 4.00873 1.57951 4.02547 1.53906C4.04221 1.4986 4.06676 1.46184 4.09772 1.43088C4.12868 1.39992 4.16544 1.37537 4.2059 1.35863C4.24635 1.34189 4.28971 1.33328 4.3335 1.33331H10.3335C10.3773 1.33328 10.4206 1.34189 10.4611 1.35863C10.5016 1.37537 10.5383 1.39992 10.5693 1.43088C10.6002 1.46184 10.6248 1.4986 10.6415 1.53906C10.6583 1.57951 10.6669 1.62287 10.6668 1.66666V5.67284Z" fill="white" fill-opacity="0.2"/>
                            </g>
                            <defs>
                            <clipPath id="clip0_40_22490">
                            <rect width="16" height="16" fill="white"/>
                            </clipPath>
                            </defs>
                        </svg>
                        <span>Regular</span>                                        
                    </div>
                </div>
            </div>
        {/each}
    </div>
{/if}

{#if selectView === "Housebiz"}
    <div class="list1">
        {#if selectCharData.houseId}
            <div class="blockhouse" style="background: rgb(35, 35, 35) url({ImgHouse}) right center / auto 100% no-repeat; background-size: auto 100% !important;">
                <div class="bgblockhouse">
                    <h1>Дом #{selectCharData.houseId}</h1>
                    <div class="infohouse">
                        <div class="lefthouse">
                            <p>Списывается в час:</p>
                            <b>{selectCharData.houseCopiesHour}</b>
                        </div>
                        <div class="righthouse">
                            <p>Гаражных мест:<b>{selectCharData.maxcars}</b></p>
                            <p>Класс дома:<b>{selectCharData.houseType}</b></p>
                            <p>На счету дома:<b>{selectCharData.houseCash}</b></p>
                        </div>
                    </div>
                </div>
            </div>
        {/if}
        {#if selectCharData.BizId}
            <div class="blockhouse" style="background: rgb(35, 35, 35) url({ImgBiz}) right center / auto 100% no-repeat; background-size: auto 100% !important;">
                <div class="bgblockhouse">
                    <h1>Бизнес #{selectCharData.BizId}</h1>
                    <div class="infohouse">
                        <div class="lefthouse">
                            <p>Списывается в час:</p>
                            <b>{selectCharData.BizCopiesHour}</b>
                        </div>
                        <div class="righthouse">
                            <p>На счету бизнеса:<b>{selectCharData.BizCash}</b></p>
                        </div>
                    </div>
                </div>
            </div>
        {/if}
    </div>
{/if}