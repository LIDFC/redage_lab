<script>
    import { sound, playSound } from 'api/uiSound'
    import { onDestroy } from 'svelte'
    import { fade, scale } from 'svelte/transition'
    import { executeClient } from 'api/rage'
    import { TimeFormat } from 'api/moment'
    import { serverDateTime } from 'store/server'
    import { charFractionID, charOrganizationID } from 'store/chars'
    import fractionNames from 'json/fraction.js'

    import FractionTable from '@/views/player/menu/elements/fractions/index.svelte'
    import Business from './apps/business/index.svelte'
    import Forbes from './apps/forbes.svelte'
    import { icons } from './icons'
    import './tablet.sass'

    // Планшет (клавиша K). Управление бизнесом, фракцией/организацией, маркетплейс и Forbes — только здесь,
    // в телефоне и в меню «I» этих разделов больше нет.

    let app = "";

    $: apps = [
        $charFractionID > 0 && { id: "Fractions", name: fractionNames[$charFractionID] || "Фракция", icon: "fraction", color: "#3B82F6" },
        $charOrganizationID > 0 && { id: "Organization", name: "Организация", icon: "organization", color: "#8B5CF6" },
        { id: "business", name: "Бизнес", icon: "business", color: "#7ED321" },
        { id: "marketplace", name: "Маркетплейс", icon: "marketplace", color: "#F59E0B" },
        { id: "forbes", name: "Forbes", icon: "forbes", color: "#E5B53A" },
    ].filter(Boolean);

    const open = (id) => {
        if (id === "marketplace") {
            executeClient("client.tablet.marketplace");
            return;
        }
        app = id;
    };

    const close = () => executeClient("client.tablet.close");

    // Открытие сразу в нужном приложении (клиент: client.tablet.openApp)
    window.tabletOpenApp = (id) => { app = id; window.tabletPendingApp = null; };
    if (window.tabletPendingApp) {
        app = window.tabletPendingApp;
        window.tabletPendingApp = null;
    }
    onDestroy(() => { window.tabletOpenApp = null; });

    const onKeyUp = (event) => {
        if (event.keyCode !== 27 || isFocus)
            return;
        // Во фракционном меню Esc закрывает его всплывающие окна — там возвращаемся кнопкой «домой»
        if (app === "Fractions" || app === "Organization")
            return;
        if (app) app = "";
        else close();
    };

    // Ввод в полях: не даём биндеру закрыть планшет по K
    let isFocus = false;
    const onFocusIn = (e) => {
        if (e.target && (e.target.tagName === "INPUT" || e.target.tagName === "TEXTAREA")) {
            isFocus = true;
            executeClient("client.tablet.inputFocus", true);
        }
    };
    const onFocusOut = (e) => {
        if (isFocus && e.target && (e.target.tagName === "INPUT" || e.target.tagName === "TEXTAREA")) {
            isFocus = false;
            executeClient("client.tablet.inputFocus", false);
        }
    };
    onDestroy(() => isFocus && executeClient("client.tablet.inputFocus", false));
</script>

<svelte:window on:keyup={onKeyUp} />

<div class="tablet" in:fade={{ duration: 150 }}>
    <div class="tablet__frame" in:scale={{ start: 0.96, duration: 200 }} on:focusin={onFocusIn} on:focusout={onFocusOut}>
        <div class="tablet__screen">
            <div class="tablet__status">
                <span>{TimeFormat($serverDateTime, "H:mm")}</span>
                <span class="tablet__status_center">{TimeFormat($serverDateTime, "dddd, D MMMM")}</span>
                <span class="tablet__status_right">MkeiitRR · 5G · 100%</span>
            </div>

            {#if !app}
                <div class="tablet__home" in:fade={{ duration: 150 }}>
                    <div class="tablet__clock">
                        <b>{TimeFormat($serverDateTime, "H:mm")}</b>
                        <span>{TimeFormat($serverDateTime, "dddd, D MMMM")}</span>
                    </div>
                    <div class="tablet__apps">
                        {#each apps as item}
                            <div class="tablet__app" use:sound={"tap"} on:click={() => open(item.id)}>
                                <div class="tablet__app_icon" style="--c: {item.color}">{@html icons[item.icon]}</div>
                                <span>{item.name}</span>
                            </div>
                        {/each}
                    </div>
                    <div class="tablet__dock">
                        {#each apps as item}
                            <div class="tablet__app_icon small" style="--c: {item.color}" use:sound={"tap"} on:click={() => open(item.id)}>{@html icons[item.icon]}</div>
                        {/each}
                    </div>
                </div>
            {:else}
                <div class="tablet__app_view" class:fraction={app === "Fractions" || app === "Organization"} in:fade={{ duration: 150 }}>
                    {#if app === "Fractions" || app === "Organization"}
                        <FractionTable selectView={app} visible={true} />
                    {:else if app === "business"}
                        <Business onClose={close} />
                    {:else if app === "forbes"}
                        <Forbes />
                    {/if}
                </div>
            {/if}

            <div class="tablet__homebar" use:sound={"tap"} on:click={() => app ? (app = "") : close()}></div>
        </div>
    </div>
</div>
