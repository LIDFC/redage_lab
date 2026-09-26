<script>
    // Панель репортов администратора (F6). Клиент: src_client/player/report.js, данные — window.reportsStore (./index.js)
    import { executeClient } from 'api/rage'
    import { selected, reportsData, text } from './index'
    import { onDestroy } from 'svelte'

    let reports = [];
    let answer = $text;
    let search = "";
    let tab = "all";
    let templateText = "";

    onDestroy(() => {
        text.set(answer);
    });

    reportsData.subscribe(value => {
        reports = value;
    });

    // Быстрые ответы: стандартные + свои (хранятся в браузере администратора)
    const defaultTemplates = [
        "Здравствуйте! Сейчас помогу.",
        "Уточните, пожалуйста, вашу проблему подробнее.",
        "Проблема решена. Приятной игры!",
        "Это игровой процесс, администрация в него не вмешивается.",
        "Обратитесь на форум в раздел жалоб, приложив доказательства.",
        "Ответ на ваш вопрос есть в меню помощи (F10).",
        "Перезайдите в игру, если проблема сохранится — напишите снова.",
    ];
    const storageKey = "reports.templates";
    let customTemplates = [];
    try {
        customTemplates = JSON.parse(window.localStorage.getItem(storageKey) || "[]") || [];
    } catch (e) {
        customTemplates = [];
    }
    const saveTemplates = () => {
        try {
            window.localStorage.setItem(storageKey, JSON.stringify(customTemplates));
        } catch (e) {}
    }
    const addTemplate = () => {
        const value = templateText.trim();
        if (!value.length || customTemplates.includes(value)) return;
        customTemplates = [...customTemplates, value];
        templateText = "";
        saveTemplates();
    }
    const removeTemplate = (value) => {
        customTemplates = customTemplates.filter(t => t !== value);
        saveTemplates();
    }
    const useTemplate = (value) => {
        answer = answer && answer.trim().length ? `${answer.trim()} ${value}` : value;
    }

    $: freeCount = reports.filter(r => !r.blocked).length;
    $: busyCount = reports.length - freeCount;
    $: query = search.trim().toLowerCase();
    $: shown = reports.filter(r =>
        (tab === "all" || (tab === "free" ? !r.blocked : r.blocked)) &&
        (!query.length
            || String(r.id).includes(query)
            || String(r.author).toLowerCase().includes(query)
            || String(r.text).toLowerCase().includes(query)));

    const actions = [
        { name: "tp", title: "ТП к игроку" },
        { name: "metp", title: "ТП к себе" },
        { name: "sp", title: "Слежка" },
        { name: "stats", title: "Статистика" },
        { name: "ptime", title: "Время в игре" },
        { name: "checkdim", title: "Измерение" },
        { name: "nhistory", title: "История ников" },
        { name: "kill", title: "Убить", danger: true },
    ];

    const func = (funcName) => {
        const report = $selected;
        if (!report) return;
        executeClient("funcreport", report.id, funcName);
    }

    let antiSpam = 0;
    const onSelectReport = (report) => {
        if (report.blocked) return;
        else if ($selected) return;
        else if (new Date().getTime() - antiSpam < 500) return;
        antiSpam = new Date().getTime();

        selected.set(report);
        executeClient("takereport", report.id);
    }

    const onSendAnswer = (report) => {
        if (!report) return;
        else if (!answer || !answer.trim().length) return;

        executeClient("sendreport", report.id, answer.trim());
        selected.set(false);
        answer = "";
    }

    const onReturnReport = (report) => {
        if (!report) return;
        executeClient("takereport", report.id);
        selected.set(false);
        answer = "";
    }

    const exitReport = () => {
        executeClient("exitreport");
    }

    const onAnswerKey = (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            onSendAnswer($selected);
        }
    }

    const onTemplateKey = (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            addTemplate();
        }
    }

    const onKeyUp = (e) => {
        if (e.keyCode === 27)
            exitReport();
    }

    const initials = (name) => String(name || "?").split(/[\s_]+/).filter(Boolean).slice(0, 2).map(p => p[0]).join("").toUpperCase();
</script>

