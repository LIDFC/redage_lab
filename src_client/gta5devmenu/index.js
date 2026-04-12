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
