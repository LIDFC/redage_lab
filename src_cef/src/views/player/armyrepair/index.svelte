<script>
    // Ремонт армейской техники мини-игрой (сервер Fractions/ArmyRP/ArmyTraining.cs, клиент fractions/army.js):
    // 1) осмотр — удерживать кнопку; 2) найти два изношенных узла; 3) затяжка — поймать бегунок в зелёной зоне 3 раза.
    import { executeClient } from "api/rage";
    import { onDestroy, onMount } from "svelte";
    import { fade } from "svelte/transition";

    export let viewData;

    const data = (() => {
        try {
            return typeof viewData === "string" ? JSON.parse(viewData) : viewData || {};
        } catch (e) {
            return {};
        }
    })();

    const title = data.title || "Ремонт техники";
    const parts = (data.parts || []).map((p, i) => ({ ...p, id: i, found: false, wrong: false }));
    const brokenCount = parts.filter((p) => p.wear > 75).length;
    const minSeconds = Math.max(5, Number(data.seconds) || 20);
    const started = Date.now();

    let step = 1;
    let message = "";

    // --- шаг 1: осмотр
    let holdProgress = 0;
    let holdTimer = null;
    const holdStart = () => {
        if (step !== 1 || holdTimer) return;
        holdTimer = setInterval(() => {
            holdProgress = Math.min(100, holdProgress + 100 / 30);
            if (holdProgress >= 100) {
                holdStop();
                step = 2;
                message = "Найдите изношенные узлы";
            }
        }, 100);
    };
    const holdStop = () => {
        if (holdTimer) clearInterval(holdTimer);
        holdTimer = null;
        if (step === 1 && holdProgress < 100) holdProgress = 0;
    };

    // --- шаг 2: неисправности
    let penaltyUntil = 0;
    const pick = (part) => {
        if (step !== 2 || part.found || Date.now() < penaltyUntil) return;
        if (part.wear > 75) {
            part.found = true;
            message = `${part.name}: заменить`;
        } else {
            part.wrong = true;
            penaltyUntil = Date.now() + 3000;
            message = `«${part.name}» в норме — перепроверка 3 сек`;
            setTimeout(() => { part.wrong = false; parts[part.id] = part; }, 3000);
        }
        parts[part.id] = part;
        if (parts.filter((p) => p.found).length >= Math.max(1, brokenCount)) {
            step = 3;
            message = "Затяните крепления: жмите, когда бегунок в зелёной зоне";
            startSlider();
        }
    };

    // --- шаг 3: затяжка
    let slider = 0;
    let direction = 1;
    let zoneStart = 40;
    const zoneWidth = 16;
    let hits = 0;
    let misses = 0;
    let sliderTimer = null;
    const startSlider = () => {
        zoneStart = 15 + Math.random() * 65;
        sliderTimer = setInterval(() => {
            slider += direction * 2.2;
            if (slider >= 100) { slider = 100; direction = -1; }
            if (slider <= 0) { slider = 0; direction = 1; }
        }, 16);
    };
    const tighten = () => {
        if (step !== 3) return;
        if (slider >= zoneStart && slider <= zoneStart + zoneWidth) {
            hits++;
            message = `Затянуто ${hits}/3`;
            zoneStart = 15 + Math.random() * 65;
            if (hits >= 3) finish();
        } else {
            misses++;
            message = "Сорвалось — ещё раз";
        }
    };

    let waiting = 0;
    const finish = () => {
        if (sliderTimer) clearInterval(sliderTimer);
        step = 4;
        const left = minSeconds * 1000 - (Date.now() - started);
        if (left <= 0) return done();
        waiting = Math.ceil(left / 1000);
        message = "Проверка систем...";
        const t = setInterval(() => {
            waiting--;
            if (waiting <= 0) {
                clearInterval(t);
                done();
            }
        }, 1000);
    };
    const done = () => executeClient("client.army.repair.done");
    const cancel = () => executeClient("client.army.repair.cancel");

    const onKey = (e) => {
        if (e.code === "Space") tighten();
        else if (e.key === "Escape") cancel();
    };

    onMount(() => window.addEventListener("keydown", onKey));
    onDestroy(() => {
        window.removeEventListener("keydown", onKey);
        holdStop();
        if (sliderTimer) clearInterval(sliderTimer);
    });
</script>

