<script>
    import { format } from "api/formatter";
    import { executeClient } from "api/rage";
    import { fade, scale } from 'svelte/transition'

    // Регистрация организации (NPC/офис). События прежние: client.org.create.buy / close
    export let viewData;

    let isCrime = false;
    let orgName = "";

    $: check = orgName ? format("createOrg", orgName) : { valid: false, text: "" };

    const types = [
        {
            crime: true,
            title: "Группировка",
            tag: "Криминал",
            text: "Преступная ячейка: альянсы с другими группировками, борьба за влияние, ограбления, похищения и противостояние силовым структурам.",
            perks: ["Захваты и война за территории", "Нелегальные заработки", "Свой офис и гараж"],
            icon: '<path d="M12 3l2.2 4.6 5 .7-3.6 3.5.9 5-4.5-2.4-4.5 2.4.9-5L4.8 8.3l5-.7L12 3z"/>',
        },
        {
            crime: false,
            title: "Сообщество",
            tag: "Легально",
            text: "Частная компания по контракту с правительством: защита интересов государства, влияние на политические и властные структуры.",
            perks: ["Госконтракты и влияние", "Легальный бизнес", "Свой офис и гараж"],
            icon: '<path d="M4 20V9l8-5 8 5v11"/><path d="M9 20v-6h6v6"/><path d="M3 20h18"/>',
        },
    ];

    const onCreate = () => {
        const result = format("createOrg", orgName);
        if (!result.valid) {
            window.notificationAdd(4, 9, result.text, 3000);
            return;
        }
        executeClient('client.org.create.buy', isCrime, orgName)
    }

    const onClose = () => executeClient('client.org.create.close')

    const onKeyUp = (event) => {
        if (event.keyCode == 13) onCreate();
        if (event.keyCode == 27) onClose();
    }
</script>

<svelte:window on:keyup={onKeyUp}/>

