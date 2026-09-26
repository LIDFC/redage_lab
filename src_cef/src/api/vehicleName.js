// Отображаемое имя машины по модели: bmwm5 → «BMW M5», mbc63s → «Mercedes-AMG C63 S».
// Берётся из справочника автосалона (марка + модель), ключ ищется без учёта регистра
// и по имени картинки. Неизвестная модель возвращается как есть.
import authInfo from '@/views/business/autoshop/authInfo';

const names = {};
for (const key in authInfo) {
    const info = authInfo[key];
    if (!info || !info.model) continue;
    const title = info.name ? `${info.name} ${info.model}` : info.model;
    names[key.toLowerCase()] = title;
    if (info.img && !names[info.img.toLowerCase()])
        names[info.img.toLowerCase()] = title;
}

// Модели, которых нет в автосалоне
Object.assign(names, {
    m3gtr: "BMW M3 GTR",
});

export const vehicleName = (model) => {
    if (typeof model !== "string" || !model.length) return model;
    return names[model.toLowerCase()] || model;
};

export default vehicleName;
