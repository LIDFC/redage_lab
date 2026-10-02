<script>
    // Доска нарядов армии: заступить на кухню/уборку, офицер назначает других; вкладка «Объявления».
    // Сервер: Fractions/ArmyRP/ArmyDuty.cs и ArmyBoard.cs, клиент: src_client/fractions/army.js
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

    // ---- Объявления
    let tab = "duty";
    let annTitle = "";
    let annText = "";
    let annMessage = "";
    let annOk = true;
    $: announcements = (data && data.announcements) || [];
    const templates = [
        { title: "Построение", text: "Общее построение на плацу сегодня в __:__. Форма — полевая, при оружии. Опоздавшим — наряд." },
        { title: "Тренировка", text: "Тренировка в тире и на полосе препятствий в __:__. Сбор у штаба." },
        { title: "Учения", text: "Плановые учения в __:__. Подробности — у командиров подразделений. Явка обязательна." },
    ];
    const useTemplate = (t) => {
        annTitle = t.title;
        annText = t.text;
    };
    const post = () => {
        if (annTitle.trim().length < 3 || annText.trim().length < 5) {
            annMessage = "Заполните заголовок (от 3 символов) и текст (от 5 символов)";
            annOk = false;
            return;
        }
        executeClient("client.army.board.post", annTitle.trim(), annText.trim());
    };
    const remove = (a) => executeClient("client.army.board.delete", a.id);

    window.events.addEvent("cef.army.board.list", (json) => {
        data = { ...data, announcements: parse(json) };
    });
    window.events.addEvent("cef.army.board.result", (text, success) => {
        annMessage = text || "";
        annOk = !!success;
        if (success && text === "Объявление опубликовано") {
            annTitle = "";
            annText = "";
        }
    });

    onDestroy(() => {
        window.events.removeEvent("cef.army.duty.update");
        window.events.removeEvent("cef.army.board.list");
        window.events.removeEvent("cef.army.board.result");
    });

    const onKey = (e) => {
        if (e.key === "Escape") close();
    };
    const stop = (e) => e.stopPropagation();
</script>

<svelte:window on:keyup={onKey} />

