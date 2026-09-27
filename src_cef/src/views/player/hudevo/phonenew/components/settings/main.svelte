<script>
    import { translateText } from 'lang'
    import { fade } from 'svelte/transition'
    import { charSim } from 'store/chars';
    import { executeClientAsyncToGroup, executeClientToGroup } from "api/rage";
    export let onSelectedView;

    let isAir = false;
    executeClientAsyncToGroup("settings.isAir").then((result) => {
        isAir = result;
    });

    const updateAirStatus = () => {
        if (!window.loaderData.delay ("phone.updateAirStatus", 1 * 60))
            return;

        isAir = !isAir;
        executeClientToGroup("settings.air")
    }

    const onRemoveSim = () => {
        if (!window.loaderData.delay ("phone.onRemoveSim", 5))
            return;

        executeClientToGroup("settings.removeSim")
    }

    // VPN: только на эту сессию, сервер решает сам (Чёрный рынок: src_client/phone/blackmarket.js)
    let isVpn = false;
    executeClientAsyncToGroup("settings.isVpn").then((result) => {
        isVpn = !!result;
    });
    window.events.addEvent("phone.vpn", (value) => isVpn = !!value);

    const updateVpn = () => {
        if (!window.loaderData.delay ("phone.updateVpn", 1))
            return;
        executeClientToGroup("settings.vpn")
    }

    let forbesVisible = false;
    executeClientAsyncToGroup("settings.forbesVisible").then((result) => {
        forbesVisible = result;
    });

    const updateForbesVisible = () => {
        if (!window.loaderData.delay ("phone.updateAirStatus", 1 * 60))
            return;

        forbesVisible = !forbesVisible;
        executeClientToGroup("settings.forbesVisible")
    }

</script>
<div class="newphone__settings_content" in:fade>
    <div class="box-center box-between w-1 newphone__project_padding16">
        <div class="newphone__maps_header">{translateText('player2', 'Настройки')}</div>
        <div></div>
    </div>
    <div class="newphone__contacts_info newphone__project_padding16 b-white">
        <div class="newphone__contacts_avatar"></div>
        <div class="box-column">
            <div class="gray">{translateText('player2', 'Текущий номер')}</div>
            <div>{$charSim == -1 ? "Нет сим-карты" : $charSim}</div>
        </div>
    </div>
    <div class="newphone__contacts_list n-p big">
        <div class="newphone__settings_element">
            <div class="newphone__settings_icon mute">
                <div class="phoneicons-black-plane"></div>
            </div>
            <div class="box-between w-1">
                <div>{translateText('player2', 'Авиарежим')}</div>
                <div class="sound__input-block switch-box" on:click={updateAirStatus}>
                    <label class="switch">
                        <input type="checkbox" checked={isAir} disabled >
                        <span class="slider round"></span>
                    </label>
                </div>
            </div>
        </div>
        <div class="newphone__settings_element">
            <div class="newphone__settings_icon vpn">VPN</div>
            <div class="box-between w-1">
                <div>VPN</div>
                <div class="sound__input-block switch-box" on:click={updateVpn}>
                    <label class="switch">
                        <input type="checkbox" checked={isVpn} disabled >
                        <span class="slider round"></span>
                    </label>
                </div>
            </div>
        </div>
        <div class="newphone__settings_element">
            <div class="newphone__settings_icon forbes">
                <div class="forbes"></div>
            </div>
            <div class="box-between w-1">
                <div>{translateText('player2', 'Приватность в Forbes')}</div>
                <div class="sound__input-block switch-box" on:click={updateForbesVisible}>
                    <label class="switch">
                        <input type="checkbox" checked={forbesVisible} disabled >
                        <span class="slider round"></span>
                    </label>
                </div>
            </div>
        </div>
        <div class="newphone__settings_element" on:click={()=> onSelectedView("Wallpaper")}>
            <div class="newphone__settings_icon wallpaper"></div>
            <div class="box-between w-1">
                <div>{translateText('player2', 'Обои')}</div>
                <div class="phoneicons-Button newphone__chevron-back"></div>
            </div>
        </div>
        <div class="newphone__settings_element" on:click={()=> onSelectedView("SoundList")}>
            <div class="newphone__settings_icon sound"></div>
            <div class="box-between w-1">
                <div>{translateText('player2', 'Рингтоны')}</div>
                <div class="phoneicons-Button newphone__chevron-back"></div>
            </div>
        </div>
        <div class="newphone__settings_element" on:click={()=> onSelectedView("SmsList")}>
            <div class="newphone__settings_icon sounds"></div>
            <div class="box-between w-1 last-child">
                <div>{translateText('player2', 'Уведомления')}</div>
                <div class="phoneicons-Button newphone__chevron-back"></div>
            </div>
        </div>
        {#if $charSim !== -1}
        <div class="newphone__settings_element bot" on:click={onRemoveSim}>
            <div class="red">
                {translateText('player2', 'Извлечь SIM карту')}
            </div>
        </div>
        {/if}
    </div>
</div>
<style>
    .newphone__settings_icon.vpn {
        display: flex;
        align-items: center;
        justify-content: center;
        background: linear-gradient(135deg, #3a3f4b, #111318);
        color: #9fe870;
        font-size: 9px;
        font-weight: 700;
        letter-spacing: 0.5px;
    }
</style>
