// NPC-трафик (машины и пешеходы GTA).
// В RAGE:MP ambient-трафик не синхронизируется: каждый игрок видит свои NPC-машины.
// Поэтому плотность умеренная, а всё, что может дать преимущество или сломать RP, выключено:
// NPC-полиция и розыск, поезда/лодки, выпадение денег с педов и посадка в NPC-машины.
// Клавиша HOME — личный переключатель (например, для слабых ПК).

const TRAFFIC = {
    vehicles: 0.45,   // машины на дорогах
    parked: 0.3,      // припаркованные
    peds: 0.6,        // пешеходы
    scenario: 0.5,    // педы в сценариях (сидят, курят и т.п.)
    budget: 2,        // бюджет популяции (0..3)
};

let trafficEnabled = true;
let forcedBudget = null; // метро и т.п. временно задают свой бюджет

const safe = (fn) => { try { fn(); } catch (e) {} };

const applyBudget = () => {
    const budget = forcedBudget !== null ? forcedBudget : (trafficEnabled ? TRAFFIC.budget : 0);
    safe(() => mp.game.streaming.setPedPopulationBudget(budget));
    safe(() => mp.game.streaming.setVehiclePopulationBudget(trafficEnabled ? TRAFFIC.budget : 0));
};

const clearAmbient = () => {
    const pos = global.localplayer.position;
    safe(() => mp.game.gameplay.clearAreaOfPeds(pos.x, pos.y, pos.z, 10000, 1));
    safe(() => mp.game.gameplay.clearAreaOfVehicles(pos.x, pos.y, pos.z, 10000, false, false, false, false, false, 0));
};

const applyWorldRules = () => {
    safe(() => mp.game.player.setMaxWantedLevel(0));
    safe(() => mp.game.ped.setCreateRandomCops(false));
    safe(() => mp.game.ped.setCreateRandomCopsNotOnScenarios(false));
    safe(() => mp.game.ped.setCreateRandomCopsOnScenarios(false));
    safe(() => mp.game.vehicle.setRandomTrains(false));
    safe(() => mp.game.vehicle.setRandomBoats(false));
    safe(() => mp.game.vehicle.setGarbageTrucks(false));
    // Скорая, пожарные, полиция и прочие службы GTA
    for (let i = 1; i <= 15; i++)
        safe(() => mp.game.gameplay.enableDispatchService(i, false));
    // SET_AMBIENT_PEDS_DROP_MONEY(false)
    safe(() => mp.game.invoke("0x6B0E6172C9A4D902", false));
};

// Вызывается из setTraffic (index.js): 0 — вернуть обычный режим, иначе временный бюджет
global.setAmbientTrafficBudget = (index) => {
    forcedBudget = index > 0 ? index : null;
    applyBudget();
};

applyWorldRules();
applyBudget();

// Часть настроек мира сбрасывается при респавне
mp.events.add('playerSpawn', () => {
    applyWorldRules();
    applyBudget();
});

mp.keys.bind(36, true, function () { // HOME
    if (global.chatActive || global.menuCheck && global.menuCheck())
        return;

    trafficEnabled = !trafficEnabled;
    applyBudget();

    if (!trafficEnabled)
        clearAmbient();

    mp.events.call('notify', 2, 9, trafficEnabled ? "NPC-трафик включён (HOME — выключить)" : "NPC-трафик выключен (HOME — включить)", 3000);
});

mp.events.add('render', () => {
    // Не даём сесть в NPC-машину: она есть только у этого игрока и не видна серверу
    const tryingHandle = mp.players.local.getVehicleIsTryingToEnter();
    if (tryingHandle && !mp.vehicles.atHandle(tryingHandle))
        safe(() => mp.players.local.clearTasks());

    if (!trafficEnabled) {
        mp.game.ped.setPedDensityMultiplierThisFrame(0);
        mp.game.ped.setScenarioPedDensityMultiplierThisFrame(0, 0);
        mp.game.vehicle.setVehicleDensityMultiplierThisFrame(0);
        mp.game.vehicle.setRandomVehicleDensityMultiplierThisFrame(0);
        mp.game.vehicle.setParkedVehicleDensityMultiplierThisFrame(0);
        return;
    }

    mp.game.ped.setPedDensityMultiplierThisFrame(TRAFFIC.peds);
    mp.game.ped.setScenarioPedDensityMultiplierThisFrame(TRAFFIC.scenario, TRAFFIC.scenario);
    mp.game.vehicle.setVehicleDensityMultiplierThisFrame(TRAFFIC.vehicles);
    mp.game.vehicle.setRandomVehicleDensityMultiplierThisFrame(TRAFFIC.vehicles);
    mp.game.vehicle.setParkedVehicleDensityMultiplierThisFrame(TRAFFIC.parked);
});
