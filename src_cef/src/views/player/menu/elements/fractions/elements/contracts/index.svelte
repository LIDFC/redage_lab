<script>
    import { executeClientToGroup } from "api/rage";
    import { addListernEvent } from "api/functions";
    import { sound, playSound } from "api/uiSound";
    import { onDestroy } from "svelte";
    import { fade } from "svelte/transition";
    import { materialIcon, money, number, duration, parseJson } from "@/views/fractions/contracts/materials";

    // Строительные подряды организации: общий набор контрактов, свои активные, принять/отменить, маршруты.
    let data = null;
    let tab = "list";
    let selectedId = 0;
    let confirmCancel = 0;
    let loadedAt = Date.now();
    let now = Date.now();

    const timer = setInterval(() => (now = Date.now()), 1000);
    onDestroy(() => clearInterval(timer));

    addListernEvent("table.contracts", (json) => {
        const previous = data;
        data = parseJson(json, null);
        loadedAt = Date.now();
        confirmCancel = 0;
        if (!data) return;
        if (data.message) playSound(data.ok ? "success" : "error");
        else if (previous && data.ok === false) playSound("error");
        // после принятия — сразу во вкладку «Мои подряды»
        if (data.ok && data.message && previous && data.contracts.find((c) => c.status === "mine" && c.id === selectedId))
            tab = "mine";
        if (!selectedId || !data.contracts.find((c) => c.id === selectedId)) {
            const source = data.contracts.filter((c) => (tab === "mine") === (c.status === "mine"));
            const first = source[0] || data.contracts[0];
            selectedId = first ? first.id : 0;
        }
    });
    executeClientToGroup("contractsLoad");

    const types = {
        RoadConstruction: "Дороги",
        BuildingConstruction: "Строительство",
        BridgeConstruction: "Мосты",
        InfrastructureRepair: "Инфраструктура",
        WarehouseConstruction: "Склады",
    };

    $: contracts = data ? data.contracts : [];
    $: mine = contracts.filter((c) => c.status === "mine");
    $: list = tab === "mine" ? mine : contracts.filter((c) => c.status !== "mine");
    $: selected = contracts.find((c) => c.id === selectedId);
    $: elapsed = (now - loadedAt) / 1000;

    const select = (c) => {
        selectedId = c.id;
        confirmCancel = 0;
    };
    const setTab = (value) => {
        tab = value;
        const source = value === "mine" ? mine : contracts.filter((c) => c.status !== "mine");
        if (!source.find((c) => c.id === selectedId)) selectedId = source.length ? source[0].id : 0;
    };

    const action = (name, id) => executeClientToGroup("contractsAction", name, id);
    const onCancel = (c) => {
        if (confirmCancel !== c.id) {
            confirmCancel = c.id;
            return;
        }
        action("cancel", c.id);
    };

    const canAccept = (c) => data && data.legal && data.canManage && c.status === "available" && data.reputation >= c.requiredRep && data.active < data.maxActive && data.money >= 0;
    const acceptHint = (c) => {
        if (!data.legal) return "Только для законных организаций";
        if (!data.canManage) return "Нужно право «Строительные подряды»";
        if (data.money < 0) return "Бюджет организации в минусе";
        if (data.reputation < c.requiredRep) return `Нужна репутация ${c.requiredRep}`;
        if (data.active >= data.maxActive) return `Уже ${data.active} из ${data.maxActive} активных`;
        return "";
    };
    const minutes = (m) => (m >= 60 ? `${Math.floor(m / 60)} ч ${m % 60 ? (m % 60) + " мин" : ""}` : `${m} мин`);
</script>

