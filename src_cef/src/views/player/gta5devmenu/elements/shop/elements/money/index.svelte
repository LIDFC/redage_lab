<script>
    import { executeClient } from 'api/rage'
    import { accountRedbucks } from 'store/account'
    import { serverDonateDoubleConvert } from 'store/server'

    const onChange1 = () => {
        let moneytransfer1 = 200;
        if ($accountRedbucks < moneytransfer1)
            return window.notificationAdd(4, 9, `Недостаточно Redbucks!`, 3000);
        executeClient ("client.donate.change", moneytransfer1);
        return window.notificationAdd(2, 9, `Вы приобрели 10 000$`, 3000);
    }

    const onChange2 = () => {
        let moneytransfer2 = 1000;
        if ($accountRedbucks < moneytransfer2)
            return window.notificationAdd(4, 9, `Недостаточно Redbucks!`, 3000);
        executeClient ("client.donate.change", moneytransfer2);
        return window.notificationAdd(2, 9, `Вы приобрели 50 000$`, 3000);
    }

    const onChange3 = () => {
        let moneytransfer3 = 4000;
        if ($accountRedbucks < moneytransfer3)
            return window.notificationAdd(4, 9, `Недостаточно Redbucks!`, 3000);
        executeClient ("client.donate.change", moneytransfer3);
        return window.notificationAdd(2, 9, `Вы приобрели 200 000$`, 3000);
    }

    const onChange4 = () => {
        let moneytransfer4 = 10000;
        if ($accountRedbucks < moneytransfer4)
            return window.notificationAdd(4, 9, `Недостаточно Redbucks!`, 3000);
        executeClient ("client.donate.change", moneytransfer4);
        return window.notificationAdd(2, 9, `Вы приобрели 500 000$`, 3000);
    }

    const onChange5 = () => {
        let moneytransfer5 = 40000;
        if ($accountRedbucks < moneytransfer5)
            return window.notificationAdd(4, 9, `Недостаточно Redbucks!`, 3000);
        executeClient ("client.donate.change", moneytransfer5);
        return window.notificationAdd(2, 9, `Вы приобрели 2 000 000$`, 3000);
    }

    const onChange6 = () => {
        let moneytransfer6 = 200000;
        if ($accountRedbucks < moneytransfer6)
            return window.notificationAdd(4, 9, `Недостаточно Redbucks!`, 3000);
        executeClient ("client.donate.change", moneytransfer6);
        return window.notificationAdd(2, 9, `Вы приобрели 10 000 000$`, 3000);
    }
</script>

