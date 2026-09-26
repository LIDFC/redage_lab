<script>
    // Мини-игра электрика «подключите кабели RJ45» — порт мини-игры Farko (Vue 3) на Svelte.
    // 1) клещами зачистить кабели, 2) надеть на них коннекторы, 3) воткнуть в порт того же цвета.
    // Сервер: Jobs/Electrician.cs, клиент: src_client/jobs/electricianGame.js
    import { executeClient } from 'api/rage'
    import { onMount, onDestroy } from 'svelte'
    import Connector from './connector.svelte'
    import Pliers from './pliers.svelte'
    import decoLeft from './images/img1.png'
    import decoRight from './images/img2.png'

    const COLORS = [
        { id: 'yellow', hex: '#f5c400' },
        { id: 'blue', hex: '#3a9fff' },
        { id: 'green', hex: '#3cd64a' },
        { id: 'red', hex: '#ff4040' },
        { id: 'orange', hex: '#ff8c00' },
    ];
    const COUNT = 5;

    const shuffle = (list) => [...list].sort(() => Math.random() - 0.5);

    const createState = () => ({
        connectors: Array.from({ length: COUNT }, (_, id) => ({ id, used: false })),
        cables: shuffle([...Array(COUNT).keys()]).map(colorIdx => ({ colorIdx, stripped: false, hasConnector: null, connected: false, portIdx: null, justStripped: false })),
        ports: shuffle([...Array(COUNT).keys()]).map(colorIdx => ({ colorIdx, connected: false })),
        phase: 'playing',
    });

    let state = createState();
    let drag = { active: false, type: null, payload: null, x: 0, y: 0 };
    let shaking = false;
    let scale = window.innerHeight / 1080;

    let panel, canvas;
    const wireEls = {};
    const portEls = {};

    // ---- звуки (Web Audio, как в оригинале) ----
    let audio = null;
    const tone = ({ type, freq, freqEnd, startOffset = 0, duration = 0.35, gainPeak = 0.2 }) => {
        try {
            if (!audio) audio = new (window.AudioContext || window.webkitAudioContext)();
            const t = audio.currentTime + startOffset;
            const osc = audio.createOscillator(), gain = audio.createGain();
            osc.connect(gain); gain.connect(audio.destination);
            osc.type = type;
            osc.frequency.setValueAtTime(freq, t);
            if (freqEnd !== undefined) osc.frequency.exponentialRampToValueAtTime(freqEnd, t + duration * 0.5);
            gain.gain.setValueAtTime(0, t);
            gain.gain.linearRampToValueAtTime(gainPeak * 0.6, t + 0.02);
            gain.gain.exponentialRampToValueAtTime(0.001, t + duration);
            osc.start(t); osc.stop(t + duration + 0.05);
        } catch (e) {}
    }
    const sounds = {
        connect: () => tone({ type: 'square', freq: 900, freqEnd: 300, duration: 0.1, gainPeak: 0.18 }),
        success: () => [523.25, 659.25, 783.99].forEach((f, i) => tone({ type: 'sine', freq: f, startOffset: i * 0.13, gainPeak: 0.25 })),
        fail: () => [220, 146.83].forEach((f, i) => tone({ type: 'sawtooth', freq: f, startOffset: i * 0.18, duration: 0.3, gainPeak: 0.3 })),
        snip: () => {
            tone({ type: 'square', freq: 1200, freqEnd: 180, duration: 0.07, gainPeak: 0.22 });
            tone({ type: 'square', freq: 900, freqEnd: 120, startOffset: 0.05, duration: 0.08, gainPeak: 0.14 });
        },
    };

    // ---- логика ----
    const stripCable = (i) => {
        const cable = state.cables[i];
        if (!cable || cable.stripped || cable.connected) return;
        cable.stripped = true;
        cable.justStripped = true;
        state = state;
        sounds.snip();
        setTimeout(() => { state.cables[i].justStripped = false; state = state; }, 400);
    }

    const attachConnector = (connId, i) => {
        const connector = state.connectors[connId], cable = state.cables[i];
        if (!connector || !cable || connector.used || !cable.stripped || cable.hasConnector !== null || cable.connected) return;
        connector.used = true;
        cable.hasConnector = connId;
        state = state;
    }

    const connectToPort = (i, portIdx) => {
        const cable = state.cables[i], port = state.ports[portIdx];
        if (!cable || !port || cable.connected || port.connected || cable.hasConnector === null) return false;
        if (cable.colorIdx !== port.colorIdx) return false;
        cable.connected = true;
        cable.portIdx = portIdx;
        port.connected = true;
        state = state;
        sounds.connect();
        if (state.cables.every(c => c.connected)) {
            setTimeout(() => {
                state.phase = 'success';
                sounds.success();
                setTimeout(() => executeClient("client.electrician.game.finished"), 1100);
            }, 250);
        }
        return true;
    }

    const triggerError = () => {
        if (state.phase !== 'playing') return;
        shaking = true;
        setTimeout(() => shaking = false, 300);
        sounds.fail();
        state.phase = 'error';
        setTimeout(() => state = createState(), 1400);
    }

    const hitIndex = (x, y, attr) => {
        for (const el of document.elementsFromPoint(x, y)) {
            const found = el.closest(`[data-${attr}]`);
            if (found) return parseInt(found.getAttribute(`data-${attr}`), 10);
        }
        return NaN;
    }

    const startDrag = (type, payload, e) => {
        if (state.phase !== 'playing' || e.button !== 0) return;
        e.preventDefault();
        drag = { active: true, type, payload, x: e.clientX, y: e.clientY };
    }

    const onMove = (e) => {
        if (!drag.active) return;
        drag = { ...drag, x: e.clientX, y: e.clientY };
        if (drag.type === 'pliers') {
            const i = hitIndex(e.clientX, e.clientY, 'cable-idx');
            if (!isNaN(i)) stripCable(i);
        }
    }

    const onUp = (e) => {
        if (!drag.active) return;
        const { type, payload } = drag;
        drag = { active: false, type: null, payload: null, x: 0, y: 0 };
        if (type === 'connector') {
            const i = hitIndex(e.clientX, e.clientY, 'cable-idx');
            if (!isNaN(i)) attachConnector(payload.connId, i);
        } else if (type === 'cable-connector') {
            const portIdx = hitIndex(e.clientX, e.clientY, 'port-idx');
            if (isNaN(portIdx)) return;
            if (!connectToPort(payload.cableIdx, portIdx)) triggerError();
        }
    }

    $: highlightedCables = new Set(drag.active ? state.cables
        .map((c, i) => ((drag.type === 'connector' && c.stripped && c.hasConnector === null && !c.connected) || (drag.type === 'pliers' && !c.stripped && !c.connected)) ? i : -1)
        .filter(i => i >= 0) : []);
    $: highlightedPorts = new Set(drag.active && drag.type === 'cable-connector' ? state.ports
        .map((p, i) => (!p.connected && p.colorIdx === state.cables[drag.payload.cableIdx].colorIdx) ? i : -1)
        .filter(i => i >= 0) : []);

    // ---- провода на canvas ----
    let raf = null;
    const bezier = (ctx, x1, y1, x2, y2, color) => {
        const midY = (y1 + y2) / 2;
        ctx.beginPath();
        ctx.moveTo(x1, y1);
        ctx.bezierCurveTo(x1, midY, x2, midY, x2, y2);
        ctx.strokeStyle = color;
        ctx.lineWidth = Math.max(3, 5 * scale);
        ctx.lineCap = 'round';
        ctx.shadowColor = color;
        ctx.shadowBlur = 10;
        ctx.stroke();
        ctx.shadowBlur = 0;
    }
    const render = () => {
        raf = requestAnimationFrame(render);
        if (!canvas || !panel) return;
        if (canvas.width !== panel.offsetWidth || canvas.height !== panel.offsetHeight) {
            canvas.width = panel.offsetWidth;
            canvas.height = panel.offsetHeight;
        }
        const ctx = canvas.getContext('2d');
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        const p = panel.getBoundingClientRect();
        const cableBottom = (i) => {
            const el = wireEls[i]; if (!el) return null;
            const r = el.getBoundingClientRect();
            return { x: r.left - p.left + r.width / 2, y: r.bottom - p.top };
        }
        const portPoint = (i) => {
            const el = portEls[i]; if (!el) return null;
            const r = el.getBoundingClientRect();
            return { x: r.left - p.left + r.width / 2, y: r.bottom - p.top + 6 * scale };
        }
        state.cables.forEach((cable, i) => {
            if (!cable.connected) return;
            const from = cableBottom(i), to = portPoint(cable.portIdx);
            if (from && to) bezier(ctx, from.x, from.y, to.x, to.y, COLORS[cable.colorIdx].hex);
        });
        if (drag.active && drag.type === 'cable-connector') {
            const from = cableBottom(drag.payload.cableIdx);
            if (from) bezier(ctx, from.x, from.y, drag.x - p.left, drag.y - p.top, COLORS[state.cables[drag.payload.cableIdx].colorIdx].hex);
        }
    }

    const onKey = (e) => {
        if (e.keyCode === 27) executeClient("client.electrician.game.exit");
    }
    const onResize = () => scale = window.innerHeight / 1080;

    onMount(() => {
        render();
        window.addEventListener('resize', onResize);
    });
    onDestroy(() => {
        cancelAnimationFrame(raf);
        window.removeEventListener('resize', onResize);
        try { audio && audio.close(); } catch (e) {}
    });
