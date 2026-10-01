<script>
    // Тренер платной качалки (Muscle Beach) — окно в стиле «Центра занятости» (views/player/jobselector).
    // Сервер: World/Gym/Fitness.cs (client.gym.trainer.open / server.gym.buy), клиент: src_client/world/gym.js
    import { executeClient } from 'api/rage'
    import { charMoney } from 'store/chars';
    import { fade, fly } from 'svelte/transition';
    import { onDestroy } from 'svelte';

    export let viewData;

    const parse = (d) => {
        try {
            return typeof d === "string" ? JSON.parse(d) : d || {};
        } catch (e) {
            return {};
        }
    };

    let data = { zone: "Muscle Beach", member: "", plans: [], str: 30, sta: 30, strLeft: 0, staLeft: 0, hourLimit: 2, money: 0, ...parse(viewData) };
    let selectedIndex = data.plans.length > 1 ? 1 : 0;
    let message = "";
    let ok = true;

    $: plans = (data.plans || []).map((p, i) => ({ ...p, index: i, perDay: Math.round(p.price / Math.max(1, p.days)) }));
    $: bestIndex = plans.length ? plans.reduce((b, p) => (p.perDay < plans[b].perDay ? p.index : b), 0) : -1;
    $: selected = plans[selectedIndex] || plans[0];
    $: money = $charMoney || data.money;
    $: notEnough = selected && selected.price > money;

    const daysName = (n) => (n % 10 === 1 && n % 100 !== 11 ? "день" : n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 10 || n % 100 >= 20) ? "дня" : "дней");
    const fmt = (n) => String(n).replace(/\B(?=(\d{3})+(?!\d))/g, " ");

    const buy = () => {
        if (!selected || notEnough) return;
        executeClient("client.gym.trainer.buy", selected.index);
    };
    const close = () => executeClient("client.gym.trainer.close");

    const onUpdate = (json, text, success) => {
        data = { ...data, ...parse(json) };
        message = text || "";
        ok = !!success;
    };
    window.events.addEvent("cef.gym.trainer.update", onUpdate);
    onDestroy(() => window.events.removeEvent("cef.gym.trainer.update"));

    const onKeyDown = (event) => {
        if (event.key === "Escape") return close();
        if (event.key === "ArrowLeft" || event.key === "ArrowUp") selectedIndex = (selectedIndex + plans.length - 1) % plans.length;
        if (event.key === "ArrowRight" || event.key === "ArrowDown") selectedIndex = (selectedIndex + 1) % plans.length;
        if (event.key === "Enter") buy();
    };

    // Иконка гантели (встроенный SVG)
    const dumbbell = `<svg viewBox="0 0 120 120" fill="none" stroke="currentColor" stroke-width="6" stroke-linecap="round"><path d="M30 60h60"/><rect x="14" y="38" width="14" height="44" rx="4"/><rect x="92" y="38" width="14" height="44" rx="4"/><path d="M8 48v24M112 48v24"/></svg>`;
</script>

<svelte:window on:keydown={onKeyDown} />

