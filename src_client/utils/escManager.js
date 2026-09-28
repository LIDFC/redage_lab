// Общий ESC-менеджер окон.
// 1) Стек «закрываемых» состояний (global.escManager.push/remove) — например предпросмотр мебели:
//    ESC закрывает верхнее состояние.
// 2) Закрытие по текущему окну CEF: менеджер сам отслеживает, какое окно открыто (перехват window.router.setView/setHud),
//    и для окон из таблицы ниже вызывает их обычное событие закрытия — раньше часть окон «залипала»,
//    если их собственный обработчик ESC в интерфейсе не срабатывал.
// 3) Страховка: если окно уже закрыто (виден HUD), а курсор/блокировка меню остались — ESC их снимает.
const stack = [];

// Окно CEF → как его закрыть (то же событие, что вызывает кнопка «Выйти» в самом окне)
const viewClose = {
    HouseFurniture: () => mp.events.call("client.furniture.close"),
    HouseMenu: () => mp.events.call("client.parking.close"),
    HouseBuy: () => mp.events.call("client.houseinfo.close"),
    HouseApartments: () => mp.events.call("client.apartments.close"),
    HouseRielt: () => mp.events.call("client.rieltagency.close"),
    PlayerLockBreak: () => mp.events.call("client.lockbreak.cancel"),
    PlayerCyberHack: () => mp.events.call("client.cyberhack.cancel"),
    AdminConfigPanel: () => mp.events.call("client.cfgpanel.close"),
    PlayerWarehouse: () => mp.events.call("client.warehouse.close"),
    QuestsDialog: () => mp.events.call("client.quest.close"),
    BusinessPetShop: () => mp.events.call("closePetshop"),
    PlayerJobSelector: () => mp.events.call("closeJobMenu"),
    FractionsStock: () => mp.events.call("closeStock"),
    FractionsContractShop: () => mp.events.call("client.orgcontracts.shop.close"),
    BlackMarket: () => mp.events.call("client.blackmarket.close"),
    PlayerDropinfo: () => mp.events.call("client.dropinfo.close"),
};

global.cefView = "Hud";

// Отслеживаем текущее окно по командам роутеру CEF
const originalEmmit = mp.gui.emmit;
mp.gui.emmit = (execute, log = 0) => {
    try {
        if (typeof execute === "string" && execute.indexOf("window.router.") !== -1) {
            const view = execute.match(/window\.router\.setView\(\s*[\\"'`]*(\w+)/);
            if (view) global.cefView = view[1];
            else if (execute.indexOf("window.router.setHud(") !== -1) global.cefView = "Hud";
        }
    } catch (e) {}
    return originalEmmit(execute, log);
};

global.escManager = {
    /** Зарегистрировать состояние, которое закрывается по ESC (повторная регистрация с тем же именем заменяет старую). */
    push(name, close) {
        this.remove(name);
        stack.push({ name, close });
    },
    remove(name) {
        const index = stack.findIndex((item) => item.name === name);
        if (index !== -1) stack.splice(index, 1);
    },
    has(name) {
        return stack.some((item) => item.name === name);
    },

    /** ESC: закрыть верхнее. true — нажатие обработано, остальные обработчики не нужны. */
    handle() {
        if (stack.length) {
            const item = stack.pop();
            try {
                item.close();
            } catch (e) {}
            return true;
        }
        const close = viewClose[global.cefView];
        if (close) {
            try {
                close();
            } catch (e) {}
            return true;
        }
        return false;
    },

    /** Смерть/телепорт: закрыть всё из стека. */
    closeAll() {
        while (stack.length) {
            const item = stack.pop();
            try {
                item.close();
            } catch (e) {}
        }
    },

    /** Страховка от «залипшего» курсора: окно закрыто (HUD), а меню считается открытым. */
    unstick() {
        setTimeout(() => {
            try {
                if (!global.menuOpened || global.cefView !== "Hud" || stack.length) return;
                if (global.isPhoneOpen || global.isTabletOpen || global.circleOpen || global.chatActive || global.isPopup) return;
                if (mp.game.ui.isPauseMenuActive()) return;
                global.menuClose();
            } catch (e) {}
        }, 400);
    },
};
