<script>
    // Окно автошколы у NPC «Офицер Бенсон» (сервер: Core/DrivingSchool.cs, клиент: src_client/player/drivingschool.js)
    // Вкладки: лицензии, теория для подготовки; экзамен идёт прямо в этом окне — вопросы присылает сервер.
    import { executeClient } from 'api/rage'
    import { format } from 'api/formatter'
    import { onDestroy } from 'svelte'
    import { fade } from 'svelte/transition'
    import { theorySections } from './theory.js'

    export let viewData;

    let data = { lvl: 0, questions: 10, passScore: 8, maxPenalties: 3, checkpoints: 10, licenses: [] };
    $: if (viewData && typeof viewData === "string")
        data = JSON.parse(viewData);
    else if (viewData && typeof viewData === "object")
        data = viewData;

    let tab = "licenses";
    let section = theorySections[0].id;
    let question = null;   // { number, total, correct, question, answers }
    let result = null;     // { passed, correct, total, need, license }
    let waiting = false;

    $: currentSection = theorySections.find(s => s.id === section);
    $: examActive = !!question && !result;

    const ICONS = [
        "M5 17a3 3 0 1 0 0 .01M19 17a3 3 0 1 0 0 .01M8 17h6l3-6h-4l-2-3H8M14 11l2-4h3",
        "M3 13l2-5h14l2 5v4h-2M3 13v4h2m0 0a2 2 0 1 0 4 0m-4 0a2 2 0 1 1 4 0m6 0a2 2 0 1 0 4 0m-4 0a2 2 0 1 1 4 0M9 17h6M3 13h18",
        "M2 6h11v10H2zM13 9h5l3 4v3h-8M5 18a2 2 0 1 0 0 .01M17 18a2 2 0 1 0 0 .01",
        "M3 16l2 3h14l2-3zM12 4v9M12 5l6 7h-6",
        "M4 6h16M12 6v3M6 12a6 3 0 0 0 12 0 6 3 0 0 0-12 0M18 12h4l1-3M8 15l-2 3h8",
        "M2 13l20-6-4 7-7 1-3 4H6l1-4z",
    ];

    const getStatus = (lic) => {
        if (lic.has) return { text: "Получена", cls: "done" };
        if (lic.theoryPassed) return { text: "Теория сдана", cls: "progress" };
        if (lic.needLvl && data.lvl < lic.needLvl) return { text: `С ${lic.needLvl} уровня`, cls: "locked" };
        return { text: lic.exam ? "Экзамен" : "Покупка", cls: "" };
    }

    const getButton = (lic) => {
        if (lic.has) return null;
        if (lic.theoryPassed) return "Начать практику";
        if (lic.needLvl && data.lvl < lic.needLvl) return null;
        return lic.exam ? `Сдать экзамен · $${format("money", lic.price)}` : `Купить · $${format("money", lic.price)}`;
    }

    const onLicense = (lic) => {
        if (waiting || !getButton(lic)) return;
        if (lic.theoryPassed) {
            executeClient("client.drivingschool.practice");
            return;
        }
        waiting = true;
        executeClient("client.drivingschool.start", lic.index);
        // если сервер откажет (нет денег, уже есть лицензия) — вопрос не придёт, разблокируем кнопки
        setTimeout(() => waiting = false, lic.exam ? 3000 : 800);
    }

    const answer = (index) => {
        if (waiting) return;
        waiting = true;
        executeClient("client.drivingschool.answer", index);
    }

    const onQuestion = (json) => {
        question = typeof json === "string" ? JSON.parse(json) : json;
        result = null;
        waiting = false;
        tab = "exam";
    }

    const onResult = (json) => {
        result = typeof json === "string" ? JSON.parse(json) : json;
        waiting = false;
        tab = "exam";
        if (result.passed)
            data.licenses = data.licenses.map(l => l.index === result.license ? { ...l, theoryPassed: true } : l);
    }

    const retry = () => {
        const lic = data.licenses.find(l => l.index === result.license);
        result = null;
        question = null;
        if (lic) onLicense(lic);
    }

    const toTheory = () => {
        result = null;
        question = null;
        tab = "theory";
    }

    const close = () => executeClient("client.drivingschool.close", true);

    const onKey = (e) => {
        if (e.keyCode !== 27) return;
        close();
    }

    window.events.addEvent("cef.drivingschool.question", onQuestion);
    window.events.addEvent("cef.drivingschool.result", onResult);
    onDestroy(() => {
        window.events.removeEvent("cef.drivingschool.question", onQuestion);
        window.events.removeEvent("cef.drivingschool.result", onResult);
    });
