-- DLC-интерьеры квартир (GTA5RP_APARTMENT). Сервер добавляет колонку сам при старте;
-- этот файл нужен, только если у пользователя БД сервера нет прав на ALTER.
ALTER TABLE `apartment_flats` ADD COLUMN IF NOT EXISTS `interior` int(11) NOT NULL DEFAULT 0;
