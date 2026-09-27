<script>
    import { sound, playSound } from 'api/uiSound'
    import { fade } from 'svelte/transition'
    import { executeClient, executeClientAsync } from 'api/rage'
    import { addListernEvent, hasJsonStructure } from 'api/functions'
    import { vehicleName } from '@/api/vehicleName'
    import { money } from './business/util'

    // Forbes на планшете: те же данные, что были в телефоне (src_client/phone/forbes.js, server.phone.forbes.load)

    let list = null;
    let selected = null;
    let selectedIndex = -1;
    let search = "";

    const loadList = () => {
        executeClientAsync("phone.forbes.getList").then((result) => {
            list = hasJsonStructure(result) ? JSON.parse(result) : [];
        });
    };
    addListernEvent("phone.forbes.load", loadList);
    executeClient("client.phone.forbes.load");

    const select = (index) => {
        selectedIndex = index;
        selected = null;
        executeClientAsync("phone.forbes.getId", index).then((result) => {
            if (hasJsonStructure(result)) selected = JSON.parse(result);
        });
    };

    $: filtered = (list || []).map((item, index) => ({ ...item, index }))
        .filter(item => !search.trim() || String(item.Name).toLowerCase().includes(search.trim().toLowerCase()));

    // Для превью без сервера
    window.tabletForbesMock = (l, s) => { list = l; if (s) { selected = s; selectedIndex = 0; } };
</script>

