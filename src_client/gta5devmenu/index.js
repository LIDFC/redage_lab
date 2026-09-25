global.gamemenu = false;
global.myStats = false;


global.binderFunctions.GTA5DEVMENU = () => {
	if (global.tableInFocus)
		return;

    if (!global.gamemenu) GTA5DEVMENU ();
};


const GTA5DEVMENU = () => {
	try
	{
		if (!global.loggedin || global.chatActive || global.editing || global.cuffed || global.isDeath == true || global.isDemorgan == true || global.attachedtotrunk || global.menuCheck() || (global.inAirsoftLobby !== undefined && global.inAirsoftLobby >= 0)) return;
		if (!global.myStats) mp.events.callRemote('server.gamemenu.updatestats');   
		mp.gui.emmit(`window.router.setView("Gta5devMenu");`);
		mp.events.call("client.battlepass.open");
		mp.events.call("client.phone.cars.load");
		mp.events.call("client.gta5devmenucars"); 
		global.gamemenu = true;
		global.menuOpen(true);
	}
	catch (e) 
	{
		mp.events.callRemote("client_trycatch", "inventory/index", "GTA5DEVMENU", e.toString());
	}
}

mp.events.add("client.stockitems", () => {
	mp.events.callRemote('server.gta5devmenustock', 0)
})

gm.events.add("client.inventory.InitOtherDataStock", (otherId, otherName, json, maxSlot, itemId, isArmyCar, isMyTent) => {
	itemstock = json
	mp.gui.emmit(`window.gta5devmenustockitems(${itemstock})`);
});

// Магазин транспорта в F3: список и цены приходят с сервера (bus_products, тип Donate)
let donateAutoroomPos = null;

mp.events.add("client.donate.vehicles.load", () => {
	mp.events.callRemote('server.donate.vehicles.load');
});

gm.events.add("client.donate.vehicles", (json, x, y) => {
	try
	{
		donateAutoroomPos = { x: x, y: y };
		const vehicles = JSON.parse(json).map(([model, price]) => {
			const hash = mp.game.joaat(model);
			const label = mp.game.vehicle.getDisplayNameFromVehicleModel(hash);
			let name = label ? mp.game.ui.getLabelText(label) : "";
			if (!name || name === "NULL") name = model;
			return {
				model: model,
				name: name,
				price: price,
				isHeli: mp.game.vehicle.getVehicleClassFromName(hash) === 15
			};
		});
		mp.gui.emmit(`window.gta5devmenuDonateVehicles('${JSON.stringify(vehicles)}')`);
	}
	catch (e)
	{
		mp.events.callRemote("client_trycatch", "gta5devmenu/index", "client.donate.vehicles", e.toString());
	}
});

mp.events.add("client.donate.vehicles.route", () => {
	if (!donateAutoroomPos) return;
	mp.game.ui.setNewWaypoint(donateAutoroomPos.x, donateAutoroomPos.y);
	mp.events.call('notify', 2, 9, "Донат-автосалон отмечен на карте. Цвет и покупка — у NPC Донаты Редбаксовны", 5000);
});
