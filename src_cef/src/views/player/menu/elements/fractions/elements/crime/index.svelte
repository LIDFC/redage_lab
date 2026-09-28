<script>
    // Вкладка «Криминал»: как и где зарабатывать (ограбления, угон, трава, скупка) + метки GPS.
    // Данные и метки — сервер Crime/CrimeGuide.cs, клиент src_client/player/crime.js.
    import { executeClient } from "api/rage";
    import { addListernEvent } from "api/functions";
    import { fade } from "svelte/transition";

    let data = null;
    let tab = "burglary";

    addListernEvent("table.crimeguide", (json) => {
        try {
            data = typeof json === "string" ? JSON.parse(json) : json;
        } catch (e) {
            data = null;
        }
    });
    executeClient("client.crime.guide.load");

    const gps = (target) => executeClient("client.crime.guide.gps", target);
    const money = (v) => "$" + Math.round(Number(v) || 0).toLocaleString("ru-RU");

    const tabs = [
        { key: "burglary", name: "Ограбление домов" },
        { key: "theft", name: "Угон машин" },
        { key: "weed", name: "Трава" },
        { key: "fence", name: "Сбыт" },
    ];

    $: w = data?.weed || {};
    $: b = data?.burglary || {};
    $: t = data?.theft || {};
    $: f = data?.fence || { items: [] };
</script>

