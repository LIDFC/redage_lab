<script>
    import { executeClient } from 'api/rage'
    import { charLVL, charWorkID } from 'store/chars';
    import { hasJsonStructure } from "api/functions";
    import { fade, fly } from 'svelte/transition';

    import imgElectro from './assets/images/electro.svg';
    import imgKosilka from './assets/images/kosilka.svg';
    import imgPochta from './assets/images/pochta.svg';
    import imgTaxi from './assets/images/taxi.svg';
    import imgBus from './assets/images/bus.svg';
    import imgMechanic from './assets/images/mechanic.svg';
    import imgTruck from './assets/images/truck.svg';
    import imgInca from './assets/images/inca.svg';

    export let viewData;

    // Статическая часть: описание и картинка. Требования, оплату и прогресс присылает сервер
    // (WorkManager.GetJobMenuData), чтобы интерфейс не расходился с реальной логикой.
    const JOBS = [
        { id: 1, name: "Электрик", image: imgElectro, text: "Ремонт электрощитков на подстанции. Работа пешком — транспорт и права не нужны." },
        { id: 5, name: "Газонокосильщик", image: imgKosilka, text: "Стрижка газона на поле гольф-клуба. За каждый пройденный круг — бонус." },
        { id: 2, name: "Почтальон", image: imgPochta, text: "Развоз посылок по жилым домам. Последние метры до двери — пешком с посылкой в руках." },
        { id: 3, name: "Таксист", image: imgTaxi, text: "Заказы от игроков и NPC-пассажиров в телефоне. Навык открывает более дорогие машины." },
        { id: 4, name: "Водитель автобуса", image: imgBus, text: "Городские маршруты с остановками. Плата за проезд уходит в казну мэрии." },
        { id: 8, name: "Автомеханик", image: imgMechanic, text: "Ремонт и заправка машин по вызовам из телефона: /repair и /sellfuel." },
        { id: 6, name: "Дальнобойщик", image: imgTruck, text: "Доставка товара в бизнесы по заказам их владельцев. Заказы — в телефоне." },
        { id: 7, name: "Инкассатор", image: imgInca, text: "Развоз сумок с деньгами по банкоматам города на броневике." },
    ];

    const MAX_SKILL = 5;

    let data = { lvl: 0, workId: 0, onWork: false, jobs: [] };

    if (hasJsonStructure(viewData))
        viewData = JSON.parse(viewData);

    if (viewData && Array.isArray(viewData.jobs)) {
        data = viewData;
    } else if (viewData && typeof viewData === "object") {
        // Совместимость со старым форматом: { jobId: minLvl }
        data.jobs = Object.keys(viewData).map((id) => ({ id: Number(id), minLvl: viewData[id], hasLic: true, hasRentLic: true, skillLvl: 0 }));
    }

    const serverJob = (id) => data.jobs.find((j) => j.id === id) || { id, minLvl: 0, hasLic: true, hasRentLic: true, skillLvl: 0 };

    $: jobs = JOBS.map((job) => ({ ...job, ...serverJob(job.id) }));

    let selectedId = $charWorkID > 0 ? $charWorkID : JOBS[0].id;
    $: selected = jobs.find((j) => j.id === selectedId) || jobs[0];

    const isLocked = (job, lvl) => lvl < job.minLvl || !job.hasLic;

    const getAction = (job, lvl, workId) => {
        if (job.id === workId) {
            if (data.onWork && workId === data.workId)
                return { type: "leave", disabled: true, hint: "Сначала завершите рабочую смену (/fjob)" };
            return { type: "leave", disabled: false, hint: "Вы работаете здесь" };
        }
        if (workId > 0)
            return { type: "join", disabled: true, hint: "Сначала уволитесь с текущей работы" };
        if (lvl < job.minLvl)
            return { type: "join", disabled: true, hint: `Нужен ${job.minLvl} уровень персонажа` };
        if (!job.hasLic)
            return { type: "join", disabled: true, hint: `Нужны права категории ${job.lic} — автошкола` };
        return { type: "join", disabled: false, hint: "Метка на базу появится на карте" };
    };

    $: action = getAction(selected, $charLVL, $charWorkID);

    const skillPercent = (job) => {
        if (!job.nextPoints || job.skillLvl >= MAX_SKILL) return 100;
        return Math.max(0, Math.min(100, Math.round((job.skillPoints / job.nextPoints) * 100)));
    };

    const onAction = () => {
        if (action.disabled) return;
        executeClient("selectJob", action.type === "leave" ? -1 : selected.id);
    };

    const onWaypoint = () => {
        if (!selected.x && !selected.y) return;
        executeClient("createWaypoint", selected.x, selected.y);
    };

    const closeJobMenu = () => executeClient("closeJobMenu");

    const onKeyDown = (event) => {
        if (event.key === "Escape") {
            closeJobMenu();
            return;
        }
        if (event.key === "ArrowDown" || event.key === "ArrowUp" || event.key === "ArrowLeft" || event.key === "ArrowRight") {
            const index = JOBS.findIndex((j) => j.id === selectedId);
            const step = event.key === "ArrowDown" ? 2 : event.key === "ArrowUp" ? -2 : event.key === "ArrowRight" ? 1 : -1;
            const next = (index + step + JOBS.length) % JOBS.length;
            selectedId = JOBS[next].id;
            event.preventDefault();
        }
        if (event.key === "Enter")
            onAction();
    };
</script>

