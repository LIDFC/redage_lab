<script>
    import { houseType } from 'json/realEstate.js'
    import { format } from 'api/formatter'
    export let selectData;
</script>

<div class="rielt__rielt_info">
    <div class="houseicon-house rielt__rielt_house-icon"></div>
    <div class="rielt__rielt_header">Квартира №{selectData.number}</div>
    <div class="rielt__gray">{selectData.buildingName}, {selectData.floor} этаж</div>
    <div class="rielt__rielt_stat">
        <div class="rielt__rielt_element">
            <div class="rielt__gray">Класс:</div>
            <div>{houseType[selectData.type]}</div>
        </div>
        <div class="rielt__rielt_element">
            <div class="rielt__gray">Гаражных мест:</div>
            <div>{selectData.cars}</div>
        </div>
        <div class="rielt__rielt_element">
            <div class="rielt__gray">Сожителей:</div>
            <div>до {selectData.maxRoommates}</div>
        </div>
        <div class="rielt__rielt_element">
            <div class="rielt__gray">Налог:</div>
            <div>${format("money", selectData.tax)}</div>
        </div>
    </div>
    <div class="box-column">
        <div class="rielt__rielt_price">${format("money", selectData.price)}</div>
        <div class="rielt__gray">Цена квартиры — покупка сразу, без поездки к дому</div>
    </div>
</div>
