<script>
    import { sound, playSound } from 'api/uiSound'
    import { vehicleName } from '@/api/vehicleName'
    import { executeClientToGroup } from 'api/rage'
    import { fade } from 'svelte/transition'

    export let panel;
    export let refresh;

    // Индекс «своего» места: 0 — водитель, 1 — переднее пассажирское, 2-3 — задние (совпадает с дверями и окнами)
    $: own = panel.seat + 1;
    $: driver = panel.driver;
    // Снаружи машины (до 15 м): двигатель (автозапуск), замок, двери, окна, свет, режим. Ремень, поворотники, радио — только внутри
    $: remote = !!panel.remote;

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
            <div class="auto__badges">
                {#if remote}<div class="auto__badge remote">Рядом · {panel.distance} м</div>{/if}
                <div class="auto__badge" class:ok={panel.engine}>{panel.engine ? 'Двигатель заведён' : 'Заглушен'}</div>
            </div>
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
            <div class="auto__tile" use:sound={"toggle"} class:on={panel.engine} class:disabled={!driver} on:click={() => driver && send("engine")}>
                <i class="auto__led"></i><span>Двигатель</span>
            </div>
            <div class="auto__tile" use:sound={"toggle"} class:on={panel.locked} on:click={() => send("lock")}>
                <i class="auto__led"></i><span>{panel.locked ? 'Закрыта' : 'Открыта'}</span>
            </div>
            {#if !remote}
                <div class="auto__tile" use:sound={"toggle"} class:on={panel.belt} on:click={() => send("belt")}>
                    <i class="auto__led"></i><span>Ремень</span>
                </div>
            {:else}
                <div class="auto__tile" use:sound={"toggle"} class:on={panel.interior} on:click={() => send("interior", panel.interior ? 0 : 1)}>
                    <i class="auto__led"></i><span>Салон</span>
                </div>
            {/if}
        </div>
    </div>

    <div class="auto__card">
        <div class="auto__card_title">Свет и сигналы</div>
        <div class="auto__segment" class:disabled={!driver}>
            {#each lights as item}
                <div use:sound={"tap"} class:active={panel.lights === item.id} on:click={() => driver && send("lights", item.id)}>{item.name}</div>
            {/each}
        </div>
        {#if !remote}
        <div class="auto__grid4 mt">
            <div class="auto__tile" use:sound={"toggle"} class:on={panel.left && !panel.right} class:disabled={!driver || !panel.engine} on:click={() => driver && send("left")}><span>◀</span></div>
            <div class="auto__tile danger" class:on={panel.left && panel.right} class:disabled={!driver || !panel.engine} on:click={() => driver && send("hazard")}><span>Аварийка</span></div>
            <div class="auto__tile" use:sound={"toggle"} class:on={panel.right && !panel.left} class:disabled={!driver || !panel.engine} on:click={() => driver && send("right")}><span>▶</span></div>
            <div class="auto__tile" use:sound={"toggle"} class:on={panel.interior} class:disabled={!driver} on:click={() => driver && send("interior", panel.interior ? 0 : 1)}><span>Салон</span></div>
        </div>
        {/if}
    </div>

    <div class="auto__card">
        <div class="auto__card_title">Двери</div>
        <div class="auto__car">
            <div class="auto__car_body">
                <!-- Машина сверху: кузов, стёкла, фары -->
                <svg viewBox="0 0 100 200" preserveAspectRatio="xMidYMid meet">
                    <defs>
                        <linearGradient id="autoBody" x1="0" x2="1">
                            <stop offset="0" stop-color="#2A303B"/><stop offset="0.5" stop-color="#3A4250"/><stop offset="1" stop-color="#2A303B"/>
                        </linearGradient>
                    </defs>
                    <rect x="14" y="6" width="72" height="188" rx="30" fill="url(#autoBody)" stroke="#4A5363" stroke-width="2"/>
                    <path d="M22 62 Q50 48 78 62 L74 86 Q50 80 26 86 Z" fill="#1E3A5F" stroke="#5B9CFF" stroke-opacity=".35"/>
                    <rect x="27" y="90" width="46" height="46" rx="8" fill="#232831" stroke="#4A5363"/>
                    <path d="M26 142 Q50 148 74 142 L77 160 Q50 170 23 160 Z" fill="#1E3A5F" stroke="#5B9CFF" stroke-opacity=".35"/>
                    <rect x="20" y="10" width="16" height="7" rx="3" fill={panel.lights ? "#FFE8A3" : "#5A6070"}/>
                    <rect x="64" y="10" width="16" height="7" rx="3" fill={panel.lights ? "#FFE8A3" : "#5A6070"}/>
                    <rect x="20" y="184" width="16" height="6" rx="3" fill="#8A2A30"/>
                    <rect x="64" y="184" width="16" height="6" rx="3" fill="#8A2A30"/>
                    <rect x="6" y="64" width="9" height="12" rx="3" fill="#3A4250"/>
                    <rect x="85" y="64" width="9" height="12" rx="3" fill="#3A4250"/>
                </svg>
            </div>
            {#each doors as door}
                <div use:sound={"toggle"} class="auto__door {door.cls}" class:on={panel.doors[door.id]} class:disabled={!canDoor(door.id)}
                     on:click={() => canDoor(door.id) && send("door", door.id)}>
                    {door.name}
                </div>
            {/each}
        </div>
        <div class="auto__card_title mt">Окна</div>
        <div class="auto__grid2">
            {#each [0, 1, 2, 3] as id}
                <div class="auto__tile" use:sound={"toggle"} class:on={windowDown(panel.windows, id)} class:disabled={!canWindow(id)}
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
                <div use:sound={"tap"} class:active={panel.drive === mode.id} on:click={() => driver && send("drive", mode.id)}>{mode.name}</div>
            {/each}
        </div>
        <div class="auto__hint">{modes[panel.drive]?.hint || ''}</div>
    </div>

    {#if !remote}
    <div class="auto__card">
        <div class="auto__card_title">Радио</div>
        <div class="auto__radio" class:disabled={!driver}>
            <div class="auto__icon_btn" use:sound={"tap"} on:click={() => driver && send("radio", -1)}>◀</div>
            <div class="auto__radio_name">{panel.radio || '—'}</div>
            <div class="auto__icon_btn" use:sound={"tap"} on:click={() => driver && send("radio", 1)}>▶</div>
            <div class="auto__icon_btn off" on:click={() => driver && send("radio", 0)}>Выкл</div>
        </div>
    </div>
    {/if}

    {#if remote}
        <div class="auto__hint">Вы рядом с машиной: доступны автозапуск, замок, двери, окна, свет и режим езды.</div>
    {:else if !driver}
        <div class="auto__hint">Вы пассажир: доступны своя дверь, своё окно, замок и ремень.</div>
    {/if}
</div>
