import { itemsInfo } from "../../../../json/itemsInfo";
import { getPng as getInventoryPng } from "../../../player/menu/elements/inventory/getPng";

// Картинки домов и бизнесов — скриншоты из раздела помощи (лежат в репозитории, CDN не нужен)
import houseImage from "../../../player/help/images/interior3.jpg";
import shop247Image from "../../../player/help/images/2472.jpg";
import fuelImage from "../../../player/help/images/azs.jpg";
import clothesImage from "../../../player/help/images/clot2.jpg";
import burgerImage from "../../../player/help/images/burger2.jpg";
import tattooImage from "../../../player/help/images/tatoo2.jpg";
import barberImage from "../../../player/help/images/barb2.jpg";
import masksImage from "../../../player/help/images/mask.jpg";
import customsImage from "../../../player/help/images/lsc1.jpg";
import carwashImage from "../../../player/help/images/lsc.jpg";
import avatarImage from "../assets/avatar.svg";

export const avatarPicture = avatarImage;

// BusinessManager.BusinessTypeNames: 0 24/7, 1 АЗС, 2-5/15 автосалоны, 6 оружейный, 7 одежда, 8 Burger-Shot,
// 9 тату, 10 барбершоп, 11 маски, 12 LS Customs, 13 мойка, 14 зоомагазин
const businessPictures = {
    0: shop247Image,
    1: fuelImage,
    2: customsImage, 3: customsImage, 4: customsImage, 5: customsImage, 15: customsImage,
    7: clothesImage,
    8: burgerImage,
    9: tattooImage,
    10: barberImage,
    11: masksImage,
    12: customsImage,
    13: carwashImage,
};

export const getPicture = (type, data) => {
    switch(type) {
        case "vehicle":
            return `${document.cloud}inventoryItems/vehicle/${data.params.model.toLowerCase()}.png`;
        case "business":
            return businessPictures[data.params.type] || shop247Image;
        case "house":
            return houseImage;
        case "item":
        case "clothes":
            // Та же логика, что в инвентаре: одежда по drawable/texture, купон на машину — картинка машины
            try {
                const itemId = Number(data.params.itemId);
                const png = getInventoryPng({ ItemId: itemId, Data: String(data.params.itemData || "") }, itemsInfo[itemId] || {});
                if (png)
                    return png;
                return `${document.cloud}inventoryItems/items/${itemId}.png`;
            } catch (e) {
                return `${document.cloud}inventoryItems/items/${data.params.itemId}.png`;
            }
    }

    return null;
}