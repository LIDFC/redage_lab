<script>
    import { translateText } from 'lang'
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
        { id: 0, name: 'ЛП', cls: 'fl' },
        { id: 1, name: 'ПП', cls: 'fr' },
        { id: 2, name: 'ЛЗ', cls: 'rl' },
        { id: 3, name: 'ПЗ', cls: 'rr' },
        { id: 5, name: 'Багажник', cls: 'trunk' },
    ];
    const canDoor = (id) => panel.hasDoor[id] !== false && (driver || id === own);
    const canWindow = (id) => panel.hasDoor[id] !== false && (driver || id === own);
    const windowDown = (mask, id) => (mask & (1 << id)) !== 0;

    const lights = [
        { id: 0, name: 'Авто' },
        { id: 1, name: 'Ближний' },
        { id: 2, name: 'Дальний' },
    ];
    const modes = [
        { id: 0, name: 'Эко', hint: 'Меньше расход, мягкий разгон' },
        { id: 1, name: 'Комфорт', hint: 'Обычная езда' },
        { id: 2, name: 'Спорт', hint: 'Резкий разгон, расход выше' },
    ];
</script>

<div class="auto__panel" in:fade>
    <div class="auto__hero">
        <div class="auto__hero_name">{vehicleName(panel.model.toLowerCase())}</div>
        <div class="auto__hero_number">{panel.number}</div>
        <div class="auto__stats">
            <div><b>{panel.speed}</b><span>км/ч</span></div>
            <div><b>{panel.fuel}</b><span>топливо</span></div>
            <div><b>{panel.health}%</b><span>мотор</span></div>
        </div>
    </div>

    <div class="auto__grid3">
        <div class="auto__tile" class:on={panel.engine} class:disabled={!driver} on:click={() => driver && send("engine")}>
            <div class="auto__dot"></div>Двигатель
        </div>
        <div class="auto__tile" class:on={panel.locked} on:click={() => send("lock")}>
            <div class="auto__dot"></div>{panel.locked ? 'Закрыта' : 'Открыта'}
        </div>
        <div class="auto__tile" class:on={panel.belt} on:click={() => send("belt")}>
            <div class="auto__dot"></div>Ремень
        </div>
    </div>

    <div class="auto__title">Поворотники</div>
    <div class="auto__grid3">
        <div class="auto__tile" class:on={panel.left && !panel.right} class:disabled={!driver || !panel.engine} on:click={() => driver && send("left")}>◀ Левый</div>
        <div class="auto__tile warn" class:on={panel.left && panel.right} class:disabled={!driver || !panel.engine} on:click={() => driver && send("hazard")}>Аварийка</div>
        <div class="auto__tile" class:on={panel.right && !panel.left} class:disabled={!driver || !panel.engine} on:click={() => driver && send("right")}>Правый ▶</div>
    </div>

    <div class="auto__title">Фары</div>
    <div class="auto__segment" class:disabled={!driver}>
        {#each lights as item}
            <div class:active={panel.lights === item.id} on:click={() => driver && send("lights", item.id)}>{item.name}</div>
        {/each}
        <div class="auto__segment_extra" class:active={panel.interior} on:click={() => driver && send("interior", panel.interior ? 0 : 1)}>Салон</div>
    </div>

    <div class="auto__title">Двери и окна</div>
    <div class="auto__car">
        <div class="auto__car_body"></div>
        {#each doors as door}
            <div class="auto__door {door.cls}" class:on={panel.doors[door.id]} class:disabled={!canDoor(door.id)}
                 on:click={() => canDoor(door.id) && send("door", door.id)}>
                {door.name}
            </div>
        {/each}
    </div>
    <div class="auto__grid4">
        {#each [0, 1, 2, 3] as id}
            <div class="auto__tile small" class:on={windowDown(panel.windows, id)} class:disabled={!canWindow(id)}
                 on:click={() => canWindow(id) && send("window", id)}>
                Окно {['ЛП', 'ПП', 'ЛЗ', 'ПЗ'][id]}
            </div>
        {/each}
    </div>

    <div class="auto__title">Режим езды</div>
    <div class="auto__segment" class:disabled={!driver}>
        {#each modes as mode}
            <div class:active={panel.drive === mode.id} on:click={() => driver && send("drive", mode.id)}>{mode.name}</div>
        {/each}
    </div>
    <div class="auto__hint">{modes[panel.drive]?.hint || ''}</div>

    <div class="auto__title">Радио</div>
    <div class="auto__radio" class:disabled={!driver}>
        <div class="auto__radio_btn" on:click={() => driver && send("radio", -1)}>◀</div>
        <div class="auto__radio_name">{panel.radio || '—'}</div>
        <div class="auto__radio_btn" on:click={() => driver && send("radio", 1)}>▶</div>
        <div class="auto__radio_btn off" on:click={() => driver && send("radio", 0)}>Выкл</div>
    </div>

    {#if !driver}
        <div class="auto__hint">Вы пассажир: доступны своя дверь, своё окно, замок и ремень.</div>
    {/if}
</div>