<div class="list1">
    <div class="blockmoney">
        <div class="iconitem">
            <img src="https://imgur.com/JrVcKvF.png" alt=""/>
        </div>
        <div class="infoitem">
            <h1>10 000$</h1>
            <span>10 000$ на ваш игровой счёт</span>
            <div class="buyinfoitem">
                <p>
                    <svg width="26" height="26" viewBox="0 0 26 26" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <g filter="url(#filter0_d_713_42)">
                    <rect x="5" y="5" width="16" height="16" rx="8" fill="#CFF80B"/>
                    </g>
                    <path d="M8.65985 15.097C8.65985 15.009 8.71852 14.965 8.83585 14.965C8.95318 14.965 9.31618 15.0457 9.92485 15.207C10.0642 14.0043 10.4565 12.699 11.1019 11.291C11.3292 10.7777 11.6005 10.521 11.9159 10.521C11.9745 10.521 12.0295 10.5833 12.0809 10.708C12.1395 10.8327 12.1689 10.9867 12.1689 11.17C12.1689 11.39 12.0515 11.995 11.8169 12.985C11.5895 13.975 11.4392 14.8367 11.3659 15.57C11.8499 15.68 12.2752 15.735 12.6419 15.735C13.0159 15.735 13.3569 15.6397 13.6649 15.449C13.9729 15.251 14.2369 14.998 14.4569 14.69C14.9115 14.0373 15.1389 13.2527 15.1389 12.336C15.1315 10.642 14.4569 9.795 13.1149 9.795C12.6382 9.80233 11.7949 10.015 10.5849 10.433C10.3502 10.5137 10.1449 10.554 9.96885 10.554C9.79285 10.554 9.70485 10.5027 9.70485 10.4C9.70485 10.2607 9.80752 10.0957 10.0129 9.905C10.2182 9.71433 10.4822 9.531 10.8049 9.355C11.5969 8.92967 12.3669 8.717 13.1149 8.717C14.1415 8.717 14.9849 8.992 15.6449 9.542C16.4222 10.1947 16.8072 11.1847 16.7999 12.512C16.7999 13.0767 16.7119 13.634 16.5359 14.184C16.3599 14.734 16.0922 15.2253 15.7329 15.658C15.3735 16.0833 14.9189 16.428 14.3689 16.692C13.8189 16.956 13.1699 17.088 12.4219 17.088C12.1212 17.088 11.7399 17.022 11.2779 16.89C11.2265 17.0293 11.0982 17.099 10.8929 17.099C10.5115 17.099 10.2512 17.0367 10.1119 16.912C9.97985 16.78 9.90285 16.5673 9.88085 16.274C9.52885 16.076 9.23552 15.8707 9.00085 15.658C8.77352 15.4453 8.65985 15.2583 8.65985 15.097Z" fill="#1E1E1E" fill-opacity="0.5"/>
                    <defs>
                    <filter id="filter0_d_713_42" x="0" y="0" width="26" height="26" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                    <feFlood flood-opacity="0" result="BackgroundImageFix"/>
                    <feColorMatrix in="SourceAlpha" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 127 0" result="hardAlpha"/>
                    <feOffset/>
                    <feGaussianBlur stdDeviation="2.5"/>
                    <feComposite in2="hardAlpha" operator="out"/>
                    <feColorMatrix type="matrix" values="0 0 0 0 0.811765 0 0 0 0 0.972549 0 0 0 0 0.0431373 0 0 0 0.25 0"/>
                    <feBlend mode="normal" in2="BackgroundImageFix" result="effect1_dropShadow_713_42"/>
                    <feBlend mode="normal" in="SourceGraphic" in2="effect1_dropShadow_713_42" result="shape"/>
                    </filter>
                    </defs>
                    </svg>
                    200
                </p>
                <div class="btnbuyitem" on:keypress on:click={onChange1}>Приобрести</div>
            </div>
        </div>
    </div>
    <div class="blockmoney">
        <div class="iconitem">
            <img src="https://imgur.com/ayC1pdc.png" alt=""/>
        </div>
        <div class="infoitem">
            <h1>50 000$</h1>
            <span>50 000$ на ваш игровой счёт</span>
            <div class="buyinfoitem">
                <p>
                    <svg width="26" height="26" viewBox="0 0 26 26" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <g filter="url(#filter0_d_713_42)">
                    <rect x="5" y="5" width="16" height="16" rx="8" fill="#CFF80B"/>
                    </g>
                    <path d="M8.65985 15.097C8.65985 15.009 8.71852 14.965 8.83585 14.965C8.95318 14.965 9.31618 15.0457 9.92485 15.207C10.0642 14.0043 10.4565 12.699 11.1019 11.291C11.3292 10.7777 11.6005 10.521 11.9159 10.521C11.9745 10.521 12.0295 10.5833 12.0809 10.708C12.1395 10.8327 12.1689 10.9867 12.1689 11.17C12.1689 11.39 12.0515 11.995 11.8169 12.985C11.5895 13.975 11.4392 14.8367 11.3659 15.57C11.8499 15.68 12.2752 15.735 12.6419 15.735C13.0159 15.735 13.3569 15.6397 13.6649 15.449C13.9729 15.251 14.2369 14.998 14.4569 14.69C14.9115 14.0373 15.1389 13.2527 15.1389 12.336C15.1315 10.642 14.4569 9.795 13.1149 9.795C12.6382 9.80233 11.7949 10.015 10.5849 10.433C10.3502 10.5137 10.1449 10.554 9.96885 10.554C9.79285 10.554 9.70485 10.5027 9.70485 10.4C9.70485 10.2607 9.80752 10.0957 10.0129 9.905C10.2182 9.71433 10.4822 9.531 10.8049 9.355C11.5969 8.92967 12.3669 8.717 13.1149 8.717C14.1415 8.717 14.9849 8.992 15.6449 9.542C16.4222 10.1947 16.8072 11.1847 16.7999 12.512C16.7999 13.0767 16.7119 13.634 16.5359 14.184C16.3599 14.734 16.0922 15.2253 15.7329 15.658C15.3735 16.0833 14.9189 16.428 14.3689 16.692C13.8189 16.956 13.1699 17.088 12.4219 17.088C12.1212 17.088 11.7399 17.022 11.2779 16.89C11.2265 17.0293 11.0982 17.099 10.8929 17.099C10.5115 17.099 10.2512 17.0367 10.1119 16.912C9.97985 16.78 9.90285 16.5673 9.88085 16.274C9.52885 16.076 9.23552 15.8707 9.00085 15.658C8.77352 15.4453 8.65985 15.2583 8.65985 15.097Z" fill="#1E1E1E" fill-opacity="0.5"/>
                    <defs>
                    <filter id="filter0_d_713_42" x="0" y="0" width="26" height="26" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                    <feFlood flood-opacity="0" result="BackgroundImageFix"/>
                    <feColorMatrix in="SourceAlpha" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 127 0" result="hardAlpha"/>
                    <feOffset/>
                    <feGaussianBlur stdDeviation="2.5"/>
                    <feComposite in2="hardAlpha" operator="out"/>
                    <feColorMatrix type="matrix" values="0 0 0 0 0.811765 0 0 0 0 0.972549 0 0 0 0 0.0431373 0 0 0 0.25 0"/>
                    <feBlend mode="normal" in2="BackgroundImageFix" result="effect1_dropShadow_713_42"/>
                    <feBlend mode="normal" in="SourceGraphic" in2="effect1_dropShadow_713_42" result="shape"/>
                    </filter>
                    </defs>
                    </svg>
                    1000
                </p>
                <div class="btnbuyitem" on:keypress on:click={onChange2}>Приобрести</div>
            </div>
        </div>
    </div>
    <div class="blockmoney">
        <div class="iconitem">
            <img src="https://imgur.com/0sXE8dx.png" alt=""/>
        </div>
        <div class="infoitem">
            <h1>200 000$</h1>
            <span>200 000$ на ваш игровой счёт</span>
            <div class="buyinfoitem">
                <p>
                    <svg width="26" height="26" viewBox="0 0 26 26" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <g filter="url(#filter0_d_713_42)">
                    <rect x="5" y="5" width="16" height="16" rx="8" fill="#CFF80B"/>
                    </g>
                    <path d="M8.65985 15.097C8.65985 15.009 8.71852 14.965 8.83585 14.965C8.95318 14.965 9.31618 15.0457 9.92485 15.207C10.0642 14.0043 10.4565 12.699 11.1019 11.291C11.3292 10.7777 11.6005 10.521 11.9159 10.521C11.9745 10.521 12.0295 10.5833 12.0809 10.708C12.1395 10.8327 12.1689 10.9867 12.1689 11.17C12.1689 11.39 12.0515 11.995 11.8169 12.985C11.5895 13.975 11.4392 14.8367 11.3659 15.57C11.8499 15.68 12.2752 15.735 12.6419 15.735C13.0159 15.735 13.3569 15.6397 13.6649 15.449C13.9729 15.251 14.2369 14.998 14.4569 14.69C14.9115 14.0373 15.1389 13.2527 15.1389 12.336C15.1315 10.642 14.4569 9.795 13.1149 9.795C12.6382 9.80233 11.7949 10.015 10.5849 10.433C10.3502 10.5137 10.1449 10.554 9.96885 10.554C9.79285 10.554 9.70485 10.5027 9.70485 10.4C9.70485 10.2607 9.80752 10.0957 10.0129 9.905C10.2182 9.71433 10.4822 9.531 10.8049 9.355C11.5969 8.92967 12.3669 8.717 13.1149 8.717C14.1415 8.717 14.9849 8.992 15.6449 9.542C16.4222 10.1947 16.8072 11.1847 16.7999 12.512C16.7999 13.0767 16.7119 13.634 16.5359 14.184C16.3599 14.734 16.0922 15.2253 15.7329 15.658C15.3735 16.0833 14.9189 16.428 14.3689 16.692C13.8189 16.956 13.1699 17.088 12.4219 17.088C12.1212 17.088 11.7399 17.022 11.2779 16.89C11.2265 17.0293 11.0982 17.099 10.8929 17.099C10.5115 17.099 10.2512 17.0367 10.1119 16.912C9.97985 16.78 9.90285 16.5673 9.88085 16.274C9.52885 16.076 9.23552 15.8707 9.00085 15.658C8.77352 15.4453 8.65985 15.2583 8.65985 15.097Z" fill="#1E1E1E" fill-opacity="0.5"/>
                    <defs>
                    <filter id="filter0_d_713_42" x="0" y="0" width="26" height="26" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                    <feFlood flood-opacity="0" result="BackgroundImageFix"/>
                    <feColorMatrix in="SourceAlpha" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 127 0" result="hardAlpha"/>
                    <feOffset/>
                    <feGaussianBlur stdDeviation="2.5"/>
                    <feComposite in2="hardAlpha" operator="out"/>
                    <feColorMatrix type="matrix" values="0 0 0 0 0.811765 0 0 0 0 0.972549 0 0 0 0 0.0431373 0 0 0 0.25 0"/>
                    <feBlend mode="normal" in2="BackgroundImageFix" result="effect1_dropShadow_713_42"/>
                    <feBlend mode="normal" in="SourceGraphic" in2="effect1_dropShadow_713_42" result="shape"/>
                    </filter>
                    </defs>
                    </svg>
                    4000
                </p>
                <div class="btnbuyitem" on:keypress on:click={onChange3}>Приобрести</div>
            </div>
        </div>
    </div>
    <div class="blockmoney">
        <div class="iconitem">
            <img src="https://imgur.com/4kBx0oq.png" alt=""/>
        </div>
        <div class="infoitem">
            <h1>500 000$</h1>
            <span>500 000$ на ваш игровой счёт</span>
            <div class="buyinfoitem">
                <p>
                    <svg width="26" height="26" viewBox="0 0 26 26" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <g filter="url(#filter0_d_713_42)">
                    <rect x="5" y="5" width="16" height="16" rx="8" fill="#CFF80B"/>
                    </g>
                    <path d="M8.65985 15.097C8.65985 15.009 8.71852 14.965 8.83585 14.965C8.95318 14.965 9.31618 15.0457 9.92485 15.207C10.0642 14.0043 10.4565 12.699 11.1019 11.291C11.3292 10.7777 11.6005 10.521 11.9159 10.521C11.9745 10.521 12.0295 10.5833 12.0809 10.708C12.1395 10.8327 12.1689 10.9867 12.1689 11.17C12.1689 11.39 12.0515 11.995 11.8169 12.985C11.5895 13.975 11.4392 14.8367 11.3659 15.57C11.8499 15.68 12.2752 15.735 12.6419 15.735C13.0159 15.735 13.3569 15.6397 13.6649 15.449C13.9729 15.251 14.2369 14.998 14.4569 14.69C14.9115 14.0373 15.1389 13.2527 15.1389 12.336C15.1315 10.642 14.4569 9.795 13.1149 9.795C12.6382 9.80233 11.7949 10.015 10.5849 10.433C10.3502 10.5137 10.1449 10.554 9.96885 10.554C9.79285 10.554 9.70485 10.5027 9.70485 10.4C9.70485 10.2607 9.80752 10.0957 10.0129 9.905C10.2182 9.71433 10.4822 9.531 10.8049 9.355C11.5969 8.92967 12.3669 8.717 13.1149 8.717C14.1415 8.717 14.9849 8.992 15.6449 9.542C16.4222 10.1947 16.8072 11.1847 16.7999 12.512C16.7999 13.0767 16.7119 13.634 16.5359 14.184C16.3599 14.734 16.0922 15.2253 15.7329 15.658C15.3735 16.0833 14.9189 16.428 14.3689 16.692C13.8189 16.956 13.1699 17.088 12.4219 17.088C12.1212 17.088 11.7399 17.022 11.2779 16.89C11.2265 17.0293 11.0982 17.099 10.8929 17.099C10.5115 17.099 10.2512 17.0367 10.1119 16.912C9.97985 16.78 9.90285 16.5673 9.88085 16.274C9.52885 16.076 9.23552 15.8707 9.00085 15.658C8.77352 15.4453 8.65985 15.2583 8.65985 15.097Z" fill="#1E1E1E" fill-opacity="0.5"/>
                    <defs>
                    <filter id="filter0_d_713_42" x="0" y="0" width="26" height="26" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                    <feFlood flood-opacity="0" result="BackgroundImageFix"/>
                    <feColorMatrix in="SourceAlpha" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 127 0" result="hardAlpha"/>
                    <feOffset/>
                    <feGaussianBlur stdDeviation="2.5"/>
                    <feComposite in2="hardAlpha" operator="out"/>
                    <feColorMatrix type="matrix" values="0 0 0 0 0.811765 0 0 0 0 0.972549 0 0 0 0 0.0431373 0 0 0 0.25 0"/>
                    <feBlend mode="normal" in2="BackgroundImageFix" result="effect1_dropShadow_713_42"/>
                    <feBlend mode="normal" in="SourceGraphic" in2="effect1_dropShadow_713_42" result="shape"/>
                    </filter>
                    </defs>
                    </svg>
                    10000
                </p>
                <div class="btnbuyitem" on:keypress on:click={onChange4}>Приобрести</div>
            </div>
        </div>
    </div>
    <div class="blockmoney">
        <div class="iconitem">
            <img src="https://imgur.com/e9tSCkg.png" alt=""/>
        </div>
        <div class="infoitem">
            <h1>2 000 000$</h1>
            <span>2 000 000$ на ваш игровой счёт</span>
            <div class="buyinfoitem">
                <p>
                    <svg width="26" height="26" viewBox="0 0 26 26" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <g filter="url(#filter0_d_713_42)">
                    <rect x="5" y="5" width="16" height="16" rx="8" fill="#CFF80B"/>
                    </g>
                    <path d="M8.65985 15.097C8.65985 15.009 8.71852 14.965 8.83585 14.965C8.95318 14.965 9.31618 15.0457 9.92485 15.207C10.0642 14.0043 10.4565 12.699 11.1019 11.291C11.3292 10.7777 11.6005 10.521 11.9159 10.521C11.9745 10.521 12.0295 10.5833 12.0809 10.708C12.1395 10.8327 12.1689 10.9867 12.1689 11.17C12.1689 11.39 12.0515 11.995 11.8169 12.985C11.5895 13.975 11.4392 14.8367 11.3659 15.57C11.8499 15.68 12.2752 15.735 12.6419 15.735C13.0159 15.735 13.3569 15.6397 13.6649 15.449C13.9729 15.251 14.2369 14.998 14.4569 14.69C14.9115 14.0373 15.1389 13.2527 15.1389 12.336C15.1315 10.642 14.4569 9.795 13.1149 9.795C12.6382 9.80233 11.7949 10.015 10.5849 10.433C10.3502 10.5137 10.1449 10.554 9.96885 10.554C9.79285 10.554 9.70485 10.5027 9.70485 10.4C9.70485 10.2607 9.80752 10.0957 10.0129 9.905C10.2182 9.71433 10.4822 9.531 10.8049 9.355C11.5969 8.92967 12.3669 8.717 13.1149 8.717C14.1415 8.717 14.9849 8.992 15.6449 9.542C16.4222 10.1947 16.8072 11.1847 16.7999 12.512C16.7999 13.0767 16.7119 13.634 16.5359 14.184C16.3599 14.734 16.0922 15.2253 15.7329 15.658C15.3735 16.0833 14.9189 16.428 14.3689 16.692C13.8189 16.956 13.1699 17.088 12.4219 17.088C12.1212 17.088 11.7399 17.022 11.2779 16.89C11.2265 17.0293 11.0982 17.099 10.8929 17.099C10.5115 17.099 10.2512 17.0367 10.1119 16.912C9.97985 16.78 9.90285 16.5673 9.88085 16.274C9.52885 16.076 9.23552 15.8707 9.00085 15.658C8.77352 15.4453 8.65985 15.2583 8.65985 15.097Z" fill="#1E1E1E" fill-opacity="0.5"/>
                    <defs>
                    <filter id="filter0_d_713_42" x="0" y="0" width="26" height="26" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                    <feFlood flood-opacity="0" result="BackgroundImageFix"/>
                    <feColorMatrix in="SourceAlpha" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 127 0" result="hardAlpha"/>
                    <feOffset/>
                    <feGaussianBlur stdDeviation="2.5"/>
                    <feComposite in2="hardAlpha" operator="out"/>
                    <feColorMatrix type="matrix" values="0 0 0 0 0.811765 0 0 0 0 0.972549 0 0 0 0 0.0431373 0 0 0 0.25 0"/>
                    <feBlend mode="normal" in2="BackgroundImageFix" result="effect1_dropShadow_713_42"/>
                    <feBlend mode="normal" in="SourceGraphic" in2="effect1_dropShadow_713_42" result="shape"/>
                    </filter>
                    </defs>
                    </svg>
                    100
                </p>
                <div class="btnbuyitem" on:keypress on:click={onChange5}>Приобрести</div>
            </div>
        </div>
    </div>
    <div class="blockmoney">
        <div class="iconitem">
            <img src="https://imgur.com/H23eohN.png" alt=""/>
        </div>
        <div class="infoitem">
            <h1>10 000 000$</h1>
            <span>10 000 000$ на ваш игровой счёт</span>
            <div class="buyinfoitem">
                <p>
                    <svg width="26" height="26" viewBox="0 0 26 26" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <g filter="url(#filter0_d_713_42)">
                    <rect x="5" y="5" width="16" height="16" rx="8" fill="#CFF80B"/>
                    </g>
                    <path d="M8.65985 15.097C8.65985 15.009 8.71852 14.965 8.83585 14.965C8.95318 14.965 9.31618 15.0457 9.92485 15.207C10.0642 14.0043 10.4565 12.699 11.1019 11.291C11.3292 10.7777 11.6005 10.521 11.9159 10.521C11.9745 10.521 12.0295 10.5833 12.0809 10.708C12.1395 10.8327 12.1689 10.9867 12.1689 11.17C12.1689 11.39 12.0515 11.995 11.8169 12.985C11.5895 13.975 11.4392 14.8367 11.3659 15.57C11.8499 15.68 12.2752 15.735 12.6419 15.735C13.0159 15.735 13.3569 15.6397 13.6649 15.449C13.9729 15.251 14.2369 14.998 14.4569 14.69C14.9115 14.0373 15.1389 13.2527 15.1389 12.336C15.1315 10.642 14.4569 9.795 13.1149 9.795C12.6382 9.80233 11.7949 10.015 10.5849 10.433C10.3502 10.5137 10.1449 10.554 9.96885 10.554C9.79285 10.554 9.70485 10.5027 9.70485 10.4C9.70485 10.2607 9.80752 10.0957 10.0129 9.905C10.2182 9.71433 10.4822 9.531 10.8049 9.355C11.5969 8.92967 12.3669 8.717 13.1149 8.717C14.1415 8.717 14.9849 8.992 15.6449 9.542C16.4222 10.1947 16.8072 11.1847 16.7999 12.512C16.7999 13.0767 16.7119 13.634 16.5359 14.184C16.3599 14.734 16.0922 15.2253 15.7329 15.658C15.3735 16.0833 14.9189 16.428 14.3689 16.692C13.8189 16.956 13.1699 17.088 12.4219 17.088C12.1212 17.088 11.7399 17.022 11.2779 16.89C11.2265 17.0293 11.0982 17.099 10.8929 17.099C10.5115 17.099 10.2512 17.0367 10.1119 16.912C9.97985 16.78 9.90285 16.5673 9.88085 16.274C9.52885 16.076 9.23552 15.8707 9.00085 15.658C8.77352 15.4453 8.65985 15.2583 8.65985 15.097Z" fill="#1E1E1E" fill-opacity="0.5"/>
                    <defs>
                    <filter id="filter0_d_713_42" x="0" y="0" width="26" height="26" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                    <feFlood flood-opacity="0" result="BackgroundImageFix"/>
                    <feColorMatrix in="SourceAlpha" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 127 0" result="hardAlpha"/>
                    <feOffset/>
                    <feGaussianBlur stdDeviation="2.5"/>
                    <feComposite in2="hardAlpha" operator="out"/>
                    <feColorMatrix type="matrix" values="0 0 0 0 0.811765 0 0 0 0 0.972549 0 0 0 0 0.0431373 0 0 0 0.25 0"/>
                    <feBlend mode="normal" in2="BackgroundImageFix" result="effect1_dropShadow_713_42"/>
                    <feBlend mode="normal" in="SourceGraphic" in2="effect1_dropShadow_713_42" result="shape"/>
                    </filter>
                    </defs>
                    </svg>
                    200000
                </p>
                <div class="btnbuyitem" on:keypress on:click={onChange6}>Приобрести</div>
            </div>
        </div>
    </div>
</div>