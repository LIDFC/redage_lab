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
