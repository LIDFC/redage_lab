<script>
    import { translateText } from 'lang'
    export let SetPopup;
    export let popupData;
    import { format } from 'api/formatter'
    import {executeClient, executeClientAsync} from 'api/rage'

    let caseData = {};
    let isLoad = false;
    executeClientAsync("donate.roulette.getCaseOne").then((result) => {
        if (result && typeof result === "string") {
            caseData = JSON.parse(result);
            isLoad = true;
        }
    });

    let selectWin = 0;
    
    const Take = (index, IndexList) => {
        if (IndexList !== -1) {
            popupData[index].Done = true;
            ClosePopup ();
        } else 
            SetPopup ()
        executeClient ("client.roullete.confirm", false, IndexList);
    }

    const Sell = (index, IndexList) => {
        if (IndexList !== -1) {
            popupData[index].Done = true;
            ClosePopup ();
        } else 
            SetPopup ()
        executeClient ("client.roullete.confirm", true, IndexList);
    }

    const ClosePopup = () => {
        let isDone = true;
        
        popupData.forEach(item => {
            if (!item.Done && item.winBlock && caseData.items[item.winBlock.ItemIndex])
                isDone = false;
        });
        
        if (isDone)
            SetPopup ()
    }

    const onAllWinBlockPrice = (data) => {
        let price = 0;
        data.forEach(item => {
            if (!item.Done && item.winBlock && item.winBlock.Price)
                price += item.winBlock.Price;
        });
        return price;
    }
</script>

{#if isLoad}
    <div class="headcasemenu">
        <div class="cases_content_data1">
            <div class="lists">
            {#each popupData as item, index}
                {#if !item.Done && item.winBlock && caseData.items[item.winBlock.ItemIndex]}
                <div class="bg">
                    <svg class="{caseData.items[item.winBlock.ItemIndex].color}" width="355" height="212" viewBox="0 0 355 212" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M354.082 201.213L6.20624 0L0 11.4326L347.223 211.666L354.082 201.213Z"/>
                        <path d="M0.331909 201.548L348.208 0.334961L354.414 11.7675L7.19144 212L0.331909 201.548Z"/>
                    </svg>                                                                
                    <div class="block">
                        <div class="linecase {caseData.items[item.winBlock.ItemIndex].color}"></div>
                        <img src="{document.cloud + `img/roulette/${caseData.items[item.winBlock.ItemIndex].image}.png`}" alt="">
                        <span>{caseData.items[item.winBlock.ItemIndex].title}</span>
                    </div>
                </div>
                {/if}
            {/each}
            </div>
        </div>
    </div>
    <div class="opencasebtns1">                  
        <div class="usebtncase" on:keypress={() => {}} on:click={() => Take(0, -1)}>
            <p>ЗАБРАТЬ СЕБЕ</p>
        </div>
        {#if onAllWinBlockPrice(popupData)}
        <div class="sellbtncase" on:keypress={() => {}} on:click={() => Sell(0, -1)}>
            <p>ПРОДАТЬ ЗА</p>
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                <g filter="url(#filter0_d_712_2760)">
                <rect x="5" y="5" width="14" height="14" rx="7" fill="#CFF80B"/>
                </g>
                <path d="M8.43871 14.27C8.43871 14.19 8.49204 14.15 8.59871 14.15C8.70537 14.15 9.03537 14.2233 9.58871 14.37C9.71537 13.2767 10.072 12.09 10.6587 10.81C10.8654 10.3433 11.112 10.11 11.3987 10.11C11.452 10.11 11.502 10.1667 11.5487 10.28C11.602 10.3933 11.6287 10.5333 11.6287 10.7C11.6287 10.9 11.522 11.45 11.3087 12.35C11.102 13.25 10.9654 14.0333 10.8987 14.7C11.3387 14.8 11.7254 14.85 12.0587 14.85C12.3987 14.85 12.7087 14.7633 12.9887 14.59C13.2687 14.41 13.5087 14.18 13.7087 13.9C14.122 13.3067 14.3287 12.5933 14.3287 11.76C14.322 10.22 13.7087 9.45 12.4887 9.45C12.0554 9.45667 11.2887 9.65 10.1887 10.03C9.97537 10.1033 9.78871 10.14 9.62871 10.14C9.46871 10.14 9.38871 10.0933 9.38871 10C9.38871 9.87333 9.48204 9.72333 9.66871 9.55C9.85537 9.37667 10.0954 9.21 10.3887 9.05C11.1087 8.66333 11.8087 8.47 12.4887 8.47C13.422 8.47 14.1887 8.72 14.7887 9.22C15.4954 9.81333 15.8454 10.7133 15.8387 11.92C15.8387 12.4333 15.7587 12.94 15.5987 13.44C15.4387 13.94 15.1954 14.3867 14.8687 14.78C14.542 15.1667 14.1287 15.48 13.6287 15.72C13.1287 15.96 12.5387 16.08 11.8587 16.08C11.5854 16.08 11.2387 16.02 10.8187 15.9C10.772 16.0267 10.6554 16.09 10.4687 16.09C10.122 16.09 9.88537 16.0333 9.75871 15.92C9.63871 15.8 9.56871 15.6067 9.54871 15.34C9.22871 15.16 8.96204 14.9733 8.74871 14.78C8.54204 14.5867 8.43871 14.4167 8.43871 14.27Z" fill="#1E1E1E" fill-opacity="0.5"/>
                <defs>
                <filter id="filter0_d_712_2760" x="0" y="0" width="24" height="24" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                <feFlood flood-opacity="0" result="BackgroundImageFix"/>
                <feColorMatrix in="SourceAlpha" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 127 0" result="hardAlpha"/>
                <feOffset/>
                <feGaussianBlur stdDeviation="2.5"/>
                <feComposite in2="hardAlpha" operator="out"/>
                <feColorMatrix type="matrix" values="0 0 0 0 0.811765 0 0 0 0 0.972549 0 0 0 0 0.0431373 0 0 0 0.25 0"/>
                <feBlend mode="normal" in2="BackgroundImageFix" result="effect1_dropShadow_712_2760"/>
                <feBlend mode="normal" in="SourceGraphic" in2="effect1_dropShadow_712_2760" result="shape"/>
                </filter>
                </defs>
            </svg>                
            <p>{format("money", onAllWinBlockPrice(popupData))}</p>
        </div>
        {/if}
    </div>
{/if}