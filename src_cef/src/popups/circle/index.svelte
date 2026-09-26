<script>
    import  './assets/css/iconscircle.css';
    import  './assets/css/circle.sass';
    import { executeClient } from 'api/rage'
    import keys from 'store/keys'
    export let popupData;


    $: if (popupData && typeof popupData === "string") 
        popupData = JSON.parse (popupData)
    

    const updateCategory = (json) => {
        popupData = JSON.parse (json)
    }
    window.events.addEvent("cef.circle.updateCategory", updateCategory);

    import { onDestroy } from 'svelte'

    onDestroy(() => {
        window.events.removeEvent("cef.circle.updateCategory", updateCategory);
    });


    // Иконки в шрифте называются circle-c-<действие> (assets/css/iconscircle.css).
    // Раньше префикс был "circle-", и в меню не показывалась ни одна иконка.
    const    
        prefix = "circle-c-";

    // Для действий, у которых нет своей иконки в шрифте, — ближайшая по смыслу
    const iconFallback = {
        inv: "carinv", phone: "offer", anim: "handshake", paired_animations: "handshake", battlepass: "badge", donate: "givemoney",
        fraction_table: "fraction", fraction_news: "fraction", fraction_mayormenu: "fraction", org_table: "family",
        vmuted: "mute", whisper: "offer",
        embrace: "handshake", kiss: "handshake", paired_five: "handshake", paired_slap: "handshake",
        carry_0: "handshake", carry_1: "handshake", carry_2: "handshake", carry_3: "leadaway",
        trunkAction: "trunk", healMenu: "heal",
        epinephrine: "heal", ticketveh: "ticket", newnumber: "sellcar", pocket: "rob",
        leave_fraction: "acancel", leave_org: "acancel",
    };

    const getIcon = (func) => {
        if (iconFallback [func])
            return iconFallback [func];
        if (/lift_/.test(func))
            return "house";
        return func;
    }

    // Кольцо с сектором, повёрнутым к курсору. Раньше его рисовал клиент спрайтом из
    // redage_textures_001.ytd; если словарь текстур не был загружен, GTA рисовала белый прямоугольник.
    let circleNode;
    let pointerAngle = -90;
    let isBackHover = false;

    const handleMouseMove = (event) => {
        if (!circleNode)
            return;
        const rect = circleNode.getBoundingClientRect();
        const cx = rect.left + rect.width / 2;
        const cy = rect.top + rect.height / 2;
        pointerAngle = Math.atan2(event.clientY - cy, event.clientX - cx) * 180 / Math.PI;
    }


    let drawname = "Назад"
    const OnHovered = (name, isBack = false) => {
        drawname = name;
        isBackHover = isBack;
        executeClient ("client.circle.isBack", isBack);
    }

    const onCircleClick = (func, index = 0) => {
        executeClient ("client.circle.select", func, index);
    }
    

    const ontest = (index, max) => {
        switch (max) {
            case 1:
                return 1;
            case 2:
                switch (index) {
                    case 0:
                        return 1;
                    case 1:
                        return 5;
                }
                return;
            case 3:
                switch (index) {
                    case 0:
                        return 1;
                    case 1:
                        return 3;
                    case 2:
                        return 5;
                }
                return;
            case 4:
                switch (index) {
                    case 0:
                        return 1;
                    case 1:
                        return 3;
                    case 2:
                        return 5;
                    case 3:
                        return 7;
                }
                return;
            case 5:
                switch (index) {
                    case 0:
                        return 1;
                    case 1:
                        return 2;
                    case 2:
                        return 4;
                    case 3:
                        return 6;
                    case 4:
                        return 8;
                }
                return;
            case 6:
                switch (index) {
                    case 0:
                        return 1;
                    case 1:
                        return 2;
                    case 2:
                        return 4;
                    case 3:
                        return 5;
                    case 4:
                        return 6;
                    case 5:
                        return 8;
                }
                return;
        
        }
        return index + 1;
    }

    const defaultCircle__closeWidth = 280;
    const defaultCircle__closeHeight = 280;

    const initCircle = (node) => {
        node = node.getBoundingClientRect();
        if (node) {
            const percentWidth = (node.width * 100 / defaultCircle__closeWidth) / 100;
            const percentHeight = (node.height * 100 / defaultCircle__closeHeight) / 100;
            executeClient ("client.circle.initCircle", percentWidth, percentHeight);
        }
    }



    const handleKeyUp = (event) => {
        const { keyCode } = event;

        for(let i = 0; i < 8; i++) {
            if (49 + i == keyCode) {
                onCircleClick (popupData [i].func, popupData [i].index)
                return;
            }
        }
    }


    const handleKeyDown = (event) => {
        const { keyCode } = event;

        if (keyCode === $keys[31])
            return onCircleClick ("back");
    }
    
    const handleMouseUp = (event) => {
        const { which } = event;

        if (which === 3)
            onCircleClick ("back");
    }
</script>

<svelte:window on:keydown={handleKeyDown} on:keyup={handleKeyUp} on:mouseup={handleMouseUp} on:mousemove={handleMouseMove} />

<div class="circle">
    <svg class="circle__ring" viewBox="0 0 280 280" aria-hidden="true">
        <circle cx="140" cy="140" r="118" class="circle__ring-bg" />
        <circle cx="140" cy="140" r="132" class="circle__ring-track" />
        {#if isBackHover}
            <circle cx="140" cy="140" r="132" class="circle__ring-back" />
        {:else}
            <g transform="rotate({pointerAngle} 140 140)">
                <path class="circle__ring-arc" d="M {140 + 132 * Math.cos(-0.45)} {140 + 132 * Math.sin(-0.45)} A 132 132 0 0 1 {140 + 132 * Math.cos(0.45)} {140 + 132 * Math.sin(0.45)}" />
            </g>
        {/if}
    </svg>
    <div class="circle__close" bind:this={circleNode} use:initCircle on:mouseenter={() => OnHovered ('Назад', true)} on:mouseleave={() => OnHovered ('Назад')} on:click={() => onCircleClick ("back")}>
        <div class="box-column">
            <div class="circle__image" class:active={drawname !== "Назад"}></div>
            <div class="circle__text">{drawname}</div>
        </div>
    </div>
    <div class="center">
        {#each popupData as data, index}
        <li on:click={() => onCircleClick (data.func, data.index)} on:mouseenter={() => OnHovered (data.name)} on:mouseleave={() => OnHovered ("Назад")} class="contents child{ontest (index, popupData.length)}">
            <span class="icons-circle {prefix}{getIcon (data.func)}" />
            <div>{data.name}</div>
            <div class="contents__index">{index + 1}</div>
        </li>
    {/each}
    </div>
</div>
<style>
    .circle__ring {
        position: absolute;
        top: 50%;
        left: 50%;
        width: 58.3%;
        height: 58.3%;
        transform: translate(-50%, -50%);
        pointer-events: none;
        overflow: visible;
    }
    .circle__ring-bg {
        fill: rgba(20, 24, 30, 0.82);
    }
    .circle__ring-track {
        fill: none;
        stroke: rgba(255, 255, 255, 0.12);
        stroke-width: 6;
    }
    .circle__ring-arc {
        fill: none;
        stroke: #DA2640;
        stroke-width: 6;
        stroke-linecap: round;
        filter: drop-shadow(0 0 6px rgba(218, 38, 64, 0.6));
    }
    .circle__ring-back {
        fill: none;
        stroke: rgba(218, 38, 64, 0.55);
        stroke-width: 6;
    }
</style>
