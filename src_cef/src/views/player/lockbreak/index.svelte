<script>
    // Взлом замка отмычкой. Механика — из rage-lock-break (Apache-2.0, см. NOTICE):
    // мышь двигает отмычку (ищем «правильный» угол защёлки), A/D поворачивают замок.
    // Поворот замка при неверном угле отмычки или в неверную сторону гнёт отмычку; прочность 0 — отмычка сломана.
    // Сервер: client.lockbreak.opened / broken / cancel → server.lockbreak.* (Crime/LockBreak.cs).
    import { executeClient } from "api/rage";
    import { onDestroy, onMount } from "svelte";
    import { fade } from "svelte/transition";
    import keyholeImg from "./assets/keyhole.png";
    import lockImg from "./assets/lock.png";
    import lockpickImg from "./assets/lockpick.png";
    import breakSoundUrl from "./assets/lockpickbreak.mp3";
    import damageSoundUrl from "./assets/lockpickdamage.mp3";
    import openSoundUrl from "./assets/openlocksong.mp3";

    export let viewData;

    const params = (() => {
        try {
            return typeof viewData === "string" ? JSON.parse(viewData) : viewData || {};
        } catch (e) {
            return {};
        }
    })();

    const title = params.title || "Взлом замка";
    // Точность угла защёлки в градусах: чем меньше, тем сложнее (оригинал — 5)
    const inaccuracy = Math.max(2, Math.min(12, Number(params.difficulty) || 5)) + 0.001;
    let picks = Math.max(0, Number(params.picks) || 1);

    const random = (min, max) => Math.floor(min + Math.random() * (max + 1 - min));
    const clamp = (n, min, max) => Math.min(Math.max(n, min), max);

    const sound = (url, volume) => {
        const audio = new Audio(url);
        audio.volume = volume;
        return audio;
    };
    const breakSound = sound(breakSoundUrl, 0.2);
    const damageSound = sound(damageSoundUrl, 0.25);
    const openSound = sound(openSoundUrl, 0.2);
    const play = (audio, from = 0) => {
        try {
            audio.currentTime = from;
            audio.play();
        } catch (e) {}
    };

    // Защёлка
    const latchMin = -50;
    const latchMax = 110;
    let latchRotation = random(latchMin, latchMax);
    let defused = false;

    // Замок (скважина)
    const rotateStep = 0.75;
    const restoreStep = 2.35;
    const turnWay = Math.random() < 0.5 ? -1 : 1;
    let keyholeRotation = 0;

    // Отмычка
    let pickRotation = 0;
    let strength = 100;
    let maxStrength = 100;
    let pickBroken = false;
    let lastMouseX = null;

    let pressedKey = null;
    let cooldown = null;
    let frozen = false;
    let opened = false;
    let waiting = false;

    const newPick = () => {
        maxStrength = strength = random(50, 100);
        pickRotation = 0;
        lastMouseX = null;
        pickBroken = false;
        frozen = false;
        waiting = false;
    };
    newPick();

    // Сервер выдал новую отмычку после поломки
    window.events.addEvent("cef.lockbreak.newpick", (count) => {
        picks = Number(count) || 0;
        newPick();
    });
    onDestroy(() => window.events.removeEvent("cef.lockbreak.newpick"));

    const wrongRotate = () => {
        pressedKey = null;
        if (!cooldown) cooldown = setTimeout(() => (cooldown = null), 100);
        strength -= random(1, 5);
        if (strength <= 0) {
            strength = 0;
            pickBroken = true;
            frozen = true;
            waiting = true;
            picks = Math.max(0, picks - 1);
            play(breakSound);
            executeClient("client.lockbreak.broken");
        } else play(damageSound);
    };

    const tick = () => {
        if (opened) return;
        if (!pressedKey) {
            keyholeRotation -= Math.sign(keyholeRotation) * restoreStep;
            if (Math.abs(keyholeRotation) < restoreStep - 0.001) keyholeRotation = 0;
            return;
        }
        if (pressedKey === 68) keyholeRotation = Math.min(90, keyholeRotation + rotateStep * 2);
        if (pressedKey === 65) keyholeRotation = Math.max(-90, keyholeRotation - rotateStep * 2);

        if (turnWay !== Math.sign(keyholeRotation) && Math.abs(keyholeRotation) > 15) {
            wrongRotate();
            return;
        }
        if (!defused) {
            wrongRotate();
            return;
        }
        if (Math.sign(keyholeRotation) === turnWay && Math.abs(keyholeRotation) >= 90 - 0.001) {
            opened = true;
            frozen = true;
            play(openSound, 0.2);
            setTimeout(() => executeClient("client.lockbreak.opened"), 450);
        }
    };

    // 30 кадров в секунду, как в оригинале
    let frameTimer;
    onMount(() => (frameTimer = setInterval(tick, 1000 / 30)));
    onDestroy(() => clearInterval(frameTimer));

    const smoothness = () => (window.innerWidth / 2) / (latchMax - latchMin);

    const onMouseMove = (e) => {
        if (frozen) return;
        if (lastMouseX !== null) pickRotation = clamp(pickRotation + (e.clientX - lastMouseX) / smoothness(), latchMin, latchMax);
        defused = Math.abs(pickRotation - latchRotation) < inaccuracy;
        lastMouseX = e.clientX;
    };

    const onKeyDown = (e) => {
        if (frozen) return;
        pressedKey = (e.keyCode === 68 || e.keyCode === 65) && !cooldown ? e.keyCode : null;
    };
    const onKeyUp = (e) => {
        if (e.keyCode === 27) {
            executeClient("client.lockbreak.cancel");
            return;
        }
        if (!frozen) pressedKey = null;
    };
