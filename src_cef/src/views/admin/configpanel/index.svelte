<script>
    // Админ-панель настроек settings/*.json: /cfg → client.cfgpanel.open (сервер Functions/ConfigPanel.cs).
    // Отправляются только изменённые поля; сервер проверяет диапазоны, применяет сразу и перезаписывает файл.
    import { executeClient } from "api/rage";
    import { onDestroy } from "svelte";
    import { fade } from "svelte/transition";

    export let viewData;

    const parse = (data) => {
        try {
            const result = typeof data === "string" ? JSON.parse(data) : data;
            return Array.isArray(result) ? result : [];
        } catch (e) {
            return [];
        }
    };

    let sections = parse(viewData);
    let activeId = sections.length ? sections[0].id : null;
    // values[sectionId][key] — текущее значение в поле ввода (строка)
    let values = {};
    let search = "";
    let saving = false;
    let status = "";
    let statusOk = true;

    const reset = () => {
        const next = {};
        for (const section of sections) {
            next[section.id] = {};
            for (const group of section.groups)
                for (const field of group.fields) next[section.id][field.key] = String(field.value);
        }
        values = next;
    };
    reset();

    const toNumber = (text) => Number(String(text).replace(",", ".").trim());

    const fieldError = (field, text) => {
        if (String(text).trim() === "") return "Пусто";
        const n = toNumber(text);
        if (!Number.isFinite(n)) return "Не число";
        if (field.type === "int" && !Number.isInteger(n)) return "Только целое";
        if (n < field.min || n > field.max) return `От ${field.min} до ${field.max}`;
        return null;
    };

    const isChanged = (field, text) => {
        const n = toNumber(text);
        return Number.isFinite(n) && Math.abs(n - field.value) > 0.00001;
    };

    $: active = sections.find((s) => s.id === activeId) || sections[0];

    $: changedCount = (() => {
        let count = 0;
        for (const section of sections)
            for (const group of section.groups)
                for (const field of group.fields)
                    if (values[section.id] && isChanged(field, values[section.id][field.key])) count++;
        return count;
    })();

    const sectionChanged = (section, vals) =>
        section.groups.some((g) => g.fields.some((f) => vals[section.id] && isChanged(f, vals[section.id][f.key])));

    $: hasErrors = sections.some((section) =>
        section.groups.some((g) => g.fields.some((f) => values[section.id] && fieldError(f, values[section.id][f.key]))));

    const matches = (field, query) => !query || field.label.toLowerCase().includes(query.toLowerCase());

    const save = () => {
        if (saving || !changedCount || hasErrors) return;
        const changes = {};
        for (const section of sections)
            for (const group of section.groups)
                for (const field of group.fields) {
                    const text = values[section.id][field.key];
                    if (!isChanged(field, text)) continue;
                    if (!changes[section.id]) changes[section.id] = {};
                    changes[section.id][field.key] = toNumber(text);
                }
        saving = true;
        status = "Сохранение...";
        statusOk = true;
        executeClient("client.cfgpanel.save", JSON.stringify(changes));
    };

    const revertField = (sectionId, field) => {
        values[sectionId][field.key] = String(field.value);
        values = values;
    };

    const close = () => executeClient("client.cfgpanel.close");

    window.events.addEvent("cef.cfgpanel.update", (ok, message, json) => {
        saving = false;
        sections = parse(json);
        if (!sections.find((s) => s.id === activeId)) activeId = sections.length ? sections[0].id : null;
        // Поля с ошибкой оставляем как ввёл админ, остальные — из свежих данных сервера
        const previous = values;
        reset();
        if (!ok) {
            for (const section of sections)
                for (const group of section.groups)
                    for (const field of group.fields) {
                        const old = previous[section.id] && previous[section.id][field.key];
                        if (old !== undefined && isChanged(field, old)) values[section.id][field.key] = old;
                    }
            values = values;
        }
        status = message || "";
        statusOk = !!ok;
    });
    onDestroy(() => window.events.removeEvent("cef.cfgpanel.update"));

    const onKey = (event) => {
        if (event.key === "Escape") close();
        else if (event.key === "Enter" && (event.ctrlKey || event.metaKey)) save();
    };
