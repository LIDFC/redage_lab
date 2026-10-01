<script>
    // Планшет → «Гауптвахта»: арест на гауптвахте и наряд (в т.ч. взыскание) у военнослужащего.
    // Сервер: Fractions/ArmyRP/ArmyDuty.cs (server.tablet.guardhouse.load), клиент: src_client/tablet/index.js
    import { executeClient } from 'api/rage'
    import { sound } from 'api/uiSound'
    import { onDestroy } from 'svelte'
    import { fade } from 'svelte/transition'

    let data = null;

    const load = () => executeClient("client.tablet.guardhouse.load");
    const onData = (json) => {
        try {
            data = typeof json === "string" ? JSON.parse(json) : json;
        } catch (e) {
            data = {};
        }
    };
    window.events.addEvent("cef.tablet.guardhouse.data", onData);
    onDestroy(() => window.events.removeEvent("cef.tablet.guardhouse.data", onData));
    load();
    // Время идёт только онлайн — обновляем раз в 30 с, пока открыто
    const timer = setInterval(load, 30000);
    onDestroy(() => clearInterval(timer));

    const waypoint = () => executeClient("client.tablet.guardhouse.waypoint");

    $: dutyPercent = data && data.duty && data.duty.total ? Math.round(data.duty.step / data.duty.total * 100) : 0;

    // Для превью без сервера
    window.tabletGuardhouseMock = (d) => onData(d);
</script>

