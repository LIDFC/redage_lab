// Форматирование для планшета бизнеса: "4 090 886" + серые ",00" как на макете
export const splitMoney = (value) => {
    const n = Math.round(Number(value) || 0);
    const sign = n < 0 ? "−" : "";
    return { int: sign + Math.abs(n).toString().replace(/\B(?=(\d{3})+(?!\d))/g, " "), frac: ",00" };
};

export const money = (value) => {
    const n = Math.round(Number(value) || 0);
    return `${n < 0 ? "−" : ""}$${splitMoney(Math.abs(n)).int}`;
};

export const pad2 = (n) => String(n).padStart(2, "0");

export const dateTime = (ms) => {
    if (!ms) return "—";
    const d = new Date(ms);
    return `${pad2(d.getHours())}:${pad2(d.getMinutes())} ${pad2(d.getDate())}.${pad2(d.getMonth() + 1)}.${d.getFullYear()}`;
};

// Товары с ценой в процентах (одежда, тюнинг, тату и т.п.) — как в телефоне (phonenew/.../business/data.js)
export const isPercent = (name, bizType) =>
    bizType == 7 || bizType == 9 || bizType == 10 || bizType == 11 || bizType == 12 ||
    name == "Татуировки" || name == "Парики" || name == "Патроны" || name == "Модификации";

export const markup = (p, bizType) => isPercent(p.name, bizType)
    ? Math.round(p.price)
    : Math.round(p.price * 100 / Math.max(1, p.defaultPrice));
