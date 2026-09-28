<script>
    import { executeClient } from 'api/rage'
    import { format } from 'api/formatter'
    import { charBankMoney } from 'store/chars';
    import { onMount } from 'svelte';
    import { playSound, sound } from 'api/uiSound'

    export let activeMain;
    export let subdata;
    export let type;
    export let placeholder;
    export let menuItem;
    export let holder;

    let value = "";
    let input;

    $: isAccount = placeholder === 'Счет зачисления';
    $: maxLength = isAccount ? 10 : 7;
    // Для дома/бизнеса сервер присылает "баланс/максимум$"
    $: target = typeof subdata === "string" && subdata.includes('/') ? subdata.replace('$', '').split('/') : null;
    // Счёт организации: сервер присылает "org:баланс:название"
    $: org = typeof subdata === "string" && subdata.startsWith('org:') ? subdata.split(':') : null;
    $: title = org ? placeholder : isAccount ? 'Перевод на счёт' : placeholder === 'Сумма для перевода' ? 'Сумма перевода' : (menuItem[activeMain] ? menuItem[activeMain].title : 'Операция');
    $: quick = !isAccount && (activeMain === 0 || activeMain === 1) ? [100, 500, 1000, 5000] : org ? [1000, 5000, 10000, 50000] : [];

    const onHandleInput = () => {
        value = String(value).replace(/\D+/g, "").replace(/^0+/, "").slice(0, maxLength);
    }

    const onNext = () => {
        if (!value) {
            playSound("error");
            return;
        }
        if (!window.loaderData.delay ("atm.next", 1))
            return;
        playSound("atmOk");
        executeClient ('atmVal', value);
        value = "";
    }

    const onPrev = () => {
        executeClient ('atmCB', type, 0);
        value = "";
    }

    // Как на настоящем банкомате: «пип» на каждую цифру
    const onKey = (e) => {
        if (e.key === "Enter") onNext();
        else if (/^[0-9]$/.test(e.key) || e.key === "Backspace") playSound("atm");
    }

    onMount(() => input && input.focus());
</script>
<h1>{title}</h1>
{#if org}
    <div class="atm__sub">Организация «{org.slice(2).join(':')}»</div>
    <div class="atm__stats">
        <div><p>Счёт организации</p><b>${format("money", org[1])}</b></div>
        <div><p>{placeholder === 'Пополнение с карты' ? 'На карте' : 'Источник'}</p><b>{placeholder === 'Пополнение с карты' ? '$' + format("money", $charBankMoney) : 'Наличные'}</b></div>
    </div>
{:else if target}
    <div class="atm__sub">{holder}</div>
    <div class="atm__stats">
        <div><p>На счету</p><b>${format("money", target[0])}</b></div>
        <div><p>Максимум</p><b>${format("money", target[1])}</b></div>
    </div>
{:else}
    <div class="atm__sub">Баланс счёта: <b>${format("money", $charBankMoney)}</b></div>
{/if}
<div class="atm__field">
    <p>{placeholder}</p>
    <div class="atm__input">
        {#if !isAccount}<span>$</span>{/if}
        <input bind:this={input} bind:value={value} type="text" on:input={onHandleInput} on:keydown={onKey} placeholder={isAccount ? "Номер счёта" : "0"} />
    </div>
    {#if quick.length}
        <div class="atm__quick">
            {#each quick as sum}
                <div use:sound={"atm"} on:click={() => value = String(sum)}>${format("money", sum)}</div>
            {/each}
        </div>
    {/if}
</div>
<div class="atm__buttons">
    <div class="atm__btn" use:sound={"atm"} on:click={onPrev}>Назад</div>
    <div class="atm__btn primary" class:disabled={!value} on:click={onNext}>{isAccount ? 'Далее' : 'Выполнить'}</div>
</div>
