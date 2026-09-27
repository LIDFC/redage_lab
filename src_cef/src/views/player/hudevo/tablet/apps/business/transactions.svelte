<script>
    import { money, dateTime, pad2 } from './util'

    export let history;
</script>

<div class="biz__body">
    <div class="biz__table big">
        <div class="biz__tr head">
            <span class="n">№</span>
            <span class="grow">Товар</span>
            <span class="grow">Покупатель</span>
            <span class="r w2">Дата</span>
            <span class="r w">Себестоимость</span>
            <span class="r w">Цена</span>
            <span class="r w">Прибыль</span>
        </div>
        <div class="biz__scroll">
            {#if history && history.transactions.length}
                {#each history.transactions as t, i}
                    <div class="biz__tr">
                        <span class="n accent">{pad2(i + 1)}</span>
                        <span class="grow">{t.item}</span>
                        <span class="grow muted">{t.buyer}</span>
                        <span class="r w2 muted">{dateTime(t.date)}</span>
                        <span class="r w">{t.cost ? money(t.cost) : "—"}</span>
                        <span class="r w">{money(t.price)}</span>
                        {#if t.cost}
                            <span class="r w" class:accent={t.price >= t.cost} class:danger={t.price < t.cost}>{t.price >= t.cost ? "+" : ""}{money(t.price - t.cost)}</span>
                        {:else}
                            <!-- Старые записи без себестоимости: прибыль посчитать нельзя -->
                            <span class="r w muted">—</span>
                        {/if}
                    </div>
                {/each}
            {:else}
                <div class="biz__tr empty">{history ? "Транзакций за период нет" : "Загрузка…"}</div>
            {/if}
        </div>
    </div>
    {#if history && history.transactions.length >= 200}
        <div class="biz__note">Показаны последние 200 транзакций</div>
    {/if}
</div>
