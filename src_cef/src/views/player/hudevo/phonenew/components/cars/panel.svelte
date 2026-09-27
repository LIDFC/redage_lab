<script>
    import { vehicleName } from '@/api/vehicleName'
    import { executeClientToGroup } from 'api/rage'
    import { fade } from 'svelte/transition'

    export let panel;
    export let refresh;

    // Индекс «своего» места: 0 — водитель, 1 — переднее пассажирское, 2-3 — задние (совпадает с дверями и окнами)
    $: own = panel.seat + 1;
    $: driver = panel.driver;

    const send = (action, value = 0) => {
        executeClientToGroup("cars.panel", action, value);
        setTimeout(refresh, 250);
    };

    const doors = [
        { id: 4, name: 'Капот', cls: 'hood' },
        { id: 0, name: 'Л. перед', cls: 'fl' },
        { id: 1, name: 'П. перед', cls: 'fr' },
        { id: 2, name: 'Л. зад', cls: 'rl' },
        { id: 3, name: 'П. зад', cls: 'rr' },
        { id: 5, name: 'Багажник', cls: 'trunk' },
    ];
    const windowNames = ['Л. перед', 'П. перед', 'Л. зад', 'П. зад'];
    const canDoor = (id) => panel.hasDoor[id] !== false && (driver || id === own);
    const canWindow = (id) => panel.hasDoor[id] !== false && (driver || id === own);
    const windowDown = (mask, id) => (mask & (1 << id)) !== 0;

    const lights = [
        { id: 0, name: 'Авто' },
        { id: 1, name: 'Ближний' },
        { id: 2, name: 'Дальний' },
    ];
    const modes = [
        { id: 0, name: 'Эко', hint: 'Мягкий разгон, расход ниже' },
        { id: 1, name: 'Комфорт', hint: 'Сбалансированный режим' },
        { id: 2, name: 'Спорт', hint: 'Резкий разгон, расход выше' },
    ];
</script>

<div class="auto__scroll" in:fade>
    <div class="auto__hero">
        <div class="auto__hero_top">
            <div>
                <div class="auto__hero_name">{vehicleName(panel.model.toLowerCase())}</div>
                <div class="auto__plate">{panel.number}</div>
            </div>
            <div class="auto__badge" class:ok={panel.engine}>{panel.engine ? 'Двигатель заведён' : 'Заглушен'}</div>
        </div>
        <div class="auto__stats">
            <div><b>{panel.speed}</b><span>км/ч</span></div>
            <div><b>{panel.fuel}</b><span>топливо, л</span></div>
            <div><b>{panel.health}%</b><span>двигатель</span></div>
        </div>
    </div>

    <div class="auto__card">
        <div class="auto__card_title">Основное</div>
        <div class="auto__grid3">
            <div class="auto__tile" class:on={panel.engine} class:disabled={!driver} on:click={() => driver && send("engine")}>
                <i class="auto__led"></i><span>Двигатель</span>
            </div>
            <div class="auto__tile" class:on={panel.locked} on:click={() => send("lock")}>
                <i class="auto__led"></i><span>{panel.locked ? 'Закрыта' : 'Открыта'}</span>
            </div>
            <div class="auto__tile" class:on={panel.belt} on:click={() => send("belt")}>
                <i class="auto__led"></i><span>Ремень</span>
            </div>
        </div>
    </div>

    <div class="auto__card">
        <div class="auto__card_title">Свет и сигналы</div>
        <div class="auto__segment" class:disabled={!driver}>
            {#each lights as item}
                <div class:active={panel.lights === item.id} on:click={() => driver && send("lights", item.id)}>{item.name}</div>
            {/each}
        </div>
        <div class="auto__grid4 mt">
            <div class="auto__tile" class:on={panel.left && !panel.right} class:disabled={!driver || !panel.engine} on:click={() => driver && send("left")}><span>◀</span></div>
            <div class="auto__tile danger" class:on={panel.left && panel.right} class:disabled={!driver || !panel.engine} on:click={() => driver && send("hazard")}><span>Аварийка</span></div>
            <div class="auto__tile" class:on={panel.right && !panel.left} class:disabled={!driver || !panel.engine} on:click={() => driver && send("right")}><span>▶</span></div>
            <div class="auto__tile" class:on={panel.interior} class:disabled={!driver} on:click={() => driver && send("interior", panel.interior ? 0 : 1)}><span>Салон</span></div>
        </div>
    </div>

    <div class="auto__card">
        <div class="auto__card_title">Двери</div>
        <div class="auto__car">
            <div class="auto__car_body"><div class="auto__car_glass"></div></div>
            {#each doors as door}
                <div class="auto__door {door.cls}" class:on={panel.doors[door.id]} class:disabled={!canDoor(door.id)}
                     on:click={() => canDoor(door.id) && send("door", door.id)}>
                    {door.name}
                </div>
            {/each}
        </div>
        <div class="auto__card_title mt">Окна</div>
        <div class="auto__grid2">
            {#each [0, 1, 2, 3] as id}
                <div class="auto__tile" class:on={windowDown(panel.windows, id)} class:disabled={!canWindow(id)}
                     on:click={() => canWindow(id) && send("window", id)}>
                    <span>{windowNames[id]}</span>
                    <em>{windowDown(panel.windows, id) ? 'опущено' : 'поднято'}</em>
                </div>
            {/each}
        </div>
    </div>

    <div class="auto__card">
        <div class="auto__card_title">Режим езды</div>
        <div class="auto__segment" class:disabled={!driver}>
            {#each modes as mode}
                <div class:active={panel.drive === mode.id} on:click={() => driver && send("drive", mode.id)}>{mode.name}</div>
            {/each}
        </div>
        <div class="auto__hint">{modes[panel.drive]?.hint || ''}</div>
    </div>

    <div class="auto__card">
        <div class="auto__card_title">Радио</div>
        <div class="auto__radio" class:disabled={!driver}>
            <div class="auto__icon_btn" on:click={() => driver && send("radio", -1)}>◀</div>
            <div class="auto__radio_name">{panel.radio || '—'}</div>
            <div class="auto__icon_btn" on:click={() => driver && send("radio", 1)}>▶</div>
            <div class="auto__icon_btn off" on:click={() => driver && send("radio", 0)}>Выкл</div>
        </div>
    </div>

    {#if !driver}
        <div class="auto__hint">Вы пассажир: доступны своя дверь, своё окно, замок и ремень.</div>
    {/if}
</div>
