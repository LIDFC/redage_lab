
<script>
    import { executeClient } from 'api/rage'
    import { translateText } from 'lang'
    import { format } from 'api/formatter'
    import './css/main.sass';
    import './css/main.css';
    import './fonts/style.css';
    import './fonts/Gilroy/stylesheet.css';
    import './fonts/SFPro/stylesheet.css';
    import authInfo from './authInfo';
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
    let list = [];
    let select = 0;
    let selectTime = -1;
    let sordId = "price";
    let colorId = 0;

    let isDonateAutoroom = false;

    window.authShop = {
        data: (value, isDonate = false) => {
            let returnList = [];
            let modelInfo;
            JSON.parse(value).forEach(value => {
                modelInfo = authInfo [value.modelName] || false;
                
                returnList = [
                    ...returnList, {
                        ...value,
                        speed: (!modelInfo || (modelInfo && !modelInfo.speed)) ? value.speed : modelInfo.speed,
                        boost: (!modelInfo || (modelInfo && !modelInfo.boost)) ? value.boost : modelInfo.boost,
                        seat: (!modelInfo || (modelInfo && !modelInfo.seats)) ? value.seat : modelInfo.seats,
                        name: (!modelInfo || (modelInfo && !modelInfo.name)) ? value.name : modelInfo.name,
                        model: (!modelInfo || (modelInfo && !modelInfo.model)) ? value.model : modelInfo.model,
                        img: (!modelInfo || (modelInfo && !modelInfo.img)) ? value.img : modelInfo.img,
                        invslots: (value.invslots !== undefined) ? value.invslots : 25,
                        ypr: (!modelInfo || (modelInfo && !modelInfo.ypr)) ? value.ypr : modelInfo.ypr,
                        fuel: (!modelInfo || (modelInfo && !modelInfo.fuel)) ? value.fuel : modelInfo.fuel,
                        break: (!modelInfo || (modelInfo && !modelInfo.break)) ? value.break : modelInfo.break,
                        desc: !modelInfo ? false : modelInfo.desc,
                    }
                ];
            });
            list = returnList;
            isDonateAutoroom = isDonate;
            return;
        }
    }


    const sort = (value) => {
        if (sordId === value) return;
        let sortList = list;
        let sortSelect = -1;
        if (select !== -1 && list[select]) sortSelect = list[select].index;
        sordId = value;
        sortList.sort(( b, a ) =>  b[value] - a[value]);
        if (sortSelect !== -1) {
            sortList.forEach((value, index) => {
                if (sortSelect === value.index) {
                    select = index;
                }
            });
        }
        list = sortList;
    }

    const setItem = (index) => {
        if (!list[index]) return;
        else if (index === select) return;
        //else if (selectTime > new Date().getTime()) return;
        //selectTime = new Date().getTime() + 1000;
        select = index;
        executeClient ('auto', 'model', list[index].index);
    }

    const setColor = (index) => {
        if (index === colorId) return;
        colorId = index;
        executeClient ('auto', 'color', index);
    }

    const startTestDrive = (type) => {
        if (select === -1 || !list[select]) return;
        executeClient ('testDrive', type);
    }

    const HandleKeyDown = (event) => {
        const { keyCode } = event;
        if (keyCode !== 27) return;

        executeClient ('closeAuto');
    }

    let searchText = "";
</script>

<svelte:window on:keyup={HandleKeyDown} />

