<script>
    import { translateText } from 'lang'
    import { daysName, getPngUrl, getTypeToTitle, statusType, getTimeFromMins } from './data'
    import { itemsInfo } from 'json/itemsInfo.js'
    import { format } from 'api/formatter'
    export let item;

</script>

<div class="blockbonus">
    <div class="headbonus">
        {#if item.status === statusType.received}
            <svg width="21" height="21" viewBox="0 0 21 21" fill="none" xmlns="http://www.w3.org/2000/svg">
                <rect width="21" height="21" rx="2" fill="#E81C5B"/>
                <path d="M8.28372 15C7.99225 14.9999 7.71274 14.8948 7.50667 14.7078L5.30852 12.7132C5.10832 12.5251 4.99754 12.2732 5.00004 12.0117C5.00255 11.7502 5.11813 11.5001 5.32191 11.3152C5.52569 11.1303 5.80135 11.0254 6.08952 11.0231C6.3777 11.0209 6.65533 11.1214 6.86261 11.303L8.22162 12.5362L14.0434 6.37347C14.1356 6.26918 14.2499 6.18276 14.3796 6.11935C14.5092 6.05594 14.6517 6.01683 14.7983 6.00436C14.945 5.99188 15.0929 6.00628 15.2333 6.04671C15.3738 6.08714 15.5038 6.15277 15.6157 6.2397C15.7276 6.32663 15.8191 6.43309 15.8848 6.55275C15.9504 6.67241 15.9889 6.80282 15.9979 6.93624C16.007 7.06966 15.9863 7.20337 15.9373 7.32941C15.8882 7.45545 15.8117 7.57125 15.7124 7.66994L9.11791 14.6509C9.01567 14.7612 8.88792 14.8496 8.74374 14.9099C8.59955 14.9702 8.44249 15.001 8.28372 15Z" fill="white"/>
            </svg>
            {:else if item.status === statusType.receive} 
            <svg width="21" height="21" viewBox="0 0 21 21" fill="none" xmlns="http://www.w3.org/2000/svg">
                <rect width="21" height="21" rx="2" fill="#E81C5B"/>
                <path d="M8.28372 15C7.99225 14.9999 7.71274 14.8948 7.50667 14.7078L5.30852 12.7132C5.10832 12.5251 4.99754 12.2732 5.00004 12.0117C5.00255 11.7502 5.11813 11.5001 5.32191 11.3152C5.52569 11.1303 5.80135 11.0254 6.08952 11.0231C6.3777 11.0209 6.65533 11.1214 6.86261 11.303L8.22162 12.5362L14.0434 6.37347C14.1356 6.26918 14.2499 6.18276 14.3796 6.11935C14.5092 6.05594 14.6517 6.01683 14.7983 6.00436C14.945 5.99188 15.0929 6.00628 15.2333 6.04671C15.3738 6.08714 15.5038 6.15277 15.6157 6.2397C15.7276 6.32663 15.8191 6.43309 15.8848 6.55275C15.9504 6.67241 15.9889 6.80282 15.9979 6.93624C16.007 7.06966 15.9863 7.20337 15.9373 7.32941C15.8882 7.45545 15.8117 7.57125 15.7124 7.66994L9.11791 14.6509C9.01567 14.7612 8.88792 14.8496 8.74374 14.9099C8.59955 14.9702 8.44249 15.001 8.28372 15Z" fill="white"/>
            </svg>           
            {:else}
            <svg width="21" height="21" viewBox="0 0 21 21" fill="none" xmlns="http://www.w3.org/2000/svg">
                <rect width="21" height="21" rx="2" fill="#3C3C3B"/>
            </svg>
        {/if}                                    
        <h1>{translateText('player1', 'Пополнение на')} {format("money", item.maxDonate)} RB</h1>
        {#if item.status === statusType.receive}
            <span class="use" on:keypress={() => {}} on:click>Получить</span>
            {:else}
            <span>{format("money", item.donate)} RB</span>
        {/if}
    </div>
    <div class="linebonus"></div>
    <div class="rewradbonus">
        <div class="blockbrew">
            <div class="bgitem">
                <div class="lineitem"></div>
                <img src="{getPngUrl (item)}" alt=""/>
            </div>
            <div class="nameitem">
                <p>Предмет инвентаря</p>
                <b>{itemsInfo[item.itemId].Name}</b>
            </div>
        </div>
    </div>
</div>