// Картинки мебели, которых нет на CDN: положите сюда <модель>.png (например apa_mp_h_stn_sofacorn_01.png) —
// после сборки интерфейса они подхватятся автоматически (магазин мебели и список мебели в телефоне).
// Если картинки нет ни здесь, ни на CDN, показывается иконка категории.
const context = require.context("./", false, /\.png$/);

const localProps = {};
context.keys().forEach((key) => {
    const model = key.replace("./", "").replace(/\.png$/, "");
    const file = context(key);
    localProps[model] = file && file.default ? file.default : file;
});

export const furnitureImage = (model) => localProps[model] || `${document.cloud}inventoryItems/furniture/${model}.png`;