</script>

<svelte:window on:keyup={onKey} />

<div class="ds">
    <div class="ds__panel">
        <div class="ds__header">
            <div class="ds__logo">
                <svg viewBox="0 0 24 24"><path d="M12 2l8 4v6c0 5-3.5 8.5-8 10-4.5-1.5-8-5-8-10V6z" /><path d="M8 12l3 3 5-6" /></svg>
            </div>
            <div class="ds__heading">
                <div class="ds__title">Автошкола Лос-Сантоса</div>
                <div class="ds__subtitle">Офицер Бенсон · выдача лицензий</div>
            </div>
            <div class="ds__tabs">
                <div class="ds__tab" class:active={tab === "licenses"} on:click={() => !examActive && (tab = "licenses")} class:disabled={examActive}>Лицензии</div>
                <div class="ds__tab" class:active={tab === "theory"} on:click={() => !examActive && (tab = "theory")} class:disabled={examActive}>Теория</div>
                {#if question || result}
                    <div class="ds__tab" class:active={tab === "exam"} on:click={() => tab = "exam"}>Экзамен</div>
                {/if}
            </div>
            <div class="ds__close" on:click={close}>{examActive ? "Прервать" : "Закрыть"} <span>ESC</span></div>
        </div>

        {#if tab === "licenses"}
            <div class="ds__content" in:fade={{ duration: 150 }}>
                <div class="ds__intro">
                    Категории <b>A, B, C</b> сдаются экзаменом: теория ({data.questions} вопросов, нужно {data.passScore}) и практика по маршруту из {data.checkpoints} точек.
                    Готовьтесь во вкладке <span class="ds__link" on:click={() => tab = "theory"}>«Теория»</span> — там есть ответы на все вопросы.
                </div>
                <div class="ds__grid">
                    {#each data.licenses as lic}
                        <div class="ds__card" class:has={lic.has}>
                            <div class="ds__card-top">
                                <div class="ds__icon"><svg viewBox="0 0 24 24"><path d={ICONS[lic.index] || ICONS[1]} /></svg></div>
                                <div class="ds__badge {getStatus(lic).cls}">{getStatus(lic).text}</div>
                            </div>
                            <div class="ds__card-name">{lic.name}</div>
                            <div class="ds__card-text">
                                {#if lic.exam}
                                    Теория + практика на учебном {lic.index === 0 ? "мотоцикле" : lic.index === 2 ? "грузовике" : "автомобиле"}
                                {:else}
                                    Покупка без экзамена{lic.needLvl ? `, с ${lic.needLvl} уровня` : ""}
                                {/if}
                            </div>
                            {#if getButton(lic)}
                                <div class="ds__btn" class:primary={lic.theoryPassed || !lic.has} class:busy={waiting} on:click={() => onLicense(lic)}>{getButton(lic)}</div>
                            {:else}
                                <div class="ds__btn ghost">{lic.has ? "Лицензия есть" : "Недоступно"}</div>
                            {/if}
                        </div>
                    {/each}
                </div>
            </div>
        {:else if tab === "theory"}
            <div class="ds__content ds__theory" in:fade={{ duration: 150 }}>
                <div class="ds__nav">
                    {#each theorySections as s, i}
                        <div class="ds__nav-item" class:active={section === s.id} on:click={() => section = s.id}>
                            <span>{i + 1}</span>{s.title}
                        </div>
                    {/each}
                    <div class="ds__nav-hint">Теория — {data.questions} случайных вопросов из этих разделов. Нужно {data.passScore} правильных.</div>
                </div>
                <div class="ds__article">
                    <div class="ds__article-title">{currentSection.title}</div>
                    {#each currentSection.items as item}
                        <div class="ds__rule">
                            <div class="ds__rule-dot"></div>
                            <div>{item}</div>
                        </div>
                    {/each}
                    {#if section === "practice"}
                        <div class="ds__practice-card">
                            <div><b>{data.checkpoints}</b>контрольных точек</div>
                            <div><b>80</b>км/ч A и B</div>
                            <div><b>70</b>км/ч для C</div>
                            <div><b>{data.maxPenalties}</b>ошибки = провал</div>
                        </div>
                    {/if}
                </div>
            </div>
        {:else if tab === "exam"}
            <div class="ds__content ds__exam" in:fade={{ duration: 150 }}>
                {#if result}
                    <div class="ds__result" class:passed={result.passed}>
                        <div class="ds__result-score">{result.correct}<span>/{result.total}</span></div>
                        <div class="ds__result-title">{result.passed ? "Теория сдана!" : "Теория не сдана"}</div>
                        <div class="ds__result-text">
                            {#if result.passed}
                                Осталась практика: {data.checkpoints} точек по городу, не быстрее ограничения и без столкновений.
                                Начните сейчас или вернитесь позже — повторно платить не нужно.
                            {:else}
                                Нужно минимум {result.need} правильных ответов. Повторите раздел «Теория» и попробуйте ещё раз.
                            {/if}
                        </div>
                        <div class="ds__result-buttons">
                            {#if result.passed}
                                <div class="ds__btn" on:click={toTheory}>Правила практики</div>
                                <div class="ds__btn primary" on:click={() => executeClient("client.drivingschool.practice")}>Начать практику</div>
                            {:else}
                                <div class="ds__btn" on:click={toTheory}>К теории</div>
                                <div class="ds__btn primary" on:click={retry}>Пересдать</div>
                            {/if}
                        </div>
                    </div>
                {:else if question}
                    <div class="ds__progress">
                        <div class="ds__progress-text">Вопрос {question.number} из {question.total}</div>
                        <div class="ds__progress-bar"><div style="width: {(question.number - 1) / question.total * 100}%"></div></div>
                    </div>
                    <div class="ds__question">{question.question}</div>
                    <div class="ds__answers">
                        {#each question.answers as a, i}
                            <div class="ds__answer" class:busy={waiting} on:click={() => answer(i)}>
                                <span>{String.fromCharCode(65 + i)}</span>{a}
                            </div>
                        {/each}
                    </div>
                    <div class="ds__exam-hint">Выход из окна прерывает экзамен, оплата не возвращается.</div>
                {/if}
            </div>
        {/if}
    </div>
</div>

<style>
    .ds {
        position: absolute;
        inset: 0;
        z-index: 1000;
        display: flex;
        align-items: center;
        justify-content: center;
        background: radial-gradient(circle at 70% 15%, rgba(20, 58, 84, 0.93), rgba(1, 14, 26, 0.96));
        color: white;
        font-family: 'TTNorms-Regular';
        font-size: 1.5vh;
    }
    .ds__panel {
        width: 128vh;
        height: 76vh;
        display: flex;
        flex-direction: column;
    }
    .ds__header {
        display: flex;
        align-items: center;
        gap: 1.6vh;
        padding-bottom: 1.8vh;
        margin-bottom: 2vh;
        border-bottom: 1px solid rgba(255, 255, 255, 0.1);
    }
    .ds__logo {
        width: 5.4vh;
        height: 5.4vh;
        border-radius: 1.2vh;
        background: #2BB6A8;
        display: flex;
        align-items: center;
        justify-content: center;
    }
    .ds__logo svg, .ds__icon svg {
        width: 60%;
        height: 60%;
        fill: none;
        stroke: white;
        stroke-width: 1.8;
        stroke-linecap: round;
        stroke-linejoin: round;
    }
    .ds__heading {
        flex: 1;
    }
    .ds__title {
        font-family: 'TTNorms-Bold';
        font-size: 2.8vh;
    }
    .ds__subtitle {
        color: rgba(255, 255, 255, 0.55);
    }
    .ds__tabs {
        display: flex;
        gap: 0.6vh;
        background: rgba(255, 255, 255, 0.06);
        padding: 0.5vh;
        border-radius: 1vh;
    }
    .ds__tab {
        padding: 0.9vh 2vh;
        border-radius: 0.7vh;
        cursor: pointer;
        color: rgba(255, 255, 255, 0.7);
    }
    .ds__tab.active {
        background: white;
        color: #011627;
        font-family: 'TTNorms-Bold';
    }
    .ds__tab.disabled {
        opacity: 0.4;
        cursor: default;
    }
    .ds__close {
        margin-left: 1vh;
        cursor: pointer;
        display: flex;
        align-items: center;
        gap: 0.8vh;
        color: rgba(255, 255, 255, 0.75);
    }
    .ds__close span {
        background: white;
        color: #011627;
        padding: 0.5vh 0.9vh;
        border-radius: 0.4vh;
        font-size: 1.3vh;
    }
    .ds__content {
        flex: 1;
        min-height: 0;
    }
    .ds__intro {
        color: rgba(255, 255, 255, 0.7);
        line-height: 1.5;
        margin-bottom: 2vh;
    }
    .ds__intro b {
        color: white;
    }
    .ds__link {
        color: #2BB6A8;
        cursor: pointer;
        text-decoration: underline;
    }
    .ds__grid {
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: 1.6vh;
    }
    .ds__card {
        background: rgba(255, 255, 255, 0.05);
        border: 1px solid rgba(255, 255, 255, 0.08);
        border-radius: 1.2vh;
        padding: 1.8vh;
        display: flex;
        flex-direction: column;
        gap: 0.8vh;
        transition: border-color 0.15s, background 0.15s;
    }
    .ds__card:hover {
        border-color: rgba(43, 182, 168, 0.6);
        background: rgba(255, 255, 255, 0.08);
    }
    .ds__card.has {
        opacity: 0.7;
    }
    .ds__card-top {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
    }
    .ds__icon {
        width: 5vh;
        height: 5vh;
        border-radius: 1vh;
        background: rgba(43, 182, 168, 0.15);
        display: flex;
        align-items: center;
        justify-content: center;
    }
    .ds__icon svg {
        stroke: #2BB6A8;
        width: 70%;
        height: 70%;
    }
    .ds__badge {
        font-size: 1.2vh;
        padding: 0.4vh 0.9vh;
        border-radius: 0.5vh;
        background: rgba(255, 255, 255, 0.1);
    }
    .ds__badge.done {
        background: rgba(43, 182, 168, 0.25);
        color: #5fe0d2;
    }
    .ds__badge.progress {
        background: rgba(255, 192, 98, 0.2);
        color: #FFC062;
    }
    .ds__badge.locked {
        background: rgba(255, 90, 90, 0.18);
        color: #ff8a8a;
    }
    .ds__card-name {
        font-family: 'TTNorms-Bold';
        font-size: 2.1vh;
        margin-top: 0.6vh;
    }
    .ds__card-text {
        color: rgba(255, 255, 255, 0.6);
        font-size: 1.4vh;
        flex: 1;
        min-height: 3.6vh;
    }
    .ds__btn {
        text-align: center;
        padding: 1.1vh 1.6vh;
        border-radius: 0.8vh;
        background: rgba(255, 255, 255, 0.1);
        cursor: pointer;
        transition: background 0.15s;
    }
    .ds__btn:hover {
        background: rgba(255, 255, 255, 0.18);
    }
    .ds__btn.primary {
        background: #2BB6A8;
        font-family: 'TTNorms-Bold';
    }
    .ds__btn.primary:hover {
        background: #34cdbd;
    }
    .ds__btn.ghost {
        background: transparent;
        border: 1px dashed rgba(255, 255, 255, 0.2);
        color: rgba(255, 255, 255, 0.5);
        cursor: default;
    }
    .ds__btn.busy, .ds__answer.busy {
        opacity: 0.6;
        pointer-events: none;
    }
    .ds__theory {
        display: flex;
        gap: 2.4vh;
    }
    .ds__nav {
        width: 32vh;
        flex-shrink: 0;
        display: flex;
        flex-direction: column;
        gap: 0.8vh;
    }
    .ds__nav-item {
        display: flex;
        align-items: center;
        gap: 1.2vh;
        padding: 1.3vh 1.4vh;
        border-radius: 0.9vh;
        background: rgba(255, 255, 255, 0.05);
        cursor: pointer;
    }
    .ds__nav-item span {
        width: 2.8vh;
        height: 2.8vh;
        border-radius: 50%;
        background: rgba(255, 255, 255, 0.1);
        display: flex;
        align-items: center;
        justify-content: center;
        font-family: 'TTNorms-Bold';
        font-size: 1.3vh;
    }
    .ds__nav-item.active {
        background: rgba(43, 182, 168, 0.18);
        border: 1px solid rgba(43, 182, 168, 0.5);
    }
    .ds__nav-item.active span {
        background: #2BB6A8;
    }
    .ds__nav-hint {
        margin-top: auto;
        color: rgba(255, 255, 255, 0.5);
        font-size: 1.3vh;
        line-height: 1.5;
    }
    .ds__article {
        flex: 1;
        background: rgba(255, 255, 255, 0.04);
        border-radius: 1.2vh;
        padding: 2.4vh 2.8vh;
        overflow-y: auto;
    }
    .ds__article-title {
        font-family: 'TTNorms-Bold';
        font-size: 2.6vh;
        margin-bottom: 1.8vh;
    }
    .ds__rule {
        display: flex;
        gap: 1.4vh;
        margin-bottom: 1.4vh;
        line-height: 1.5;
        font-size: 1.65vh;
        color: rgba(255, 255, 255, 0.85);
    }
    .ds__rule-dot {
        width: 0.8vh;
        height: 0.8vh;
        margin-top: 0.8vh;
        border-radius: 50%;
        background: #2BB6A8;
        flex-shrink: 0;
    }
    .ds__practice-card {
        margin-top: 2.4vh;
        display: grid;
        grid-template-columns: repeat(4, 1fr);
        gap: 1.2vh;
    }
    .ds__practice-card div {
        background: rgba(43, 182, 168, 0.1);
        border-radius: 1vh;
        padding: 1.4vh;
        display: flex;
        flex-direction: column;
        color: rgba(255, 255, 255, 0.65);
        font-size: 1.3vh;
    }
    .ds__practice-card b {
        font-family: 'TTNorms-Bold';
        font-size: 3vh;
        color: white;
    }
    .ds__exam {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
    }
    .ds__progress {
        width: 70vh;
        margin-bottom: 3vh;
    }
    .ds__progress-text {
        color: rgba(255, 255, 255, 0.6);
        margin-bottom: 0.8vh;
    }
    .ds__progress-bar {
        height: 0.8vh;
        border-radius: 0.4vh;
        background: rgba(255, 255, 255, 0.1);
        overflow: hidden;
    }
    .ds__progress-bar div {
        height: 100%;
        background: #2BB6A8;
        transition: width 0.3s;
    }
    .ds__question {
        width: 70vh;
        font-family: 'TTNorms-Bold';
        font-size: 3vh;
        line-height: 1.3;
        margin-bottom: 3vh;
    }
    .ds__answers {
        width: 70vh;
        display: flex;
        flex-direction: column;
        gap: 1.2vh;
    }
    .ds__answer {
        display: flex;
        align-items: center;
        gap: 1.6vh;
        padding: 1.8vh 2vh;
        border-radius: 1vh;
        background: rgba(255, 255, 255, 0.06);
        border: 1px solid rgba(255, 255, 255, 0.1);
        cursor: pointer;
        font-size: 1.8vh;
        transition: border-color 0.15s, background 0.15s;
    }
    .ds__answer:hover {
        border-color: #2BB6A8;
        background: rgba(43, 182, 168, 0.12);
    }
    .ds__answer span {
        width: 3.4vh;
        height: 3.4vh;
        border-radius: 0.8vh;
        background: rgba(255, 255, 255, 0.1);
        display: flex;
        align-items: center;
        justify-content: center;
        font-family: 'TTNorms-Bold';
        flex-shrink: 0;
    }
    .ds__exam-hint {
        margin-top: 3vh;
        color: rgba(255, 255, 255, 0.45);
        font-size: 1.3vh;
    }
    .ds__result {
        width: 60vh;
        text-align: center;
        display: flex;
        flex-direction: column;
        align-items: center;
    }
    .ds__result-score {
        font-family: 'TTNorms-Bold';
        font-size: 9vh;
        color: #ff8a8a;
        line-height: 1;
    }
    .ds__result.passed .ds__result-score {
        color: #2BB6A8;
    }
    .ds__result-score span {
        font-size: 4vh;
        color: rgba(255, 255, 255, 0.4);
    }
    .ds__result-title {
        font-family: 'TTNorms-Bold';
        font-size: 3vh;
        margin: 1.6vh 0 1vh;
    }
    .ds__result-text {
        color: rgba(255, 255, 255, 0.7);
        line-height: 1.5;
    }
    .ds__result-buttons {
        display: flex;
        gap: 1.2vh;
        margin-top: 3vh;
        width: 100%;
    }
    .ds__result-buttons .ds__btn {
        flex: 1;
    }
</style>