<div class="forbes" in:fade={{ duration: 150 }}>
    <div class="forbes__head">
        <div>
            <div class="forbes__logo">Forbes</div>
            <div class="forbes__sub">Самые богатые жители штата</div>
        </div>
        <input class="forbes__search" placeholder="Поиск по имени" bind:value={search} />
    </div>

    <div class="forbes__layout">
        <div class="forbes__list">
            <div class="forbes__row head"><span class="pos">#</span><span class="name">Имя</span><span class="sum">Состояние</span></div>
            {#if list === null}
                <div class="forbes__empty">Загрузка…</div>
            {:else}
                {#each filtered as item}
                    <div class="forbes__row" use:sound={"tap"} class:active={item.index === selectedIndex} class:top={item.index < 3} on:click={() => select(item.index)}>
                        <span class="pos p{item.index + 1}">{item.index + 1}</span>
                        <span class="name">{item.IsShowForbes === false ? "Скрыл данные" : item.Name}</span>
                        <span class="sum">{money(item.Money)}</span>
                    </div>
                {:else}
                    <div class="forbes__empty">Никого не нашли</div>
                {/each}
            {/if}
        </div>

        <div class="forbes__detail">
            {#if selectedIndex === -1}
                <div class="forbes__empty">Выберите человека из списка</div>
            {:else if !selected}
                <div class="forbes__empty">Загрузка…</div>
            {:else}
                <div class="forbes__person">
                    <div class="forbes__badge p{selectedIndex + 1}">{selectedIndex + 1}</div>
                    <div>
                        <b>{selected.Name}</b>
                        <span>Уровень {selected.Lvl}</span>
                    </div>
                </div>
                <div class="forbes__stats">
                    <div><p>Состояние</p><b>{money(selected.SumMoney)}</b></div>
                    <div><p>Наличные и счёт</p><b>{money(selected.Money)}</b></div>
                </div>
                {#if selected.IsShowForbes}
                    {#each [["Недвижимость", selected.houses, "house"], ["Бизнесы", selected.biz, "biz"], ["Транспорт", selected.vehicles, "car"]] as group}
                        {#if group[1] && group[1].length}
                            <div class="forbes__group">{group[0]}</div>
                            {#each group[1] as asset}
                                <div class="forbes__asset">
                                    <span>{group[2] === "car" ? vehicleName(asset.Name) : asset.Name}</span>
                                    <b>{money(asset.Money)}</b>
                                </div>
                            {/each}
                        {/if}
                    {/each}
                {:else}
                    <div class="forbes__empty small">Человек скрыл список имущества</div>
                {/if}
            {/if}
        </div>
    </div>
</div>

<style>
    .forbes {
        flex: 1;
        min-height: 0;
        display: flex;
        flex-direction: column;
        padding: 2vh 3vh 3vh;
        background: linear-gradient(180deg, #0F1012 0%, #0B0B0D 100%);
        color: #fff;
    }
    .forbes__head {
        display: flex;
        align-items: flex-end;
        justify-content: space-between;
        margin-bottom: 2vh;
    }
    .forbes__logo {
        font-family: Georgia, 'Times New Roman', serif;
        font-size: 4.2vh;
        font-weight: 700;
        letter-spacing: -0.1vh;
        line-height: 1;
    }
    .forbes__sub { margin-top: 0.6vh; font-size: 1.35vh; color: rgba(255,255,255,0.45); }
    .forbes__search {
        width: 32vh;
        padding: 1.2vh 1.6vh;
        border-radius: 0.8vh;
        background: #17191C;
        border: 0.1vh solid rgba(255,255,255,0.08);
        color: #fff;
        font-family: inherit;
        font-size: 1.4vh;
        outline: none;
    }
    .forbes__search:focus { border-color: #E5B53A; }
    .forbes__layout {
        flex: 1;
        min-height: 0;
        display: grid;
        grid-template-columns: 1.3fr 1fr;
        gap: 1.6vh;
    }
    .forbes__list, .forbes__detail {
        min-height: 0;
        overflow-y: auto;
        background: #15171A;
        border-radius: 1vh;
        padding: 0.8vh;
    }
    .forbes__list::-webkit-scrollbar, .forbes__detail::-webkit-scrollbar { width: 0.4vh; }
    .forbes__list::-webkit-scrollbar-thumb, .forbes__detail::-webkit-scrollbar-thumb { background: rgba(255,255,255,0.12); border-radius: 1vh; }
    .forbes__row {
        display: flex;
        align-items: center;
        gap: 1.4vh;
        padding: 1.1vh 1.4vh;
        border-radius: 0.6vh;
        font-size: 1.45vh;
        cursor: pointer;
        transition: background .15s ease;
    }
    .forbes__row:hover { background: rgba(255,255,255,0.04); }
    .forbes__row.active { background: rgba(229,181,58,0.1); }
    .forbes__row.head { cursor: default; font-size: 1.2vh; font-weight: 600; color: #E5B53A; background: none; }
    .forbes__row .pos {
        width: 3.2vh; height: 3.2vh; flex-shrink: 0;
        display: flex; align-items: center; justify-content: center;
        border-radius: 0.6vh; font-weight: 700; font-size: 1.3vh;
        background: rgba(255,255,255,0.05); color: rgba(255,255,255,0.6);
    }
    .forbes__row.head .pos { background: none; }
    .pos.p1, .forbes__badge.p1 { background: linear-gradient(145deg, #F6D365, #C9962B) !important; color: #1A1204 !important; }
    .pos.p2, .forbes__badge.p2 { background: linear-gradient(145deg, #E8ECF1, #9AA3AE) !important; color: #15181C !important; }
    .pos.p3, .forbes__badge.p3 { background: linear-gradient(145deg, #E7A36B, #9C5B2C) !important; color: #1A0E05 !important; }
    .forbes__row .name { flex: 1; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .forbes__row.top .name { font-weight: 700; }
    .forbes__row .sum { font-weight: 700; font-variant-numeric: tabular-nums; }
    .forbes__row.head .name, .forbes__row.head .sum { font-weight: 600; }
    .forbes__detail { padding: 2vh; }
    .forbes__person { display: flex; align-items: center; gap: 1.6vh; margin-bottom: 2vh; }
    .forbes__person b { display: block; font-size: 2.2vh; }
    .forbes__person span { font-size: 1.3vh; color: rgba(255,255,255,0.45); }
    .forbes__badge {
        width: 6vh; height: 6vh; border-radius: 1.4vh;
        display: flex; align-items: center; justify-content: center;
        font-size: 2.4vh; font-weight: 800;
        background: rgba(255,255,255,0.06);
    }
    .forbes__stats { display: grid; grid-template-columns: 1fr 1fr; gap: 1vh; margin-bottom: 1.6vh; }
    .forbes__stats div { padding: 1.4vh; border-radius: 0.8vh; background: #1C1F23; }
    .forbes__stats p { margin: 0 0 0.4vh; font-size: 1.15vh; color: rgba(255,255,255,0.45); }
    .forbes__stats b { font-size: 1.9vh; }
    .forbes__group { margin: 1.6vh 0 0.6vh; font-size: 1.15vh; font-weight: 700; text-transform: uppercase; letter-spacing: 0.1vh; color: #E5B53A; }
    .forbes__asset {
        display: flex; justify-content: space-between; gap: 1vh;
        padding: 1vh 1.2vh; border-radius: 0.6vh; font-size: 1.35vh;
        background: rgba(255,255,255,0.025); margin-bottom: 0.4vh;
    }
    .forbes__asset span { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .forbes__empty { padding: 4vh 2vh; text-align: center; color: rgba(255,255,255,0.4); font-size: 1.4vh; }
    .forbes__empty.small { padding: 2vh; }
</style>