<svelte:window on:keydown={onKeyDown} />

<div class="jobcenter" in:fade={{ duration: 180 }}>
    <div class="jobcenter__panel" in:fly={{ y: 24, duration: 260 }}>

        <!-- Левая колонка: список вакансий -->
        <section class="jobcenter__board">
            <header class="jobcenter__head">
                <div>
                    <div class="jobcenter__eyebrow">Мэрия Los Santos · Эмма Смит</div>
                    <h1 class="jobcenter__title">Центр занятости</h1>
                </div>
                <div class="jobcenter__me">
                    <div class="jobcenter__me-lvl">
                        <span class="jobcenter__me-label">Уровень</span>
                        <span class="jobcenter__me-value">{$charLVL}</span>
                    </div>
                    <div class="jobcenter__me-job">
                        <span class="jobcenter__me-label">Текущая работа</span>
                        <span class="jobcenter__me-value small">{$charWorkID > 0 ? (JOBS.find((j) => j.id === $charWorkID) || {}).name || "—" : "Безработный"}</span>
                    </div>
                </div>
            </header>

            <div class="jobcenter__grid">
                {#each jobs as job (job.id)}
                    <button
                        class="tile"
                        class:tile--active={job.id === selectedId}
                        class:tile--locked={isLocked(job, $charLVL)}
                        class:tile--current={job.id === $charWorkID}
                        style="background-image: url('{job.image}')"
                        on:click={() => (selectedId = job.id)}>
                        <span class="tile__shade"></span>
                        <span class="tile__lvl">LVL {job.minLvl}+</span>
                        {#if job.id === $charWorkID}
                            <span class="tile__badge">Ваша работа</span>
                        {:else if isLocked(job, $charLVL)}
                            <span class="tile__lock" aria-label="Закрыто"></span>
                        {/if}
                        <span class="tile__name">{job.name}</span>
                        <span class="tile__skill">
                            {#each Array(MAX_SKILL) as _, i}
                                <i class:on={i < job.skillLvl}></i>
                            {/each}
                        </span>
                    </button>
                {/each}
            </div>

            <footer class="jobcenter__hint">
                <span><b>↑↓←→</b> выбор</span>
                <span><b>Enter</b> устроиться</span>
                <span><b>Esc</b> закрыть</span>
            </footer>
        </section>

        <!-- Правая колонка: подробности -->
        {#key selected.id}
            <section class="details" in:fade={{ duration: 160 }}>
                <div class="details__hero" style="background-image: url('{selected.image}')">
                    <span class="details__hero-shade"></span>
                    <button class="details__close" on:click={closeJobMenu} aria-label="Закрыть">✕</button>
                    <h2 class="details__name">{selected.name}</h2>
                </div>

                <div class="details__body">
                    <p class="details__text">{selected.text}</p>

                    <div class="block">
                        <div class="block__title">Требования</div>
                        <ul class="reqs">
                            <li class:ok={$charLVL >= selected.minLvl}>
                                <span class="reqs__mark"></span>
                                <span>Уровень персонажа {selected.minLvl}+</span>
                                <span class="reqs__val">у вас {$charLVL}</span>
                            </li>
                            {#if selected.lic}
                                <li class:ok={selected.hasLic}>
                                    <span class="reqs__mark"></span>
                                    <span>Права категории {selected.lic}</span>
                                    <span class="reqs__val">{selected.hasLic ? "есть" : "нет"}</span>
                                </li>
                            {/if}
                            {#if selected.rentLic && selected.rentLic !== selected.lic}
                                <li class:ok={selected.hasRentLic}>
                                    <span class="reqs__mark"></span>
                                    <span>Права {selected.rentLic} для аренды транспорта</span>
                                    <span class="reqs__val">{selected.hasRentLic ? "есть" : "нет"}</span>
                                </li>
                            {/if}
                            {#if !selected.lic && !selected.rentLic}
                                <li class="ok">
                                    <span class="reqs__mark"></span>
                                    <span>Права не нужны</span>
                                </li>
                            {/if}
                        </ul>
                    </div>

                    <div class="row">
                        <div class="block">
                            <div class="block__title">Оплата</div>
                            <div class="pay">{selected.pay || "—"}</div>
                            <div class="block__note">без учёта VIP и бонуса навыка</div>
                        </div>
                        <div class="block">
                            <div class="block__title">Навык · ур. {selected.skillLvl}/{MAX_SKILL}</div>
                            <div class="skill">
                                <div class="skill__bar" style="width: {skillPercent(selected)}%"></div>
                            </div>
                            <div class="block__note">
                                {#if selected.skillLvl >= MAX_SKILL}
                                    максимальный уровень
                                {:else if selected.nextPoints}
                                    {selected.skillPoints || 0} / {selected.nextPoints} до следующего
                                {:else}
                                    растёт с каждым выполненным заданием
                                {/if}
                            </div>
                        </div>
                    </div>
                </div>

                <div class="details__actions">
                    <button class="btn btn--ghost" on:click={onWaypoint}>Метка на базу</button>
                    <button
                        class="btn"
                        class:btn--danger={action.type === "leave"}
                        class:btn--disabled={action.disabled}
                        disabled={action.disabled}
                        on:click={onAction}>
                        {action.type === "leave" ? "Уволиться" : "Устроиться"}
                    </button>
                </div>
                <div class="details__status" class:warn={action.disabled}>{action.hint}</div>
            </section>
        {/key}
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
</style>
