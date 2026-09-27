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

// Заливка топлива: шум жидкости через полосовой фильтр с «бульканьем» и щелчок пистолета в конце
const fuel = (liters = 20) => {
    const ac = getCtx();
    if (!ac) return;
    const duration = Math.min(4, Math.max(1.6, 1.2 + liters / 25));
    const start = ac.currentTime;

    const length = Math.floor(ac.sampleRate * duration);
    const buffer = ac.createBuffer(1, length, ac.sampleRate);
    const data = buffer.getChannelData(0);
    let last = 0;
    for (let i = 0; i < length; i++) {
        // «Коричневый» шум — мягче белого, похож на поток жидкости
        last = (last + 0.02 * (Math.random() * 2 - 1)) / 1.02;
        data[i] = last * 3.5;
    }
    const noise = ac.createBufferSource();
    noise.buffer = buffer;

    const filter = ac.createBiquadFilter();
    filter.type = "bandpass";
    filter.frequency.value = 700;
    filter.Q.value = 1.2;

    // Бульканье: частота фильтра «гуляет»
    const lfo = ac.createOscillator();
    const lfoGain = ac.createGain();
    lfo.frequency.value = 7;
    lfoGain.gain.value = 220;
    lfo.connect(lfoGain).connect(filter.frequency);

    const gain = ac.createGain();
    gain.gain.setValueAtTime(0.0001, start);
    gain.gain.exponentialRampToValueAtTime(0.35, start + 0.15);
    gain.gain.setValueAtTime(0.35, start + duration - 0.35);
    gain.gain.exponentialRampToValueAtTime(0.0001, start + duration);

    noise.connect(filter).connect(gain).connect(ac.destination);
    noise.start(start);
    lfo.start(start);
    noise.stop(start + duration);
    lfo.stop(start + duration);

    // Щелчок пистолета после заливки
    tone(140, 0.09, "sine", 0.12, duration + 0.05);
    tone(2400, 0.02, "square", 0.02, duration + 0.05);
};

const presets = {
    fuel,
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

export const playSound = (name = "tap", ...args) => {
    try {
        (presets[name] || presets.tap)(...args);
    } catch (e) {}
};

// Для вызова из клиента (mp.gui.emmit): window.playUiSound("fuel", литры)
window.playUiSound = playSound;

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
