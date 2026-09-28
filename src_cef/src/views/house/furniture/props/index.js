// Картинки мебели, которых нет на CDN: положите сюда <модель>.png или .jpg (например apa_mp_h_stn_sofacorn_01.jpg) —
// превью новой мебели скачивает tools/furniture_previews/download.py;
// после сборки интерфейса они подхватятся автоматически (магазин мебели и список мебели в телефоне).
// Если картинки нет ни здесь, ни на CDN, показывается иконка категории.
const context = require.context("./", false, /\.(png|jpe?g)$/);

const localProps = {};
context.keys().forEach((key) => {
    const model = key.replace("./", "").replace(/\.(png|jpe?g)$/, "");
    const file = context(key);
    localProps[model] = file && file.default ? file.default : file;
});

export const furnitureImage = (model) => localProps[model] || `${document.cloud}inventoryItems/furniture/${model}.png`;