</script>

<svelte:window on:keyup={onKey} />

<div class="cfgpanel" in:fade={{ duration: 150 }}>
    <div class="cfgpanel__window">
        <div class="cfgpanel__header">
            <div class="cfgpanel__title">Настройки сервера</div>
            <input class="cfgpanel__search" placeholder="Поиск по названию" bind:value={search} />
            <div class="cfgpanel__close" on:click={close}>✕</div>
        </div>

        <div class="cfgpanel__body">
            <div class="cfgpanel__tabs">
                {#each sections as section (section.id)}
                    <div class="cfgpanel__tab" class:active={active && active.id === section.id} on:click={() => (activeId = section.id)}>
                        {section.title}
                        {#if sectionChanged(section, values)}<span class="cfgpanel__dot" />{/if}
                    </div>
                {/each}
            </div>

            <div class="cfgpanel__content">
                {#if active}
                    <div class="cfgpanel__file">Файл: {active.file}. Изменения действуют сразу, рестарт не нужен.</div>
                    {#each active.groups as group}
                        {#if group.fields.some((f) => matches(f, search))}
                            <div class="cfgpanel__group">{group.title}</div>
                            {#each group.fields.filter((f) => matches(f, search)) as field (field.key)}
                                <div class="cfgpanel__row" class:changed={isChanged(field, values[active.id][field.key])}>
                                    <div class="cfgpanel__label">
                                        {field.label}
                                        {#if field.hint}<div class="cfgpanel__hint">{field.hint}</div>{/if}
                                    </div>
                                    <div class="cfgpanel__input">
                                        <input
                                            class:error={fieldError(field, values[active.id][field.key])}
                                            bind:value={values[active.id][field.key]}
                                            inputmode="decimal" />
                                        {#if fieldError(field, values[active.id][field.key])}
                                            <div class="cfgpanel__error">{fieldError(field, values[active.id][field.key])}</div>
                                        {:else if isChanged(field, values[active.id][field.key])}
                                            <div class="cfgpanel__was" on:click={() => revertField(active.id, field)} title="Вернуть">было {field.value} ↺</div>
                                        {/if}
                                    </div>
                                </div>
                            {/each}
                        {/if}
                    {/each}
                {:else}
                    <div class="cfgpanel__file">Нет данных</div>
                {/if}
            </div>
        </div>

        <div class="cfgpanel__footer">
            <div class="cfgpanel__status" class:bad={!statusOk}>{status}</div>
            <div class="cfgpanel__btn secondary" on:click={() => { reset(); status = ""; }}>Сбросить</div>
            <div class="cfgpanel__btn" class:disabled={saving || !changedCount || hasErrors} on:click={save}>
                Сохранить{changedCount ? ` (${changedCount})` : ""}
            </div>
        </div>
    </div>
</div>

<style>
    .cfgpanel {
        position: absolute;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        display: flex;
        align-items: center;
        justify-content: center;
        background: rgba(0, 0, 0, 0.55);
        font-family: "Gilroy", "Montserrat", sans-serif;
        color: #fff;
    }
    .cfgpanel__window {
        width: 980px;
        max-width: 94vw;
        height: 720px;
        max-height: 90vh;
        display: flex;
        flex-direction: column;
        background: #16181d;
        border: 1px solid rgba(255, 255, 255, 0.08);
        border-radius: 12px;
        overflow: hidden;
    }
    .cfgpanel__header {
        display: flex;
        align-items: center;
        gap: 16px;
        padding: 16px 20px;
        border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    }
    .cfgpanel__title {
        font-size: 20px;
        font-weight: 700;
    }
    .cfgpanel__search {
        margin-left: auto;
        width: 260px;
        padding: 8px 12px;
        border-radius: 8px;
        border: 1px solid rgba(255, 255, 255, 0.12);
        background: #0f1115;
        color: #fff;
        outline: none;
    }
    .cfgpanel__close {
        cursor: pointer;
        font-size: 18px;
        opacity: 0.7;
        padding: 4px 8px;
    }
    .cfgpanel__close:hover {
        opacity: 1;
    }
    .cfgpanel__body {
        flex: 1;
        display: flex;
        min-height: 0;
    }
    .cfgpanel__tabs {
        width: 190px;
        padding: 12px;
        border-right: 1px solid rgba(255, 255, 255, 0.08);
        display: flex;
        flex-direction: column;
        gap: 6px;
    }
    .cfgpanel__tab {
        padding: 10px 12px;
        border-radius: 8px;
        cursor: pointer;
        opacity: 0.75;
        display: flex;
        align-items: center;
        justify-content: space-between;
    }
    .cfgpanel__tab:hover {
        background: rgba(255, 255, 255, 0.05);
        opacity: 1;
    }
    .cfgpanel__tab.active {
        background: #2d6cdf;
        opacity: 1;
    }
    .cfgpanel__dot {
        width: 8px;
        height: 8px;
        border-radius: 50%;
        background: #f5b83d;
    }
    .cfgpanel__content {
        flex: 1;
        overflow-y: auto;
        padding: 12px 20px 20px;
    }
    .cfgpanel__file {
        font-size: 12px;
        opacity: 0.5;
        margin-bottom: 4px;
    }
    .cfgpanel__group {
        margin: 18px 0 8px;
        font-size: 13px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: #8fb4ff;
    }
    .cfgpanel__row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 16px;
        padding: 8px 10px;
        border-radius: 8px;
    }
    .cfgpanel__row:nth-child(even) {
        background: rgba(255, 255, 255, 0.02);
    }
    .cfgpanel__row.changed {
        background: rgba(245, 184, 61, 0.1);
    }
    .cfgpanel__label {
        font-size: 14px;
    }
    .cfgpanel__hint {
        font-size: 11px;
        opacity: 0.5;
        margin-top: 2px;
    }
    .cfgpanel__input {
        display: flex;
        flex-direction: column;
        align-items: flex-end;
        min-width: 150px;
    }
    .cfgpanel__input input {
        width: 150px;
        padding: 7px 10px;
        border-radius: 6px;
        border: 1px solid rgba(255, 255, 255, 0.12);
        background: #0f1115;
        color: #fff;
        text-align: right;
        font-size: 14px;
        outline: none;
    }
    .cfgpanel__input input:focus {
        border-color: #2d6cdf;
    }
    .cfgpanel__input input.error {
        border-color: #e5484d;
    }
    .cfgpanel__error {
        font-size: 11px;
        color: #ff6b6f;
        margin-top: 3px;
    }
    .cfgpanel__was {
        font-size: 11px;
        color: #f5b83d;
        margin-top: 3px;
        cursor: pointer;
    }
    .cfgpanel__footer {
        display: flex;
        align-items: center;
        gap: 12px;
        padding: 14px 20px;
        border-top: 1px solid rgba(255, 255, 255, 0.08);
    }
    .cfgpanel__status {
        flex: 1;
        font-size: 13px;
        color: #7ee2a8;
    }
    .cfgpanel__status.bad {
        color: #ff6b6f;
    }
    .cfgpanel__btn {
        padding: 10px 22px;
        border-radius: 8px;
        background: #2d6cdf;
        cursor: pointer;
        font-weight: 600;
        user-select: none;
    }
    .cfgpanel__btn.secondary {
        background: rgba(255, 255, 255, 0.08);
    }
    .cfgpanel__btn.disabled {
        opacity: 0.4;
        cursor: default;
    }
</style>
