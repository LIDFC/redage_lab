<script>
    import { formatThousands } from "../../modules/money";

    export let value;
    export let update;
    export let prefix;

    export let min;
    export let max;

    export let minLabel;
    export let maxLabel;

    export let rightText = null;

    let values = [value],
        inputValue = `${values[0]} ${prefix}`;
        
    $: inputValue = `${formatThousands(Number(values[0]))} ${prefix}`;
    $: if (values[0] != value) {
        update(values[0])
    }
    
    $: percent = max > min ? Math.min(100, Math.max(0, (Number(values[0]) - min) / (max - min) * 100)) : 0;

    function handleInput(event) {
        event.preventDefault();
        let val = event.target.value;
        
        if (event.key == "Backspace")
            val = values[0].toString().slice(0, -1);
        else if (!isNaN(event.key))
            val = values[0].toString() + event.key;
        else if (event.key == "ArrowLeft")
            val = values[0] - 1;
        else if (event.key == "ArrowRight")
            val = values[0] + 1;

        let formattedValue = val.toString().replace(/\D/g, '');
        if (Number(formattedValue) < min || formattedValue == '')
            formattedValue = `${min}`;

        values[0] = Number(formattedValue);
        update(values[0]);
    }
</script>

<div class="market-create__slider">
    <div class="slider__label">
        <span>{ minLabel }</span>
        <span class="slider__label-right">{ maxLabel }</span>
    </div>
    <div class="slider__content">
        <div class="market-input">
            <input on:keydown={handleInput} bind:value={inputValue} />
            <div class="market-input__additional-info">{ rightText || `${max} ${prefix}` }</div>
            <div class="market-input__slider">
                <!-- Встроенный range вместо пакета svelte-range-slider-pips (без лишней зависимости) -->
                <div class="rangeSlider">
                    <div class="rangeBar" style="width: {percent}%"></div>
                    <input class="native-range" type="range" min={min} max={max} step="1"
                        value={values[0]} on:input={(e) => values[0] = Number(e.target.value)} />
                </div>
            </div>
            <div class="slider-background"></div>
        </div>
    </div>
</div>
<style>
    .rangeSlider {
        position: relative;
    }
    .rangeBar {
        position: absolute;
        left: 0;
        top: 0;
    }
    .native-range {
        position: absolute;
        left: 0;
        top: 50%;
        width: 100%;
        height: 1.85185vh;
        margin: 0;
        transform: translateY(-50%);
        background: transparent;
        -webkit-appearance: none;
        cursor: pointer;
    }
    .native-range::-webkit-slider-runnable-track {
        background: transparent;
        height: 100%;
    }
    .native-range::-webkit-slider-thumb {
        -webkit-appearance: none;
        width: 1.85185vh;
        height: 1.85185vh;
        border-radius: 50%;
        background-color: rgb(255, 255, 255);
        border: 0.185185vh solid rgb(232, 232, 232);
    }
</style>