</script>

<svelte:window on:mousemove={onMouseMove} on:keydown={onKeyDown} on:keyup={onKeyUp} />

<div class="lb" in:fade={{ duration: 200 }}>
    <div class="lb__lock" style="background-image:url({lockImg})">
        <div class="lb__keyhole">
            <img class="lb__screwdriver" src={keyholeImg} alt="" style="transform: rotateZ({keyholeRotation}deg)" />
            <img class="lb__pick" class:broken={pickBroken} src={lockpickImg} alt="" style={pickBroken ? "" : `transform: translate(-100%, -100%) rotateZ(${pickRotation}deg)`} />
        </div>
    </div>

    <div class="lb__panel">
        <div class="lb__title">{title}</div>
        <div class="lb__row">
            <span>Отмычки</span><b>{picks}</b>
        </div>
        <div class="lb__row">
            <span>Прочность</span>
            <div class="lb__bar"><div style="width:{(strength / maxStrength) * 100}%" class:low={strength / maxStrength < 0.3}></div></div>
        </div>
        {#if opened}
            <div class="lb__state ok">Замок открыт</div>
        {:else if waiting}
            <div class="lb__state bad">{picks > 0 ? "Отмычка сломалась…" : "Отмычки закончились"}</div>
        {/if}
    </div>

    <div class="lb__help">
        <div><span>A / D</span>Повернуть замок</div>
        <div><span>← мышь →</span>Повернуть отмычку</div>
        <div><span>ESC</span>Отменить</div>
    </div>
</div>

<style>
    .lb {
        position: absolute;
        top: 0;
        left: 0;
        right: 0;
        bottom: 0;
        overflow: hidden;
        user-select: none;
        background: radial-gradient(circle at 50% 50%, rgba(10, 12, 16, 0.35), rgba(5, 6, 9, 0.85));
        font-family: "Gilroy", "Montserrat", sans-serif;
        color: #e9edf3;
    }
    .lb__lock {
        position: absolute;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%);
        width: 100%;
        height: 100vh;
        background-repeat: no-repeat;
        background-position: center;
        background-size: 50.3vh 42.1vh;
        display: flex;
        align-items: center;
        justify-content: center;
    }
    .lb__keyhole {
        position: relative;
        display: flex;
        align-items: center;
        justify-content: center;
    }
    .lb__screwdriver {
        width: 111vh;
        height: 111vh;
    }
    .lb__pick {
        position: absolute;
        top: 50%;
        left: 50%;
        width: 26.9vh;
        height: 41.7vh;
        transform-origin: bottom right;
        transform: translate(-100%, -100%) rotateZ(0deg);
    }
    .lb__pick.broken {
        animation: lb-break 2s forwards;
    }
    @keyframes lb-break {
        0% {
            transform: translate(-100%, -100%) scale(1) rotate(0deg);
        }
        100% {
            transform: translate(-30%, -30%) scale(0) rotate(360deg);
        }
    }
    .lb__panel {
        position: absolute;
        top: 4vh;
        right: 4vh;
        width: 30vh;
        padding: 2vh;
        border-radius: 1.4vh;
        background: rgba(18, 21, 27, 0.92);
        border: 1px solid rgba(255, 255, 255, 0.07);
        display: flex;
        flex-direction: column;
        gap: 1.2vh;
    }
    .lb__title {
        font-size: 2vh;
        font-weight: 700;
    }
    .lb__row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 1.2vh;
        font-size: 1.4vh;
        color: #8b93a1;
    }
    .lb__row b {
        font-size: 1.8vh;
        color: #f5a524;
    }
    .lb__bar {
        flex: 1;
        height: 0.8vh;
        border-radius: 0.4vh;
        background: rgba(255, 255, 255, 0.08);
        overflow: hidden;
    }
    .lb__bar div {
        height: 100%;
        background: #7ed321;
        transition: width 0.15s;
    }
    .lb__bar div.low {
        background: #ff6b6b;
    }
    .lb__state {
        font-size: 1.4vh;
        font-weight: 600;
    }
    .lb__state.ok {
        color: #7ed321;
    }
    .lb__state.bad {
        color: #ff6b6b;
    }
    .lb__help {
        position: absolute;
        left: 50%;
        bottom: 5vh;
        transform: translate(-50%);
        display: flex;
        gap: 2vh;
    }
    .lb__help div {
        display: flex;
        align-items: center;
        padding: 1vh 2.4vh;
        border-radius: 5vh;
        font-size: 1.4vh;
        font-weight: 600;
        background: rgba(18, 21, 27, 0.9);
        border: 1px solid rgba(255, 255, 255, 0.08);
    }
    .lb__help span {
        margin-right: 1vh;
        padding: 0.6vh 1.2vh;
        border-radius: 1.2vh;
        background: #f5a524;
        color: #16181d;
        font-weight: 700;
    }
</style>
