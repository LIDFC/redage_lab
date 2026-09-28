<script>
    import { executeClient } from "api/rage";
    import { sound, playSound } from "api/uiSound";
    import { onDestroy } from "svelte";
    import { fade, scale } from "svelte/transition";
    import { materialIcon, money, number, duration, parseJson } from "../contracts/materials";

    // Государственный склад стройматериалов: закупка под активные подряды организации за её бюджет.
    export let viewData;

    let data = parseJson(viewData, { org: "", money: 0, contracts: [] });
    let selectedId = data.contracts.length ? data.contracts[0].id : 0;
    let amounts = {};
    let busy = false;
    let lastResult = null;

    window.events.addEvent("cef.orgcontracts.shop.update", (ok, message, json) => {
        busy = false;
        data = parseJson(json, data);
        lastResult = { ok, message };
        if (!data.contracts.find((c) => c.id === selectedId))
            selectedId = data.contracts.length ? data.contracts[0].id : 0;
        amounts = {};
        playSound(ok ? "success" : "error");
    });
    onDestroy(() => window.events.removeEvent("cef.orgcontracts.shop.update"));

    let now = Date.now();
    const opened = Date.now();
    const timer = setInterval(() => (now = Date.now()), 1000);
    onDestroy(() => clearInterval(timer));

    $: contract = data.contracts.find((c) => c.id === selectedId);

    const key = (m) => `${selectedId}_${m.id}`;
    const getAmount = (m, all) => {
        const value = all[key(m)];
        return value === undefined ? Math.min(m.left, m.pallet) : value;
    };
    const setAmount = (m, value) => {
        value = Math.max(0, Math.min(m.left, Math.round(value) || 0));
        amounts = { ...amounts, [key(m)]: value };
    };
    const step = (m, dir) => {
        const current = getAmount(m, amounts);
        // шаг — паллета, «хвост» докупается точным количеством
        const next = dir > 0 ? Math.min(m.left, (Math.floor(current / m.pallet) + 1) * m.pallet) : Math.max(0, (Math.ceil(current / m.pallet) - 1) * m.pallet);
        setAmount(m, next);
    };
    const pallets = (m, units) => Math.ceil(units / Math.max(1, m.pallet));

    const buy = (m) => {
        const units = getAmount(m, amounts);
        if (busy || units <= 0 || !contract) return;
        busy = true;
        lastResult = null;
        executeClient("client.orgcontracts.shop.buy", contract.id, m.id, units);
        setTimeout(() => (busy = false), 3000);
    };

    const close = () => executeClient("client.orgcontracts.shop.close");
    const onKey = (e) => {
        if (e.keyCode === 27) close();
    };
</script>

<svelte:window on:keyup={onKey} />

