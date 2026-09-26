-- Многоквартирные дома (dotnet/resources/NeptuneEvo/Houses/Apartments/ApartmentManager.cs).
-- Идея из «Apartment System for RedAge 1.1» (koltr), но таблицу `houses` мы НЕ меняем
-- (авторский houses.sql делал DROP TABLE houses — его выполнять нельзя).
-- Квартира — обычный дом из `houses`; связь дом → многоквартирный дом лежит в `apartment_flats`.
-- Квартиры создаются сервером сами при старте по колонке `plan`:
--   [{"type": класс дома 0-9 (кроме 7), "price": цена, "garage": тип гаража 0-9, "count": сколько квартир}]
-- Координаты: entrance — точка у подъезда на уровне земли, garage — въезд в гараж (туда же выезжают машины).
-- Если точка стоит неудачно, её можно перенести в игре: /apartentrance id, /apartgarage id (сидя в машине).
-- Повторный запуск безопасен: таблицы создаются через IF NOT EXISTS, дома — INSERT IGNORE.

CREATE TABLE IF NOT EXISTS `apartment_buildings` (
  `id` int(11) NOT NULL,
  `name` varchar(64) NOT NULL,
  `address` varchar(128) NOT NULL DEFAULT '',
  `entrance` varchar(255) NOT NULL,
  `garage` varchar(255) NOT NULL,
  `garage_heading` float NOT NULL DEFAULT 0,
  `plan` text NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `apartment_flats` (
  `house_id` int(11) NOT NULL,
  `building_id` int(11) NOT NULL,
  `floor` int(11) NOT NULL DEFAULT 2,
  `number` int(11) NOT NULL DEFAULT 1,
  PRIMARY KEY (`house_id`),
  KEY `building_id` (`building_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Классы: 2 Эконом+, 3 Комфорт, 4 Комфорт+ (интерьер 4 Integrity Way), 5 Премиум, 9 Люкс (интерьер Eclipse Towers)
-- Гаражи: 0 — 2 места, 1 — 3, 2 — 4, 3 — 5, 5 — 10
INSERT IGNORE INTO `apartment_buildings` (`id`, `name`, `address`, `entrance`, `garage`, `garage_heading`, `plan`) VALUES
(1, '4 Integrity Way', 'Пиллбокс-Хилл, Integrity Way',
    '{"x":-47.3,"y":-585.9,"z":36.95}', '{"x":-15.6,"y":-612.0,"z":35.86}', 70,
    '[{"type":4,"price":450000,"garage":2,"count":8},{"type":5,"price":850000,"garage":3,"count":4}]'),
(2, 'Del Perro Heights', 'Дель-Перро, Bay City Ave',
    '{"x":-1447.2,"y":-537.7,"z":33.74}', '{"x":-1455.0,"y":-503.5,"z":32.1}', 210,
    '[{"type":3,"price":260000,"garage":1,"count":8},{"type":4,"price":430000,"garage":2,"count":4}]'),
(3, 'Richards Majestic', 'Бертон, Prosperity St',
    '{"x":-936.1,"y":-378.7,"z":37.96}', '{"x":-876.5,"y":-359.0,"z":36.1}', 200,
    '[{"type":5,"price":900000,"garage":3,"count":6},{"type":9,"price":2200000,"garage":5,"count":2}]'),
(4, 'Tinsel Towers', 'Рокфорд-Хиллз, South Mo Milton Dr',
    '{"x":-618.6,"y":37.1,"z":42.59}', '{"x":-639.0,"y":56.6,"z":43.7}', 90,
    '[{"type":4,"price":420000,"garage":2,"count":8},{"type":5,"price":820000,"garage":3,"count":4}]'),
(5, 'Weazel Plaza', 'Рокфорд-Хиллз, Movie Star Way',
    '{"x":-913.8,"y":-455.3,"z":38.6}', '{"x":-823.5,"y":-434.9,"z":36.6}', 120,
    '[{"type":2,"price":110000,"garage":0,"count":8},{"type":3,"price":230000,"garage":1,"count":6}]'),
(6, 'Alta Street', 'Центр, Alta St',
    '{"x":-270.9,"y":-957.8,"z":30.22}', '{"x":-281.5,"y":-995.0,"z":24.1}', 250,
    '[{"type":2,"price":95000,"garage":0,"count":10},{"type":3,"price":210000,"garage":1,"count":6}]');