<div class="repair" in:fade={{ duration: 150 }}>
    <div class="repair__window">
        <div class="repair__head">
            <div class="repair__title">{title}</div>
            <div class="repair__steps">
                <span class:active={step === 1} class:done={step > 1}>1. Осмотр</span>
                <span class:active={step === 2} class:done={step > 2}>2. Неисправности</span>
                <span class:active={step === 3} class:done={step > 3}>3. Затяжка</span>
            </div>
            <div class="repair__close" on:click={cancel}>✕</div>
        </div>

        {#if step === 1}
            <div class="repair__body">
                <div class="repair__hint">Удерживайте кнопку, чтобы осмотреть технику</div>
                <div class="repair__bar"><div class="repair__fill" style="width:{holdProgress}%" /></div>
                <div class="repair__btn" on:mousedown={holdStart} on:mouseup={holdStop} on:mouseleave={holdStop}>Осматривать</div>
            </div>
        {:else if step === 2}
            <div class="repair__body">
                <div class="repair__hint">Диагностика показала износ узлов. Выберите неисправные (износ выше 75%)</div>
                <div class="repair__grid">
                    {#each parts as part (part.id)}
                        <div class="repair__part" class:found={part.found} class:wrong={part.wrong} on:click={() => pick(part)}>
                            <div class="repair__pname">{part.name}</div>
                            <div class="repair__wear" class:bad={part.wear > 75}>износ {part.wear}%</div>
                        </div>
                    {/each}
                </div>
            </div>
        {:else if step === 3}
            <div class="repair__body">
                <div class="repair__hint">Нажмите «Затянуть» или пробел, когда бегунок в зелёной зоне</div>
                <div class="repair__track">
                    <div class="repair__zone" style="left:{zoneStart}%;width:{zoneWidth}%" />
                    <div class="repair__runner" style="left:{slider}%" />
                </div>
                <div class="repair__btn" on:click={tighten}>Затянуть ({hits}/3)</div>
            </div>
        {:else}
            <div class="repair__body">
                <div class="repair__hint">Проверка систем{waiting > 0 ? ` — ${waiting} сек` : ""}</div>
            </div>
        {/if}

        <div class="repair__msg">{message}</div>
    </div>
</div>

<style>
    .repair {
        position: absolute;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        display: flex;
        align-items: center;
        justify-content: center;
        background: rgba(0, 0, 0, 0.5);
        font-family: "Gilroy", "Montserrat", sans-serif;
        color: #fff;
    }
    .repair__window {
        width: 640px;
        max-width: 94vw;
        background: #16181d;
        border: 1px solid rgba(255, 255, 255, 0.08);
        border-radius: 12px;
        padding: 20px 24px;
    }
    .repair__head {
        display: flex;
        align-items: center;
        gap: 16px;
        margin-bottom: 18px;
    }
    .repair__title {
        font-size: 20px;
        font-weight: 700;
    }
    .repair__steps {
        display: flex;
        gap: 10px;
        margin-left: auto;
        font-size: 12px;
        opacity: 0.8;
    }
    .repair__steps span {
        white-space: nowrap;
        padding: 4px 8px;
        border-radius: 6px;
        background: rgba(255, 255, 255, 0.05);
    }
    .repair__steps span.active {
        background: #4b6b2a;
    }
    .repair__steps span.done {
        opacity: 0.5;
    }
    .repair__close {
        cursor: pointer;
        opacity: 0.7;
        padding: 4px 8px;
    }
    .repair__body {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 16px;
        min-height: 170px;
        justify-content: center;
    }
    .repair__hint {
        font-size: 14px;
        opacity: 0.8;
        text-align: center;
    }
    .repair__bar,
    .repair__track {
        position: relative;
        width: 100%;
        height: 16px;
        border-radius: 8px;
        background: rgba(255, 255, 255, 0.08);
        overflow: hidden;
    }
    .repair__fill {
        height: 100%;
        background: #6b8e23;
    }
    .repair__zone {
        position: absolute;
        top: 0;
        height: 100%;
        background: rgba(47, 163, 107, 0.7);
    }
    .repair__runner {
        position: absolute;
        top: -2px;
        width: 4px;
        height: 20px;
        margin-left: -2px;
        background: #fff;
    }
    .repair__btn {
        padding: 10px 26px;
        border-radius: 8px;
        background: #4b6b2a;
        font-weight: 600;
        cursor: pointer;
        user-select: none;
    }
    .repair__grid {
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: 10px;
        width: 100%;
    }
    .repair__part {
        padding: 12px;
        border-radius: 8px;
        background: rgba(255, 255, 255, 0.05);
        cursor: pointer;
        border: 1px solid transparent;
    }
    .repair__part:hover {
        border-color: rgba(255, 255, 255, 0.2);
    }
    .repair__part.found {
        border-color: #2fa36b;
        background: rgba(47, 163, 107, 0.15);
    }
    .repair__part.wrong {
        border-color: #e5484d;
    }
    .repair__pname {
        font-size: 14px;
        font-weight: 600;
    }
    .repair__wear {
        font-size: 12px;
        opacity: 0.7;
        margin-top: 4px;
    }
    .repair__wear.bad {
        color: #ff8a65;
        opacity: 1;
    }
    .repair__msg {
        margin-top: 14px;
        min-height: 18px;
        font-size: 13px;
        color: #b5d77b;
        text-align: center;
    }
</style>
