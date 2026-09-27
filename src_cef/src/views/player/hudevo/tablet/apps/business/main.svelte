<script>
    import { sound, playSound } from 'api/uiSound'
    import { splitMoney, dateTime, money } from './util'

    export let data;
    export let history;
    export let action;
    export let openModal;

    $: cash = splitMoney(data.cash);
    $: tax = splitMoney(data.taxBalance);
    $: revenue = splitMoney(history ? history.revenue : 0);
    $: average = splitMoney(history ? history.average : 0);
    $: taxRoom = Math.max(0, data.taxMax - data.taxBalance);
</script>

<div class="biz__body">
    <div class="biz__cards">
        <div class="biz__card">
            <div class="biz__card_head">
                <span>Баланс кассы</span>
                <div class="biz__mini" use:sound={"tap"} class:disabled={data.cash <= 0} on:click={() => data.cash > 0 && action("withdraw")}>Вывести</div>
            </div>
            <div class="biz__value"><em>$</em>{cash.int}<small>{cash.frac}</small></div>
        </div>
        <div class="biz__card">
            <div class="biz__card_head">
                <span>Налоговый счёт</span>
                <div class="biz__mini" use:sound={"tap"} class:disabled={taxRoom <= 0}
                     on:click={() => taxRoom > 0 && openModal({ type: "payTax", max: taxRoom, bank: data.bank, taxHour: data.taxHour })}>Пополнить</div>
            </div>
            <div class="biz__value"><em>$</em>{tax.int}<small>{tax.frac}</small></div>
        </div>
        <div class="biz__card">
            <div class="biz__card_head">
                <span>Оплачен до</span>
                <span class="biz__hint">налог {money(data.taxHour)}/час</span>
            </div>
            <div class="biz__value" class:danger={!data.paidUntil}>{data.paidUntil ? dateTime(data.paidUntil) : "Не оплачен"}</div>
        </div>
        <div class="biz__card">
            <div class="biz__card_head"><span>Выручка</span></div>
            <div class="biz__value">{#if history}<em>$</em>{revenue.int}<small>{revenue.frac}</small>{:else}<i class="biz__skeleton"></i>{/if}</div>
        </div>
        <div class="biz__card">
            <div class="biz__card_head"><span>Продажи</span></div>
            <div class="biz__value">{#if history}{history.sales}{:else}<i class="biz__skeleton"></i>{/if}</div>
        </div>
        <div class="biz__card">
            <div class="biz__card_head"><span>Средний чек</span></div>
            <div class="biz__value">{#if history}<em>$</em>{average.int}<small>{average.frac}</small>{:else}<i class="biz__skeleton"></i>{/if}</div>
        </div>
    </div>

    <div class="biz__tables">
        <div class="biz__table_wrap">
            <div class="biz__h2">Рейтинг покупателей</div>
            <div class="biz__table">
                <div class="biz__tr head"><span class="n">№</span><span class="grow">Имя и фамилия</span><span class="r">Покупки</span><span class="r w">Общая сумма</span></div>
                {#if history && history.clients.length}
                    {#each history.clients as c, i}
                        <div class="biz__tr"><span class="n">{i + 1}</span><span class="grow">{c.name}</span><span class="r">{c.count}</span><span class="r w">{money(c.sum)}</span></div>
                    {/each}
                {:else}
                    <div class="biz__tr empty">{history ? "Покупок за период нет" : "Загрузка…"}</div>
                {/if}
            </div>
        </div>
        <div class="biz__table_wrap">
            <div class="biz__h2">Самые популярные товары</div>
            <div class="biz__table">
                <div class="biz__tr head"><span class="n">№</span><span class="grow">Название товара</span><span class="r">Покупки</span><span class="r w">Общая сумма</span></div>
                {#if history && history.products.length}
                    {#each history.products as p, i}
                        <div class="biz__tr"><span class="n">{i + 1}</span><span class="grow">{p.name}</span><span class="r">{p.count}</span><span class="r w">{money(p.sum)}</span></div>
                    {/each}
                {:else}
                    <div class="biz__tr empty">{history ? "Продаж за период нет" : "Загрузка…"}</div>
                {/if}
            </div>
        </div>
    </div>

    <div class="biz__footer">
        <div use:sound={"tap"} class="biz__btn" on:click={() => action("gps")}>Показать на карте</div>
        <div use:sound={"tap"} class="biz__btn danger" on:click={() => action("sell")}>Продать государству за {money(data.sellPrice)}</div>
    </div>
</div>