<div class="octr" in:fade={{ duration: 150 }}>
    {#if !data}
        <div class="octr__empty">Загрузка…</div>
    {:else if !data.legal}
        <div class="octr__empty">
            <b>Подряды недоступны</b>
            <span>Строительные подряды берут только законные организации («Сообщество»). Криминальные группировки к ним не допускаются.</span>
        </div>
    {:else}
        <div class="octr__head">
            <div class="octr__title">
                <h2>Строительные подряды</h2>
                <span>Общие контракты сервера · новые через {duration(data.nextGen - elapsed)}</span>
            </div>
            <div class="octr__stat"><span>Репутация</span><b class="accent">{number(data.reputation)}</b></div>
            <div class="octr__stat"><span>Бюджет</span><b class:red={data.money < 0}>{money(data.money)}</b></div>
            <div class="octr__stat"><span>В работе</span><b>{data.active} / {data.maxActive}</b></div>
        </div>

        <div class="octr__tabs">
            <div class="octr__tab" use:sound={"tap"} class:active={tab === "list"} on:click={() => setTab("list")}>Доступные</div>
            <div class="octr__tab" use:sound={"tap"} class:active={tab === "mine"} on:click={() => setTab("mine")}>Мои подряды{#if mine.length}<i>{mine.length}</i>{/if}</div>
            {#if data.message}
                <div class="octr__msg" class:ok={data.ok}>{data.message}</div>
            {/if}
        </div>

        <div class="octr__body">
            <div class="octr__list">
                {#each list as c (c.id)}
                    <div class="octr__card" class:active={c.id === selectedId} class:taken={c.status === "taken"} use:sound={"tap"} on:click={() => select(c)}>
                        <div class="octr__card_top">
                            <span class="octr__tag">{types[c.type] || "Подряд"}</span>
                            <span class="octr__id">#{c.id}</span>
                        </div>
                        <b>{c.title}</b>
                        <span class="octr__point">{c.point}</span>
                        <div class="octr__card_bottom">
                            <span class="octr__reward">{money(c.reward)}</span>
                            {#if c.status === "taken"}
                                <span class="octr__badge grey">Взят: {c.takenBy}</span>
                            {:else if c.status === "mine"}
                                <span class="octr__badge">{c.progress}% · {duration(c.deadlineLeft - elapsed)}</span>
                            {:else}
                                <span class="octr__badge" class:red={data.reputation < c.requiredRep}>Реп. {c.requiredRep}</span>
                            {/if}
                        </div>
                        {#if c.status === "mine"}
                            <div class="octr__bar"><div style="width:{c.progress}%"></div></div>
                        {/if}
                    </div>
                {:else}
                    <div class="octr__empty small">
                        {tab === "mine" ? "У организации нет активных подрядов" : "Свободных подрядов нет — дождитесь следующего набора"}
                    </div>
                {/each}
            </div>

            <div class="octr__details">
                {#if selected}
                    {#key selected.id}
                        <div class="octr__scroll" in:fade={{ duration: 120 }}>
                            <div class="octr__eyebrow">Строительный подряд #{selected.id} · {types[selected.type] || selected.type}</div>
                            <h3>{selected.title}</h3>
                            <p>{selected.description}</p>

                            {#if selected.status === "taken"}
                                <div class="octr__notice">Контракт уже взят другой организацией: {selected.takenBy}</div>
                            {/if}

                            <div class="octr__grid">
                                <div><span>Награда</span><b class="green">{money(selected.reward)}</b></div>
                                <div><span>Неустойка</span><b class="red">{money(selected.penalty)}</b></div>
                                <div><span>Мин. репутация</span><b class:red={data.reputation < selected.requiredRep}>{selected.requiredRep}</b></div>
                                <div><span>Репутация</span><b>+{selected.repReward} / −{selected.repPenalty}</b></div>
                                <div>
                                    <span>{selected.status === "mine" ? "Осталось" : "Срок"}</span>
                                    <b class:red={selected.status === "mine" && selected.deadlineLeft - elapsed < 900}>
                                        {selected.status === "mine" ? duration(selected.deadlineLeft - elapsed) : minutes(selected.deadlineMinutes)}
                                    </b>
                                </div>
                                <div><span>Место сдачи</span><b>{selected.point}</b></div>
                            </div>

                            <div class="octr__section">
                                <div class="octr__section_title">
                                    Необходимые материалы
                                    {#if selected.status === "mine"}<em>Прогресс {selected.progress}%</em>{:else}<em>Закупка ≈ {money(selected.cost)}</em>{/if}
                                </div>
                                {#each selected.materials as m (m.id)}
                                    <div class="octr__mat">
                                        <div class="octr__icon" style="color:{materialIcon(m.icon).color}">
                                            <svg viewBox="0 0 24 24">{@html materialIcon(m.icon).svg}</svg>
                                        </div>
                                        <div class="octr__mat_info">
                                            <div class="octr__mat_row">
                                                <b>{m.name}</b>
                                                <span>{#if selected.status === "mine"}{number(m.delivered)} / {number(m.required)}{:else}{number(m.required)}{/if}</span>
                                            </div>
                                            {#if selected.status === "mine"}
                                                <div class="octr__bar two">
                                                    <div class="bought" style="width:{Math.min(100, (m.purchased / m.required) * 100)}%"></div>
                                                    <div style="width:{Math.min(100, (m.delivered / m.required) * 100)}%"></div>
                                                </div>
                                            {/if}
                                            <small>
                                                {number(m.required * m.kg)} кг{#if selected.status === "mine"} · куплено {number(m.purchased)}{/if}
                                                · {m.shops && m.shops.length ? m.shops.join(", ") : "нет складов"}
                                            </small>
                                        </div>
                                    </div>
                                {/each}
                            </div>

                            <div class="octr__section">
                                <div class="octr__section_title">Транспорт <em>Общий вес {number(selected.totalKg)} кг</em></div>
                                <div class="octr__transport">
                                    <b>Организационный грузовой автомобиль</b>
                                    <span>Только транспорт из гаража организации — купить его можно в «Грузовом автосалоне» кнопкой «Купить (ОРГ)». Груз можно возить несколькими рейсами.</span>
                                    <div class="octr__models">
                                        {#each data.vehicles as v}
                                            <div><b>{v.name}</b><span>{v.slots} пал. · {number(v.kg)} кг</span></div>
                                        {/each}
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div class="octr__actions">
                            {#if selected.status === "available"}
                                <div class="octr__btn primary" class:disabled={!canAccept(selected)} on:click={() => canAccept(selected) && action("accept", selected.id)}>
                                    Принять подряд
                                </div>
                                {#if !canAccept(selected)}<span class="octr__hint">{acceptHint(selected)}</span>{/if}
                            {:else if selected.status === "mine"}
                                <div class="octr__btn primary" use:sound={"tap"} on:click={() => action("route", selected.id)}>Маршрут к точке</div>
                                <div class="octr__btn" use:sound={"tap"} on:click={() => action("shop", selected.id)}>Ближайший склад</div>
                                {#if selected.isAcceptor}
                                    <div class="octr__btn danger" on:click={() => onCancel(selected)}>
                                        {confirmCancel === selected.id ? `Точно? Неустойка ${money(selected.penalty)}` : "Отменить"}
                                    </div>
                                {:else}
                                    <span class="octr__hint">Принял: {selected.acceptedBy}. Отменить может только он.</span>
                                {/if}
                            {/if}
                        </div>
                    {/key}
                {:else}
                    <div class="octr__empty small">Выберите подряд слева</div>
                {/if}
            </div>
        </div>
    {/if}
</div>

<style>
    .octr {
        width: 100%;
        height: 100%;
        display: flex;
        flex-direction: column;
        color: #e9edf3;
        font-family: "Gilroy", "TTNorms-Regular", sans-serif;
        min-height: 0;
    }
    .octr__head {
        display: flex;
        align-items: center;
        gap: 1.2vh;
    }
    .octr__title {
        flex: 1;
    }
    .octr__title h2 {
        margin: 0;
        font-size: 2.6vh;
        font-weight: 700;
    }
    .octr__title span {
        font-size: 1.3vh;
        color: #8b93a1;
    }
    .octr__stat {
        padding: 0.9vh 1.4vh;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.04);
        border: 1px solid rgba(255, 255, 255, 0.05);
        text-align: right;
        min-width: 10vh;
    }
    .octr__stat span {
        display: block;
        font-size: 1.1vh;
        color: #8b93a1;
    }
    .octr__stat b {
        font-size: 1.8vh;
    }
    .accent {
        color: #f5a524;
    }
    .green {
        color: #7ed321;
    }
    .red {
        color: #ff6b6b !important;
    }
    .octr__tabs {
        display: flex;
        align-items: center;
        gap: 0.8vh;
        margin: 1.8vh 0 1.4vh;
    }
    .octr__tab {
        padding: 0.8vh 1.6vh;
        border-radius: 0.9vh;
        font-size: 1.4vh;
        color: #8b93a1;
        background: rgba(255, 255, 255, 0.03);
        cursor: pointer;
        display: flex;
        align-items: center;
        gap: 0.6vh;
    }
    .octr__tab i {
        font-style: normal;
        font-size: 1.1vh;
        padding: 0.1vh 0.6vh;
        border-radius: 0.6vh;
        background: #f5a524;
        color: #16181d;
    }
    .octr__tab.active {
        color: #fff;
        background: rgba(245, 165, 36, 0.14);
    }
    .octr__msg {
        margin-left: auto;
        font-size: 1.3vh;
        color: #ff6b6b;
    }
    .octr__msg.ok {
        color: #7ed321;
    }
    .octr__body {
        flex: 1;
        min-height: 0;
        display: flex;
        gap: 1.6vh;
    }
    .octr__list {
        width: 38%;
        overflow-y: auto;
        display: flex;
        flex-direction: column;
        gap: 0.9vh;
        padding-right: 0.4vh;
    }
    .octr__card {
        padding: 1.3vh 1.4vh;
        border-radius: 1.1vh;
        background: rgba(255, 255, 255, 0.03);
        border: 1px solid rgba(255, 255, 255, 0.05);
        display: flex;
        flex-direction: column;
        gap: 0.5vh;
        cursor: pointer;
        transition: background 0.15s;
    }
    .octr__card:hover {
        background: rgba(255, 255, 255, 0.06);
    }
    .octr__card.active {
        border-color: rgba(245, 165, 36, 0.6);
        background: rgba(245, 165, 36, 0.07);
    }
    .octr__card.taken {
        opacity: 0.55;
    }
    .octr__card b {
        font-size: 1.5vh;
    }
    .octr__card_top,
    .octr__card_bottom {
        display: flex;
        justify-content: space-between;
        align-items: center;
    }
    .octr__tag {
        font-size: 1vh;
        text-transform: uppercase;
        letter-spacing: 0.1vh;
        color: #f5a524;
    }
    .octr__id,
    .octr__point {
        font-size: 1.2vh;
        color: #8b93a1;
    }
    .octr__reward {
        font-size: 1.6vh;
        font-weight: 700;
        color: #7ed321;
    }
    .octr__badge {
        font-size: 1.15vh;
        padding: 0.3vh 0.8vh;
        border-radius: 0.6vh;
        background: rgba(245, 165, 36, 0.14);
        color: #f5a524;
    }
    .octr__badge.grey {
        background: rgba(255, 255, 255, 0.06);
        color: #aab1bd;
    }
    .octr__badge.red {
        background: rgba(255, 107, 107, 0.12);
    }
    .octr__bar {
        position: relative;
        height: 0.5vh;
        border-radius: 0.3vh;
        background: rgba(255, 255, 255, 0.07);
        overflow: hidden;
    }
    .octr__bar div {
        position: absolute;
        left: 0;
        top: 0;
        bottom: 0;
        background: #7ed321;
        border-radius: 0.3vh;
    }
    .octr__bar.two {
        height: 0.6vh;
    }
    .octr__bar .bought {
        background: rgba(245, 165, 36, 0.45);
    }
    .octr__details {
        flex: 1;
        min-width: 0;
        display: flex;
        flex-direction: column;
        border-radius: 1.3vh;
        background: rgba(255, 255, 255, 0.025);
        border: 1px solid rgba(255, 255, 255, 0.05);
        overflow: hidden;
    }
    .octr__scroll {
        flex: 1;
        overflow-y: auto;
        padding: 2vh 2.2vh;
    }
    .octr__eyebrow {
        font-size: 1.1vh;
        text-transform: uppercase;
        letter-spacing: 0.1vh;
        color: #8b93a1;
    }
    .octr__scroll h3 {
        margin: 0.5vh 0 0.6vh;
        font-size: 2.2vh;
    }
    .octr__scroll p {
        margin: 0;
        font-size: 1.4vh;
        color: #aab1bd;
    }
    .octr__notice {
        margin-top: 1.2vh;
        padding: 1vh 1.2vh;
        border-radius: 0.9vh;
        background: rgba(255, 255, 255, 0.05);
        font-size: 1.3vh;
        color: #aab1bd;
    }
    .octr__grid {
        margin-top: 1.6vh;
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: 0.8vh;
    }
    .octr__grid div {
        padding: 1vh 1.2vh;
        border-radius: 0.9vh;
        background: rgba(255, 255, 255, 0.04);
        display: flex;
        flex-direction: column;
        gap: 0.3vh;
        min-width: 0;
    }
    .octr__grid span {
        font-size: 1.1vh;
        color: #8b93a1;
    }
    .octr__grid b {
        font-size: 1.5vh;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
    }
    .octr__section {
        margin-top: 1.8vh;
    }
    .octr__section_title {
        display: flex;
        justify-content: space-between;
        font-size: 1.2vh;
        text-transform: uppercase;
        letter-spacing: 0.1vh;
        color: #8b93a1;
        margin-bottom: 0.8vh;
    }
    .octr__section_title em {
        font-style: normal;
        text-transform: none;
        letter-spacing: 0;
        color: #f5a524;
    }
    .octr__mat {
        display: flex;
        align-items: center;
        gap: 1.2vh;
        padding: 0.9vh 0;
        border-bottom: 1px solid rgba(255, 255, 255, 0.04);
    }
    .octr__icon {
        width: 4.2vh;
        height: 4.2vh;
        flex: none;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.05);
        display: flex;
        align-items: center;
        justify-content: center;
    }
    .octr__icon svg {
        width: 60%;
        height: 60%;
        fill: none;
        stroke: currentColor;
        stroke-width: 1.7;
        stroke-linecap: round;
        stroke-linejoin: round;
    }
    .octr__mat_info {
        flex: 1;
        min-width: 0;
        display: flex;
        flex-direction: column;
        gap: 0.4vh;
    }
    .octr__mat_row {
        display: flex;
        justify-content: space-between;
        font-size: 1.45vh;
    }
    .octr__mat_row span {
        font-weight: 600;
    }
    .octr__mat small {
        font-size: 1.15vh;
        color: #8b93a1;
    }
    .octr__transport {
        padding: 1.2vh 1.4vh;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.04);
        display: flex;
        flex-direction: column;
        gap: 0.5vh;
    }
    .octr__transport > b {
        font-size: 1.45vh;
    }
    .octr__transport > span {
        font-size: 1.2vh;
        color: #8b93a1;
    }
    .octr__models {
        margin-top: 0.6vh;
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: 0.6vh;
    }
    .octr__models div {
        padding: 0.7vh 0.9vh;
        border-radius: 0.7vh;
        background: rgba(255, 255, 255, 0.04);
        display: flex;
        flex-direction: column;
    }
    .octr__models b {
        font-size: 1.2vh;
        font-weight: 600;
    }
    .octr__models span {
        font-size: 1.05vh;
        color: #8b93a1;
    }
    .octr__actions {
        display: flex;
        align-items: center;
        gap: 0.9vh;
        padding: 1.4vh 2.2vh;
        border-top: 1px solid rgba(255, 255, 255, 0.05);
        min-height: 4vh;
    }
    .octr__btn {
        padding: 1.1vh 1.8vh;
        border-radius: 0.9vh;
        font-size: 1.4vh;
        font-weight: 700;
        cursor: pointer;
        background: rgba(255, 255, 255, 0.07);
        color: #e9edf3;
        transition: filter 0.15s;
    }
    .octr__btn:hover {
        filter: brightness(1.15);
    }
    .octr__btn.primary {
        background: #f5a524;
        color: #16181d;
    }
    .octr__btn.danger {
        margin-left: auto;
        background: rgba(255, 107, 107, 0.14);
        color: #ff6b6b;
    }
    .octr__btn.disabled {
        background: rgba(255, 255, 255, 0.06);
        color: #6b7280;
        cursor: default;
        filter: none;
    }
    .octr__hint {
        font-size: 1.25vh;
        color: #8b93a1;
    }
    .octr__empty {
        margin: auto;
        text-align: center;
        display: flex;
        flex-direction: column;
        gap: 1vh;
        max-width: 60vh;
    }
    .octr__empty b {
        font-size: 2.2vh;
    }
    .octr__empty span {
        font-size: 1.5vh;
        color: #8b93a1;
    }
    .octr__empty.small {
        margin: 3vh auto;
        font-size: 1.4vh;
        color: #8b93a1;
    }
</style>
