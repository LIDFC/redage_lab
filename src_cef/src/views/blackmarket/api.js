// Общие функции приложения «Чёрный рынок»
import { executeClient } from 'api/rage'
import { format } from 'api/formatter'
import { itemsInfo } from 'json/itemsInfo'
import { getPng } from '@/views/player/menu/elements/inventory/getPng'

// Любое действие уходит на сервер: клиент передаёт только id и введённые числа
export const action = (name, args = {}) => executeClient("client.blackmarket.action", name, JSON.stringify(args));

export const btc = (value) => `${format("money", Math.round(Number(value) || 0))} BTC`;
export const usd = (value) => `$${format("money", Math.round(Number(value) || 0))}`;

export const itemIcon = (itemId) => {
    try {
        return getPng({ ItemId: itemId, Data: "" }, itemsInfo[itemId] || {});
    } catch (e) {
        return "";
    }
};

export const timeLeft = (minutes) => {
    minutes = Math.max(0, Number(minutes) || 0);
    if (minutes >= 60 * 24) return `${Math.floor(minutes / 60 / 24)} д ${Math.floor(minutes / 60) % 24} ч`;
    if (minutes >= 60) return `${Math.floor(minutes / 60)} ч ${minutes % 60} мин`;
    return `${minutes} мин`;
};

export const categories = [
    { key: "all", name: "Всё" },
    { key: "drugs", name: "Вещества" },
    { key: "weapons", name: "Оружие" },
    { key: "ammo", name: "Патроны" },
    { key: "melee", name: "Холодное" },
    { key: "mods", name: "Обвесы" },
    { key: "armor", name: "Броня" },
    { key: "tools", name: "Инструменты" },
];

export const toInt = (value) => {
    const n = Math.floor(Number(String(value).replace(/\s/g, "").replace(",", ".")));
    return Number.isFinite(n) ? n : 0;
};
