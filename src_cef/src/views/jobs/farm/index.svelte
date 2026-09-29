<script>
    // Мини-игра фермы «посадите рассаду» (в стиле мини-игры электрика).
    // 1) лопаткой перенести землю из кучек в ямки с ростками — закопать; 2) лейкой полить каждый росток.
    // Сервер: Jobs/DayLabor/DayLabor.cs, клиент: src_client/jobs/daylabor.js
    import { executeClient } from 'api/rage'
    import { onMount, onDestroy } from 'svelte'

    const COUNT = 5;
    const SCOOPS = 3;          // сколько лопат земли нужно на ямку
    const WATER_TIME = 1300;   // мс полива одного ростка

    const createState = () => ({
        holes: Array.from({ length: COUNT }, (_, id) => ({ id, fill: 0, water: 0 })),
        mounds: Array.from({ length: COUNT }, () => SCOOPS),
        phase: 'dig',          // dig → water → success
    });

    let state = createState();
    let drag = { active: false, type: null, x: 0, y: 0 };
    let loaded = false;        // на лопатке есть земля
    let hoverHole = -1;
    let scale = window.innerHeight / 1080;

    $: buried = state.holes.every(h => h.fill >= SCOOPS);
    $: watered = state.holes.every(h => h.water >= 100);

    // ---- звуки ----
    let audio = null;
    const tone = ({ type, freq, freqEnd, startOffset = 0, duration = 0.3, gainPeak = 0.2 }) => {
        try {
            if (!audio) audio = new (window.AudioContext || window.webkitAudioContext)();
            const t = audio.currentTime + startOffset;
            const osc = audio.createOscillator(), gain = audio.createGain();
            osc.connect(gain); gain.connect(audio.destination);
            osc.type = type;
            osc.frequency.setValueAtTime(freq, t);
            if (freqEnd !== undefined) osc.frequency.exponentialRampToValueAtTime(freqEnd, t + duration * 0.6);
            gain.gain.setValueAtTime(0, t);
            gain.gain.linearRampToValueAtTime(gainPeak * 0.6, t + 0.02);
            gain.gain.exponentialRampToValueAtTime(0.001, t + duration);
            osc.start(t); osc.stop(t + duration + 0.05);
        } catch (e) {}
    }
    const sounds = {
        dig: () => tone({ type: 'triangle', freq: 180, freqEnd: 90, duration: 0.12, gainPeak: 0.25 }),
        plop: () => tone({ type: 'sine', freq: 140, freqEnd: 70, duration: 0.18, gainPeak: 0.3 }),
        done: () => tone({ type: 'sine', freq: 660, freqEnd: 880, duration: 0.2, gainPeak: 0.18 }),
        success: () => [523.25, 659.25, 783.99].forEach((f, i) => tone({ type: 'sine', freq: f, startOffset: i * 0.13, gainPeak: 0.25 })),
    };

    // ---- логика ----
    const hitIndex = (x, y, attr) => {
        for (const el of document.elementsFromPoint(x, y)) {
            const found = el.closest(`[data-${attr}]`);
            if (found) return parseInt(found.getAttribute(`data-${attr}`), 10);
        }
        return NaN;
    }

    const startDrag = (type, e) => {
        if (e.button !== 0) return;
        if (type === 'shovel' && state.phase !== 'dig') return;
        if (type === 'can' && state.phase !== 'water') return;
        e.preventDefault();
        drag = { active: true, type, x: e.clientX, y: e.clientY };
    }

    const onMove = (e) => {
        if (!drag.active) return;
        drag = { ...drag, x: e.clientX, y: e.clientY };
        if (drag.type === 'shovel') {
            if (!loaded) {
                const m = hitIndex(e.clientX, e.clientY, 'mound-idx');
                if (!isNaN(m) && state.mounds[m] > 0) {
                    state.mounds[m]--;
                    loaded = true;
                    state = state;
                    sounds.dig();
                }
            } else {
                const h = hitIndex(e.clientX, e.clientY, 'hole-idx');
                const hole = state.holes[h];
                if (hole && hole.fill < SCOOPS) {
                    hole.fill++;
                    loaded = false;
                    state = state;
                    sounds.plop();
                    if (state.holes.every(x => x.fill >= SCOOPS)) {
                        sounds.done();
                        drag = { active: false, type: null, x: 0, y: 0 };
                        setTimeout(() => { state.phase = 'water'; state = state; }, 400);
                    }
                }
            }
        } else if (drag.type === 'can') {
            const h = hitIndex(e.clientX, e.clientY, 'hole-idx');
            hoverHole = isNaN(h) ? -1 : h;
        }
    }

    const onUp = () => {
        if (!drag.active) return;
        drag = { active: false, type: null, x: 0, y: 0 };
        hoverHole = -1;
    }

    // Полив: пока лейка над ростком — растёт влажность
    let raf = null, last = 0;
    const tick = (now) => {
        raf = requestAnimationFrame(tick);
        const dt = last ? Math.min(100, now - last) : 0;
        last = now;
        if (state.phase !== 'water' || !drag.active || drag.type !== 'can' || hoverHole < 0) return;
        const hole = state.holes[hoverHole];
        if (!hole || hole.water >= 100) return;
        hole.water = Math.min(100, hole.water + dt / WATER_TIME * 100);
        if (hole.water >= 100) sounds.done();
        state = state;
        if (state.holes.every(x => x.water >= 100)) {
            drag = { active: false, type: null, x: 0, y: 0 };
            hoverHole = -1;
            state.phase = 'success';
            sounds.success();
            setTimeout(() => executeClient("client.daylabor.farm.finished"), 1200);
        }
    }

    const onKey = (e) => {
        if (e.keyCode === 27) executeClient("client.daylabor.farm.exit");
    }
    const onResize = () => scale = window.innerHeight / 1080;

    onMount(() => {
        raf = requestAnimationFrame(tick);
        window.addEventListener('resize', onResize);
    });
    onDestroy(() => {
        cancelAnimationFrame(raf);
        window.removeEventListener('resize', onResize);
        try { audio && audio.close(); } catch (e) {}
    });
