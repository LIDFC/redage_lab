<script>
    // Программатор «Breach protocol» (пакет cyber-hack, прислан владельцем сервера; тексты переведены).
    // Сервер: Crime/CyberHack.cs, клиент: src_client/player/cyberhack.js.
    import { onMount, onDestroy } from 'svelte';
    import { executeClient } from 'api/rage';

    export let viewData;

    const params = (() => {
        try {
            return typeof viewData === "string" ? JSON.parse(viewData) : viewData || {};
        } catch (e) {
            return {};
        }
    })();
    let difficulty = Math.max(4, Math.min(7, Number(params.difficulty) || 5));
    let timeSeconds = Math.max(10, Number(params.timeSeconds) || 30);
    const title = params.title || "Взлом";

    let hexDb = ['1C', 'E9', '55', 'BD', '7A'];
    let bufSize = 5;
    
    let matrix = [];
    let targetSeq = [];
    let buffer = [];
    
    let mode = 'ROW';
    let activeIndex = 0;
    
    let timer = null;
    let timeLeft = timeSeconds;
    let isPlaying = true;
    let isWin = false;
    let failReason = "";
    
    let endScreenVisible = false;

    function initGame() {
        matrix = [];
        for (let r = 0; r < difficulty; r++) {
            let row = [];
            for (let c = 0; c < difficulty; c++) {
                row.push({
                    r, c,
                    val: hexDb[Math.floor(Math.random() * hexDb.length)],
                    disabled: false,
                    selected: false,
                    highlighted: false
                });
            }
            matrix.push(row);
        }
        
        targetSeq = [];
        let currRow = 0;
        let currCol = Math.floor(Math.random() * difficulty);
        let seqLen = 3;

        targetSeq.push(matrix[currRow][currCol].val);

        for (let i = 1; i < seqLen; i++) {
            if (i % 2 !== 0) {
                let nR = Math.floor(Math.random() * difficulty);
                while (nR === currRow) nR = Math.floor(Math.random() * difficulty);
                currRow = nR;
            } else {
                let nC = Math.floor(Math.random() * difficulty);
                while (nC === currCol) nC = Math.floor(Math.random() * difficulty);
                currCol = nC;
            }
            targetSeq.push(matrix[currRow][currCol].val);
        }
        
        updateMatrixState();
        
        timer = setInterval(() => {
            if(!isPlaying) return;
            timeLeft--;
            if(timeLeft <= 0) {
                endGame(false, "время вышло");
            }
        }, 1000);
    }
    
    function updateMatrixState() {
        for (let r = 0; r < difficulty; r++) {
            for (let c = 0; c < difficulty; c++) {
                matrix[r][c].disabled = true;
            }
        }
        
        if (mode === 'ROW') {
            for (let c = 0; c < difficulty; c++) {
                if(!matrix[activeIndex][c].selected) matrix[activeIndex][c].disabled = false;
            }
        } else {
            for (let r = 0; r < difficulty; r++) {
                if(!matrix[r][activeIndex].selected) matrix[r][activeIndex].disabled = false;
            }
        }
        matrix = matrix; 
    }
    
    function hoverCell(r, c) {
        if(!isPlaying) return;
        
        for (let i = 0; i < difficulty; i++) {
            for (let j = 0; j < difficulty; j++) {
                matrix[i][j].highlighted = false;
            }
        }
        
        if (mode === 'ROW' && r === activeIndex) {
            for (let j = 0; j < difficulty; j++) {
                if(!matrix[r][j].selected) matrix[r][j].highlighted = true;
            }
        } else if (mode === 'COL' && c === activeIndex) {
            for (let i = 0; i < difficulty; i++) {
                if(!matrix[i][c].selected) matrix[i][c].highlighted = true;
            }
        }
        matrix = matrix;
    }
    
    function clearHighlights() {
        for (let i = 0; i < difficulty; i++) {
            for (let j = 0; j < difficulty; j++) {
                matrix[i][j].highlighted = false;
            }
        }
        matrix = matrix;
    }
    
    function clickCell(r, c) {
        if(!isPlaying) return;
        if(matrix[r][c].disabled || matrix[r][c].selected) return;
        
        buffer = [...buffer, matrix[r][c].val];
        matrix[r][c].selected = true;
        
        mode = (mode === 'ROW') ? 'COL' : 'ROW';
        activeIndex = (mode === 'ROW') ? r : c;
        
        updateMatrixState();
        checkWinCond();
        matrix = matrix;
    }
    
    function checkWinCond() {
        if (buffer.length >= targetSeq.length) {
            let diff = true;
            for (let i=0; i < targetSeq.length; i++) {
                if (buffer[i] !== targetSeq[i]) diff = false;
            }
            if (diff) { endGame(true); return; }
        }

        if (buffer.length >= bufSize) {
            endGame(false, "буфер переполнен");
        }
    }
    
    function endGame(win, reason = "") {
        isPlaying = false;
        clearInterval(timer);
        isWin = win;
        failReason = reason;
        endScreenVisible = true;
        
        setTimeout(() => executeClient('client.cyberhack.finish', isWin), 1600);
    }

    // ESC — просто отключиться (без последствий), в отличие от провала взлома
    function abortHack() {
        if (!isPlaying) return;
        isPlaying = false;
        clearInterval(timer);
        executeClient('client.cyberhack.cancel');
    }

    onMount(() => {
        initGame();
        
        const handleKeys = (e) => {
            if(e.key === 'Escape' && isPlaying) {
                abortHack();
            }
        };
        window.addEventListener('keydown', handleKeys);
        return () => window.removeEventListener('keydown', handleKeys);
    });
    
    onDestroy(() => {
        if(timer) clearInterval(timer);
    });