<div class="jobcenter" in:fade={{ duration: 180 }}>
    <div class="jobcenter__panel" in:fly={{ y: 24, duration: 260 }}>

        <!-- Левая колонка: тарифы -->
        <section class="jobcenter__board">
            <header class="jobcenter__head">
                <div>
                    <div class="jobcenter__eyebrow">{data.zone} · тренер</div>
                    <h1 class="jobcenter__title">Абонемент</h1>
                </div>
                <div class="jobcenter__me">
                    <div class="jobcenter__me-lvl">
                        <span class="jobcenter__me-label">Сила</span>
                        <span class="jobcenter__me-value">{data.str}</span>
                    </div>
                    <div class="jobcenter__me-job">
                        <span class="jobcenter__me-label">Абонемент</span>
                        <span class="jobcenter__me-value small">{data.member ? `до ${data.member}` : "Нет"}</span>
                    </div>
                </div>
            </header>

            <div class="jobcenter__grid">
                {#each plans as plan (plan.index)}
                    <button
                        class="tile"
                        class:tile--active={plan.index === selectedIndex}
                        on:click={() => (selectedIndex = plan.index)}>
                        <span class="tile__icon">{@html dumbbell}</span>
                        <span class="tile__shade"></span>
                        <span class="tile__lvl">${fmt(plan.price)}</span>
                        {#if plan.index === bestIndex && plans.length > 1}
                            <span class="tile__badge">Выгодно</span>
                        {/if}
                        <span class="tile__name">{plan.days} {daysName(plan.days)}</span>
                        <span class="tile__perday">${fmt(plan.perDay)} в день</span>
                    </button>
                {/each}
            </div>

            <footer class="jobcenter__hint">
                <span><b>←→</b> выбор</span>
                <span><b>Enter</b> купить</span>
                <span><b>Esc</b> закрыть</span>
            </footer>
        </section>

        <!-- Правая колонка: подробности -->
        {#if selected}
            {#key selected.index}
                <section class="details" in:fade={{ duration: 160 }}>
                    <div class="details__hero">
                        <span class="details__icon">{@html dumbbell}</span>
                        <span class="details__hero-shade"></span>
                        <button class="details__close" on:click={close} aria-label="Закрыть">✕</button>
                        <h2 class="details__name">{selected.days} {daysName(selected.days)}</h2>
                    </div>

                    <div class="details__body">
                        <p class="details__text">Доступ ко всем тренажёрам {data.zone}: турники, скамьи, штанги и коврики. В остальных местах города качаться можно бесплатно.</p>

                        <div class="row">
                            <div class="block">
                                <div class="block__title">Сила · {data.str}/100</div>
                                <div class="skill"><div class="skill__bar" style="width: {data.str}%"></div></div>
                                <div class="block__note">{data.str >= 100 ? "максимум" : `за этот час ещё +${data.strLeft}`}</div>
                            </div>
                            <div class="block">
                                <div class="block__title">Выносливость · {data.sta}/100</div>
                                <div class="skill"><div class="skill__bar" style="width: {data.sta}%"></div></div>
                                <div class="block__note">{data.sta >= 100 ? "максимум" : `за этот час ещё +${data.staLeft}`}</div>
                            </div>
                        </div>

                        <div class="row">
                            <div class="block">
                                <div class="block__title">Стоимость</div>
                                <div class="pay">${fmt(selected.price)}</div>
                                <div class="block__note">${fmt(selected.perDay)} в день</div>
                            </div>
                            <div class="block">
                                <div class="block__title">Наличные</div>
                                <div class="pay" class:pay--bad={notEnough}>${fmt(money)}</div>
                                <div class="block__note">{data.member ? "срок прибавится к текущему" : "начнёт действовать сразу"}</div>
                            </div>
                        </div>
                    </div>

                    <div class="details__actions">
                        <button class="btn btn--ghost" on:click={close}>Закрыть</button>
                        <button class="btn" class:btn--disabled={notEnough} disabled={notEnough} on:click={buy}>
                            {data.member ? "Продлить" : "Купить"}
                        </button>
                    </div>
                    <div class="details__status" class:warn={message ? !ok : notEnough}>
                        {message || (notEnough ? "Не хватает наличных" : "Без тренировок форма понемногу уходит")}
                    </div>
                </section>
            {/key}
        {/if}
    </div>
</div>

<style>
    .jobcenter {
        --bg: rgba(12, 13, 16, 0.94);
        --panel: rgba(255, 255, 255, 0.035);
        --line: rgba(255, 255, 255, 0.08);
        --text: #f3f1ec;
        --muted: rgba(243, 241, 236, 0.55);
        --accent: #ffb400;
        --accent-dark: #1a1405;
        --ok: #58d68d;
        --bad: #ff5d52;

        position: absolute;
        top: 0; right: 0; bottom: 0; left: 0;
        display: flex;
        align-items: center;
        justify-content: center;
        background: radial-gradient(ellipse at center, rgba(0, 0, 0, 0.35), rgba(0, 0, 0, 0.7));
        font-family: 'TTNorms-Medium', 'UniNeue', sans-serif;
        color: var(--text);
        user-select: none;
    }

    @media (max-width: 1400px) { .jobcenter__panel { zoom: 80%; } }
    @media (min-width: 2400px) { .jobcenter__panel { zoom: 125%; } }

    .jobcenter__panel {
        position: relative;
        display: grid;
        grid-template-columns: 600px 420px;
        width: 1020px;
        height: 640px;
        background: var(--bg);
        border: 1px solid var(--line);
        border-radius: 14px;
        overflow: hidden;
        box-shadow: 0 30px 80px rgba(0, 0, 0, 0.55);
    }

    /* Сигнальная полоса «рабочей» тематики */
    .jobcenter__panel::before {
        content: "";
        position: absolute;
        left: 0; top: 0; right: 0;
        height: 4px;
        background: repeating-linear-gradient(-45deg, var(--accent) 0 14px, #111 14px 28px);
        opacity: 0.9;
    }

    /* ---------- Левая колонка ---------- */
    .jobcenter__board {
        display: flex;
        flex-direction: column;
        padding: 28px 24px 18px 28px;
        min-height: 0;
    }

    .jobcenter__head {
        display: flex;
        align-items: flex-end;
        justify-content: space-between;
        margin-bottom: 20px;
    }

    .jobcenter__eyebrow {
        font-size: 12px;
        letter-spacing: 0.12em;
        text-transform: uppercase;
        color: var(--accent);
        margin-bottom: 6px;
    }

    .jobcenter__title {
        margin: 0;
        font-family: 'RF Dewi Expanded', 'TTNorms-Bold', sans-serif;
        font-weight: 800;
        font-size: 24px;
        line-height: 1;
        white-space: nowrap;
        text-transform: uppercase;
    }

    .jobcenter__me {
        display: flex;
        text-align: right;
    }

    .jobcenter__me > div + div { margin-left: 18px; }

    .jobcenter__me > div { display: flex; flex-direction: column; }
    .jobcenter__me-label { white-space: nowrap; font-size: 11px; color: var(--muted); text-transform: uppercase; letter-spacing: 0.08em; }
    .jobcenter__me-value { font-family: 'RF Dewi Expanded', sans-serif; font-weight: 700; font-size: 22px; margin-top: 2px; }
    .jobcenter__me-value.small { font-family: 'TTNorms-Bold', sans-serif; font-size: 15px; margin-top: 6px; }

    .jobcenter__grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        grid-auto-rows: 1fr;
        gap: 10px;
        flex: 1;
        min-height: 0;
    }

    .tile {
        position: relative;
        border: 1px solid var(--line);
        border-radius: 10px;
        background-color: #16171b;
        background-size: cover;
        background-position: center;
        overflow: hidden;
        cursor: pointer;
        padding: 0;
        color: inherit;
        font: inherit;
        text-align: left;
        outline: none;
        transition: transform 0.15s ease, border-color 0.15s ease, filter 0.15s ease;
    }

    .tile:hover { transform: translateY(-2px); border-color: rgba(255, 180, 0, 0.45); }

    .tile--active {
        border-color: var(--accent);
        box-shadow: 0 0 0 1px var(--accent), 0 10px 24px rgba(255, 180, 0, 0.18);
    }

    .tile--locked { filter: grayscale(0.85) brightness(0.75); }
    .tile--locked.tile--active { filter: grayscale(0.4) brightness(0.9); }

    .tile__shade {
        position: absolute;
        top: 0; right: 0; bottom: 0; left: 0;
        background: linear-gradient(180deg, rgba(0, 0, 0, 0.05) 20%, rgba(0, 0, 0, 0.85) 100%);
    }

    .tile__lvl {
        position: absolute;
        top: 8px; right: 8px;
        padding: 3px 7px;
        border-radius: 5px;
        font-size: 11px;
        font-family: 'TTNorms-Bold', sans-serif;
        background: rgba(0, 0, 0, 0.6);
        border: 1px solid var(--line);
    }

    .tile__badge {
        position: absolute;
        top: 8px; left: 8px;
        padding: 3px 8px;
        border-radius: 5px;
        font-size: 11px;
        font-family: 'TTNorms-Bold', sans-serif;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        background: var(--accent);
        color: var(--accent-dark);
    }

    .tile__lock {
        position: absolute;
        top: 9px; left: 10px;
        width: 12px; height: 9px;
        border-radius: 2px;
        background: rgba(255, 255, 255, 0.85);
    }

    .tile__lock::before {
        content: "";
        position: absolute;
        left: 2px; top: -6px;
        width: 8px; height: 8px;
        border: 2px solid rgba(255, 255, 255, 0.85);
        border-bottom: none;
        border-radius: 5px 5px 0 0;
        box-sizing: border-box;
    }

    .tile__name {
        position: absolute;
        left: 12px; bottom: 22px;
        font-family: 'TTNorms-Bold', sans-serif;
        font-size: 16px;
    }

    .tile__skill {
        position: absolute;
        left: 12px; bottom: 11px;
        display: flex;
    }

    .tile__skill i + i { margin-left: 3px; }

    .tile__skill i {
        display: block;
        width: 16px; height: 3px;
        border-radius: 2px;
        background: rgba(255, 255, 255, 0.22);
    }

    .tile__skill i.on { background: var(--accent); }

    .jobcenter__hint {
        display: flex;
        margin-top: 14px;
        font-size: 12px;
        color: var(--muted);
    }

    .jobcenter__hint span + span { margin-left: 18px; }

    .jobcenter__hint b {
        font-family: 'TTNorms-Bold', sans-serif;
        font-weight: normal;
        color: var(--text);
        padding: 1px 6px;
        margin-right: 4px;
        border: 1px solid var(--line);
        border-radius: 4px;
    }

    /* ---------- Правая колонка ---------- */
    .details {
        display: flex;
        flex-direction: column;
        background: var(--panel);
        border-left: 1px solid var(--line);
        min-height: 0;
    }

    .details__hero {
        position: relative;
        height: 190px;
        flex-shrink: 0;
        background-size: cover;
        background-position: center;
    }

    .details__hero-shade {
        position: absolute;
        top: 0; right: 0; bottom: 0; left: 0;
        background: linear-gradient(180deg, rgba(12, 13, 16, 0) 30%, rgba(12, 13, 16, 0.96) 100%);
    }

    .details__close {
        position: absolute;
        top: 14px; right: 14px;
        width: 32px; height: 32px;
        border-radius: 8px;
        border: 1px solid var(--line);
        background: rgba(0, 0, 0, 0.55);
        color: var(--text);
        font-size: 14px;
        cursor: pointer;
    }

    .details__close:hover { border-color: var(--accent); color: var(--accent); }

    .details__name {
        position: absolute;
        left: 24px; bottom: 12px;
        margin: 0;
        font-family: 'RF Dewi Expanded', 'TTNorms-Bold', sans-serif;
        font-weight: 800;
        font-size: 22px;
        text-transform: uppercase;
    }

    .details__body {
        flex: 1;
        padding: 6px 24px 0;
        overflow-y: auto;
        min-height: 0;
    }

    .details__text {
        margin: 0 0 16px;
        font-size: 14px;
        line-height: 1.45;
        color: rgba(243, 241, 236, 0.8);
    }

    .row { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }

    .block {
        padding: 12px 14px;
        margin-bottom: 10px;
        border: 1px solid var(--line);
        border-radius: 10px;
        background: rgba(0, 0, 0, 0.25);
    }

    .block__title {
        font-size: 11px;
        text-transform: uppercase;
        letter-spacing: 0.1em;
        color: var(--muted);
        margin-bottom: 8px;
    }

    .block__note { font-size: 11px; color: var(--muted); margin-top: 6px; }

    .reqs { list-style: none; margin: 0; padding: 0; }

    .reqs li {
        display: flex;
        align-items: center;
        font-size: 13px;
        padding: 4px 0;
        color: var(--bad);
    }

    .reqs li span:nth-child(2) { color: var(--text); flex: 1; }
    .reqs li.ok { color: var(--ok); }

    .reqs__mark {
        width: 16px; height: 16px;
        border-radius: 50%;
        border: 2px solid currentColor;
        box-sizing: border-box;
        position: relative;
        flex-shrink: 0;
        margin-right: 10px;
    }

    .reqs__val { margin-left: 10px; }

    .reqs li.ok .reqs__mark::after {
        content: "";
        position: absolute;
        left: 3px; top: 0px;
        width: 4px; height: 8px;
        border: solid currentColor;
        border-width: 0 2px 2px 0;
        transform: rotate(45deg);
    }

    .reqs li:not(.ok) .reqs__mark::after {
        content: "";
        position: absolute;
        left: 5px; top: 2px;
        width: 2px; height: 8px;
        background: currentColor;
    }

    .reqs__val { font-size: 12px; color: var(--muted) !important; }

    .pay {
        font-family: 'TTNorms-Bold', sans-serif;
        font-size: 14px;
        line-height: 1.35;
        color: var(--accent);
    }

    .skill {
        height: 8px;
        border-radius: 4px;
        background: rgba(255, 255, 255, 0.08);
        overflow: hidden;
        margin-top: 4px;
    }

    .skill__bar {
        height: 100%;
        border-radius: 4px;
        background: linear-gradient(90deg, #ff8a00, var(--accent));
        transition: width 0.3s ease;
    }

    .details__actions {
        display: grid;
        grid-template-columns: 1fr 1.4fr;
        gap: 10px;
        padding: 14px 24px 6px;
    }

    .btn {
        height: 46px;
        border-radius: 10px;
        border: none;
        background: var(--accent);
        color: var(--accent-dark);
        font-family: 'TTNorms-Bold', sans-serif;
        font-size: 15px;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        cursor: pointer;
        transition: filter 0.15s ease, transform 0.1s ease;
    }

    .btn:hover { filter: brightness(1.1); }
    .btn:active { transform: scale(0.98); }

    .btn--ghost {
        background: transparent;
        color: var(--text);
        border: 1px solid var(--line);
    }

    .btn--ghost:hover { border-color: var(--accent); color: var(--accent); filter: none; }

    .btn--danger { background: var(--bad); color: #fff; }

    .btn--disabled,
    .btn--disabled:hover {
        background: rgba(255, 255, 255, 0.08);
        color: var(--muted);
        cursor: not-allowed;
        filter: none;
        transform: none;
    }

    .details__status {
        padding: 2px 24px 18px;
        font-size: 12px;
        color: var(--muted);
        text-align: right;
    }

    .details__status.warn { color: var(--bad); }

    .tile__perday {
        position: absolute;
        left: 12px;
        bottom: 6px;
        font-size: 11px;
        color: var(--muted);
        z-index: 2;
    }
    .pay--bad { color: var(--bad) !important; }
    .tile__icon {
        position: absolute;
        right: 14%;
        top: 22%;
        width: 44%;
        color: var(--accent);
        opacity: 0.85;
        z-index: 1;
    }
    .details__icon {
        position: absolute;
        right: 10%;
        top: 14%;
        width: 34%;
        color: var(--accent);
        opacity: 0.8;
    }
    .tile__icon :global(svg), .details__icon :global(svg) { width: 100%; height: auto; display: block; }
</style>
