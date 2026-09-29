<script>
    // Доска нарядов армии: заступить на кухню/уборку, офицер назначает других.
    // Сервер: Fractions/ArmyRP/ArmyDuty.cs, клиент: src_client/fractions/army.js
    import { executeClient } from "api/rage";
    import { onDestroy } from "svelte";
    import { fade } from "svelte/transition";

    export let viewData;

    const parse = (d) => {
        try {
            return typeof d === "string" ? JSON.parse(d) : d || {};
        } catch (e) {
            return {};
        }
    };

    let data = parse(viewData);
    let message = "";
    let ok = true;
    let punishment = false;

    const cards = [
        { type: "kitchen", title: "Кухня", text: "Мытьё посуды и помощь повару. Обойти точки кухни и отработать на каждой.", icon: "🍳" },
        { type: "clean", title: "Уборка", text: "Подмести плац и территорию базы. Обойти точки уборки с метлой.", icon: "🧹" },
    ];

    const take = (type) => executeClient("client.army.duty.take", type);
    const assign = (soldier, type) => executeClient("client.army.duty.assign", soldier.id, type, punishment);
    const close = () => executeClient("client.army.duty.close");

    window.events.addEvent("cef.army.duty.update", (json, text, success) => {
        data = parse(json);
        message = text || "";
        ok = !!success;
    });
    onDestroy(() => window.events.removeEvent("cef.army.duty.update"));

    const onKey = (e) => {
        if (e.key === "Escape") close();
    };
</script>

<svelte:window on:keyup={onKey} />

<div class="duty" in:fade={{ duration: 150 }}>
    <div class="duty__window">
        <div class="duty__head">
            <div class="duty__title">Доска нарядов</div>
            <div class="duty__close" on:click={close}>✕</div>
        </div>

        {#if data.order}
            <div class="duty__order">
                <div class="duty__order_title">Ваш наряд: {data.order.name}</div>
                <div class="duty__order_text">
                    Выполнено точек: {data.order.step} из {data.order.total}. Осталось {data.order.minutesLeft} мин онлайна.
                    {#if data.order.officer}Назначил: {data.order.officer}.{/if}
                    {#if data.order.punishment}<span class="duty__bad">Взыскание — без премии.</span>{/if}
                </div>
                <div class="duty__hint">Идите к метке на карте и нажмите E на точке.</div>
            </div>
        {/if}

        <div class="duty__cards">
            {#each cards as card}
                <div class="duty__card">
                    <div class="duty__icon">{card.icon}</div>
                    <div class="duty__card_title">{card.title}</div>
                    <div class="duty__card_text">{card.text}</div>
                    <div class="duty__meta">
                        Точек: {(data[card.type] && data[card.type].points) || 0} · Премия: ${(data[card.type] && data[card.type].reward) || 0}
                    </div>
                    <div class="duty__btn" class:disabled={!!data.order || !(data[card.type] && data[card.type].points)} on:click={() => !data.order && take(card.type)}>
                        {data.order ? "Вы уже в наряде" : "Заступить"}
                    </div>
                </div>
            {/each}
        </div>

        {#if data.isOfficer}
            <div class="duty__section">
                <div class="duty__section_head">
                    <div class="duty__section_title">Назначить военнослужащего</div>
                    <label class="duty__check"><input type="checkbox" bind:checked={punishment} /> Взыскание (без премии)</label>
                </div>
                {#if !data.soldiers || !data.soldiers.length}
                    <div class="duty__empty">Нет военных на смене</div>
                {:else}
                    <div class="duty__list">
                        {#each data.soldiers as soldier (soldier.id)}
                            <div class="duty__row">
                                <div class="duty__name">{soldier.name} <span class="duty__rank">{soldier.rank}</span></div>
                                {#if soldier.busy}
                                    <div class="duty__busy">в наряде</div>
                                {:else}
                                    <div class="duty__btn small" on:click={() => assign(soldier, "kitchen")}>На кухню</div>
                                    <div class="duty__btn small" on:click={() => assign(soldier, "clean")}>На уборку</div>
                                {/if}
                            </div>
                        {/each}
                    </div>
                {/if}
            </div>
        {/if}

        <div class="duty__msg" class:bad={!ok}>{message}</div>
    </div>
</div>

<style>
    .duty {
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
    .duty__window {
        width: 760px;
        max-width: 94vw;
        max-height: 90vh;
        overflow-y: auto;
        background: #16181d;
        border: 1px solid rgba(255, 255, 255, 0.08);
        border-radius: 12px;
        padding: 20px 24px;
    }
    .duty__head {
        display: flex;
        align-items: center;
        margin-bottom: 16px;
    }
    .duty__title {
        font-size: 20px;
        font-weight: 700;
        margin-right: auto;
    }
    .duty__close {
        cursor: pointer;
        opacity: 0.7;
        padding: 4px 8px;
    }
    .duty__order {
        padding: 12px 14px;
        border-radius: 10px;
        background: rgba(107, 142, 35, 0.15);
        border: 1px solid rgba(107, 142, 35, 0.5);
        margin-bottom: 16px;
    }
    .duty__order_title {
        font-weight: 700;
        margin-bottom: 4px;
    }
    .duty__order_text,
    .duty__hint {
        font-size: 13px;
        opacity: 0.85;
        line-height: 1.5;
    }
    .duty__bad {
        color: #ff8a65;
    }
    .duty__cards {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 12px;
    }
    .duty__card {
        padding: 14px;
        border-radius: 10px;
        background: rgba(255, 255, 255, 0.04);
        display: flex;
        flex-direction: column;
        gap: 8px;
    }
    .duty__icon {
        font-size: 28px;
    }
    .duty__card_title {
        font-size: 16px;
        font-weight: 700;
    }
    .duty__card_text,
    .duty__meta {
        font-size: 13px;
        opacity: 0.75;
        line-height: 1.4;
    }
    .duty__btn {
        padding: 9px 16px;
        border-radius: 8px;
        background: #4b6b2a;
        font-weight: 600;
        cursor: pointer;
        text-align: center;
        user-select: none;
        white-space: nowrap;
    }
    .duty__btn.small {
        padding: 6px 10px;
        font-size: 12px;
    }
    .duty__btn.disabled {
        opacity: 0.4;
        cursor: default;
    }
    .duty__section {
        margin-top: 18px;
    }
    .duty__section_head {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-bottom: 8px;
    }
    .duty__section_title {
        font-weight: 700;
    }
    .duty__check {
        font-size: 13px;
        opacity: 0.85;
        display: flex;
        align-items: center;
        gap: 6px;
    }
    .duty__list {
        display: flex;
        flex-direction: column;
        gap: 6px;
    }
    .duty__row {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 8px 10px;
        border-radius: 8px;
        background: rgba(255, 255, 255, 0.03);
    }
    .duty__name {
        flex: 1;
        font-size: 14px;
    }
    .duty__rank {
        opacity: 0.55;
        font-size: 12px;
        margin-left: 6px;
    }
    .duty__busy {
        font-size: 12px;
        opacity: 0.6;
    }
    .duty__empty {
        opacity: 0.5;
        font-size: 13px;
    }
    .duty__msg {
        margin-top: 14px;
        min-height: 18px;
        font-size: 13px;
        color: #b5d77b;
    }
    .duty__msg.bad {
        color: #ff6b6f;
    }
</style>