<div class="gh" in:fade={{ duration: 150 }}>
    <div class="gh__head">
        <div class="gh__icon">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><rect x="4" y="4" width="16" height="16" rx="2"/><path d="M9 4v16M15 4v16"/></svg>
        </div>
        <div>
            <div class="gh__title">Гауптвахта</div>
            <div class="gh__sub">Взыскания и наряды военнослужащего</div>
        </div>
        <div class="gh__refresh" use:sound={"tap"} on:click={load}>Обновить</div>
    </div>

    {#if data === null}
        <div class="gh__empty">Загрузка…</div>
    {:else if !data.arrest && !data.duty}
        <div class="gh__clean">
            <div class="gh__clean_icon">✓</div>
            <div class="gh__clean_title">Взысканий нет</div>
            <div class="gh__clean_text">Служите достойно — здесь появятся арест на гауптвахте и назначенные наряды.</div>
        </div>
    {:else}
        <div class="gh__cards">
            {#if data.arrest}
                <div class="gh__card arrest">
                    <div class="gh__card_head">
                        <span class="gh__badge red">Арест</span>
                        <span class="gh__big">{data.arrest.minutesLeft} мин</span>
                    </div>
                    <div class="gh__card_title">Гауптвахта</div>
                    <div class="gh__rows">
                        <div><span>Причина</span><b>{data.arrest.reason}</b></div>
                        <div><span>Отправил</span><b>{data.arrest.officer}</b></div>
                        {#if data.arrest.minutes}<div><span>Срок</span><b>{data.arrest.minutes} мин{data.arrest.date ? ` · с ${data.arrest.date}` : ""}</b></div>{/if}
                    </div>
                    <div class="gh__note">Срок идёт только пока вы в игре.</div>
                </div>
            {/if}
            {#if data.duty}
                <div class="gh__card duty" class:punish={data.duty.punishment}>
                    <div class="gh__card_head">
                        <span class="gh__badge" class:red={data.duty.punishment}>{data.duty.punishment ? "Взыскание" : "Наряд"}</span>
                        <span class="gh__big">{data.duty.minutesLeft} мин</span>
                    </div>
                    <div class="gh__card_title">Наряд: {data.duty.name}</div>
                    <div class="gh__progress"><div style="width: {dutyPercent}%"></div></div>
                    <div class="gh__rows">
                        <div><span>Выполнено</span><b>{data.duty.step} из {data.duty.total} точек</b></div>
                        <div><span>Назначил</span><b>{data.duty.officer || "сам заступил"}</b></div>
                        <div><span>Премия</span><b>{data.duty.punishment ? "нет (взыскание)" : "есть"}</b></div>
                    </div>
                    <div class="gh__note">Осталось {data.duty.minutesLeft} мин онлайна. Не успеете — офицерам уйдёт доклад.</div>
                    <div class="gh__btn" use:sound={"tap"} on:click={waypoint}>Показать точку на карте</div>
                </div>
            {/if}
        </div>
    {/if}
</div>

<style>
    .gh {
        height: 100%;
        padding: 28px 34px;
        color: #fff;
        overflow-y: auto;
        font-family: 'Gilroy', 'Montserrat', sans-serif;
    }
    .gh__head {
        display: flex;
        align-items: center;
        gap: 14px;
        margin-bottom: 24px;
    }
    .gh__icon {
        width: 46px;
        height: 46px;
        border-radius: 12px;
        background: linear-gradient(145deg, rgba(255, 255, 255, 0.2), rgba(0, 0, 0, 0.3)), #6b7f3a;
        display: flex;
        align-items: center;
        justify-content: center;
    }
    .gh__icon svg {
        width: 26px;
        height: 26px;
    }
    .gh__title {
        font-size: 24px;
        font-weight: 700;
    }
    .gh__sub {
        font-size: 13px;
        opacity: 0.6;
    }
    .gh__refresh {
        margin-left: auto;
        padding: 8px 14px;
        border-radius: 10px;
        background: rgba(255, 255, 255, 0.08);
        font-size: 13px;
        cursor: pointer;
    }
    .gh__empty {
        opacity: 0.6;
    }
    .gh__clean {
        margin: 60px auto 0;
        max-width: 420px;
        text-align: center;
    }
    .gh__clean_icon {
        width: 64px;
        height: 64px;
        margin: 0 auto 14px;
        border-radius: 50%;
        background: rgba(110, 240, 122, 0.15);
        color: #6ef07a;
        font-size: 32px;
        line-height: 64px;
    }
    .gh__clean_title {
        font-size: 20px;
        font-weight: 700;
        margin-bottom: 6px;
    }
    .gh__clean_text {
        font-size: 14px;
        opacity: 0.6;
        line-height: 1.5;
    }
    .gh__cards {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
        gap: 16px;
    }
    .gh__card {
        padding: 18px 20px;
        border-radius: 16px;
        background: rgba(255, 255, 255, 0.05);
        border: 1px solid rgba(255, 255, 255, 0.08);
    }
    .gh__card.arrest,
    .gh__card.punish {
        border-color: rgba(255, 107, 111, 0.35);
        background: rgba(255, 107, 111, 0.06);
    }
    .gh__card_head {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-bottom: 8px;
    }
    .gh__badge {
        padding: 4px 10px;
        border-radius: 20px;
        font-size: 12px;
        font-weight: 700;
        background: rgba(139, 211, 107, 0.18);
        color: #b5d77b;
    }
    .gh__badge.red {
        background: rgba(255, 107, 111, 0.18);
        color: #ff8a8d;
    }
    .gh__big {
        font-size: 22px;
        font-weight: 700;
    }
    .gh__card_title {
        font-size: 17px;
        font-weight: 700;
        margin-bottom: 12px;
    }
    .gh__progress {
        height: 6px;
        border-radius: 6px;
        background: rgba(255, 255, 255, 0.1);
        overflow: hidden;
        margin-bottom: 12px;
    }
    .gh__progress div {
        height: 100%;
        background: linear-gradient(90deg, #6b8e23, #b5d77b);
    }
    .gh__rows div {
        display: flex;
        justify-content: space-between;
        gap: 12px;
        padding: 6px 0;
        border-bottom: 1px solid rgba(255, 255, 255, 0.06);
        font-size: 14px;
    }
    .gh__rows span {
        opacity: 0.6;
    }
    .gh__rows b {
        text-align: right;
    }
    .gh__note {
        margin-top: 10px;
        font-size: 12px;
        opacity: 0.6;
    }
    .gh__btn {
        margin-top: 14px;
        padding: 10px;
        border-radius: 10px;
        text-align: center;
        font-weight: 600;
        background: #4b6b2a;
        cursor: pointer;
    }
</style>
