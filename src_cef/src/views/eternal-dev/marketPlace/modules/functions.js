import { executeClient } from "api/rage";
import { getFavouriteData } from "./formats";

export const createLot = (lotData, price, data) => {
    const type = lotData.type;
    let id = lotData.params ? lotData.params.id : 0;

    if (type == "clothes")
        id = `${lotData.params.itemId}^${lotData.params.itemData}`;

    // Предмет с данными (серийник оружия и т.п.) — вместе с данными, иначе только номер
    if (type == "item")
        id = lotData.params.itemData ? `${lotData.params.itemId}^${lotData.params.itemData}` : `${lotData.params.itemId}`;

    // Вещь из инвентаря (а не со склада маркетплейса) — сервер списывает из инвентаря
    if ((type == "item" || type == "clothes") && lotData.source == "inv")
        id = `inv:${id}`;

    executeClient("client.marketPlace.create_lot", 
        lotData.type, id,

        data.comment || "", 
        data.title || "", 
        
        price || 0, 
        
        data.hours, 
        data.count, 
        data.paymentType
    );
};

export const deleteLot = (lotId) => {
    executeClient("client.marketPlace.delete_lot", lotId);
}

export const editLot = (lotId, lotData, data) => {
    executeClient("client.marketPlace.edit_lot", lotId, data.comment || lotData.comment, data.title || lotData.params.name, data.price || lotData.cost);
}

export const setModalState = (modalName) => {
    executeClient("client.marketPlace.setModal", modalName);
}

export const marketLotActions = (type, ...args) => {
    executeClient("client.marketPlace.actions", type, ...args);
}

export const buyLot = (lotId, count, paymentType) => {
    executeClient("client.marketPlace.buy", lotId, count, paymentType);
}

export const unloadLot = (lotId) => {
    executeClient("client.marketPlace.unload", lotId);
}

export const marketAuctionSetBet = (lotId, mult) => {
    executeClient("client.marketPlace.auction.setBet", lotId, mult);
}

export const marketSelectItem = (lotType, lotId) => {
    executeClient("client.marketPlace.selectItem", lotType, lotId);
}

export const marketProlong = (lotId, hours, paymentType) => {
    executeClient("client.marketPlace.prolong", lotId, hours, paymentType);
}

export const marketContactAuthor = (type, phoneNumber) => {
    executeClient("client.marketPlace.contactAuthor", type, phoneNumber);
}

export const setFavourite = (data) => {
    const favouriteData = getFavouriteData(data);
    executeClient("client.marketPlace.set_favourite", favouriteData.type, favouriteData.id, favouriteData.subData);
};