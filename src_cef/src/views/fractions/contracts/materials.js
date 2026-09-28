// Общие данные для UI подрядов: иконки материалов, форматирование денег и времени.

const icons = {
    concrete: { color: "#9AA4B2", svg: '<path d="M5 9l7-4 7 4v8l-7 4-7-4z"/><path d="M5 9l7 4 7-4M12 13v8"/>' },
    brick: { color: "#E0714F", svg: '<rect x="3" y="6" width="18" height="12" rx="1"/><path d="M3 12h18M9 6v6M15 12v6"/>' },
    steel: { color: "#7FB2E5", svg: '<path d="M4 7h16M4 17h16M7 7v10M17 7v10"/><path d="M7 12h10"/>' },
    asphalt: { color: "#5C6370", svg: '<ellipse cx="12" cy="6" rx="7" ry="2.5"/><path d="M5 6v11c0 1.4 3.1 2.5 7 2.5s7-1.1 7-2.5V6"/><path d="M5 11.5c0 1.4 3.1 2.5 7 2.5s7-1.1 7-2.5"/>' },
    wood: { color: "#C8964F", svg: '<circle cx="7" cy="16" r="3"/><circle cx="17" cy="16" r="3"/><circle cx="12" cy="8" r="3"/>' },
};

export const materialIcon = (id) => icons[id] || { color: "#F5A524", svg: '<rect x="4" y="4" width="16" height="16" rx="2"/>' };

export const money = (value) => {
    const sign = value < 0 ? "-" : "";
    return `${sign}$${Math.abs(Math.round(value || 0)).toString().replace(/\B(?=(\d{3})+(?!\d))/g, " ")}`;
};

export const number = (value) => Math.round(value || 0).toString().replace(/\B(?=(\d{3})+(?!\d))/g, " ");

export const duration = (seconds) => {
    seconds = Math.max(0, Math.floor(seconds || 0));
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = seconds % 60;
    const pad = (v) => (v < 10 ? "0" : "") + v;
    return `${pad(h)}:${pad(m)}:${pad(s)}`;
};

export const parseJson = (value, fallback) => {
    try {
        return typeof value === "string" ? JSON.parse(value) : value || fallback;
    } catch (e) {
        return fallback;
    }
};
