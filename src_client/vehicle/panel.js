// Панель автомобиля (приложение «Авто» в телефоне): окна, фары, салонный свет, режим езды.
// Состояния хранятся в shared data машины (сервер Core/VehiclePanel.cs) и применяются у всех игроков в зоне стрима.

const isValid = (entity) => entity && mp.vehicles.exists(entity) && entity.type === 'vehicle' && entity.handle !== 0;

const applyWindows = (entity, mask) => {
	if (typeof mask !== "number")
		mask = entity.getVariable("vWindows") || 0;
	for (let i = 0; i < 4; i++) {
		if (mask & (1 << i)) entity.rollDownWindow(i);
		else entity.rollUpWindow(i);
	}
};

// 0 — авто (как в игре), 1 — ближний всегда, 2 — дальний. При заглушённом двигателе фары гасит UpdateVehicleEngine.
const applyLights = (entity, mode) => {
	if (typeof mode !== "number")
		mode = entity.getVariable("vLights") || 0;
	if (!entity.getIsEngineRunning()) {
		entity.setFullbeam(false);
		return;
	}
	entity.setLights(mode > 0 ? 2 : 0);
	entity.setFullbeam(mode === 2);
};

const applyInterior = (entity, toggle) => {
	if (typeof toggle !== "boolean")
		toggle = !!entity.getVariable("vInterior");
	entity.setInteriorlight(toggle);
};

global.applyVehiclePanel = (entity) => {
	try {
		if (!isValid(entity))
			return;
		applyWindows(entity);
		applyLights(entity);
		applyInterior(entity);
	} catch (e) {
		mp.events.callRemote("client_trycatch", "vehicle/panel", "applyVehiclePanel", e.toString());
	}
};

mp.events.addDataHandler("vWindows", (entity, value) => {
	try { if (isValid(entity)) applyWindows(entity, Number(value) || 0); }
	catch (e) { mp.events.callRemote("client_trycatch", "vehicle/panel", "vWindows", e.toString()); }
});

mp.events.addDataHandler("vLights", (entity, value) => {
	try { if (isValid(entity)) applyLights(entity, Number(value) || 0); }
	catch (e) { mp.events.callRemote("client_trycatch", "vehicle/panel", "vLights", e.toString()); }
});

mp.events.addDataHandler("vInterior", (entity, value) => {
	try { if (isValid(entity)) applyInterior(entity, !!value); }
	catch (e) { mp.events.callRemote("client_trycatch", "vehicle/panel", "vInterior", e.toString()); }
});

// Множитель крутящего момента для режима езды (используется в player/render.js у водителя)
global.getDriveModeTorque = (vehicle) => {
	const mode = vehicle.getVariable("vDriveMode");
	if (mode === 0) return 0.8;
	if (mode === 2) return 1.25;
	return 1;
};

// Звук заливки топлива (АЗС и механик) — генерируется в интерфейсе (api/uiSound.js)
gm.events.add("client.fuel.filled", (liters) => {
	mp.gui.emmit(`window.playUiSound && window.playUiSound("fuel", ${Number(liters) || 0})`);
});
