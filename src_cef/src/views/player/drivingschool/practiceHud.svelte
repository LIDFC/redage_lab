<script>
    // Панель практического экзамена автошколы (показывается на HUD, данные — src_client/player/drivingschool.js)
    import { onDestroy } from 'svelte'
    import { fly } from 'svelte/transition'

    export let visible = true;

    let hud = null; // { license, check, total, penalties, maxPenalties, limit, speed, startedAt }
    let now = Date.now();

    const onHud = (json) => {
        const value = typeof json === "string" ? JSON.parse(json) : json;
        hud = value ? { ...(hud || {}), ...value } : null;
    }

    const timer = setInterval(() => now = Date.now(), 1000);

    window.events.addEvent("cef.drivingschool.hud", onHud);
    onDestroy(() => {
        clearInterval(timer);
        window.events.removeEvent("cef.drivingschool.hud", onHud);
    });

    const pad = (n) => String(n).padStart(2, "0");
    $: elapsed = hud && hud.startedAt ? Math.max(0, Math.floor((now - hud.startedAt) / 1000)) : 0;
    $: overLimit = hud && hud.speed > hud.limit;
</script>

{#if visible && hud}
    <div class="dsh" transition:fly={{ x: 60, duration: 250 }}>
        <div class="dsh__head">
            <div>
                <div class="dsh__caption">Практический экзамен</div>
                <div class="dsh__title">{hud.license}</div>
            </div>
            <div class="dsh__time">{pad(Math.floor(elapsed / 60))}:{pad(elapsed % 60)}</div>
        </div>

        <div class="dsh__row">
            <span>Контрольные точки</span>
            <b>{hud.check} / {hud.total}</b>
        </div>
        <div class="dsh__bar"><div style="width: {hud.total ? hud.check / hud.total * 100 : 0}%"></div></div>

        <div class="dsh__bottom">
            <div class="dsh__limit" class:over={overLimit}>
                <div class="dsh__sign">{hud.limit}</div>
                <div>
                    <div class="dsh__speed">{Math.round(hud.speed || 0)}</div>
                    <div class="dsh__unit">км/ч</div>
                </div>
            </div>
            <div class="dsh__errors">
                <div class="dsh__unit">Ошибки</div>
                <div class="dsh__dots">
                    {#each Array(hud.maxPenalties || 3) as _, i}
                        <div class="dsh__dot" class:bad={i < hud.penalties}></div>
                    {/each}
                </div>
            </div>
        </div>
    </div>
{/if}

<style>
    .dsh {
        position: absolute;
        z-index: 900;
        right: 2.4vh;
        top: 30vh;
        width: 30vh;
        padding: 1.8vh;
        border-radius: 1.4vh;
        background: rgba(1, 18, 32, 0.82);
        border: 1px solid rgba(43, 182, 168, 0.35);
        color: white;
        font-family: 'TTNorms-Regular';
        font-size: 1.4vh;
        pointer-events: none;
    }
    .dsh__head {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        margin-bottom: 1.4vh;
    }
    .dsh__caption {
        color: #2BB6A8;
        text-transform: uppercase;
        letter-spacing: 0.12em;
        font-size: 1.1vh;
    }
    .dsh__title {
        font-family: 'TTNorms-Bold';
        font-size: 1.9vh;
    }
    .dsh__time {
        font-family: 'TTNorms-Bold';
        font-size: 1.8vh;
        color: rgba(255, 255, 255, 0.7);
    }
    .dsh__row {
        display: flex;
        justify-content: space-between;
        color: rgba(255, 255, 255, 0.65);
        margin-bottom: 0.6vh;
    }
    .dsh__row b {
        color: white;
    }
    .dsh__bar {
        height: 0.6vh;
        border-radius: 0.3vh;
        background: rgba(255, 255, 255, 0.12);
        overflow: hidden;
        margin-bottom: 1.6vh;
    }
    .dsh__bar div {
        height: 100%;
        background: #2BB6A8;
        transition: width 0.3s;
    }
    .dsh__bottom {
        display: flex;
        justify-content: space-between;
        align-items: center;
    }
    .dsh__limit {
        display: flex;
        align-items: center;
        gap: 1.2vh;
    }
    .dsh__sign {
        width: 4.6vh;
        height: 4.6vh;
        border-radius: 50%;
        border: 0.5vh solid #e53935;
        background: white;
        color: #111;
        font-family: 'TTNorms-Bold';
        font-size: 1.7vh;
        display: flex;
        align-items: center;
        justify-content: center;
    }
    .dsh__speed {
        font-family: 'TTNorms-Bold';
        font-size: 3vh;
        line-height: 1;
    }
    .dsh__limit.over .dsh__speed {
        color: #ff6b6b;
    }
    .dsh__unit {
        color: rgba(255, 255, 255, 0.55);
        font-size: 1.2vh;
    }
    .dsh__errors {
        text-align: right;
    }
    .dsh__dots {
        display: flex;
        gap: 0.6vh;
        margin-top: 0.6vh;
        justify-content: flex-end;
    }
    .dsh__dot {
        width: 1.6vh;
        height: 1.6vh;
        border-radius: 50%;
        background: rgba(255, 255, 255, 0.15);
    }
    .dsh__dot.bad {
        background: #ff5a5a;
    }
</style>