</script>

<svelte:window on:keyup={onKey} on:mousemove={onMove} on:mouseup={onUp} />

<div class="fm">
    <div class="fm__title">
        <div class="fm__caption">Ферма · подработка</div>
        <div class="fm__heading">Посадите рассаду</div>
        <div class="fm__steps">
            <span class:active={state.phase === 'dig'} class:done={buried}>1. Закопайте ростки лопаткой</span>
            <span class:active={state.phase === 'water'} class:done={watered}>2. Полейте из лейки</span>
        </div>
    </div>

    <div class="fm__layout">
        <div class="fm__sidebar">
            <div class="fm__label">Инструменты</div>
            <div class="fm__sublabel">{state.phase === 'dig' ? 'Земля из кучки → в ямку' : 'Держите над ростком'}</div>
            <div class="fm__tool" class:disabled={state.phase !== 'dig'} class:dragging={drag.active && drag.type === 'shovel'}
                 on:mousedown={(e) => startDrag('shovel', e)} title="Лопатка">
                <svg viewBox="0 0 40 90" width={40 * scale} height={90 * scale}>
                    <rect x="17" y="2" width="6" height="40" rx="3" fill="#9b6a3c" />
                    <rect x="14" y="0" width="12" height="10" rx="4" fill="#3b3b3b" />
                    <path d="M20 40 C4 44 4 70 20 88 C36 70 36 44 20 40 Z" fill="#b8c2cc" stroke="#6d7780" stroke-width="2" />
                </svg>
            </div>
            <div class="fm__divider"></div>
            <div class="fm__tool" class:disabled={state.phase !== 'water'} class:dragging={drag.active && drag.type === 'can'}
                 on:mousedown={(e) => startDrag('can', e)} title="Лейка">
                <svg viewBox="0 0 90 70" width={80 * scale} height={62 * scale}>
                    <path d="M18 22 H58 V62 Q58 68 52 68 H24 Q18 68 18 62 Z" fill="#2f8f4e" stroke="#1c5d31" stroke-width="2" />
                    <path d="M58 30 L84 12 L88 16 L60 42 Z" fill="#2f8f4e" stroke="#1c5d31" stroke-width="2" />
                    <path d="M26 22 Q38 2 50 22" fill="none" stroke="#1c5d31" stroke-width="4" />
                    <path d="M18 34 Q4 40 18 54" fill="none" stroke="#1c5d31" stroke-width="4" />
                </svg>
            </div>
        </div>

        <div class="fm__panel">
            <div class="fm__rows">
                {#each state.holes as hole, i}
                    <div class="fm__cell">
                        <div class="fm__hole" class:highlight={(drag.active && drag.type === 'shovel' && loaded && hole.fill < SCOOPS) || (drag.type === 'can' && hoverHole === i)}
                             data-hole-idx={i}>
                            <div class="fm__pit" style="opacity: {1 - hole.fill / SCOOPS * 0.85}"></div>
                            <div class="fm__soil" style="height: {hole.fill / SCOOPS * 100}%"></div>
                            <svg class="fm__sprout" viewBox="0 0 40 50"
                                 style="transform: translateX(-50%) scale({0.8 + hole.water / 100 * 0.45}); filter: saturate({0.55 + hole.water / 100 * 0.6})">
                                <path d="M20 50 V20" stroke="#4caf50" stroke-width="4" stroke-linecap="round" />
                                <path d="M20 26 C8 26 2 16 4 8 C14 8 20 16 20 26 Z" fill="#66bb6a" />
                                <path d="M20 22 C30 22 38 12 36 4 C26 4 20 12 20 22 Z" fill="#81c784" />
                            </svg>
                            {#if drag.type === 'can' && hoverHole === i && hole.water < 100}
                                <div class="fm__drops">
                                    {#each [0, 1, 2, 3] as d}<span style="left: {20 + d * 18}%; animation-delay: {d * 0.12}s"></span>{/each}
                                </div>
                            {/if}
                        </div>
                        {#if state.phase === 'dig'}
                            <div class="fm__mound" class:highlight={drag.active && drag.type === 'shovel' && !loaded && state.mounds[i] > 0}
                                 class:empty={state.mounds[i] === 0} data-mound-idx={i}>
                                <div class="fm__mound-body" style="transform: scale({0.35 + state.mounds[i] / SCOOPS * 0.65})"></div>
                            </div>
                        {:else}
                            <div class="fm__water">
                                <div class="fm__water-fill" style="width: {hole.water}%"></div>
                            </div>
                        {/if}
                    </div>
                {/each}
            </div>
            {#if state.phase === 'success'}
                <div class="fm__overlay"><div class="fm__overlay-icon">✓</div><div>Грядка засажена!</div></div>
            {/if}
        </div>
    </div>

    <div class="fm__hints">
        <div><span>Тащить</span>Лопатку и лейку</div>
        <div><span>Esc</span>Бросить работу</div>
    </div>

    {#if drag.active}
        <div class="fm__ghost" class:can={drag.type === 'can'} style="left: {drag.x}px; top: {drag.y}px">
            {#if drag.type === 'shovel'}
                <svg viewBox="0 0 40 90" width={40 * scale} height={90 * scale}>
                    <rect x="17" y="2" width="6" height="40" rx="3" fill="#9b6a3c" />
                    <rect x="14" y="0" width="12" height="10" rx="4" fill="#3b3b3b" />
                    <path d="M20 40 C4 44 4 70 20 88 C36 70 36 44 20 40 Z" fill="#b8c2cc" stroke="#6d7780" stroke-width="2" />
                    {#if loaded}<ellipse cx="20" cy="66" rx="12" ry="9" fill="#5b3a1e" />{/if}
                </svg>
            {:else}
                <svg viewBox="0 0 90 70" width={80 * scale} height={62 * scale}>
                    <path d="M18 22 H58 V62 Q58 68 52 68 H24 Q18 68 18 62 Z" fill="#2f8f4e" stroke="#1c5d31" stroke-width="2" />
                    <path d="M58 30 L84 12 L88 16 L60 42 Z" fill="#2f8f4e" stroke="#1c5d31" stroke-width="2" />
                    <path d="M26 22 Q38 2 50 22" fill="none" stroke="#1c5d31" stroke-width="4" />
                    <path d="M18 34 Q4 40 18 54" fill="none" stroke="#1c5d31" stroke-width="4" />
                </svg>
            {/if}
        </div>
    {/if}
</div>

<style>
    .fm {
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
    .fm__title {
        text-align: center;
        text-shadow: 0 0.2vh 0.8vh rgba(0, 0, 0, 0.9);
        margin-bottom: 2.6vh;
    }
    .fm__caption {
        text-transform: uppercase;
        letter-spacing: 0.15em;
        font-size: 1.3vh;
        color: #8bd36b;
    }
    .fm__heading {
        font-family: 'TTNorms-Bold';
        font-size: 3vh;
        margin: 0.4vh 0 1vh;
    }
    .fm__steps {
        display: flex;
        gap: 1vh;
        justify-content: center;
    }
    .fm__steps span {
        padding: 0.5vh 1.2vh;
        border-radius: 2vh;
        background: rgba(0, 0, 0, 0.6);
        font-size: 1.3vh;
        color: rgba(255, 255, 255, 0.6);
    }
    .fm__steps span.active {
        color: white;
        box-shadow: inset 0 0 0 1px rgba(139, 211, 107, 0.6);
    }
    .fm__steps span.done {
        background: rgba(20, 90, 30, 0.75);
        color: #6ef07a;
    }
    .fm__layout {
        display: flex;
        align-items: flex-start;
    }
    .fm__sidebar {
        display: flex;
        flex-direction: column;
        align-items: center;
        padding: 1.3vh 1.1vh;
        min-width: 12vh;
        margin-top: 2.6vh;
        background: rgba(0, 0, 0, 0.55);
        border: 1px solid rgba(255, 255, 255, 0.07);
        border-right: none;
        border-radius: 0.9vh 0 0 0.9vh;
    }
    .fm__label {
        font-family: 'TTNorms-Bold';
        font-size: 1vh;
        letter-spacing: 0.25em;
        text-transform: uppercase;
    }
    .fm__sublabel {
        font-size: 1vh;
        color: rgba(255, 255, 255, 0.4);
        margin: 0.2vh 0 1.3vh;
        max-width: 10vh;
        text-align: center;
    }
    .fm__tool {
        cursor: grab;
        filter: drop-shadow(0 0.3vh 0.6vh rgba(0, 0, 0, 0.7));
        transition: transform 0.12s, opacity 0.15s;
    }
    .fm__tool:hover {
        transform: translateY(-0.2vh) scale(1.06);
    }
    .fm__tool.disabled {
        opacity: 0.25;
        cursor: default;
        transform: none;
    }
    .fm__tool.dragging {
        opacity: 0;
    }
    .fm__divider {
        width: 80%;
        height: 1px;
        background: rgba(255, 255, 255, 0.1);
        margin: 1.3vh 0;
    }
    .fm__panel {
        position: relative;
        width: 62vh;
        height: 40vh;
        border-radius: 0 0.9vh 0.9vh 0;
        border: 1px solid rgba(255, 255, 255, 0.1);
        box-shadow: 0 0.8vh 4vh rgba(0, 0, 0, 0.9);
        overflow: hidden;
        background:
            repeating-linear-gradient(90deg, rgba(0, 0, 0, 0.12) 0 0.4vh, transparent 0.4vh 3vh),
            radial-gradient(ellipse at 50% 30%, #7a5230 0%, #5a3a1f 60%, #3d2714 100%);
    }
    .fm__rows {
        position: absolute;
        inset: 0;
        display: flex;
        justify-content: space-evenly;
        align-items: center;
        padding: 0 2vh;
    }
    .fm__cell {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 2.4vh;
    }
    .fm__hole {
        position: relative;
        width: 8vh;
        height: 8vh;
        border-radius: 50%;
        background: #3a2412;
        box-shadow: inset 0 0.6vh 1.2vh rgba(0, 0, 0, 0.8);
        overflow: visible;
        transition: box-shadow 0.15s;
    }
    .fm__hole.highlight {
        box-shadow: inset 0 0.6vh 1.2vh rgba(0, 0, 0, 0.8), 0 0 0 0.3vh rgba(139, 211, 107, 0.8);
    }
    .fm__pit {
        position: absolute;
        inset: 0.8vh;
        border-radius: 50%;
        background: #1c1008;
        transition: opacity 0.2s;
    }
    .fm__soil {
        position: absolute;
        left: 0;
        right: 0;
        bottom: 0;
        border-radius: 0 0 4vh 4vh;
        background: radial-gradient(ellipse at 50% 20%, #6d4526, #4e311a);
        transition: height 0.2s;
    }
    .fm__sprout {
        position: absolute;
        left: 50%;
        bottom: 2.4vh;
        width: 4.4vh;
        height: 5.5vh;
        transform-origin: 50% 100%;
        pointer-events: none;
        transition: transform 0.2s, filter 0.2s;
    }
    .fm__drops {
        position: absolute;
        left: 0;
        right: 0;
        top: -3vh;
        height: 6vh;
        pointer-events: none;
    }
    .fm__drops span {
        position: absolute;
        top: 0;
        width: 0.5vh;
        height: 1.1vh;
        border-radius: 50%;
        background: #6ec6ff;
        animation: drop 0.5s linear infinite;
    }
    @keyframes drop {
        from { transform: translateY(0); opacity: 1; }
        to { transform: translateY(5vh); opacity: 0; }
    }
    .fm__mound {
        width: 8vh;
        height: 5vh;
        display: flex;
        align-items: flex-end;
        justify-content: center;
        border-radius: 1vh;
        transition: box-shadow 0.15s;
    }
    .fm__mound.highlight {
        box-shadow: 0 0 0 0.3vh rgba(245, 196, 0, 0.7);
    }
    .fm__mound.empty {
        opacity: 0.3;
    }
    .fm__mound-body {
        width: 7vh;
        height: 4vh;
        border-radius: 50% 50% 0.6vh 0.6vh;
        background: radial-gradient(ellipse at 40% 30%, #8a5a33, #5b3a1e);
        box-shadow: 0 0.4vh 0.8vh rgba(0, 0, 0, 0.5);
        transform-origin: 50% 100%;
        transition: transform 0.15s;
    }
    .fm__water {
        width: 8vh;
        height: 0.9vh;
        margin: 2vh 0;
        border-radius: 1vh;
        background: rgba(0, 0, 0, 0.5);
        overflow: hidden;
    }
    .fm__water-fill {
        height: 100%;
        background: linear-gradient(90deg, #3a9fff, #6ec6ff);
    }
    .fm__overlay {
        position: absolute;
        inset: 0;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        gap: 1vh;
        font-family: 'TTNorms-Bold';
        font-size: 2.4vh;
        background: rgba(20, 60, 25, 0.75);
        color: #6ef07a;
        z-index: 20;
    }
    .fm__overlay-icon {
        font-size: 6vh;
    }
    .fm__hints {
        display: flex;
        gap: 2.4vh;
        margin-top: 2vh;
        font-size: 1.3vh;
        color: rgba(255, 255, 255, 0.7);
        text-shadow: 0 0.2vh 0.6vh rgba(0, 0, 0, 0.9);
    }
    .fm__hints span {
        display: inline-block;
        padding: 0.3vh 0.8vh;
        margin-right: 0.7vh;
        border-radius: 0.5vh;
        background: rgba(0, 0, 0, 0.7);
        font-family: 'TTNorms-Bold';
    }
    .fm__ghost {
        position: fixed;
        pointer-events: none;
        z-index: 2000;
        transform: translate(-50%, -85%);
        filter: drop-shadow(0 0.6vh 1vh rgba(0, 0, 0, 0.7));
    }
    .fm__ghost.can {
        transform: translate(-90%, -40%) rotate(-25deg);
    }
</style>
