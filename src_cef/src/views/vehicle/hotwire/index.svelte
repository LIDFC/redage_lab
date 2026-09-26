<script>
    // Мини-игра «соедините провода» — порт github.com/NikaKondr/hotwire (MIT, автор Nika Kondr) с React на Svelte.
    // Открывается при ремонте машины без ключа (сервер: Core/VehicleRepair.cs, клиент: src_client/vehicle/hotwire.js).
    import { executeClient } from 'api/rage'
    import { onMount, onDestroy, tick } from 'svelte'
    import engineImage from './images/engine.png'
    import panelImage from './images/background.png'
    import pinImage from './images/pin.png'
    import pinEffectImage from './images/pineffect.png'

    const COLORS = {
        1: ['#7E9570', '#2C6907'],
        2: ['#1FFF1B', '#138511'],
        3: ['#FFF732', '#A6A00A'],
        4: ['#FF440A', '#A03C1D'],
        5: ['#002343', '#6CB9FF'],
        6: ['#18005B', '#7747FF'],
        7: ['#FF3636', '#881616'],
    };
    const IDS = Object.keys(COLORS).map(Number);

    const shuffle = (list) => [...list].sort(() => Math.random() - 0.5);

    let leftLine = shuffle(IDS);
    let rightLine = shuffle(IDS);
    let connected = {};     // id → { curveX, curveY }
    let dragging = null;    // { id, curveX, curveY, x, y }
    let wrongId = null;
    let finished = false;

    let field;
    let canvas;
    const leftEls = {};
    const rightEls = {};

    const random = (min, max) => Math.random() * (max - min) + min;

    // Точка выхода провода слева и точка входа справа — считаются по DOM, поэтому не зависят от разрешения
    const leftPoint = (id) => {
        const el = leftEls[id];
        if (!el || !field) return null;
        const r = el.getBoundingClientRect(), f = field.getBoundingClientRect();
        return { x: r.right - f.left - 1, y: r.top - f.top + r.height / 2 };
    }
    const rightPoint = (id) => {
        const el = rightEls[id];
        if (!el || !field) return null;
        const r = el.getBoundingClientRect(), f = field.getBoundingClientRect();
        return { x: r.left - f.left + 1, y: r.top - f.top + r.height / 2 };
    }

    const controlPoints = (start, end, curveX, curveY) => {
        const midX = (start.x + end.x) / 2;
        const midY = (start.y + end.y) / 2;
        const a = { x: midX + (end.y - start.y) * curveX, y: midY - (end.x - start.x) * curveY };
        const b = { x: midX - (end.y - start.y) * curveX, y: midY + (end.x - start.x) * curveY };
        return end.y > start.y ? [a, b] : [b, a];
    }

    const drawWire = (ctx, start, end, curveX, curveY, color) => {
        const [c1, c2] = controlPoints(start, end, curveX, curveY);
        ctx.beginPath();
        ctx.moveTo(start.x, start.y);
        ctx.bezierCurveTo(c1.x, c1.y, c2.x, c2.y, end.x, end.y);
        ctx.strokeStyle = color;
        ctx.lineWidth = Math.max(3, Math.round(window.innerHeight / 216));
        ctx.lineCap = "round";
        ctx.stroke();
    }

    const redraw = () => {
        if (!canvas || !field) return;
        const rect = field.getBoundingClientRect();
        if (canvas.width !== Math.round(rect.width) || canvas.height !== Math.round(rect.height)) {
            canvas.width = Math.round(rect.width);
            canvas.height = Math.round(rect.height);
        }
        const ctx = canvas.getContext("2d");
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        for (const id in connected) {
            const start = leftPoint(id), end = rightPoint(id);
            if (start && end)
                drawWire(ctx, start, end, connected[id].curveX, connected[id].curveY, COLORS[id][1]);
        }
        if (dragging) {
            const start = leftPoint(dragging.id);
            if (start)
                drawWire(ctx, start, { x: dragging.x, y: dragging.y }, dragging.curveX, dragging.curveY, '#9a9a9a');
        }
    }

    const startDrag = (id, event) => {
        if (finished || event.button !== 0) return;
        const copy = { ...connected };
        delete copy[id];
        connected = copy;
        const f = field.getBoundingClientRect();
        dragging = { id, curveX: random(0.15, 0.85), curveY: random(0.05, 0.55), x: event.clientX - f.left, y: event.clientY - f.top };
        redraw();
    }

    const onMove = (event) => {
        if (!dragging || !field) return;
        const f = field.getBoundingClientRect();
        dragging = { ...dragging, x: event.clientX - f.left, y: event.clientY - f.top };
        redraw();
    }

    const dropOn = (id) => {
        if (!dragging) return;
        if (dragging.id === id) {
            connected = { ...connected, [id]: { curveX: dragging.curveX, curveY: dragging.curveY } };
        } else {
            wrongId = id;
            setTimeout(() => wrongId = null, 400);
        }
        dragging = null;
        redraw();
    }

    // Отпустили мышь не над правым контактом — провод «отпружинивает»
    const onUp = () => {
        if (!dragging) return;
        setTimeout(() => {
            if (dragging) {
                dragging = null;
                redraw();
            }
        }, 0);
    }

    const cancel = (event) => {
        event.preventDefault();
        dragging = null;
        redraw();
    }

    $: if (!finished && Object.keys(connected).length === IDS.length) {
        finished = true;
        setTimeout(() => executeClient("client.hotwire.finished"), 900);
    }

    const onKey = (event) => {
        if (event.keyCode === 27)
            executeClient("client.hotwire.exit");
    }

    const onResize = () => redraw();

    onMount(async () => {
        await tick();
        redraw();
        window.addEventListener("resize", onResize);
    });
    onDestroy(() => window.removeEventListener("resize", onResize));