<div class="crg" in:fade={{ duration: 150 }}>
    {#if !data}
        <div class="crg__empty">Загрузка…</div>
    {:else if !data.allowed}
        <div class="crg__empty">Криминальные заработки доступны бандам, мафии, байкерам и криминальным организациям.</div>
    {:else}
        <div class="crg__head">
            <div class="crg__title">Криминал</div>
            <div class="crg__sub">Где брать работу, что нужно с собой и куда сбывать добычу</div>
        </div>
        {#if data.payoutNote}
            <div class="crg__fund" class:org={!data.isFraction}>
                {#if data.isFraction}
                    <b>Общак банды.</b> С каждой продажи и найденных наличных {data.fundPercent}% автоматически уходит в общак банды — все цены в этом разделе уже указаны за вычетом общака.
                {:else}
                    <b>Без общака.</b> Ваша организация не платит долю в общак банды, поэтому выплаты на {data.orgBonus}% выше, чем у фракционных банд. Цены в этом разделе уже с учётом этого.
                {/if}
            </div>
        {/if}
        <div class="crg__tabs">
            {#each tabs as item}
                <div class="crg__tab" class:active={tab === item.key} on:click={() => (tab = item.key)}>{item.name}</div>
            {/each}
        </div>

        <div class="crg__body">
            {#if tab === "burglary"}
                <div class="crg__status">
                    <div class:ok={b.picks > 0}><span>Отмычки</span><b>{b.picks || 0}</b></div>
                    <div class:ok={b.mask}><span>Маска</span><b>{b.mask ? "надета" : "не надета"}</b></div>
                    <div class:ok={!b.cooldown}><span>Можно грабить</span><b>{b.cooldown ? `через ${b.cooldown} мин` : "сейчас"}</b></div>
                </div>
                <ol class="crg__steps">
                    <li><b>Подготовьтесь.</b> Купите отмычки у Мавра ({money(b.lockpickPrice)} за штуку, лучше взять 2–3 — они ломаются) и наденьте маску.</li>
                    <li><b>Выберите дом.</b> Подойдёт любой ничейный дом или запертый чужой дом (не квартира и не парковка). Грабить можно в любое время суток. Если хозяин или сожители в игре — им придёт тревога.</li>
                    <li><b>Взломайте замок.</b> Нажмите E у двери → «Да». Мышью двигайте отмычку, клавишами A/D поворачивайте замок. Если замок упирается — отмычка гнётся, найдите другой угол. ESC — отменить.</li>
                    <li><b>Обыщите дом.</b> Внутри отмечены 3 места — подойдите и нажмите E. На всё 5 минут, потом соседи точно позвонят в полицию.</li>
                    <li><b>Сбудьте краденое.</b> Техника и украшения продаются Мавру в «Скупке краденого».</li>
                </ol>
                <div class="crg__warn">Соседи вызывают полицию с шансом 35–50% при взломе и 15% при выходе с добычей — это всегда звёзды розыска. Один дом можно грабить раз в 6 часов, сами вы отдыхаете 15 минут.</div>
                <div class="crg__buttons">
                    <div class="crg__btn" on:click={() => gps("mavr")}>Мавр — отмычки и скупка</div>
                </div>
            {:else if tab === "theft"}
                <div class="crg__status">
                    <div class:ok={!t.cooldown}><span>Новый заказ</span><b>{t.cooldown ? `через ${t.cooldown} мин` : "доступен"}</b></div>
                </div>
                <ol class="crg__steps">
                    <li><b>Возьмите заказ у NPC.</b> {#if data.isGang}Банды — у Carter Scott (выдача миссий банды) → «Угон автотранспорта», или {/if}у Мавра → «Заказ на угон». Новый заказ на команду — раз в 5 минут.</li>
                    <li><b>Найдите машину.</b> Она отмечена в GPS и стоит где-то на улице. Модель и номер пишутся в уведомлении.</li>
                    <li><b>Вскройте замок.</b> В уведомлении написано, какой замок у машины. Подойдите к двери и нажмите <b>G → «Взломать транспорт»</b> — нужный прибор выберется сам (или инвентарь → прибор → «Использовать»):
                        <br />— <b>обычная машина</b> — «Отмычка» (та же мини-игра, что у домов: мышь + A/D);
                        <br />— <b>дорогая машина</b> — «Программатор» (Мавр, {money(data.programmerPrice)}): соберите нужную последовательность кодов в матрице, пока не кончилось время. Провал — программатор сгорает и срабатывает сигнализация.</li>
                    <li><b>Эксклюзивные заказы</b> (примерно каждый десятый): Lexus LX570, Mercedes G63 6×6, Mercedes GLS 63, BMW X6M. Только программатор с усиленной защитой, при угоне с шансом 90% полиция получит сигнал трекера и вы — 3 звезды. Зато 14–16 деталей и {money(data.superBonus)} наличными от Мавра сразу при разборке.</li>
                    <li><b>Угоните.</b> Садитесь за руль — провода замкнутся, двигатель заведётся. Сесть может только ваша команда. У дешёвых машин с шансом 30% сработает сигнализация — полиция получит метку, вы — звёзды.</li>
                    <li><b>Пригоните к Мавру на разборку.</b> Заезжайте в зону разборки (20 м вокруг Мавра) и нажмите E за рулём. Через 10 секунд машину разберут на «Детали угнанного авто» — чем целее машина, тем больше деталей.</li>
                    <li><b>Продайте детали</b> Мавру в «Скупке краденого».</li>
                </ol>
                <div class="crg__warn">Полиция может отобрать машину и сдать её на штрафстоянку. Машину миссии «Перевозка» разобрать нельзя — её везут по адресу.</div>
                <div class="crg__buttons">
                    {#if data.isGang}<div class="crg__btn" on:click={() => gps("carter")}>Carter Scott — заказы банды</div>{/if}
                    <div class="crg__btn" on:click={() => gps("mavr")}>Мавр — заказ, отмычки, программатор</div>
                    <div class="crg__btn" on:click={() => gps("chop")}>Разборка</div>
                </div>
            {:else if tab === "weed"}
                <div class="crg__status">
                    <div class:ok={w.seeds > 0}><span>Семена</span><b>{w.seeds || 0}</b></div>
                    <div class:ok={w.bottles > 0}><span>Вода</span><b>{w.bottles || 0}</b></div>
                    <div><span>Мои кусты</span><b>{w.plants || 0} / {w.maxPlants}{w.ready ? ` · созрело ${w.ready}` : ""}</b></div>
                    <div><span>Свежая / готовая</span><b>{w.raw || 0} / {w.drugs || 0} г</b></div>
                    <div class:ok={w.spotsFree > 0}><span>Свободные поляны</span><b>{w.spotsFree} из {w.spots}</b></div>
                </div>
                <ol class="crg__steps">
                    <li><b>Купите семена и воду.</b> Семена — у Мавра ({money(w.seedPrice)}), вода — у Мавра ({money(w.waterPrice)}) или в любом магазине 24/7.</li>
                    <li><b>Найдите поляну.</b> Поляны отмечены на карте зелёными листьями (красные — заняты). Рядом на земле виден зелёный круг. Можно сажать и дома в горшке (свой дом или дом, где вы сожитель, до {w.homeMax} кустов).</li>
                    <li><b>Посадите.</b> Встаньте в круг → инвентарь → «Семена конопли» → «Использовать». Всего у вас может быть до {w.maxPlants} кустов.</li>
                    <li><b>Поливайте.</b> Подойдите к кусту и нажмите E (нужна бутылка воды). Поливать нужно не реже, чем раз в {w.water} мин, иначе куст засохнет. Над кустом написано, когда поливать.</li>
                    <li><b>Соберите урожай.</b> Через {w.grow} мин куст созреет — нажмите E. Получите {w.yieldMin}–{w.yieldMax} «Свежей конопли». Не собрали за {w.rot} мин — сгниёт. Созревший куст может собрать кто угодно, так что не опаздывайте.</li>
                    <li><b>Высушите и расфасуйте.</b> Свежая конопля сохнет {w.dry} мин (просто лежит в инвентаре). Потом инвентарь → «Свежая конопля» → «Использовать» — вся высохшая конопля расфасуется в «Наркотики» (1 к 1).</li>
                    <li><b>Продайте.</b> NPC-покупателям у задних дворов магазинов 24/7 в разных районах ({money(w.buyerPrice)}–{money(w.buyerPriceMax)} за грамм, у каждого дневной лимит) или Мавру в «Скупке краденого».</li>
                </ol>
                <div class="crg__warn">Полиция уничтожает кусты за награду. При сборе на поляне шанс 10%, что вас заметят, при продаже NPC — 12%, что покупатель сдаст. Это звёзды розыска.</div>
                <div class="crg__buttons">
                    <div class="crg__btn" on:click={() => gps("glade")}>Ближайшая свободная поляна</div>
                    <div class="crg__btn" on:click={() => gps("buyer")}>Ближайший покупатель</div>
                    <div class="crg__btn" on:click={() => gps("mavr")}>Мавр — семена и вода</div>
                </div>
            {:else if tab === "fence"}
                <ol class="crg__steps">
                    <li><b>Приезжайте к Мавру лично</b> и выберите «Скупка краденого».</li>
                    <li><b>Выберите товар и количество.</b> Цена падает с каждой продажей и восстанавливается со временем — выгоднее продавать понемногу.</li>
                    <li><b>Выберите оплату:</b> наличными или в BTC на криптокошелёк (в BTC Мавр платит на {f.btcBonus}% больше).</li>
                </ol>
                <div class="crg__prices">
                    {#each f.items as item}
                        <div class="crg__price">
                            <span>{item.name}</span>
                            <b>{money(item.price)}</b>
                            <i>спрос {item.demand}%{item.have ? ` · у вас ${item.have}` : ""}</i>
                        </div>
                    {/each}
                </div>
                <div class="crg__buttons">
                    <div class="crg__btn" on:click={() => gps("mavr")}>Мавр</div>
                </div>
            {/if}
        </div>
    {/if}
</div>

<style>
    .crg { width: 100%; height: 100%; display: flex; flex-direction: column; color: #fff; font-family: "Gilroy", sans-serif; overflow: hidden; }
    .crg__empty { margin: auto; max-width: 60vh; text-align: center; font-size: 1.6vh; color: rgba(255, 255, 255, 0.6); }
    .crg__head { margin-bottom: 1.6vh; }
    .crg__title { font-size: 2.6vh; font-weight: 700; }
    .crg__sub { font-size: 1.4vh; color: rgba(255, 255, 255, 0.5); margin-top: 0.4vh; }
    .crg__fund { padding: 1.1vh 1.4vh; border-radius: 1vh; margin-bottom: 1.4vh; font-size: 1.35vh; line-height: 1.4; color: rgba(255, 255, 255, 0.8); background: rgba(216, 57, 75, 0.1); border: 1px solid rgba(216, 57, 75, 0.35); }
    .crg__fund.org { background: rgba(126, 211, 33, 0.08); border-color: rgba(126, 211, 33, 0.4); }
    .crg__fund b { color: #fff; }
    .crg__tabs { display: flex; gap: 0.8vh; margin-bottom: 1.6vh; }
    .crg__tab { padding: 1vh 1.8vh; border-radius: 1vh; background: rgba(255, 255, 255, 0.05); font-size: 1.4vh; cursor: pointer; color: rgba(255, 255, 255, 0.7); }
    .crg__tab:hover { background: rgba(255, 255, 255, 0.09); }
    .crg__tab.active { background: #d8394b; color: #fff; font-weight: 600; }
    .crg__body { flex: 1; overflow-y: auto; padding-right: 1vh; }
    .crg__status { display: flex; flex-wrap: wrap; gap: 1vh; margin-bottom: 1.6vh; }
    .crg__status div { padding: 1vh 1.4vh; border-radius: 1vh; background: rgba(255, 255, 255, 0.05); border: 1px solid rgba(255, 107, 107, 0.35); display: flex; flex-direction: column; gap: 0.3vh; min-width: 14vh; }
    .crg__status div.ok { border-color: rgba(126, 211, 33, 0.45); }
    .crg__status span { font-size: 1.2vh; color: rgba(255, 255, 255, 0.5); }
    .crg__status b { font-size: 1.6vh; }
    .crg__steps { margin: 0 0 1.4vh; padding-left: 2.4vh; display: flex; flex-direction: column; gap: 1vh; }
    .crg__steps li { list-style: decimal; font-size: 1.45vh; line-height: 1.45; color: rgba(255, 255, 255, 0.82); }
    .crg__steps b { color: #fff; }
    .crg__warn { padding: 1.2vh 1.4vh; border-radius: 1vh; background: rgba(245, 165, 36, 0.1); border: 1px solid rgba(245, 165, 36, 0.35); color: #f5c060; font-size: 1.35vh; line-height: 1.4; margin-bottom: 1.4vh; }
    .crg__buttons { display: flex; flex-wrap: wrap; gap: 1vh; }
    .crg__btn { padding: 1.1vh 1.8vh; border-radius: 1vh; background: rgba(216, 57, 75, 0.18); border: 1px solid rgba(216, 57, 75, 0.5); font-size: 1.4vh; cursor: pointer; }
    .crg__btn:hover { background: rgba(216, 57, 75, 0.35); }
    .crg__prices { display: grid; grid-template-columns: 1fr 1fr; gap: 1vh; margin-bottom: 1.4vh; }
    .crg__price { padding: 1.2vh 1.4vh; border-radius: 1vh; background: rgba(255, 255, 255, 0.05); display: flex; flex-direction: column; gap: 0.3vh; }
    .crg__price span { font-size: 1.4vh; }
    .crg__price b { font-size: 1.8vh; color: #f5a524; }
    .crg__price i { font-style: normal; font-size: 1.2vh; color: rgba(255, 255, 255, 0.5); }
</style>