</script>

<svelte:window on:keyup={onKey} on:mousemove={onMove} on:mouseup={onUp} />

<div class="el">
    <img class="el__deco left" src={decoLeft} alt="" />
    <img class="el__deco right" src={decoRight} alt="" />

    <div class="el__title">
        <div class="el__caption">Стройка · работа электрика</div>
        <div class="el__heading">Подключите кабели к щитку</div>
        <div class="el__steps">
            <span class:done={state.cables.every(c => c.stripped)}>1. Зачистите клещами</span>
            <span class:done={state.cables.every(c => c.hasConnector !== null)}>2. Наденьте коннекторы</span>
            <span class:done={state.cables.every(c => c.connected)}>3. Воткните в порт того же цвета</span>
        </div>
    </div>

    <div class="el__layout">
        <div class="el__sidebar">
            <div class="el__label">Коннекторы</div>
            <div class="el__sublabel">Перетащите на кабели</div>
            <div class="el__pool">
                {#each state.connectors as conn (conn.id)}
                    <div class="el__pool-item" class:used={conn.used} on:mousedown={(e) => !conn.used && startDrag('connector', { connId: conn.id }, e)}>
                        <Connector scale={scale} uid="pool-{conn.id}" />
                    </div>
                {/each}
            </div>
            <div class="el__divider"></div>
            <div class="el__label">Инструмент</div>
            <div class="el__sublabel">Зачистить кабели</div>
            <div class="el__pliers" class:dragging={drag.active && drag.type === 'pliers'} on:mousedown={(e) => startDrag('pliers', null, e)}>
                <Pliers width={56 * scale} height={84 * scale} />
            </div>
        </div>

        <div class="el__panel" bind:this={panel}>
            {#each ['tl', 'tr', 'bl', 'br'] as corner}
                <div class="el__screw {corner}"></div>
            {/each}
            <canvas bind:this={canvas}></canvas>

            <div class="el__ports">
                {#each state.ports as port, i}
                    <div class="el__port" class:highlight={highlightedPorts.has(i)} data-port-idx={i}>
                        <div class="el__port-indicator" style="background: {COLORS[port.colorIdx].hex}; box-shadow: 0 0 {port.connected ? 16 : 8}px {port.connected ? 6 : 2}px {COLORS[port.colorIdx].hex}{port.connected ? 'cc' : '88'}"></div>
                        <div class="el__port-body" class:connected={port.connected} bind:this={portEls[i]}>
                            {#if port.connected}
                                <div class="el__port-connector"><Connector scale={0.9 * scale} uid="port-{i}" /></div>
                            {/if}
                        </div>
                    </div>
                {/each}
            </div>

            <div class="el__cables">
                {#each state.cables as cable, i}
                    <div class="el__cable"
                         class:stripped={cable.stripped}
                         class:has-connector={cable.hasConnector !== null}
                         class:just-stripped={cable.justStripped}
                         class:highlight={highlightedCables.has(i)}
                         data-cable-idx={i}>
                        {#if cable.hasConnector !== null && !cable.connected && !(drag.active && drag.type === 'cable-connector' && drag.payload.cableIdx === i)}
                            <div class="el__cable-connector" on:mousedown|stopPropagation={(e) => startDrag('cable-connector', { cableIdx: i }, e)}>
                                <Connector scale={scale} uid="cable-{i}" />
                            </div>
                        {/if}
                        <div class="el__wire"
                             bind:this={wireEls[i]}
                             style="background: linear-gradient(180deg, {COLORS[cable.colorIdx].hex} 0%, {COLORS[cable.colorIdx].hex}cc 100%); box-shadow: 0 0 6px 1px {COLORS[cable.colorIdx].hex}55; visibility: {cable.connected || (drag.active && drag.type === 'cable-connector' && drag.payload.cableIdx === i) ? 'hidden' : 'visible'}"></div>
                    </div>
                {/each}
            </div>

            {#if state.phase === 'success'}
                <div class="el__overlay success"><div class="el__overlay-icon">✓</div><div>Подключено!</div></div>
            {:else if state.phase === 'error'}
                <div class="el__overlay error"><div class="el__overlay-icon">✕</div><div>Не тот порт!</div></div>
            {/if}
        </div>
    </div>

    <div class="el__hints">
        <div><span>Тащить</span>Инструменты и кабели</div>
        <div><span>Esc</span>Бросить работу</div>
    </div>

    {#if drag.active && drag.type !== null}
        <div class="el__ghost" class:shake={shaking} style="left: {drag.x}px; top: {drag.y}px">
            {#if drag.type === 'pliers'}
                <Pliers width={52 * scale} height={78 * scale} />
            {:else}
                <Connector scale={scale} uid="ghost" />
            {/if}
        </div>
    {/if}
</div>

<style>
    .el {
        position: absolute;
        inset: 0;
        z-index: 1000;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        font-family: 'TTNorms-Regular';
        color: white;
        user-select: none;
        overflow: hidden;
    }
    .el__deco {
        position: absolute;
        pointer-events: none;
        opacity: 0.9;
    }
    .el__deco.left {
        bottom: -7vh;
        left: -32vh;
        width: 92vh;
        transform: rotate(230deg);
    }
    .el__deco.right {
        top: -2vh;
        right: -37vh;
        width: 83vh;
        transform: rotate(70deg);
    }
    .el__title {
        position: relative;
        text-align: center;
        text-shadow: 0 0.2vh 0.8vh rgba(0, 0, 0, 0.9);
        margin-bottom: 2.6vh;
    }
    .el__caption {
        text-transform: uppercase;
        letter-spacing: 0.15em;
        font-size: 1.3vh;
        color: #f5c400;
    }
    .el__heading {
        font-family: 'TTNorms-Bold';
        font-size: 3vh;
        margin: 0.4vh 0 1vh;
    }
    .el__steps {
        display: flex;
        gap: 1vh;
        justify-content: center;
    }
    .el__steps span {
        padding: 0.5vh 1.2vh;
        border-radius: 2vh;
        background: rgba(0, 0, 0, 0.6);
        font-size: 1.3vh;
        color: rgba(255, 255, 255, 0.8);
    }
    .el__steps span.done {
        background: rgba(20, 90, 30, 0.75);
        color: #6ef07a;
    }
    .el__layout {
        position: relative;
        display: flex;
        align-items: flex-start;
    }
    .el__sidebar {
        display: flex;
        flex-direction: column;
        align-items: center;
        padding: 1.3vh 1.1vh;
        min-width: 10.7vh;
        margin-top: 2.6vh;
        background: rgba(0, 0, 0, 0.55);
        border: 1px solid rgba(255, 255, 255, 0.07);
        border-right: none;
        border-radius: 0.9vh 0 0 0.9vh;
    }
    .el__label {
        font-family: 'TTNorms-Bold';
        font-size: 1vh;
        letter-spacing: 0.25em;
        text-transform: uppercase;
    }
    .el__sublabel {
        font-size: 1vh;
        color: rgba(255, 255, 255, 0.35);
        margin: 0.2vh 0 1.3vh;
    }
    .el__pool {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 1.1vh;
        justify-items: center;
    }
    .el__pool-item, .el__pliers {
        cursor: grab;
        filter: drop-shadow(0 0.3vh 0.6vh rgba(0, 0, 0, 0.7));
        transition: transform 0.12s, opacity 0.15s;
    }
    .el__pool-item:hover, .el__pliers:hover {
        transform: translateY(-0.2vh) scale(1.06);
    }
    .el__pool-item.used, .el__pliers.dragging {
        opacity: 0;
        pointer-events: none;
    }
    .el__divider {
        width: 80%;
        height: 1px;
        background: rgba(255, 255, 255, 0.1);
        margin: 1.3vh 0;
    }
    .el__panel {
        position: relative;
        width: 53.7vh;
        height: 54.6vh;
        background: #0a0a0c;
        border-radius: 0 0.9vh 0.9vh 0;
        border: 1px solid rgba(255, 255, 255, 0.1);
        box-shadow: 0 0.8vh 4vh rgba(0, 0, 0, 0.9), inset 0 1px 0 rgba(255, 255, 255, 0.06);
        overflow: hidden;
        background-image: linear-gradient(rgba(255, 255, 255, 0.03) 1px, transparent 1px), linear-gradient(90deg, rgba(255, 255, 255, 0.03) 1px, transparent 1px);
        background-size: 5.4vh 5.4vh;
    }
    .el__panel canvas {
        position: absolute;
        inset: 0;
        width: 100%;
        height: 100%;
        pointer-events: none;
        z-index: 3;
    }
    .el__screw {
        position: absolute;
        width: 1.5vh;
        height: 1.5vh;
        border-radius: 50%;
        background: radial-gradient(circle at 38% 32%, #666, #1e1e1e);
        border: 1px solid #3a3a3a;
        z-index: 10;
    }
    .el__screw.tl { top: 0.9vh; left: 0.9vh; }
    .el__screw.tr { top: 0.9vh; right: 0.9vh; }
    .el__screw.bl { bottom: 0.9vh; left: 0.9vh; }
    .el__screw.br { bottom: 0.9vh; right: 0.9vh; }
    .el__ports, .el__cables {
        position: absolute;
        left: 0;
        right: 0;
        display: flex;
        justify-content: space-evenly;
        padding: 0 2.8vh;
        z-index: 5;
    }
    .el__ports {
        top: 0;
    }
    .el__cables {
        bottom: 0;
        align-items: flex-end;
    }
    .el__port, .el__cable {
        width: 7vh;
        display: flex;
        flex-direction: column;
        align-items: center;
        position: relative;
    }
    .el__port-indicator {
        width: 4.44vh;
        height: 0.56vh;
        border-radius: 0.3vh 0.3vh 0 0;
    }
    .el__port-body {
        width: 4.44vh;
        height: 4.07vh;
        background: linear-gradient(180deg, #141418 0%, #0a0a0c 100%);
        border: 1px solid rgba(255, 255, 255, 0.1);
        border-top: none;
        border-radius: 0 0 0.55vh 0.55vh;
        position: relative;
        box-shadow: inset 0 0.4vh 0.9vh rgba(0, 0, 0, 0.8);
    }
    .el__port-body.connected {
        background: transparent;
        box-shadow: none;
    }
    .el__port.highlight .el__port-body {
        border-color: rgba(255, 255, 255, 0.5);
        box-shadow: inset 0 0 1.3vh rgba(255, 255, 255, 0.12), 0 0 0.9vh rgba(255, 255, 255, 0.15);
    }
    .el__port-connector {
        position: absolute;
        bottom: 0;
        left: 50%;
        transform: translateX(-50%);
    }
    .el__wire {
        width: 0.65vh;
        height: 13vh;
        border-radius: 0.33vh;
        position: relative;
    }
    .el__cable.stripped .el__wire {
        border-radius: 0.33vh 0.33vh 0 0;
    }
    .el__cable.stripped:not(.has-connector) .el__wire::after {
        content: '';
        position: absolute;
        top: -0.9vh;
        left: 0.09vh;
        width: 0.46vh;
        height: 1.1vh;
        background: repeating-linear-gradient(90deg, #c8a24a 0, #c8a24a 1px, #e6c060 1px, #e6c060 2px);
        border-radius: 1px;
    }
    .el__cable.highlight .el__wire {
        box-shadow: 0 0 1.3vh 0.4vh rgba(255, 255, 255, 0.3) !important;
    }
    .el__cable.just-stripped .el__wire {
        animation: el-flash 0.35s ease-out;
    }
    .el__cable-connector {
        position: absolute;
        top: -4.8vh;
        left: 50%;
        transform: translateX(-50%);
        cursor: grab;
        z-index: 6;
    }
    .el__overlay {
        position: absolute;
        inset: 0;
        z-index: 20;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        font-family: 'TTNorms-Bold';
        font-size: 2vh;
        letter-spacing: 0.3em;
        text-transform: uppercase;
        pointer-events: none;
        animation: el-pop 0.35s ease both;
    }
    .el__overlay-icon {
        font-size: 7vh;
        margin-bottom: 1vh;
    }
    .el__overlay.success {
        background: rgba(0, 255, 100, 0.08);
        color: #00ff64;
        text-shadow: 0 0 2.6vh rgba(0, 255, 100, 0.7);
    }
    .el__overlay.error {
        background: rgba(255, 50, 50, 0.1);
        color: #ff4d4d;
        text-shadow: 0 0 2.6vh rgba(255, 50, 50, 0.7);
    }
    .el__hints {
        position: relative;
        display: flex;
        gap: 1.4vh;
        margin-top: 2.6vh;
    }
    .el__hints div {
        display: flex;
        align-items: center;
        gap: 0.8vh;
        padding: 0.6vh 1.6vh 0.6vh 0.6vh;
        background: rgba(0, 0, 0, 0.55);
        border: 1px solid rgba(255, 255, 255, 0.08);
        border-radius: 2vh;
        font-size: 1.3vh;
        color: rgba(255, 255, 255, 0.6);
    }
    .el__hints span {
        padding: 0.4vh 1vh;
        border-radius: 0.5vh;
        background: #1e1e24;
        border: 1px solid rgba(255, 255, 255, 0.15);
        color: white;
        font-family: 'TTNorms-Bold';
        text-transform: uppercase;
        font-size: 1.1vh;
    }
    .el__ghost {
        position: fixed;
        pointer-events: none;
        z-index: 9999;
        transform: translate(-50%, -50%);
        filter: drop-shadow(0 0.4vh 1.1vh rgba(0, 0, 0, 0.6));
    }
    .el__ghost.shake {
        animation: el-shake 0.3s ease;
    }
    @keyframes el-flash {
        0% { filter: brightness(1); }
        25% { filter: brightness(2.5) saturate(0.5); }
        100% { filter: brightness(1); }
    }
    @keyframes el-pop {
        from { opacity: 0; transform: scale(0.9); }
        to { opacity: 1; transform: scale(1); }
    }
    @keyframes el-shake {
        0%, 100% { transform: translate(-50%, -50%); }
        25% { transform: translate(calc(-50% - 1.1vh), -50%); }
        75% { transform: translate(calc(-50% + 1.1vh), -50%); }
    }
</style>