</script>

<svelte:window on:keyup={onKey} on:mouseup={onUp} on:mousemove={onMove} />

<div class="hw">
    <div class="hw__title">
        <div class="hw__caption">Ремонт без ключа</div>
        <div class="hw__heading">{finished ? "Проводка восстановлена!" : "Соедините провода одного цвета"}</div>
    </div>

    <div class="hw__game" style="background-image: url({panelImage})">
        <div class="hw__field" bind:this={field} style="background-image: url({engineImage})" on:contextmenu={cancel}>
            <canvas bind:this={canvas}></canvas>
            <div class="hw__line">
                {#each leftLine as id}
                    <div class="hw__box">
                        <div class="hw__pin" style="background-image: url({pinImage})"></div>
                        <div class="hw__wire"
                             class:done={connected[id]}
                             bind:this={leftEls[id]}
                             style="--color1: {COLORS[id][0]}; --color2: {COLORS[id][1]}; --effect: url({pinEffectImage})"
                             on:mousedown={(e) => startDrag(id, e)}></div>
                    </div>
                {/each}
            </div>
            <div class="hw__line right">
                {#each rightLine as id}
                    <div class="hw__box" on:mouseup={() => dropOn(id)}>
                        <div class="hw__pin" style="background-image: url({pinImage})"></div>
                        <div class="hw__wire"
                             class:done={connected[id]}
                             class:wrong={wrongId === id}
                             bind:this={rightEls[id]}
                             style="--color1: {COLORS[id][0]}; --color2: {COLORS[id][1]}; --effect: url({pinEffectImage})"></div>
                    </div>
                {/each}
            </div>
        </div>
    </div>

    <div class="hw__progress">{Object.keys(connected).length} / {IDS.length}</div>

    <div class="hw__help">
        <div class="hw__hint"><span>ЛКМ</span>Тянуть провод</div>
        <div class="hw__hint"><span>ПКМ</span>Отменить</div>
        <div class="hw__hint"><span>Esc</span>Прекратить ремонт</div>
    </div>
</div>

<style>
    .hw {
        position: absolute;
        inset: 0;
        z-index: 1000;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        background-size: cover;
        background-position: center;
        font-family: 'TTNorms-Regular';
        color: white;
        user-select: none;
        animation: hw-show 0.5s ease forwards;
    }
    .hw > * {
        position: relative;
    }
    .hw__title {
        text-align: center;
        text-shadow: 0 0.2vh 0.8vh rgba(0, 0, 0, 0.9);
        margin-bottom: 2.4vh;
    }
    .hw__caption {
        text-transform: uppercase;
        letter-spacing: 0.15em;
        font-size: 1.3vh;
        color: #2BB6A8;
    }
    .hw__heading {
        font-family: 'TTNorms-Bold';
        font-size: 3vh;
        margin-top: 0.4vh;
    }
    .hw__game {
        width: 70.56vh;
        height: 48.61vh;
        background-size: cover;
        background-repeat: no-repeat;
        filter: drop-shadow(0.28vh 0.46vh 1.85vh #2B2B2B);
    }
    .hw__field {
        position: absolute;
        left: 15.09vh;
        top: 5vh;
        width: 35.83vh;
        height: 38.61vh;
        background-size: cover;
        background-repeat: no-repeat;
        display: flex;
        align-items: center;
        justify-content: space-between;
    }
    .hw__field canvas {
        position: absolute;
        inset: 0;
        width: 100%;
        height: 100%;
        pointer-events: none;
    }
    .hw__line {
        display: flex;
        flex-direction: column;
    }
    .hw__line.right .hw__box {
        transform: rotate(180deg);
    }
    .hw__box {
        margin: 0.56vh 0;
        display: flex;
        align-items: center;
        z-index: 2;
    }
    .hw__pin {
        width: 3.43vh;
        height: 3.24vh;
        background-size: cover;
    }
    .hw__wire {
        position: relative;
        width: 2.31vh;
        height: 1.39vh;
        border-radius: 0.19vh;
        background: linear-gradient(180deg, var(--color1) 0%, var(--color2) 100%);
        cursor: pointer;
        transition: opacity 0.2s, transform 0.2s;
    }
    .hw__wire::after {
        content: "";
        position: absolute;
        inset: 0;
        background: var(--effect) no-repeat;
        background-size: cover;
        pointer-events: none;
    }
    .hw__wire:hover {
        opacity: 0.7;
    }
    .hw__wire.done {
        box-shadow: 0 0 0.9vh var(--color1);
    }
    .hw__wire.wrong {
        animation: hw-shake 0.35s;
        box-shadow: 0 0 1vh #ff3b3b;
    }
    .hw__progress {
        margin-top: 2vh;
        text-shadow: 0 0.2vh 0.8vh rgba(0, 0, 0, 0.9);
        font-family: 'TTNorms-Bold';
        font-size: 2.2vh;
        color: rgba(255, 255, 255, 0.8);
    }
    .hw__help {
        display: flex;
        gap: 1.48vh;
        margin-top: 3vh;
    }
    .hw__hint {
        display: flex;
        align-items: center;
        gap: 0.93vh;
        padding: 1vh 2.6vh 1vh 1vh;
        background: white;
        color: black;
        border-radius: 4.6vh;
        font-size: 1.6vh;
    }
    .hw__hint span {
        padding: 0.9vh 1.1vh;
        background: black;
        color: white;
        border-radius: 0.93vh;
        font-family: 'TTNorms-Bold';
        font-size: 1.4vh;
    }
    @keyframes hw-show {
        from { opacity: 0; }
        to { opacity: 1; }
    }
    @keyframes hw-shake {
        0%, 100% { transform: translateX(0); }
        25% { transform: translateX(-0.4vh); }
        75% { transform: translateX(0.4vh); }
    }
</style>