<div class="orgc" in:fade={{ duration: 150 }}>
    <div class="orgc__box" in:scale={{ start: 0.97, duration: 200 }}>
        <div class="orgc__head">
            <div>
                <div class="orgc__eyebrow">Регистрация</div>
                <h1>Новая организация</h1>
                <p>Выберите направление и придумайте название — после создания вы станете её лидером.</p>
            </div>
            <div class="orgc__close" on:click={onClose} title="Закрыть (Esc)"></div>
        </div>

        <div class="orgc__types">
            {#each types as t}
                <div class="orgc__type" class:active={isCrime === t.crime} class:crime={t.crime} on:click={() => isCrime = t.crime}>
                    <div class="orgc__type_top">
                        <div class="orgc__icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">{@html t.icon}</svg></div>
                        <div>
                            <b>{t.title}</b>
                            <span class="orgc__tag">{t.tag}</span>
                        </div>
                        <div class="orgc__radio"></div>
                    </div>
                    <p>{t.text}</p>
                    <ul>
                        {#each t.perks as perk}<li>{perk}</li>{/each}
                    </ul>
                </div>
            {/each}
        </div>

        <div class="orgc__field">
            <label for="orgc-name">Название организации</label>
            <input id="orgc-name" placeholder="Например: Black Wolves" maxlength="30" bind:value={orgName} autofocus>
            <div class="orgc__rules" class:error={orgName && !check.valid}>
                {orgName && !check.valid ? check.text : "3–30 символов: латиница, цифры и знаки - _ ."}
                <span>{orgName.length}/30</span>
            </div>
        </div>

        <div class="orgc__footer">
            <div class="orgc__price">
                <span>Стоимость создания</span>
                <b>${format("money", viewData)}</b>
            </div>
            <div class="orgc__btn ghost" on:click={onClose}>Отмена</div>
            <div class="orgc__btn" class:disabled={!check.valid} on:click={onCreate}>Создать организацию</div>
        </div>
    </div>
</div>

<style>
    .orgc {
        position: absolute;
        inset: 0;
        display: flex;
        align-items: center;
        justify-content: center;
        background: rgba(5, 6, 8, 0.7);
        font-family: 'Gilroy', sans-serif;
        color: #fff;
    }
    .orgc__box {
        width: 100vh;
        padding: 3.6vh;
        border-radius: 2vh;
        background: linear-gradient(160deg, #17191D 0%, #0E0F12 100%);
        border: 0.1vh solid rgba(255,255,255,0.07);
        box-shadow: 0 3vh 8vh rgba(0,0,0,0.6);
    }
    .orgc__head { display: flex; justify-content: space-between; gap: 2vh; margin-bottom: 3vh; }
    .orgc__eyebrow { font-size: 1.2vh; font-weight: 700; letter-spacing: 0.3vh; text-transform: uppercase; color: #7ED321; }
    .orgc__head h1 { margin: 0.6vh 0 0.8vh; font-size: 3.4vh; font-weight: 800; }
    .orgc__head p { margin: 0; font-size: 1.45vh; color: rgba(255,255,255,0.5); max-width: 60vh; }
    .orgc__close {
        position: relative; flex-shrink: 0;
        width: 4vh; height: 4vh; border-radius: 1vh;
        background: rgba(255,255,255,0.05); cursor: pointer;
    }
    .orgc__close::before, .orgc__close::after {
        content: ""; position: absolute; left: 50%; top: 50%;
        width: 1.6vh; height: 0.2vh; background: rgba(255,255,255,0.7);
    }
    .orgc__close::before { transform: translate(-50%, -50%) rotate(45deg); }
    .orgc__close::after { transform: translate(-50%, -50%) rotate(-45deg); }
    .orgc__close:hover { background: rgba(242,85,90,0.2); }

    .orgc__types { display: grid; grid-template-columns: 1fr 1fr; gap: 1.6vh; }
    .orgc__type {
        --tone: #5B9CFF;
        padding: 2.2vh;
        border-radius: 1.4vh;
        background: #16181C;
        border: 0.15vh solid rgba(255,255,255,0.06);
        cursor: pointer;
        transition: all .2s ease;
    }
    .orgc__type.crime { --tone: #F2555A; }
    .orgc__type:hover { border-color: rgba(255,255,255,0.14); }
    .orgc__type.active { border-color: var(--tone); background: linear-gradient(160deg, rgba(255,255,255,0.03), #16181C); box-shadow: 0 0 0 0.3vh rgba(255,255,255,0.02); }
    .orgc__type_top { display: flex; align-items: center; gap: 1.4vh; }
    .orgc__type_top b { display: block; font-size: 2vh; }
    .orgc__icon {
        width: 5vh; height: 5vh; border-radius: 1.2vh; flex-shrink: 0;
        display: flex; align-items: center; justify-content: center;
        color: var(--tone); background: rgba(255,255,255,0.05);
    }
    .orgc__icon svg { width: 55%; height: 55%; }
    .orgc__tag { display: inline-block; margin-top: 0.4vh; font-size: 1.1vh; font-weight: 700; color: var(--tone); text-transform: uppercase; letter-spacing: 0.1vh; }
    .orgc__radio {
        margin-left: auto; width: 2.2vh; height: 2.2vh; border-radius: 50%;
        border: 0.2vh solid rgba(255,255,255,0.25); box-sizing: border-box;
    }
    .orgc__type.active .orgc__radio { border: 0.65vh solid var(--tone); }
    .orgc__type p { margin: 1.6vh 0 1.2vh; font-size: 1.35vh; line-height: 1.5; color: rgba(255,255,255,0.6); }
    .orgc__type ul { margin: 0; padding: 0; list-style: none; }
    .orgc__type li { position: relative; padding-left: 1.8vh; margin-top: 0.6vh; font-size: 1.3vh; color: rgba(255,255,255,0.8); }
    .orgc__type li::before { content: ""; position: absolute; left: 0; top: 0.55vh; width: 0.7vh; height: 0.7vh; border-radius: 50%; background: var(--tone); }

    .orgc__field { margin-top: 2.6vh; }
    .orgc__field label { display: block; margin-bottom: 0.8vh; font-size: 1.3vh; font-weight: 600; color: rgba(255,255,255,0.7); }
    .orgc__field input {
        width: 100%; box-sizing: border-box;
        padding: 1.6vh 1.8vh; border-radius: 1vh;
        background: #0B0C0E; border: 0.15vh solid rgba(255,255,255,0.08);
        color: #fff; font-family: inherit; font-size: 1.9vh; font-weight: 700; outline: none;
    }
    .orgc__field input:focus { border-color: #7ED321; }
    .orgc__rules { display: flex; justify-content: space-between; margin-top: 0.8vh; font-size: 1.2vh; color: rgba(255,255,255,0.4); }
    .orgc__rules.error { color: #F2555A; }

    .orgc__footer { display: flex; align-items: center; gap: 1.2vh; margin-top: 3vh; padding-top: 2.4vh; border-top: 0.1vh solid rgba(255,255,255,0.06); }
    .orgc__price { margin-right: auto; }
    .orgc__price span { display: block; font-size: 1.2vh; color: rgba(255,255,255,0.45); }
    .orgc__price b { font-size: 2.6vh; font-weight: 800; color: #7ED321; }
    .orgc__btn {
        padding: 1.6vh 3vh; border-radius: 1vh;
        font-size: 1.5vh; font-weight: 700;
        background: #7ED321; color: #0E1A04; cursor: pointer; transition: all .2s ease;
    }
    .orgc__btn:hover { background: #8EE22E; }
    .orgc__btn.ghost { background: rgba(255,255,255,0.05); color: rgba(255,255,255,0.75); }
    .orgc__btn.ghost:hover { background: rgba(255,255,255,0.1); color: #fff; }
    .orgc__btn.disabled { opacity: 0.4; pointer-events: none; }
</style>