</script>

<div class="hack-wrapper">
    <div class="hack-container">
        {#if endScreenVisible}
        <div class="overlay-msg show">
            <div class={isWin ? "msg-title text-win" : "msg-title text-fail"}>
                {isWin ? "ДОСТУП ПОЛУЧЕН" : "ВЗЛОМ ПРОВАЛЕН"}
            </div>
            <div class="msg-sub">
                {isWin ? "Замок отключён. Сигнализация подавлена." : `Причина: ${failReason}. Срабатывает защита…`}
            </div>
        </div>
        {/if}

        <div class="matrix-section">
            <div class="matrix-header">
                <div>{title}</div>
                <div style="text-align: right">
                    <span style="font-size: 20px;">{Math.floor(timeLeft / 60).toString().padStart(2, '0')}:{(timeLeft % 60).toString().padStart(2, '0')}</span>
                    <div class="timer-bar">
                        <div class="timer-fill" style="width: {(timeLeft / timeSeconds) * 100}%; background: { (timeLeft / timeSeconds * 100) <= 25 ? 'var(--neon-red)' : 'var(--neon-green)' };"></div>
                    </div>
                </div>
            </div>
            <div class="matrix-grid" style="grid-template-columns: repeat({difficulty}, 1fr)">
                {#each matrix as row, r}
                    {#each row as cell, c}
                        <!-- svelte-ignore a11y-click-events-have-key-events -->
                        <div class="hex-cell {cell.selected ? 'is-selected' : ''} {cell.disabled ? 'is-disabled' : 'is-active'} {cell.highlighted ? 'is-highlighted' : ''}" 
                             on:mouseenter={() => hoverCell(r, c)} 
                             on:mouseleave={clearHighlights}
                             on:click={() => clickCell(r, c)}>
                            {cell.val}
                        </div>
                    {/each}
                {/each}
            </div>
        </div>

        <div class="info-section">
            <div>
                <div class="info-title">Буфер ({buffer.length}/{bufSize})</div>
                <div class="buffer-container">
                    {#each Array(bufSize) as _, i}
                        <div class="buffer-slot {buffer[i] ? 'filled' : ''}">
                            {buffer[i] ? buffer[i] : ''}
                        </div>
                    {/each}
                </div>
            </div>

            <div>
                <div class="info-title">Нужная последовательность</div>
                <div class="daemon-container">
                    <div style="font-size:12px; color:rgba(255,255,255,0.4)">immobilizer_bypass.exe</div>
                    <div class="daemon-seq">
                        {#each targetSeq as tHex, i}
                            <div class="seq-hex {buffer[i] === tHex ? 'matched' : (buffer[i] ? 'failed' : '')}">
                                {tHex}
                            </div>
                        {/each}
                    </div>
                </div>
            </div>
            
            <div style="margin-top:auto; font-size:12px; color:rgba(255,255,255,0.3); border-top:1px solid rgba(255,255,255,0.1); padding-top:10px;">
                <p>1. Начните с подсвеченной СТРОКИ.</p>
                <p>2. Каждый выбор переключает СТРОКУ ↔ СТОЛБЕЦ.</p>
                <p>3. Соберите нужную последовательность по порядку, пока не кончилось время.</p>
                <p>Провал — программатор сгорит и сработает сигнализация.</p>
            </div>
            
            <button class="btn-close" on:click={abortHack}>Отключиться (ESC)</button>
        </div>
    </div>
</div>

<style>
    .hack-wrapper {
        --bg-color: rgba(10, 15, 10, 0.95);
        --neon-green: #22c55e;
        --neon-red: #ef4444;
        --neon-yellow: #eab308;
        --grid-bg: rgba(20, 30, 20, 0.8);
        --cell-border: rgba(34, 197, 94, 0.1);
        --glitch-text: #ffffff;
        --font-main: 'Share Tech Mono', 'Consolas', 'Courier New', monospace;

        position: absolute;
        top: 0;
        left: 0;
        height: 100vh;
        width: 100vw;
        display: flex;
        justify-content: center;
        align-items: center;
        background-color: rgba(0,0,0,0.5);
        font-family: var(--font-main);
        user-select: none;
    }

    .hack-container {
        width: 900px;
        height: 600px;
        background: var(--bg-color);
        border: 2px solid var(--neon-green);
        box-shadow: 0 0 50px rgba(34, 197, 94, 0.1) inset, 0 0 20px rgba(0,0,0,0.8);
        display: flex;
        padding: 30px;
        gap: 40px;
        position: relative;
    }

    .hack-container::after {
        content: "";
        position: absolute;
        top: 0; left: 0; right: 0; bottom: 0;
        background: linear-gradient(rgba(34, 197, 94, 0.03) 1px, transparent 1px);
        background-size: 100% 4px;
        pointer-events: none;
    }

    .matrix-section {
        flex: 1;
        display: flex;
        flex-direction: column;
        gap: 20px;
    }

    .matrix-header {
        color: var(--neon-green);
        font-size: 24px;
        text-transform: uppercase;
        letter-spacing: 2px;
        border-bottom: 2px solid var(--neon-green);
        padding-bottom: 10px;
        display: flex;
        justify-content: space-between;
    }

    .timer-bar {
        width: 150px;
        height: 10px;
        background: rgba(255, 255, 255, 0.1);
        margin-top: 10px;
        position: relative;
    }

    .timer-fill {
        position: absolute;
        top: 0; left: 0; bottom: 0;
        transition: width 1s linear, background 0.5s;
    }

    .matrix-grid {
        display: grid;
        gap: 6px;
        background: var(--grid-bg);
        padding: 10px;
        border: 1px solid var(--cell-border);
    }

    .hex-cell {
        aspect-ratio: 1;
        display: flex;
        justify-content: center;
        align-items: center;
        font-size: 22px;
        color: var(--glitch-text);
        cursor: pointer;
        transition: 0.15s;
        position: relative;
        background: transparent;
    }

    .hex-cell::after {
        content: "[]";
        position: absolute;
        font-size: 32px;
        color: transparent;
        font-weight: 300;
    }

    .hex-cell.is-active, .hex-cell:hover.is-active {
        color: #000;
        background: var(--neon-green);
        font-weight: bold;
    }

    .hex-cell.is-disabled {
        color: rgba(255,255,255,0.1);
        cursor: not-allowed;
        pointer-events: none;
    }

    .hex-cell.is-highlighted {
        background: rgba(34, 197, 94, 0.1);
    }

    .hex-cell.is-highlighted::after {
        color: rgba(34, 197, 94, 0.3);
    }

    .hex-cell.is-selected {
        color: rgba(255,255,255,0.2) !important;
        background: transparent !important;
        pointer-events: none;
    }
    
    .hex-cell.is-selected::after {
        color: rgba(255,255,255,0.2);
        content: "[»]";
        font-size: 16px;
    }

    .info-section {
        width: 320px;
        display: flex;
        flex-direction: column;
        gap: 30px;
        z-index: 2;
    }

    .info-title {
        color: var(--neon-green);
        font-size: 14px;
        text-transform: uppercase;
        letter-spacing: 1px;
        margin-bottom: 10px;
    }

    .buffer-container {
        display: flex;
        gap: 10px;
        flex-wrap: wrap;
    }

    .buffer-slot {
        width: 45px;
        height: 45px;
        border: 2px solid rgba(255,255,255,0.2);
        display: flex;
        justify-content: center;
        align-items: center;
        color: var(--neon-green);
        font-size: 18px;
        font-weight: bold;
        background: rgba(0,0,0,0.5);
        transition: 0.2s;
    }

    .buffer-slot.filled {
        border-color: var(--neon-green);
        color: var(--glitch-text);
    }

    .daemon-container {
        background: rgba(0, 0, 0, 0.3);
        border: 1px solid var(--cell-border);
        padding: 15px;
    }

    .daemon-seq {
        display: flex;
        gap: 12px;
        margin-top: 5px;
    }

    .seq-hex {
        font-size: 20px;
        color: var(--glitch-text);
        transition: 0.3s;
    }

    .seq-hex.matched {
        color: var(--neon-green);
        text-shadow: 0 0 10px var(--neon-green);
    }

    .seq-hex.failed {
        color: var(--neon-red);
        text-decoration: line-through;
    }
    
    .overlay-msg {
        position: absolute;
        top: 0; left: 0; right: 0; bottom: 0;
        background: rgba(0,0,0,0.9);
        display: flex;
        flex-direction: column;
        justify-content: center;
        align-items: center;
        z-index: 10;
        display: none;
    }

    .overlay-msg.show { display: flex; animation: fadeIn 0.3s; }
    
    .msg-title { font-size: 48px; text-transform: uppercase; letter-spacing: 4px; margin-bottom: 20px;}
    .msg-sub { font-size: 18px; color: var(--glitch-text); }
    .text-win { color: var(--neon-green); text-shadow: 0 0 15px var(--neon-green); }
    .text-fail { color: var(--neon-red); text-shadow: 0 0 15px var(--neon-red); }

    .btn-close {
        background: transparent;
        border: 1px solid var(--neon-red);
        color: var(--neon-red);
        padding: 10px;
        cursor: pointer;
        font-family: inherit;
        text-transform: uppercase;
        transition: 0.2s;
    }
    .btn-close:hover {
        background: rgba(239, 68, 68, 0.1);
    }

    @keyframes fadeIn { from {opacity:0} to {opacity:1} }
</style>