<div class="autodilergta5dev">
    <div class="autolmenu">
        <img src="{document.cloud}img/autoshop_logo.png" alt=""/>
        <h1>Добро пожаловать</h1>
        <span>Введите название</span>
        <div class="seachcars">
            <input bind:value={searchText} placeholder="Поиск">
            <svg width="14" height="14" viewBox="0 0 14 14" fill="none" xmlns="http://www.w3.org/2000/svg">
                <g clip-path="url(#clip0_7_45)">
                <path d="M13.8289 13.0041L9.84773 9.02291C10.6189 8.07035 11.0832 6.85993 11.0832 5.54162C11.0832 2.48615 8.59706 0 5.54159 0C2.48612 0 0 2.48612 0 5.54159C0 8.59706 2.48615 11.0832 5.54162 11.0832C6.85993 11.0832 8.07035 10.6189 9.02291 9.84773L13.0041 13.8289C13.1179 13.9427 13.2672 13.9999 13.4165 13.9999C13.5659 13.9999 13.7152 13.9427 13.829 13.8289C14.057 13.6009 14.057 13.2322 13.8289 13.0041ZM5.54162 9.91655C3.12897 9.91655 1.16666 7.95425 1.16666 5.54159C1.16666 3.12894 3.12897 1.16664 5.54162 1.16664C7.95427 1.16664 9.91658 3.12894 9.91658 5.54159C9.91658 7.95425 7.95425 9.91655 5.54162 9.91655Z" fill="white" fill-opacity="0.4"/>
                </g>
                <defs>
                <clipPath id="clip0_7_45">
                <rect width="14" height="14" fill="white"/>
                </clipPath>
                </defs>
            </svg>                                       
        </div>
        <div class="allist" on:mouseenter={() => executeClient ("client.camera.toggled", false)} on:mouseleave={() => executeClient ("client.camera.toggled", true)}>
            {#each list as value, index}
            {#if (!searchText || !searchText.length) || 
                (searchText && value.name && value.name.toLowerCase().trim().includes(searchText.toLowerCase().trim())) || 
                (searchText && value.model && value.model.toLowerCase().trim().includes(searchText.toLowerCase().trim()))}
                <div class="carsblock" class:act={select === index} on:keypress={() => {}} on:click={() => setItem (index)}>
                  <p>{@html value.name} {@html value.model}</p>
                </div>
              {/if}
            {/each}
        </div>
        <div class="alexit" on:keypress={() => {}} on:click={() => executeClient ('closeAuto')}>
            <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                <g clip-path="url(#clip0_7_88)">
                <path d="M14 0H2C0.895503 0 0 0.895503 0 2V14C0 15.1045 0.895503 16 2 16H14C15.1045 16 16 15.1045 16 14V2C16 0.895503 15.1045 0 14 0ZM11 8.5H5.70701L7.7675 10.5605C7.96301 10.756 7.96301 11.0725 7.7675 11.2675C7.572 11.4625 7.2555 11.463 7.0605 11.2675L4.232 8.43901C4.1125 8.3195 4.07501 8.15501 4.10248 8C4.07498 7.84499 4.1125 7.6805 4.2325 7.5605L7.06099 4.732C7.2565 4.5365 7.57299 4.5365 7.768 4.732C7.9635 4.9275 7.9635 5.244 7.768 5.43901L5.70701 7.5H11C11.276 7.5 11.5 7.724 11.5 8C11.5 8.276 11.276 8.5 11 8.5Z" fill="white" fill-opacity="0.5"/>
                </g>
                <defs>
                <clipPath id="clip0_7_88">
                <rect width="16" height="16" fill="white"/>
                </clipPath>
                </defs>
            </svg>                
            <p>Покинуть автосалон</p>                        
        </div>
    </div>
    {#if (select !== -1 && list [select])}
        <div class="autormenu">
            <div class="armenuauto">
                <h2>{list [select].name} {list [select].model}</h2>
                <div class="hautohead">
                    <div class="blockleft">
                        <h1>Багажник:</h1>
                        <p>{list [select].invslots}
                            <svg width="14" height="14" viewBox="0 0 14 14" fill="none" xmlns="http://www.w3.org/2000/svg">
                                <g clip-path="url(#clip0_7_16)">
                                <path d="M7.00001 0C5.93051 0 5.0604 0.870105 5.0604 1.9396C5.0604 2.03697 5.06781 2.13259 5.08173 2.22616H5.92452C5.89957 2.13269 5.88694 2.03635 5.88695 1.9396C5.88695 1.32584 6.38628 0.82652 7.00003 0.82652C7.61379 0.82652 8.11309 1.32584 8.11309 1.9396C8.11308 2.03635 8.10045 2.13268 8.07552 2.22616H8.91831C8.93222 2.13262 8.93963 2.03697 8.93963 1.9396C8.93955 0.870105 8.06947 0 7.00001 0ZM13.5911 11.0198L12.554 5.14957C12.3393 3.93452 11.288 3.05266 10.0542 3.05266H3.94582C2.71201 3.05266 1.66067 3.93452 1.446 5.14957L0.408904 11.0198C0.27801 11.7608 0.480299 12.5164 0.963846 13.0929C1.44742 13.6694 2.15628 14 2.90875 14H11.0913C11.8438 14 12.5526 13.6694 13.0362 13.0929C13.5198 12.5164 13.722 11.7608 13.5911 11.0198ZM6.6671 10.1808C6.59009 10.2555 6.48701 10.2972 6.37974 10.2971C6.32431 10.2972 6.26943 10.2861 6.21839 10.2644C6.16735 10.2428 6.1212 10.2111 6.08271 10.1712L4.92459 8.97447V9.88384C4.92459 10.1121 4.73955 10.2971 4.51131 10.2971C4.28308 10.2971 4.09804 10.1121 4.09804 9.88384V7.16882C4.09804 6.94058 4.28308 6.75555 4.51131 6.75555C4.73955 6.75555 4.92459 6.94058 4.92459 7.16882V8.07819L6.08271 6.88144C6.24144 6.71743 6.50303 6.71314 6.6671 6.87184C6.8311 7.03057 6.83537 7.29217 6.67669 7.4562L5.64113 8.52633L6.67669 9.59645C6.8354 9.76046 6.8311 10.0221 6.6671 10.1808ZM8.57897 10.2971C7.84941 10.2971 7.25586 9.70356 7.25586 8.974V8.07871C7.25586 7.34915 7.84941 6.7556 8.57897 6.7556C8.98935 6.7556 9.36961 6.94127 9.62238 7.26502C9.76285 7.44491 9.73085 7.70465 9.5509 7.84506C9.37096 7.98552 9.11127 7.9535 8.97083 7.77361C8.87584 7.65193 8.733 7.58209 8.57894 7.58209C8.30512 7.58209 8.08235 7.80486 8.08235 8.07868V8.97395C8.08235 9.24777 8.30512 9.47054 8.57894 9.47054C8.82468 9.47054 9.02935 9.29108 9.0687 9.0563H8.86843C8.64019 9.0563 8.45516 8.87127 8.45516 8.64303C8.45516 8.41479 8.64019 8.22976 8.86843 8.22976H9.48878C9.71702 8.22976 9.90205 8.41479 9.90205 8.64303V8.97395H9.90208C9.90208 9.70356 9.30853 10.2971 8.57897 10.2971Z" fill="#C0C0C0"/>
                                </g>
                                <defs>
                                <clipPath id="clip0_7_16">
                                <rect width="14" height="14" fill="white"/>
                                </clipPath>
                                </defs>
                            </svg>                                    
                        </p>
                    </div>
                    <div class="blockcenter">
                        <h1>Бак:</h1>
                        <p>{list [select].fuel} л.</p>
                    </div>
                    <div class="blockright">
                        <h1>Тип топлива:</h1>
                        <p>Regular
                            <svg width="14" height="14" viewBox="0 0 14 14" fill="none" xmlns="http://www.w3.org/2000/svg">
                                <path d="M0.694444 12.1111V1.69444C0.694444 1.51027 0.767609 1.33363 0.897842 1.2034C1.02808 1.07316 1.20471 1 1.38889 1H7.63889C7.82307 1 7.9997 1.07316 8.12993 1.2034C8.26017 1.33363 8.33333 1.51027 8.33333 1.69444V7.25H9.72222C10.0906 7.25 10.4438 7.39633 10.7043 7.6568C10.9648 7.91726 11.1111 8.27053 11.1111 8.63889V11.4167C11.1111 11.6008 11.1843 11.7775 11.3145 11.9077C11.4447 12.0379 11.6214 12.1111 11.8056 12.1111C11.9897 12.1111 12.1664 12.0379 12.2966 11.9077C12.4268 11.7775 12.5 11.6008 12.5 11.4167V6.55556H11.1111C10.9269 6.55556 10.7503 6.48239 10.6201 6.35216C10.4898 6.22192 10.4167 6.04529 10.4167 5.86111V3.37083L9.26597 2.22014L10.2479 1.23819L13.6854 4.67569C13.75 4.74007 13.8013 4.8166 13.8362 4.90086C13.8711 4.98512 13.889 5.07546 13.8889 5.16667V11.4167C13.8889 11.9692 13.6694 12.4991 13.2787 12.8898C12.888 13.2805 12.3581 13.5 11.8056 13.5C11.253 13.5 10.7231 13.2805 10.3324 12.8898C9.94171 12.4991 9.72222 11.9692 9.72222 11.4167V8.63889H8.33333V12.1111H9.02778V13.5H0V12.1111H0.694444ZM2.08333 2.38889V6.55556H6.94444V2.38889H2.08333Z" fill="#989898"/>
                            </svg>                                    
                        </p>
                    </div>
                </div>
                <div class="hauto">
                    <div class="hautotop">
                        <p>Скорость</p>
                        <b>{list [select].speed} км/ч.</b>
                    </div>
                    <div class="hautodown">
                        <div class="bgproghauto">
                            <div style="width: {list [select].speed / 3.1}%" class="proghauto"></div>
                        </div>
                    </div>
                </div>
                <div class="hauto">
                    <div class="hautotop">
                        <p>Ускорение</p>
                        <b>{list [select].boost}</b>
                    </div>
                    <div class="hautodown">
                        <div class="bgproghauto">
                            <div style="width: {list [select].boost / 1}%" class="proghauto"></div>
                        </div>
                    </div>
                </div>
                <div class="hauto">
                    <div class="hautotop">
                        <p>Торможение</p>
                        <b>{list [select].break}</b>
                    </div>
                    <div class="hautodown">
                        <div class="bgproghauto">
                            <div style="width: {list [select].break / 1.25}%" class="proghauto"></div>
                        </div>
                    </div>
                </div>
                <div class="hauto">
                    <div class="hautotop">
                        <p>Управляемость</p>
                        <b>{list [select].ypr} км/ч.</b>
                    </div>
                    <div class="hautodown">
                        <div class="bgproghauto">
                            <div style="width: {list [select].ypr / 0.5}%" class="proghauto"></div>
                        </div>
                    </div>
                </div>
                <div class="colorscars">
                    <h1>Выберите цвет:</h1>
                    <div class="listcolors">
                        {#each authColors as value, index}
                            <div key={index} class={`blockcolor ${colorId !== index || "act"}`} on:keypress={() => {}} on:click={() => setColor (index)} style="background: {value}" ></div>
                        {/each}
                    </div>
                </div>
                <div class="pricecars">
                    <p>Цена:</p>
                    <b>${format("money", list [select].gosPrice)}</b>
                </div>
                <div class="buycars">
                    <div class="buyblock" on:keypress={() => {}} on:click={() => executeClient ('buyAuto', 1, 'closeAuto')}>
                        <p>Купить</p>
                    </div>
                    <div class="buyblock" on:keypress={() => {}} on:click={() => executeClient ('buyAuto', 2, 'closeAuto')}>
                        <p>Купить(ОРГ)</p>
                    </div>
                </div>
                <div class="testdrive" on:keypress={() => {}} on:click={() => startTestDrive (1)}>
                    <p>Тест-драйв</p>
                </div>
            </div>
            <div class="textdown">
                <p>Удерживайте левую кнопку мыши, чтобы поворачивать камеру</p>
            </div>
        </div>
    {/if}
</div>