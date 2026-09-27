// Короткие звуки интерфейса без аудиофайлов (Web Audio): «пип» банкомата, мягкий тап планшета и т.п.
// Использование: import { playSound, sound } from 'api/uiSound'
//   playSound("atm")                     — разово
//   <div use:sound={"tap"}>              — на нажатие элемента

let ctx = null;
const getCtx = () => {
    try {
        if (!ctx) {
            const AC = window.AudioContext || window.webkitAudioContext;
            if (!AC) return null;
            ctx = new AC();
        }
        if (ctx.state === "suspended") ctx.resume();
        return ctx;
    } catch (e) {
        return null;
    }
};

// Одна нота: частота, длительность (сек), форма волны, громкость, задержка
const tone = (freq, duration, type = "square", volume = 0.05, delay = 0) => {
    const ac = getCtx();
    if (!ac) return;
    const start = ac.currentTime + delay;
    const osc = ac.createOscillator();
    const gain = ac.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(freq, start);
    // Мягкая атака и затухание, чтобы не щёлкало
    gain.gain.setValueAtTime(0.0001, start);
    gain.gain.exponentialRampToValueAtTime(volume, start + 0.005);
    gain.gain.exponentialRampToValueAtTime(0.0001, start + duration);
    osc.connect(gain).connect(ac.destination);
    osc.start(start);
    osc.stop(start + duration + 0.02);
};

const presets = {
    // Клавиша банкомата: короткий высокий «пип»
    atm: () => tone(1320, 0.08, "square", 0.035),
    // Банкомат: операция принята — два восходящих «пипа»
    atmOk: () => { tone(1320, 0.08, "square", 0.035); tone(1760, 0.12, "square", 0.035, 0.1); },
    // Ошибка/отказ — низкий двойной
    error: () => { tone(330, 0.12, "square", 0.04); tone(247, 0.16, "square", 0.04, 0.14); },
    // Мягкий тап для планшета/телефона
    tap: () => tone(880, 0.045, "sine", 0.05),
    // Переключатель вкл/выкл
    toggle: () => tone(660, 0.05, "triangle", 0.05),
    // Успешное действие в приложении
    success: () => { tone(784, 0.07, "sine", 0.05); tone(1175, 0.11, "sine", 0.05, 0.07); },
};

export const playSound = (name = "tap") => {
    try {
        (presets[name] || presets.tap)();
    } catch (e) {}
};

// Svelte action: звук при нажатии на элемент (не мешает on:click)
export const sound = (node, name = "tap") => {
    let current = name;
    const handler = () => playSound(current);
    node.addEventListener("mousedown", handler);
    return {
        update(value) { current = value; },
        destroy() { node.removeEventListener("mousedown", handler); },
    };
};
