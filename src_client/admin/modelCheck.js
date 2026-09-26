// /modelcheck <модель> — проверка, видит ли игра модель (например, машину из DLC).
// Ничего не меняет, только пишет результат в чат. Работает у любого игрока.
mp.events.add('playerCommand', (command) => {
    const args = command.trim().split(/\s+/);
    if (args[0].toLowerCase() !== 'modelcheck')
        return;

    const name = args[1];
    if (!name) {
        mp.gui.chat.push('Использование: /modelcheck <название модели>');
        return;
    }

    const hash = mp.game.joaat(name);
    const inImage = mp.game.streaming.isModelInCdimage(hash);
    const valid = mp.game.streaming.isModelValid(hash);
    let vehicle = false;
    try { vehicle = mp.game.streaming.isModelAVehicle(hash); } catch (e) {}

    let verdict;
    if (!inImage)
        verdict = '!{#ff6b6b}модели НЕТ в игре — DLC-пакет не подключился или название другое';
    else if (!vehicle)
        verdict = '!{#ffc062}модель есть, но это не транспорт';
    else
        verdict = '!{#2bb6a8}машина есть в игре — спавн должен работать';

    mp.gui.chat.push(`[modelcheck] ${name} (hash ${hash >>> 0}): в игре=${inImage}, валидна=${valid}, транспорт=${vehicle}`);
    mp.gui.chat.push(verdict);
});