<svelte:window on:keyup={onKeyUp} />

<div class="rep">
    <div class="rep__panel">
        <div class="rep__head">
            <div class="rep__title">
                <div class="rep__caption">Панель администратора</div>
                <div class="rep__name">Репорты</div>
            </div>
            <div class="rep__stats">
                <div><span>{reports.length}</span>всего</div>
                <div class="free"><span>{freeCount}</span>свободно</div>
                <div class="busy"><span>{busyCount}</span>в работе</div>
            </div>
            <div class="rep__close" on:click={exitReport}>Закрыть <span>ESC</span></div>
        </div>

        <div class="rep__body">
            <div class="rep__col rep__list-col">
                <div class="rep__tabs">
                    <div class="rep__tab" class:active={tab === "all"} on:click={() => tab = "all"}>Все</div>
                    <div class="rep__tab" class:active={tab === "free"} on:click={() => tab = "free"}>Свободные</div>
                    <div class="rep__tab" class:active={tab === "busy"} on:click={() => tab = "busy"}>В работе</div>
                </div>
                <input class="rep__search" bind:value={search} placeholder="Поиск: номер, ник или текст" />
                <div class="rep__list">
                    {#each shown as report (report.id)}
                        <div class="rep__item"
                             class:blocked={report.blocked}
                             class:active={$selected && $selected.id === report.id}
                             on:click={() => onSelectReport(report)}>
                            <div class="rep__item-top">
                                <div class="rep__avatar">{initials(report.author)}</div>
                                <div class="rep__item-author">
                                    <div class="rep__item-name">{report.author}</div>
                                    <div class="rep__item-id">Репорт №{report.id}</div>
                                </div>
                                {#if report.blocked}
                                    <div class="rep__badge busy">{report.blockedBy}</div>
                                {:else}
                                    <div class="rep__badge">Новый</div>
                                {/if}
                            </div>
                            <div class="rep__item-text">{report.text}</div>
                        </div>
                    {:else}
                        <div class="rep__empty">{reports.length ? "Ничего не найдено" : "Репортов нет — всё спокойно"}</div>
                    {/each}
                </div>
            </div>

            <div class="rep__col rep__work">
                {#if $selected}
                    <div class="rep__ticket">
                        <div class="rep__item-top">
                            <div class="rep__avatar big">{initials($selected.author)}</div>
                            <div class="rep__item-author">
                                <div class="rep__item-name">{$selected.author}</div>
                                <div class="rep__item-id">Репорт №{$selected.id} · вы отвечаете</div>
                            </div>
                        </div>
                        <div class="rep__question">{$selected.text}</div>
                    </div>

                    <div class="rep__section">Действия с игроком</div>
                    <div class="rep__actions">
                        {#each actions as a}
                            <div class="rep__action" class:danger={a.danger} on:click={() => func(a.name)}>
                                {a.title}<span>/{a.name}</span>
                            </div>
                        {/each}
                    </div>

                    <div class="rep__section">Ответ</div>
                    <textarea class="rep__answer" bind:value={answer} on:keydown={onAnswerKey} placeholder="Введите ответ игроку… (Enter — отправить, Shift+Enter — новая строка)"></textarea>
                    <div class="rep__buttons">
                        <div class="rep__btn primary" class:disabled={!answer || !answer.trim().length} on:click={() => onSendAnswer($selected)}>Ответить</div>
                        <div class="rep__btn" on:click={() => onReturnReport($selected)}>Вернуть в очередь</div>
                    </div>
                {:else}
                    <div class="rep__placeholder">
                        <div class="rep__placeholder-icon">?</div>
                        <div class="rep__placeholder-title">Выберите репорт</div>
                        <div class="rep__placeholder-text">Нажмите на свободный репорт слева, чтобы взять его в работу. Пока вы отвечаете, другие администраторы видят, что он занят.</div>
                    </div>
                {/if}
            </div>

            <div class="rep__col rep__templates">
                <div class="rep__section first">Быстрые ответы</div>
                <div class="rep__tpl-list">
                    {#each customTemplates as t}
                        <div class="rep__tpl custom" class:disabled={!$selected} on:click={() => $selected && useTemplate(t)}>
                            <span>{t}</span>
                            <div class="rep__tpl-remove" on:click|stopPropagation={() => removeTemplate(t)}>×</div>
                        </div>
                    {/each}
                    {#each defaultTemplates as t}
                        <div class="rep__tpl" class:disabled={!$selected} on:click={() => $selected && useTemplate(t)}>{t}</div>
                    {/each}
                </div>
                <div class="rep__tpl-add">
                    <input bind:value={templateText} on:keydown={onTemplateKey} placeholder="Свой шаблон…" />
                    <div class="rep__btn primary small" on:click={addTemplate}>+</div>
                </div>
            </div>
        </div>
    </div>
</div>

<style>
    .rep {
        position: absolute;
        inset: 0;
        z-index: 1000;
        display: flex;
        align-items: center;
        justify-content: center;
        background: radial-gradient(circle at 30% 20%, rgba(18, 52, 78, 0.9), rgba(1, 14, 26, 0.95));
        color: white;
        font-family: 'TTNorms-Regular';
        font-size: 1.5vh;
    }
    .rep__panel {
        width: 150vh;
        max-width: 96vw;
        height: 84vh;
        display: flex;
        flex-direction: column;
        border-radius: 1.6vh;
        background: rgba(6, 22, 38, 0.92);
        border: 1px solid rgba(255, 255, 255, 0.08);
        box-shadow: 0 2vh 6vh rgba(0, 0, 0, 0.45);
        overflow: hidden;
    }
    .rep__head {
        display: flex;
        align-items: center;
        gap: 3vh;
        padding: 2.2vh 3vh;
        border-bottom: 1px solid rgba(255, 255, 255, 0.07);
    }
    .rep__caption {
        font-size: 1.2vh;
        text-transform: uppercase;
        letter-spacing: 0.2vh;
        color: #2BB6A8;
    }
    .rep__name {
        font-family: 'TTNorms-Bold';
        font-size: 3vh;
    }
    .rep__stats {
        display: flex;
        gap: 1.2vh;
        margin-left: auto;
    }
    .rep__stats div {
        display: flex;
        flex-direction: column;
        align-items: center;
        min-width: 8vh;
        padding: 0.8vh 1.4vh;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.05);
        color: rgba(255, 255, 255, 0.5);
        font-size: 1.2vh;
    }
    .rep__stats span {
        font-family: 'TTNorms-Bold';
        font-size: 2.2vh;
        color: white;
    }
    .rep__stats .free span { color: #2BB6A8; }
    .rep__stats .busy span { color: #FFC062; }
    .rep__close {
        cursor: pointer;
        padding: 1vh 1.6vh;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.06);
        color: rgba(255, 255, 255, 0.75);
        transition: background 0.15s;
    }
    .rep__close:hover { background: rgba(255, 255, 255, 0.12); }
    .rep__close span {
        margin-left: 0.8vh;
        padding: 0.2vh 0.6vh;
        border-radius: 0.5vh;
        background: rgba(255, 255, 255, 0.1);
        font-size: 1.1vh;
    }

    .rep__body {
        flex: 1;
        min-height: 0;
        display: grid;
        grid-template-columns: 38fr 62fr 34fr;
    }
    .rep__col {
        min-height: 0;
        display: flex;
        flex-direction: column;
        padding: 2vh 2.4vh;
    }
    .rep__col + .rep__col { border-left: 1px solid rgba(255, 255, 255, 0.07); }

    .rep__tabs {
        display: flex;
        gap: 0.6vh;
        padding: 0.5vh;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.04);
    }
    .rep__tab {
        flex: 1;
        text-align: center;
        cursor: pointer;
        padding: 0.8vh 0;
        border-radius: 0.8vh;
        color: rgba(255, 255, 255, 0.55);
        transition: all 0.15s;
    }
    .rep__tab.active {
        background: #2BB6A8;
        color: #011627;
        font-family: 'TTNorms-Bold';
    }
    .rep__search, .rep__tpl-add input {
        margin: 1.2vh 0;
        padding: 1.1vh 1.4vh;
        border-radius: 1vh;
        border: 1px solid rgba(255, 255, 255, 0.08);
        background: rgba(255, 255, 255, 0.04);
        color: white;
        font-family: inherit;
        font-size: 1.4vh;
        outline: none;
    }
    .rep__search:focus, .rep__tpl-add input:focus, .rep__answer:focus { border-color: rgba(43, 182, 168, 0.6); }

    .rep__list, .rep__tpl-list {
        flex: 1;
        min-height: 0;
        overflow-y: auto;
        display: flex;
        flex-direction: column;
        gap: 0.9vh;
        padding-right: 0.4vh;
    }
    .rep__list::-webkit-scrollbar, .rep__tpl-list::-webkit-scrollbar { width: 0.4vh; }
    .rep__list::-webkit-scrollbar-thumb, .rep__tpl-list::-webkit-scrollbar-thumb {
        background: rgba(255, 255, 255, 0.15);
        border-radius: 0.4vh;
    }
    .rep__item {
        flex-shrink: 0;
        cursor: pointer;
        padding: 1.3vh 1.4vh;
        border-radius: 1.1vh;
        background: rgba(255, 255, 255, 0.04);
        border: 1px solid rgba(255, 255, 255, 0.06);
        transition: all 0.15s;
    }
    .rep__item:hover { background: rgba(255, 255, 255, 0.07); }
    .rep__item.active {
        border-color: #2BB6A8;
        background: rgba(43, 182, 168, 0.12);
    }
    .rep__item.blocked {
        cursor: default;
        opacity: 0.55;
    }
    .rep__item-top {
        display: flex;
        align-items: center;
        gap: 1.1vh;
    }
    .rep__avatar {
        flex-shrink: 0;
        width: 3.6vh;
        height: 3.6vh;
        border-radius: 50%;
        display: flex;
        align-items: center;
        justify-content: center;
        background: rgba(43, 182, 168, 0.18);
        color: #2BB6A8;
        font-family: 'TTNorms-Bold';
        font-size: 1.3vh;
    }
    .rep__avatar.big {
        width: 4.6vh;
        height: 4.6vh;
        font-size: 1.7vh;
    }
    .rep__item-author {
        flex: 1;
        min-width: 0;
    }
    .rep__item-name {
        font-family: 'TTNorms-Bold';
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
    }
    .rep__item-id {
        font-size: 1.2vh;
        color: rgba(255, 255, 255, 0.45);
    }
    .rep__badge {
        flex-shrink: 0;
        max-width: 12vh;
        padding: 0.4vh 0.9vh;
        border-radius: 0.6vh;
        background: rgba(43, 182, 168, 0.18);
        color: #2BB6A8;
        font-size: 1.1vh;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
    }
    .rep__badge.busy {
        background: rgba(255, 192, 98, 0.15);
        color: #FFC062;
    }
    .rep__item-text {
        margin-top: 0.9vh;
        color: rgba(255, 255, 255, 0.75);
        line-height: 1.35;
        display: -webkit-box;
        -webkit-line-clamp: 3;
        -webkit-box-orient: vertical;
        overflow: hidden;
        word-break: break-word;
    }
    .rep__empty {
        margin-top: 4vh;
        text-align: center;
        color: rgba(255, 255, 255, 0.35);
    }

    .rep__ticket {
        padding: 1.8vh;
        border-radius: 1.2vh;
        background: rgba(43, 182, 168, 0.08);
        border: 1px solid rgba(43, 182, 168, 0.25);
    }
    .rep__question {
        margin-top: 1.4vh;
        max-height: 14vh;
        overflow-y: auto;
        line-height: 1.45;
        font-size: 1.6vh;
        word-break: break-word;
    }
    .rep__section {
        margin: 2vh 0 1vh;
        font-size: 1.2vh;
        text-transform: uppercase;
        letter-spacing: 0.15vh;
        color: rgba(255, 255, 255, 0.45);
    }
    .rep__section.first { margin-top: 0; }
    .rep__actions {
        display: grid;
        grid-template-columns: repeat(4, 1fr);
        gap: 0.8vh;
    }
    .rep__action {
        cursor: pointer;
        display: flex;
        flex-direction: column;
        gap: 0.2vh;
        padding: 1vh 1.1vh;
        border-radius: 0.9vh;
        background: rgba(255, 255, 255, 0.05);
        border: 1px solid rgba(255, 255, 255, 0.06);
        font-size: 1.35vh;
        transition: all 0.15s;
    }
    .rep__action span {
        font-size: 1.1vh;
        color: rgba(255, 255, 255, 0.4);
    }
    .rep__action:hover {
        background: rgba(43, 182, 168, 0.15);
        border-color: rgba(43, 182, 168, 0.5);
    }
    .rep__action.danger:hover {
        background: rgba(230, 80, 80, 0.15);
        border-color: rgba(230, 80, 80, 0.55);
    }
    .rep__answer {
        flex: 1;
        min-height: 10vh;
        resize: none;
        padding: 1.4vh;
        border-radius: 1.1vh;
        border: 1px solid rgba(255, 255, 255, 0.08);
        background: rgba(255, 255, 255, 0.04);
        color: white;
        font-family: inherit;
        font-size: 1.5vh;
        line-height: 1.4;
        outline: none;
    }
    .rep__buttons {
        display: flex;
        gap: 1vh;
        margin-top: 1.2vh;
    }
    .rep__btn {
        cursor: pointer;
        padding: 1.2vh 2.2vh;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.07);
        text-align: center;
        transition: all 0.15s;
    }
    .rep__btn:hover { background: rgba(255, 255, 255, 0.12); }
    .rep__btn.primary {
        flex: 1;
        background: #2BB6A8;
        color: #011627;
        font-family: 'TTNorms-Bold';
    }
    .rep__btn.primary:hover { background: #34cdbd; }
    .rep__btn.disabled {
        opacity: 0.45;
        pointer-events: none;
    }
    .rep__btn.small {
        flex: none;
        padding: 0 1.8vh;
        display: flex;
        align-items: center;
        font-size: 2vh;
    }
    .rep__placeholder {
        margin: auto;
        max-width: 42vh;
        text-align: center;
    }
    .rep__placeholder-icon {
        width: 7vh;
        height: 7vh;
        margin: 0 auto 1.6vh;
        border-radius: 50%;
        display: flex;
        align-items: center;
        justify-content: center;
        background: rgba(43, 182, 168, 0.12);
        color: #2BB6A8;
        font-family: 'TTNorms-Bold';
        font-size: 3.2vh;
    }
    .rep__placeholder-title {
        font-family: 'TTNorms-Bold';
        font-size: 2.2vh;
    }
    .rep__placeholder-text {
        margin-top: 1vh;
        color: rgba(255, 255, 255, 0.5);
        line-height: 1.45;
    }

    .rep__tpl {
        flex-shrink: 0;
        cursor: pointer;
        padding: 1vh 1.2vh;
        border-radius: 0.9vh;
        background: rgba(255, 255, 255, 0.04);
        border: 1px solid rgba(255, 255, 255, 0.06);
        color: rgba(255, 255, 255, 0.8);
        font-size: 1.35vh;
        line-height: 1.35;
        transition: all 0.15s;
    }
    .rep__tpl:hover {
        background: rgba(43, 182, 168, 0.12);
        border-color: rgba(43, 182, 168, 0.45);
    }
    .rep__tpl.custom {
        display: flex;
        align-items: flex-start;
        gap: 0.8vh;
        border-color: rgba(255, 192, 98, 0.3);
    }
    .rep__tpl.custom span { flex: 1; }
    .rep__tpl.disabled {
        opacity: 0.45;
        cursor: default;
    }
    .rep__tpl-remove {
        cursor: pointer;
        color: rgba(255, 255, 255, 0.4);
        font-size: 1.8vh;
        line-height: 1;
    }
    .rep__tpl-remove:hover { color: #e65050; }
    .rep__tpl-add {
        display: flex;
        gap: 0.8vh;
        margin-top: 1.2vh;
    }
    .rep__tpl-add input {
        flex: 1;
        min-width: 0;
        margin: 0;
    }
</style>