<div class="duty" in:fade={{ duration: 150 }}>
    <div class="duty__window">
        <div class="duty__head">
            <div class="duty__title">Доска нарядов</div>
            <div class="duty__close" on:click={close}>✕</div>
        </div>

        <div class="duty__tabs">
            <div class="duty__tab" class:active={tab === "duty"} on:click={() => (tab = "duty")}>Наряды</div>
            <div class="duty__tab" class:active={tab === "news"} on:click={() => (tab = "news")}>
                Объявления{#if announcements.length}<span class="duty__count">{announcements.length}</span>{/if}
            </div>
        </div>

        {#if tab === "news"}
            {#if data.canPost}
                <div class="duty__form">
                    <div class="duty__section_title">Новое объявление</div>
                    <div class="duty__templates">
                        {#each templates as t}
                            <div class="duty__chip" on:click={() => useTemplate(t)}>{t.title}</div>
                        {/each}
                    </div>
                    <input class="duty__input" maxlength="60" placeholder="Заголовок (например: Построение в 20:00)" bind:value={annTitle} on:keyup={stop} on:keydown={stop} />
                    <textarea class="duty__input duty__textarea" maxlength="600" placeholder="Текст объявления: что, где, когда, форма одежды" bind:value={annText} on:keyup={stop} on:keydown={stop}></textarea>
                    <div class="duty__form_foot">
                        <div class="duty__counter">{annText.length}/600</div>
                        <div class="duty__btn" on:click={post}>Опубликовать</div>
                    </div>
                    <div class="duty__hint">Все военнослужащие в сети получат оповещение по рации.</div>
                </div>
            {/if}
            {#if annMessage}<div class="duty__msg" class:bad={!annOk}>{annMessage}</div>{/if}
            {#if !announcements.length}
                <div class="duty__empty duty__empty_news">Объявлений пока нет</div>
            {:else}
                <div class="duty__news">
                    {#each announcements as a (a.id)}
                        <div class="duty__news_item">
                            <div class="duty__news_head">
                                <div class="duty__news_title">{a.title}</div>
                                <div class="duty__news_date">{a.date}</div>
                                {#if a.canDelete}<div class="duty__news_del" title="Удалить" on:click={() => remove(a)}>✕</div>{/if}
                            </div>
                            <div class="duty__news_text">{a.text}</div>
                            <div class="duty__news_author">{a.rank} {a.author}</div>
                        </div>
                    {/each}
                </div>
            {/if}
        {:else}

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
        {/if}
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
    .duty__tabs {
        display: flex;
        gap: 6px;
        margin-bottom: 16px;
        border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    }
    .duty__tab {
        padding: 8px 14px;
        font-weight: 600;
        font-size: 14px;
        opacity: 0.6;
        cursor: pointer;
        border-bottom: 2px solid transparent;
        margin-bottom: -1px;
        display: flex;
        align-items: center;
        gap: 6px;
    }
    .duty__tab.active {
        opacity: 1;
        border-bottom-color: #7ea53f;
    }
    .duty__count {
        font-size: 11px;
        background: #4b6b2a;
        border-radius: 8px;
        padding: 1px 6px;
    }
    .duty__form {
        padding: 12px 14px;
        border-radius: 10px;
        background: rgba(255, 255, 255, 0.04);
        display: flex;
        flex-direction: column;
        gap: 8px;
        margin-bottom: 12px;
    }
    .duty__templates {
        display: flex;
        gap: 6px;
        flex-wrap: wrap;
    }
    .duty__chip {
        font-size: 12px;
        padding: 4px 10px;
        border-radius: 12px;
        background: rgba(107, 142, 35, 0.2);
        border: 1px solid rgba(107, 142, 35, 0.5);
        cursor: pointer;
    }
    .duty__input {
        width: 100%;
        box-sizing: border-box;
        padding: 8px 10px;
        border-radius: 8px;
        border: 1px solid rgba(255, 255, 255, 0.1);
        background: rgba(0, 0, 0, 0.25);
        color: #fff;
        font-family: inherit;
        font-size: 13px;
        outline: none;
    }
    .duty__input:focus {
        border-color: #7ea53f;
    }
    .duty__textarea {
        min-height: 80px;
        resize: none;
    }
    .duty__form_foot {
        display: flex;
        align-items: center;
        justify-content: space-between;
    }
    .duty__counter {
        font-size: 12px;
        opacity: 0.5;
    }
    .duty__empty_news {
        padding: 20px 0;
        text-align: center;
    }
    .duty__news {
        display: flex;
        flex-direction: column;
        gap: 8px;
    }
    .duty__news_item {
        padding: 12px 14px;
        border-radius: 10px;
        background: rgba(255, 255, 255, 0.04);
        border-left: 3px solid #6b8e23;
    }
    .duty__news_head {
        display: flex;
        align-items: center;
        gap: 10px;
    }
    .duty__news_title {
        font-weight: 700;
        font-size: 15px;
        flex: 1;
        min-width: 0;
        overflow-wrap: anywhere;
    }
    .duty__news_date {
        font-size: 12px;
        opacity: 0.5;
    }
    .duty__news_del {
        cursor: pointer;
        opacity: 0.5;
        font-size: 12px;
        padding: 2px 6px;
    }
    .duty__news_del:hover {
        opacity: 1;
        color: #ff6b6f;
    }
    .duty__news_text {
        margin-top: 6px;
        font-size: 13px;
        line-height: 1.5;
        opacity: 0.9;
        white-space: pre-wrap;
        overflow-wrap: anywhere;
    }
    .duty__news_author {
        margin-top: 6px;
        font-size: 12px;
        opacity: 0.55;
    }
</style>