<div class="cshop" in:fade={{ duration: 150 }}>
    <div class="cshop__box" in:scale={{ start: 0.97, duration: 200 }}>
        <div class="cshop__head">
            <div class="cshop__logo">
                <svg viewBox="0 0 24 24"><path d="M3 21h18M5 21V10l7-5 7 5v11"/><path d="M9 21v-6h6v6"/></svg>
            </div>
            <div class="cshop__title">
                <div class="cshop__eyebrow">Государственный склад</div>
                <h1>{data.shop || "Строительные материалы"}</h1>
            </div>
            <div class="cshop__budget" class:debt={data.money < 0}>
                <span>Бюджет · {data.org}</span>
                <b>{money(data.money)}</b>
            </div>
            <div class="cshop__close" use:sound={"tap"} on:click={close} title="Закрыть (Esc)">✕</div>
        </div>

        {#if !data.contracts.length}
            <div class="cshop__empty">
                <b>Нет активных подрядов</b>
                <span>Материалы продаются только под подряд организации. Примите подряд: планшет → Организация → Подряды.</span>
            </div>
        {:else}
            <div class="cshop__body">
                <div class="cshop__side">
                    <div class="cshop__label">Подряды организации</div>
                    {#each data.contracts as item}
                        <div class="cshop__contract" class:active={item.id === selectedId} use:sound={"tap"} on:click={() => (selectedId = item.id)}>
                            <b>#{item.id} {item.title}</b>
                            <span>{item.point}</span>
                            <em>Осталось {duration(item.deadline - (now - opened) / 1000)}</em>
                        </div>
                    {/each}
                </div>

                <div class="cshop__main">
                    {#if contract}
                        <div class="cshop__label">Материалы по подряду #{contract.id}</div>
                        {#each contract.materials as m (m.id)}
                            <div class="cshop__row" class:done={m.left <= 0}>
                                <div class="cshop__icon" style="color:{materialIcon(m.icon || m.id).color}">
                                    <svg viewBox="0 0 24 24">{@html materialIcon(m.icon || m.id).svg}</svg>
                                </div>
                                <div class="cshop__info">
                                    <b>{m.name}</b>
                                    <span>{money(m.price)} / ед. · {m.kg} кг · паллета {m.pallet} ед.</span>
                                    <div class="cshop__bar">
                                        <div class="bought" style="width:{Math.min(100, (m.purchased / m.required) * 100)}%"></div>
                                        <div class="delivered" style="width:{Math.min(100, (m.delivered / m.required) * 100)}%"></div>
                                    </div>
                                    <small>Куплено {number(m.purchased)} / {number(m.required)} · сдано {number(m.delivered)}</small>
                                </div>
                                {#if m.left > 0 && m.sold === false}
                                    <div class="cshop__elsewhere">
                                        <span>Здесь не продаётся</span>
                                        <b>{m.where && m.where.length ? m.where.join(", ") : "Нет на других складах"}</b>
                                    </div>
                                {:else if m.left > 0}
                                    <div class="cshop__qty">
                                        <div class="cshop__stepper">
                                            <div use:sound={"tap"} on:click={() => step(m, -1)}>−</div>
                                            <input type="number" min="0" max={m.left} value={getAmount(m, amounts)} on:input={(e) => setAmount(m, e.target.value)} />
                                            <div use:sound={"tap"} on:click={() => step(m, 1)}>+</div>
                                        </div>
                                        <div class="cshop__max" use:sound={"tap"} on:click={() => setAmount(m, m.left)}>Всё · {number(m.left)}</div>
                                    </div>
                                    <div class="cshop__buy">
                                        <span>{pallets(m, getAmount(m, amounts))} пал. · {number(getAmount(m, amounts) * m.kg)} кг</span>
                                        <div class="cshop__btn" class:disabled={busy || getAmount(m, amounts) <= 0 || getAmount(m, amounts) * m.price > data.money}
                                            on:click={() => buy(m)}>
                                            Купить {money(getAmount(m, amounts) * m.price)}
                                        </div>
                                    </div>
                                {:else}
                                    <div class="cshop__ok">Закуплено</div>
                                {/if}
                            </div>
                        {/each}
                    {/if}
                </div>
            </div>
        {/if}

        <div class="cshop__foot">
            {#if lastResult}
                <span class:ok={lastResult.ok} class:err={!lastResult.ok}>{lastResult.message}</span>
            {:else}
                <span>Оплата — из бюджета организации. Паллеты появятся на площадке погрузки склада: погрузите их в грузовик организации.</span>
            {/if}
        </div>
    </div>
</div>

<style>
    .cshop {
        position: absolute;
        top: 0;
        left: 0;
        right: 0;
        bottom: 0;
        display: flex;
        align-items: center;
        justify-content: center;
        background: radial-gradient(circle at 50% 40%, rgba(16, 18, 24, 0.75), rgba(5, 6, 9, 0.92));
        font-family: "Gilroy", "Montserrat", sans-serif;
        color: #e9edf3;
    }
    .cshop__box {
        width: 118vh;
        max-width: 96vw;
        max-height: 88vh;
        display: flex;
        flex-direction: column;
        background: #12151b;
        border: 1px solid rgba(255, 255, 255, 0.07);
        border-radius: 1.6vh;
        box-shadow: 0 3vh 8vh rgba(0, 0, 0, 0.55);
        overflow: hidden;
    }
    .cshop__head {
        display: flex;
        align-items: center;
        gap: 1.6vh;
        padding: 2.2vh 2.6vh;
        border-bottom: 1px solid rgba(255, 255, 255, 0.06);
        background: linear-gradient(90deg, rgba(245, 165, 36, 0.12), rgba(245, 165, 36, 0) 60%);
    }
    .cshop__logo {
        width: 5.2vh;
        height: 5.2vh;
        border-radius: 1.2vh;
        background: rgba(245, 165, 36, 0.14);
        display: flex;
        align-items: center;
        justify-content: center;
    }
    .cshop__logo svg,
    .cshop__icon svg {
        width: 60%;
        height: 60%;
        fill: none;
        stroke: currentColor;
        stroke-width: 1.7;
        stroke-linecap: round;
        stroke-linejoin: round;
    }
    .cshop__logo svg {
        color: #f5a524;
    }
    .cshop__title {
        flex: 1;
    }
    .cshop__eyebrow,
    .cshop__label {
        font-size: 1.2vh;
        letter-spacing: 0.12vh;
        text-transform: uppercase;
        color: #8b93a1;
    }
    .cshop__title h1 {
        margin: 0.3vh 0 0;
        font-size: 2.6vh;
        font-weight: 700;
    }
    .cshop__budget {
        text-align: right;
        padding: 1vh 1.6vh;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.04);
    }
    .cshop__budget span {
        display: block;
        font-size: 1.2vh;
        color: #8b93a1;
    }
    .cshop__budget b {
        font-size: 2.2vh;
        color: #7ed321;
    }
    .cshop__budget.debt b {
        color: #ff5a5a;
    }
    .cshop__close {
        width: 4.2vh;
        height: 4.2vh;
        border-radius: 1vh;
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 1.8vh;
        color: #8b93a1;
        background: rgba(255, 255, 255, 0.04);
        cursor: pointer;
    }
    .cshop__close:hover {
        color: #fff;
        background: rgba(255, 255, 255, 0.1);
    }
    .cshop__body {
        display: flex;
        min-height: 0;
        flex: 1;
    }
    .cshop__side {
        width: 30vh;
        padding: 2vh;
        border-right: 1px solid rgba(255, 255, 255, 0.06);
        overflow-y: auto;
        display: flex;
        flex-direction: column;
        gap: 1vh;
    }
    .cshop__contract {
        padding: 1.4vh;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.03);
        border: 1px solid transparent;
        cursor: pointer;
        display: flex;
        flex-direction: column;
        gap: 0.4vh;
    }
    .cshop__contract b {
        font-size: 1.5vh;
    }
    .cshop__contract span {
        font-size: 1.3vh;
        color: #8b93a1;
    }
    .cshop__contract em {
        font-style: normal;
        font-size: 1.2vh;
        color: #f5a524;
    }
    .cshop__contract.active {
        border-color: rgba(245, 165, 36, 0.6);
        background: rgba(245, 165, 36, 0.08);
    }
    .cshop__main {
        flex: 1;
        padding: 2vh;
        overflow-y: auto;
        display: flex;
        flex-direction: column;
        gap: 1.2vh;
    }
    .cshop__row {
        display: flex;
        align-items: center;
        gap: 1.6vh;
        padding: 1.6vh;
        border-radius: 1.2vh;
        background: rgba(255, 255, 255, 0.03);
        border: 1px solid rgba(255, 255, 255, 0.05);
    }
    .cshop__row.done {
        opacity: 0.6;
    }
    .cshop__icon {
        width: 5.6vh;
        height: 5.6vh;
        flex: none;
        border-radius: 1.2vh;
        background: rgba(255, 255, 255, 0.05);
        display: flex;
        align-items: center;
        justify-content: center;
    }
    .cshop__info {
        flex: 1;
        min-width: 0;
        display: flex;
        flex-direction: column;
        gap: 0.5vh;
    }
    .cshop__info b {
        font-size: 1.8vh;
    }
    .cshop__info span,
    .cshop__info small {
        font-size: 1.25vh;
        color: #8b93a1;
    }
    .cshop__bar {
        position: relative;
        height: 0.7vh;
        border-radius: 0.4vh;
        background: rgba(255, 255, 255, 0.07);
        overflow: hidden;
    }
    .cshop__bar div {
        position: absolute;
        left: 0;
        top: 0;
        bottom: 0;
        border-radius: 0.4vh;
    }
    .cshop__bar .bought {
        background: rgba(245, 165, 36, 0.45);
    }
    .cshop__bar .delivered {
        background: #7ed321;
    }
    .cshop__qty {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 0.6vh;
    }
    .cshop__stepper {
        display: flex;
        align-items: center;
        border-radius: 0.9vh;
        background: rgba(255, 255, 255, 0.05);
        overflow: hidden;
    }
    .cshop__stepper div {
        width: 3.6vh;
        height: 3.6vh;
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 2vh;
        cursor: pointer;
        color: #c9ced6;
    }
    .cshop__stepper div:hover {
        background: rgba(255, 255, 255, 0.08);
    }
    .cshop__stepper input {
        width: 7vh;
        height: 3.6vh;
        border: none;
        outline: none;
        background: transparent;
        color: #fff;
        text-align: center;
        font-size: 1.6vh;
        font-family: inherit;
    }
    .cshop__stepper input::-webkit-inner-spin-button {
        -webkit-appearance: none;
    }
    .cshop__max {
        font-size: 1.2vh;
        color: #f5a524;
        cursor: pointer;
    }
    .cshop__buy {
        width: 19vh;
        display: flex;
        flex-direction: column;
        align-items: stretch;
        gap: 0.6vh;
        text-align: center;
    }
    .cshop__buy span {
        font-size: 1.2vh;
        color: #8b93a1;
    }
    .cshop__btn {
        padding: 1.2vh 1vh;
        border-radius: 0.9vh;
        background: #f5a524;
        color: #16181d;
        font-weight: 700;
        font-size: 1.5vh;
        cursor: pointer;
        transition: filter 0.15s;
    }
    .cshop__btn:hover {
        filter: brightness(1.1);
    }
    .cshop__btn.disabled {
        background: rgba(255, 255, 255, 0.08);
        color: #6b7280;
        pointer-events: none;
    }
    .cshop__elsewhere {
        width: 36vh;
        display: flex;
        flex-direction: column;
        gap: 0.4vh;
        text-align: right;
    }
    .cshop__elsewhere span {
        font-size: 1.2vh;
        color: #8b93a1;
    }
    .cshop__elsewhere b {
        font-size: 1.4vh;
        color: #f5a524;
        font-weight: 600;
    }
    .cshop__ok {
        width: 19vh;
        text-align: center;
        font-size: 1.5vh;
        color: #7ed321;
    }
    .cshop__empty {
        padding: 6vh 4vh;
        text-align: center;
        display: flex;
        flex-direction: column;
        gap: 1vh;
    }
    .cshop__empty b {
        font-size: 2.2vh;
    }
    .cshop__empty span {
        font-size: 1.5vh;
        color: #8b93a1;
    }
    .cshop__foot {
        padding: 1.4vh 2.6vh;
        border-top: 1px solid rgba(255, 255, 255, 0.06);
        font-size: 1.3vh;
        color: #8b93a1;
    }
    .cshop__foot .ok {
        color: #7ed321;
    }
    .cshop__foot .err {
        color: #ff6b6b;
    }
</style>
