<script>
    import { executeClientToGroup } from "api/rage";
    import { addListernEvent } from "api/functions";
    import { sound, playSound } from "api/uiSound";
    import { charBankMoney } from "store/chars";
    import { onDestroy } from "svelte";
    import { fade } from "svelte/transition";
    import { onInputFocus, onInputBlur } from "@/views/player/hudevo/phonenew/data";
    import Header from "../header.svelte";
    import HomeButton from "../homebutton.svelte";
    import Loader from "../loader.svelte";
    import "./bank.sass";

    // Fleeca в телефоне: всё, что делает банкомат, кроме наличных (сервер Players/Phone/Fleeca).
    let data = null;
    let tab = "home";
    let result = null;
    let busy = false;
    let history = null;

    const parse = (json) => {
        try {
            return typeof json === "string" ? JSON.parse(json) : json;
        } catch (e) {
            return null;
        }
    };

    addListernEvent("phone.bank.data", (json) => (data = parse(json)));
    addListernEvent("phone.bank.result", (ok, message, json) => {
        busy = false;
        const fresh = parse(json);
        if (fresh) data = fresh;
        result = { ok, message };
        playSound(ok ? "success" : "error");
        if (ok) {
            amount = "";
            account = "";
            history = null;
        }
    });
    addListernEvent("phone.bank.history", (json) => (history = parse(json) || []));
    executeClientToGroup("bank.load");
    onDestroy(() => onInputBlur());

    const money = (v) => {
        const n = Math.round(Number(v) || 0);
        return (n < 0 ? "-$" : "$") + Math.abs(n).toString().replace(/\B(?=(\d{3})+(?!\d))/g, " ");
    };
    const cardNumber = (n) => String(n || "").padStart(8, "0").replace(/(\d{4})(?=\d)/g, "$1 ");

    let amount = "";
    let account = "";
    let taxTarget = "house";
    const digits = (v, max = 9) => String(v).replace(/\D+/g, "").replace(/^0+/, "").slice(0, max);

    const setTab = (value) => {
        tab = value;
        result = null;
        amount = "";
        if (value === "history" && history === null) executeClientToGroup("bank.historyLoad");
    };

    const send = (action, arg1, arg2 = 0) => {
        if (busy) return;
        busy = true;
        result = null;
        executeClientToGroup("bank.action", action, arg1, arg2);
        setTimeout(() => (busy = false), 2500);
    };

    $: taxes = data ? [
        ...(data.house ? [{ key: "house", title: `Дом #${data.house.id}`, balance: data.house.balance, max: data.house.max }] : []),
        ...(data.businesses || []).map((b) => ({ key: `biz${b.index}`, index: b.index, title: b.name, balance: b.balance, max: b.max })),
    ] : [];
    $: selectedTax = taxes.find((t) => t.key === taxTarget) || taxes[0];
    $: balance = $charBankMoney || (data ? data.balance : 0);

    const payTax = () => {
        if (!selectedTax || !amount) return;
        if (selectedTax.key === "house") send("house", Number(amount));
        else send("biz", selectedTax.index, Number(amount));
    };

    const formatDate = (value) => {
        const date = new Date(value);
        if (isNaN(date)) return "";
        const pad = (n) => (n < 10 ? "0" : "") + n;
        return `${pad(date.getDate())}.${pad(date.getMonth() + 1)} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
    };

    const tabs = [
        { key: "home", name: "Счёт" },
        { key: "transfer", name: "Перевод" },
        { key: "taxes", name: "Налоги" },
        { key: "org", name: "Организация" },
        { key: "history", name: "История" },
    ];
</script>

{#if !data}
    <Loader />
{:else}
    <div class="newphone__rent fleeca" in:fade>
        <Header />
        <div class="fleeca__head">
            <div class="fleeca__brand"><b>FLEECA</b><span>Mobile Banking</span></div>
        </div>

        <div class="fleeca__tabs">
            {#each tabs as item}
                <div class="fleeca__tab" use:sound={"tap"} class:active={tab === item.key} on:click={() => setTab(item.key)}>{item.name}</div>
            {/each}
        </div>

        <div class="fleeca__body">
            {#if tab === "home"}
                <div class="fleeca__card">
                    <div class="fleeca__card_top"><b>FLEECA</b><i class="fleeca__chip"></i></div>
                    <div class="fleeca__card_number">{cardNumber(data.account)}</div>
                    <div class="fleeca__card_bottom">
                        <div><span>Владелец</span><b>{data.holder}</b></div>
                        <div class="right"><span>Баланс</span><b>{money(balance)}</b></div>
                    </div>
                </div>
                <div class="fleeca__stats">
                    <div><span>Наличные</span><b>{money(data.cash)}</b></div>
                    <div><span>Счёт №</span><b>{data.account}</b></div>
                </div>
                <div class="fleeca__actions">
                    <div use:sound={"tap"} on:click={() => setTab("transfer")}><i>⇄</i>Перевод</div>
                    <div use:sound={"tap"} on:click={() => setTab("taxes")}><i>⌂</i>Налоги</div>
                    <div use:sound={"tap"} on:click={() => setTab("org")}><i>▦</i>Организация</div>
                    <div use:sound={"tap"} on:click={() => setTab("history")}><i>≡</i>История</div>
                </div>
                <div class="fleeca__hint">Внести и снять наличные можно только в банкомате.</div>

            {:else if tab === "transfer"}
                <div class="fleeca__panel">
                    <div class="fleeca__title">Перевод на счёт</div>
                    <label>Номер счёта получателя</label>
                    <input class="fleeca__input" placeholder="Например, 104233" value={account}
                        on:input={(e) => (account = e.target.value = digits(e.target.value, 10))} on:focus={onInputFocus} on:blur={onInputBlur} />
                    <label>Сумма</label>
                    <input class="fleeca__input" placeholder="$0" value={amount}
                        on:input={(e) => (amount = e.target.value = digits(e.target.value))} on:focus={onInputFocus} on:blur={onInputBlur} />
                    <div class="fleeca__small">На карте {money(balance)} · потребуется подтверждение</div>
                    <div class="fleeca__btn" class:disabled={!account || !amount || busy} on:click={() => account && amount && send("transfer", Number(account), Number(amount))}>Перевести</div>
                </div>

            {:else if tab === "taxes"}
                {#if !taxes.length}
                    <div class="fleeca__empty">У вас нет дома или бизнеса с налоговым счётом</div>
                {:else}
                    {#each taxes as tax}
                        <div class="fleeca__tax" class:active={selectedTax && selectedTax.key === tax.key} use:sound={"tap"} on:click={() => (taxTarget = tax.key)}>
                            <div class="fleeca__tax_row"><b>{tax.title}</b><span>{money(tax.balance)} / {money(tax.max)}</span></div>
                            <div class="fleeca__bar"><div style="width:{Math.min(100, (tax.balance / Math.max(1, tax.max)) * 100)}%"></div></div>
                        </div>
                    {/each}
                    {#if selectedTax}
                        <div class="fleeca__panel">
                            <label>Сумма оплаты с карты · до {money(Math.max(0, selectedTax.max - selectedTax.balance))}</label>
                            <input class="fleeca__input" placeholder="$0" value={amount}
                                on:input={(e) => (amount = e.target.value = digits(e.target.value))} on:focus={onInputFocus} on:blur={onInputBlur} />
                            <div class="fleeca__chips">
                                <div use:sound={"tap"} on:click={() => (amount = String(Math.max(0, selectedTax.max - selectedTax.balance)))}>Оплатить всё</div>
                            </div>
                            <div class="fleeca__small">Налог можно оплатить вперёд на {data.days} дн.</div>
                            <div class="fleeca__btn" class:disabled={!amount || busy} on:click={payTax}>Оплатить</div>
                        </div>
                    {/if}
                {/if}

            {:else if tab === "org"}
                {#if !data.org}
                    <div class="fleeca__empty">Вы не состоите в организации</div>
                {:else}
                    <div class="fleeca__org">
                        <span>Счёт организации</span>
                        <b>{data.org.name}</b>
                        <em class:red={data.org.money < 0}>{money(data.org.money)}</em>
                    </div>
                    <div class="fleeca__panel">
                        <label>Пополнить с карты</label>
                        <input class="fleeca__input" placeholder="$0" value={amount}
                            on:input={(e) => (amount = e.target.value = digits(e.target.value, 8))} on:focus={onInputFocus} on:blur={onInputBlur} />
                        <div class="fleeca__chips">
                            {#each [10000, 50000, 100000] as sum}
                                <div use:sound={"tap"} on:click={() => (amount = String(sum))}>{money(sum)}</div>
                            {/each}
                        </div>
                        <div class="fleeca__small">На карте {money(balance)} · за раз до {money(data.orgMax)}</div>
                        <div class="fleeca__btn" class:disabled={!amount || busy} on:click={() => amount && send("org", Number(amount))}>Пополнить</div>
                    </div>
                {/if}

            {:else if tab === "history"}
                {#if history === null}
                    <div class="fleeca__empty">Загрузка…</div>
                {:else if !history.length}
                    <div class="fleeca__empty">Операций пока нет</div>
                {:else}
                    <div class="fleeca__hint">Последние {history.length} операций наличными и по карте</div>
                    {#each history as item}
                        <div class="fleeca__history fleeca__history_row">
                            <div>
                                <span>{formatDate(item.date)}</span>
                                <p>{item.text}</p>
                            </div>
                            {#if item.amount !== undefined}
                                <b class:plus={item.amount > 0} class:minus={item.amount < 0}>{item.amount > 0 ? "+" : "−"}{money(Math.abs(item.amount))}</b>
                            {/if}
                        </div>
                    {/each}
                {/if}
            {/if}

            {#if result}
                <div class="fleeca__result" class:ok={result.ok} in:fade>{result.message}</div>
            {/if}
        </div>
        <HomeButton />
    </div>
{/if}
