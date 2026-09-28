<script>
    // Админ-панель настроек: /cfg → client.cfgpanel.open (сервер Functions/ConfigPanel.cs).
    // Отправляются только изменённые поля; сервер проверяет права и диапазоны, делает бэкап, применяет сразу и сохраняет.
    // Вкладки настроек + «История» (откат) + «Пресеты» (наборы изменений, например «Выходные x2»).
    import { executeClient } from "api/rage";
    import { onDestroy } from "svelte";
    import { fade } from "svelte/transition";

    export let viewData;

    const parse = (data) => {
        try {
            const result = typeof data === "string" ? JSON.parse(data) : data;
            if (Array.isArray(result)) return { sections: result, history: [], presets: [], canPresets: false };
            return {
                sections: Array.isArray(result && result.sections) ? result.sections : [],
                history: Array.isArray(result && result.history) ? result.history : [],
                presets: Array.isArray(result && result.presets) ? result.presets : [],
                canPresets: !!(result && result.canPresets),
            };
        } catch (e) {
            return { sections: [], history: [], presets: [], canPresets: false };
        }
    };

    let data = parse(viewData);
    $: sections = data.sections;
    let activeId = data.sections.length ? data.sections[0].id : "history";
    // values[sectionId][key] — текущее значение в поле ввода (строка)
    let values = {};
    let search = "";
    let saving = false;
    let status = "";
    let statusOk = true;
    let presetName = "";
    let confirmKey = null;
    let openPreset = null;
    let historyFilter = "";

    const reset = () => {
        const next = {};
        for (const section of data.sections) {
            next[section.id] = {};
            for (const group of section.groups)
                for (const field of group.fields) next[section.id][field.key] = String(field.value);
        }
        values = next;
    };
    reset();

    const toNumber = (text) => Number(String(text).replace(",", ".").trim());

    const fieldError = (field, text) => {
        if (field.type === "bool" || field.type === "select") return null;
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

    const shown = (field, value) => {
        if (field.type === "bool") return Number(value) >= 0.5 ? "вкл" : "выкл";
        if (field.type === "select" && field.options) {
            const option = field.options.find((o) => Math.abs(o.value - Number(value)) < 0.00001);
            if (option) return option.label;
        }
        return String(value);
    };

    const collectChanges = (vals) => {
        const changes = {};
        let count = 0;
        for (const section of data.sections) {
            if (!section.canEdit) continue;
            for (const group of section.groups)
                for (const field of group.fields) {
                    const text = vals[section.id] && vals[section.id][field.key];
                    if (text === undefined || !isChanged(field, text)) continue;
                    if (!changes[section.id]) changes[section.id] = {};
                    changes[section.id][field.key] = toNumber(text);
                    count++;
                }
        }
        return { changes, count };
    };

    $: active = sections.find((s) => s.id === activeId);
    $: pending = collectChanges(values);
    $: changedCount = pending.count;

    const sectionChanged = (section, vals) =>
        section.groups.some((g) => g.fields.some((f) => vals[section.id] && isChanged(f, vals[section.id][f.key])));

    $: hasErrors = sections.some((section) =>
        section.canEdit && section.groups.some((g) => g.fields.some((f) => values[section.id] && fieldError(f, values[section.id][f.key]))));

    const matches = (field, query) => !query || field.label.toLowerCase().includes(query.toLowerCase());

    const busy = (text) => {
        saving = true;
        status = text;
        statusOk = true;
        confirmKey = null;
    };

    const save = () => {
        if (saving || !changedCount || hasErrors) return;
        busy("Сохранение...");
        executeClient("client.cfgpanel.save", JSON.stringify(pending.changes));
    };

    const toggleBool = (section, field) => {
        if (!section.canEdit) return;
        values[section.id][field.key] = Number(values[section.id][field.key]) >= 0.5 ? "0" : "1";
    };

    const revertField = (sectionId, field) => {
        values[sectionId][field.key] = String(field.value);
    };

    // Двойное нажатие для опасных действий: первое — «Точно?», второе — выполнить
    const confirm = (key, action) => {
        if (confirmKey === key) {
            confirmKey = null;
            action();
        } else confirmKey = key;
    };

    const rollback = (entry) => confirm(`h${entry.id}`, () => {
        busy("Откат...");
        executeClient("client.cfgpanel.rollback", entry.id);
    });

    const reload = (section) => confirm(`r${section.id}`, () => {
        busy("Перечитываю...");
        executeClient("client.cfgpanel.reload", section.id);
    });

    const savePreset = () => {
        if (saving || !presetName.trim() || !changedCount || hasErrors) return;
        busy("Сохраняю пресет...");
        executeClient("client.cfgpanel.preset.save", presetName.trim(), JSON.stringify(pending.changes));
    };

    const applyPreset = (preset) => confirm(`p${preset.name}`, () => {
        busy(`Применяю «${preset.name}»...`);
        executeClient("client.cfgpanel.preset.apply", preset.name);
    });

    const deletePreset = (preset) => confirm(`d${preset.name}`, () => {
        busy("Удаляю пресет...");
        executeClient("client.cfgpanel.preset.delete", preset.name);
    });

    const sectionTitle = (id) => {
        const section = sections.find((s) => s.id === id);
        return section ? section.title : id;
    };

    const fieldOf = (sectionId, key) => {
        const section = sections.find((s) => s.id === sectionId);
        if (!section) return null;
        for (const group of section.groups) {
            const field = group.fields.find((f) => f.key === key);
            if (field) return field;
        }
        return null;
    };

    const canEditSection = (id) => {
        const section = sections.find((s) => s.id === id);
        return !!(section && section.canEdit);
    };

    $: historyShown = data.history.filter((h) => {
        if (!historyFilter) return true;
        const q = historyFilter.toLowerCase();
        return `${h.admin} ${h.sectionTitle} ${h.label}`.toLowerCase().includes(q);
    });

    const close = () => executeClient("client.cfgpanel.close");

    window.events.addEvent("cef.cfgpanel.update", (ok, message, json) => {
        saving = false;
        const previous = values;
        data = parse(json);
        if (activeId !== "history" && activeId !== "presets" && !data.sections.find((s) => s.id === activeId))
            activeId = data.sections.length ? data.sections[0].id : "history";
        reset();
        // При ошибке поля остаются как ввёл админ, чтобы поправить; при успехе — свежие значения сервера
        if (!ok) {
            for (const section of data.sections)
                for (const group of section.groups)
                    for (const field of group.fields) {
                        const old = previous[section.id] && previous[section.id][field.key];
                        if (old !== undefined && isChanged(field, old)) values[section.id][field.key] = old;
                    }
            values = values;
        } else presetName = "";
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
            {#if activeId === "history"}
                <input class="cfgpanel__search" placeholder="Фильтр: админ, вкладка, поле" bind:value={historyFilter} />
            {:else if activeId !== "presets"}
                <input class="cfgpanel__search" placeholder="Поиск по названию" bind:value={search} />
            {/if}
            <div class="cfgpanel__close" on:click={close}>✕</div>
        </div>

        <div class="cfgpanel__body">
            <div class="cfgpanel__tabs">
                {#each sections as section (section.id)}
                    <div class="cfgpanel__tab" class:active={activeId === section.id} on:click={() => (activeId = section.id)}>
                        <span>{section.title}{#if !section.canEdit} 🔒{/if}</span>
                        {#if sectionChanged(section, values)}<span class="cfgpanel__dot" />{/if}
                    </div>
                {/each}
                <div class="cfgpanel__sep" />
                <div class="cfgpanel__tab" class:active={activeId === "history"} on:click={() => (activeId = "history")}>
                    <span>История</span><span class="cfgpanel__count">{data.history.length}</span>
                </div>
                <div class="cfgpanel__tab" class:active={activeId === "presets"} on:click={() => (activeId = "presets")}>
                    <span>Пресеты</span><span class="cfgpanel__count">{data.presets.length}</span>
                </div>
            </div>

            <div class="cfgpanel__content">
                {#if activeId === "history"}
                    <div class="cfgpanel__file">Последние изменения (новые сверху). «Вернуть» ставит старое значение — это тоже попадает в историю.</div>
                    {#if !historyShown.length}
                        <div class="cfgpanel__empty">Изменений пока не было</div>
                    {/if}
                    {#each historyShown as entry (entry.id)}
                        <div class="cfgpanel__hrow">
                            <div class="cfgpanel__htime">{entry.time}</div>
                            <div class="cfgpanel__hmain">
                                <div><b>{entry.sectionTitle}</b>: {entry.label}</div>
                                <div class="cfgpanel__hint">{entry.admin} · #{entry.id}</div>
                            </div>
                            <div class="cfgpanel__hval">
                                {fieldOf(entry.section, entry.key) ? shown(fieldOf(entry.section, entry.key), entry.old) : entry.old}
                                →
                                {fieldOf(entry.section, entry.key) ? shown(fieldOf(entry.section, entry.key), entry.new) : entry.new}
                            </div>
                            {#if canEditSection(entry.section) && fieldOf(entry.section, entry.key)}
                                <div class="cfgpanel__btn small secondary" class:warn={confirmKey === `h${entry.id}`} on:click={() => rollback(entry)}>
                                    {confirmKey === `h${entry.id}` ? "Точно?" : "Вернуть"}
                                </div>
                            {/if}
                        </div>
                    {/each}
                {:else if activeId === "presets"}
                    <div class="cfgpanel__file">
                        Пресет — набор изменений, который применяется одной кнопкой (например «Выходные x2» и «Обычные»).
                        Файлы лежат в settings/cfg_presets — их можно копировать на другой сервер.
                    </div>
                    {#if data.canPresets}
                        <div class="cfgpanel__presetnew">
                            <input placeholder="Название пресета" maxlength="40" bind:value={presetName} />
                            <div class="cfgpanel__btn" class:disabled={saving || !presetName.trim() || !changedCount || hasErrors} on:click={savePreset}>
                                Сохранить изменения ({changedCount}) в пресет
                            </div>
                        </div>
                        <div class="cfgpanel__hint">Измени нужные поля на вкладках (не сохраняя), затем впиши название и нажми кнопку. Сами настройки при этом не меняются.</div>
                    {/if}
                    {#if !data.presets.length}
                        <div class="cfgpanel__empty">Пресетов нет</div>
                    {/if}
                    {#each data.presets as preset (preset.name)}
                        <div class="cfgpanel__preset">
                            <div class="cfgpanel__hmain" on:click={() => (openPreset = openPreset === preset.name ? null : preset.name)}>
                                <div><b>{preset.name}</b> <span class="cfgpanel__hint">{preset.count} настроек {openPreset === preset.name ? "▴" : "▾"}</span></div>
                            </div>
                            <div class="cfgpanel__btn small" class:warn={confirmKey === `p${preset.name}`} on:click={() => applyPreset(preset)}>
                                {confirmKey === `p${preset.name}` ? "Точно применить?" : "Применить"}
                            </div>
                            {#if data.canPresets}
                                <div class="cfgpanel__btn small secondary" class:warn={confirmKey === `d${preset.name}`} on:click={() => deletePreset(preset)}>
                                    {confirmKey === `d${preset.name}` ? "Точно?" : "Удалить"}
                                </div>
                            {/if}
                        </div>
                        {#if openPreset === preset.name && preset.changes}
                            <div class="cfgpanel__presetlist">
                                {#each Object.keys(preset.changes) as sectionId}
                                    {#each Object.keys(preset.changes[sectionId] || {}) as key}
                                        <div>
                                            {sectionTitle(sectionId)}: {fieldOf(sectionId, key) ? fieldOf(sectionId, key).label : key}
                                            → <b>{fieldOf(sectionId, key) ? shown(fieldOf(sectionId, key), preset.changes[sectionId][key]) : preset.changes[sectionId][key]}</b>
                                        </div>
                                    {/each}
                                {/each}
                            </div>
                        {/if}
                    {/each}
                {:else if active}
                    <div class="cfgpanel__sectionhead">
                        <div class="cfgpanel__file">
                            Источник: {active.file}. Изменения действуют сразу, рестарт не нужен.
                            {#if !active.canEdit}<span class="cfgpanel__ro">Только просмотр: менять можно с {active.editLevel} ур.</span>{/if}
                        </div>
                        {#if active.canReload}
                            <div class="cfgpanel__btn small secondary" class:warn={confirmKey === `r${active.id}`} on:click={() => reload(active)}
                                title="Если файл правили вручную">
                                {confirmKey === `r${active.id}` ? "Точно? Несохранённое пропадёт" : "Перечитать с диска"}
                            </div>
                        {/if}
                    </div>
                    {#if active.info && active.info.length}
                        <div class="cfgpanel__info">
                            {#each active.info as line}<div>≈ {line}</div>{/each}
                        </div>
                    {/if}
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
                                        {#if field.type === "bool"}
                                            <div class="cfgpanel__toggle" class:on={Number(values[active.id][field.key]) >= 0.5} class:disabled={!active.canEdit}
                                                on:click={() => toggleBool(active, field)}>
                                                <div class="cfgpanel__knob" />
                                            </div>
                                        {:else if field.type === "select"}
                                            <select bind:value={values[active.id][field.key]} disabled={!active.canEdit}>
                                                {#each field.options || [] as option}
                                                    <option value={String(option.value)}>{option.label}</option>
                                                {/each}
                                            </select>
                                        {:else}
                                            <input
                                                class:error={fieldError(field, values[active.id][field.key])}
                                                bind:value={values[active.id][field.key]}
                                                disabled={!active.canEdit}
                                                inputmode="decimal" />
                                        {/if}
                                        {#if fieldError(field, values[active.id][field.key])}
                                            <div class="cfgpanel__error">{fieldError(field, values[active.id][field.key])}</div>
                                        {:else if isChanged(field, values[active.id][field.key])}
                                            <div class="cfgpanel__was" on:click={() => revertField(active.id, field)} title="Вернуть">было {shown(field, field.value)} ↺</div>
                                        {/if}
                                    </div>
                                </div>
                            {/each}
                        {/if}
                    {/each}
                {:else}
                    <div class="cfgpanel__empty">Нет данных</div>
                {/if}
            </div>
        </div>

        <div class="cfgpanel__footer">
            <div class="cfgpanel__status" class:bad={!statusOk}>{status}</div>
            <div class="cfgpanel__btn secondary" on:click={() => { reset(); status = ""; confirmKey = null; }}>Сбросить</div>
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
        width: 1040px;
        max-width: 94vw;
        height: 760px;
        max-height: 92vh;
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
        margin-right: auto;
    }
    .cfgpanel__search,
    .cfgpanel__presetnew input {
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
        width: 200px;
        padding: 12px;
        border-right: 1px solid rgba(255, 255, 255, 0.08);
        display: flex;
        flex-direction: column;
        gap: 6px;
        overflow-y: auto;
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
    .cfgpanel__sep {
        height: 1px;
        background: rgba(255, 255, 255, 0.08);
        margin: 6px 0;
    }
    .cfgpanel__count {
        font-size: 12px;
        opacity: 0.6;
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
    .cfgpanel__sectionhead {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        gap: 12px;
    }
    .cfgpanel__file {
        font-size: 12px;
        opacity: 0.55;
        margin-bottom: 6px;
        line-height: 1.5;
    }
    .cfgpanel__ro {
        display: block;
        color: #f5b83d;
        opacity: 1;
    }
    .cfgpanel__info {
        margin: 8px 0 4px;
        padding: 10px 12px;
        border-radius: 8px;
        background: rgba(45, 108, 223, 0.12);
        border: 1px solid rgba(45, 108, 223, 0.3);
        font-size: 13px;
        line-height: 1.6;
    }
    .cfgpanel__empty {
        opacity: 0.5;
        padding: 24px 0;
        text-align: center;
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
    .cfgpanel__input input,
    .cfgpanel__input select {
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
    .cfgpanel__input input:disabled,
    .cfgpanel__input select:disabled {
        opacity: 0.55;
    }
    .cfgpanel__input input:focus {
        border-color: #2d6cdf;
    }
    .cfgpanel__input input.error {
        border-color: #e5484d;
    }
    .cfgpanel__toggle {
        width: 46px;
        height: 24px;
        border-radius: 12px;
        background: rgba(255, 255, 255, 0.15);
        position: relative;
        cursor: pointer;
        transition: background 0.15s;
    }
    .cfgpanel__toggle.on {
        background: #2fa36b;
    }
    .cfgpanel__toggle.disabled {
        opacity: 0.55;
        cursor: default;
    }
    .cfgpanel__knob {
        position: absolute;
        top: 3px;
        left: 3px;
        width: 18px;
        height: 18px;
        border-radius: 50%;
        background: #fff;
        transition: left 0.15s;
    }
    .cfgpanel__toggle.on .cfgpanel__knob {
        left: 25px;
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
    .cfgpanel__hrow,
    .cfgpanel__preset {
        display: flex;
        align-items: center;
        gap: 14px;
        padding: 9px 10px;
        border-radius: 8px;
        font-size: 13px;
    }
    .cfgpanel__hrow:nth-child(even),
    .cfgpanel__preset {
        background: rgba(255, 255, 255, 0.03);
    }
    .cfgpanel__preset {
        margin-top: 8px;
    }
    .cfgpanel__htime {
        width: 80px;
        opacity: 0.6;
        flex-shrink: 0;
    }
    .cfgpanel__hmain {
        flex: 1;
        cursor: default;
    }
    .cfgpanel__preset .cfgpanel__hmain {
        cursor: pointer;
    }
    .cfgpanel__hval {
        font-weight: 600;
        white-space: nowrap;
    }
    .cfgpanel__presetnew {
        display: flex;
        gap: 12px;
        align-items: center;
        margin: 10px 0 4px;
    }
    .cfgpanel__presetlist {
        margin: 4px 0 4px 16px;
        padding: 8px 12px;
        border-left: 2px solid rgba(45, 108, 223, 0.5);
        font-size: 12px;
        line-height: 1.7;
        opacity: 0.85;
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
        white-space: nowrap;
    }
    .cfgpanel__btn.small {
        padding: 6px 12px;
        font-size: 12px;
    }
    .cfgpanel__btn.secondary {
        background: rgba(255, 255, 255, 0.08);
    }
    .cfgpanel__btn.warn {
        background: #c2410c;
    }
    .cfgpanel__btn.disabled {
        opacity: 0.4;
        cursor: default;
    }
</style>
