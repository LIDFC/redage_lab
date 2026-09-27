// Иконки приложений планшета — встроенные SVG (белые глифы на цветной плашке), без внешних хостингов
const svg = (path) => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${path}</svg>`;

export const icons = {
    fraction: svg('<path d="M12 3l7 3v5c0 4.5-3 8.3-7 10-4-1.7-7-5.5-7-10V6l7-3z"/><path d="M9 12l2 2 4-4"/>'),
    organization: svg('<circle cx="9" cy="8" r="3"/><circle cx="17" cy="9" r="2.3"/><path d="M3.5 19c.6-3.2 2.8-5 5.5-5s4.9 1.8 5.5 5"/><path d="M15 14.5c2.6-.3 4.8 1.2 5.5 4"/>'),
    business: svg('<path d="M4 20V10l8-5 8 5v10"/><path d="M9 20v-6h6v6"/><path d="M3 20h18"/>'),
    marketplace: svg('<path d="M4 7h16l-1.5 11a2 2 0 0 1-2 1.7h-9a2 2 0 0 1-2-1.7L4 7z"/><path d="M9 7V6a3 3 0 0 1 6 0v1"/>'),
    forbes: svg('<path d="M5 20V10"/><path d="M12 20V4"/><path d="M19 20v-7"/><path d="M3 20h18"/>'),
};
