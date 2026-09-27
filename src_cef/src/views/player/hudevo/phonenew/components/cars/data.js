export const categorieName = {
    rent : "Аренда",
    property : "Личные",
    homeowner : "Домовладельца",
    homeprisoner : "Подселённого",
}
import { TimeFormat } from 'api/moment'

// Статус машины для карточек «Авто»
export const carState = (car, garage) => {
    if (car.isRent) return { cls: "blue", text: car.isJob ? 'Рабочая' : `Аренда до ${TimeFormat(car.date, "H:mm DD.MM")}` };
    if (car.ticket) return { cls: "red", text: 'Штрафстоянка' };
    if (car.isCarGarage) return { cls: "green", text: car.place >= 0 && garage && !garage.parking ? `В гараже · место ${car.place + 1}` : 'В гараже' };
    if (car.isCreate) return { cls: "amber", text: 'На улице' };
    return { cls: "gray", text: 'Не вызвана' };
}

// Картинка машины с CDN; если её нет (часто у DLC-моделей) — показываем силуэт
export const vehicleImage = (model) => `${document.cloud}inventoryItems/vehicle/${String(model || "").toLowerCase()}.png`;
export const onVehicleImageError = (event) => {
    const img = event.target;
    if (img.dataset.fallback) return;
    img.dataset.fallback = "1";
    img.src = "data:image/svg+xml;utf8," + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 90"><path d="M18 62c0-9 6-14 15-16l22-5 20-15c5-4 11-6 18-6h34c8 0 14 3 19 8l17 16 17 4c7 2 11 7 11 14v8c0 3-2 5-5 5h-12a18 18 0 0 0-35 0H63a18 18 0 0 0-35 0h-5c-3 0-5-2-5-5z" fill="#2A303B" stroke="#4A5363" stroke-width="2"/><path d="M60 42l17-13c4-3 8-4 13-4h14v17z M112 25h9c6 0 10 2 14 6l11 11h-34z" fill="#1E3A5F"/><circle cx="45" cy="70" r="12" fill="#15181D" stroke="#4A5363" stroke-width="3"/><circle cx="155" cy="70" r="12" fill="#15181D" stroke="#4A5363" stroke-width="3"/></svg>');
};
