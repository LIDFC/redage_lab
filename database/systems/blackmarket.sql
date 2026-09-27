-- Чёрный рынок и крипта (NeptuneEvo/BlackMarket). Сервер создаёт эти таблицы сам при старте,
-- файл — для ручной установки/просмотра схемы: mysql -u root -p ra3_main < database/systems/blackmarket.sql
-- Предметы лотов и закладок хранятся в обычной items_data (data_id = bmlot_{id} / bmdrop_{id}, location = blackmarket).

CREATE TABLE IF NOT EXISTS `crypto_wallets` (
  `uuid` int(11) NOT NULL,
  `balance` bigint(20) NOT NULL DEFAULT 0,
  `reserved` bigint(20) NOT NULL DEFAULT 0,
  PRIMARY KEY (`uuid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `crypto_fraction_wallets` (
  `fraction_id` int(11) NOT NULL,
  `balance` bigint(20) NOT NULL DEFAULT 0,
  PRIMARY KEY (`fraction_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `crypto_system` (
  `id` int(11) NOT NULL,
  `balance` bigint(20) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `crypto_history` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `uuid` int(11) NOT NULL DEFAULT 0,
  `fraction_id` int(11) NOT NULL DEFAULT 0,
  `kind` varchar(32) NOT NULL,
  `title` varchar(128) NOT NULL,
  `amount` bigint(20) NOT NULL,
  `currency` varchar(8) NOT NULL DEFAULT 'BTC',
  `created` datetime NOT NULL,
  PRIMARY KEY (`id`),
  KEY `uuid` (`uuid`),
  KEY `fraction_id` (`fraction_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `blackmarket_audit` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `created` datetime NOT NULL,
  `action` varchar(32) NOT NULL,
  `actor_uuid` int(11) NOT NULL DEFAULT 0,
  `target_uuid` int(11) NOT NULL DEFAULT 0,
  `item_id` int(11) NOT NULL DEFAULT 0,
  `count` int(11) NOT NULL DEFAULT 0,
  `amount` bigint(20) NOT NULL DEFAULT 0,
  `source` varchar(32) NOT NULL DEFAULT '',
  `fee` bigint(20) NOT NULL DEFAULT 0,
  `result` varchar(32) NOT NULL DEFAULT 'ok',
  `details` text NULL,
  PRIMARY KEY (`id`),
  KEY `actor_uuid` (`actor_uuid`),
  KEY `target_uuid` (`target_uuid`),
  KEY `action` (`action`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `blackmarket_lots` (
  `id` int(11) NOT NULL,
  `owner_uuid` int(11) NOT NULL,
  `item_id` int(11) NOT NULL,
  `count` int(11) NOT NULL,
  `price_unit` bigint(20) NOT NULL,
  `created` datetime NOT NULL,
  `ends` datetime NOT NULL,
  PRIMARY KEY (`id`),
  KEY `owner_uuid` (`owner_uuid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `blackmarket_drops` (
  `id` int(11) NOT NULL,
  `buyer_uuid` int(11) NOT NULL,
  `lot_id` int(11) NOT NULL,
  `item_id` int(11) NOT NULL,
  `count` int(11) NOT NULL,
  `pos_x` float NOT NULL,
  `pos_y` float NOT NULL,
  `pos_z` float NOT NULL,
  `created` datetime NOT NULL,
  `expires` datetime NOT NULL,
  PRIMARY KEY (`id`),
  KEY `buyer_uuid` (`buyer_uuid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `crypto_p2p` (
  `id` int(11) NOT NULL,
  `owner_uuid` int(11) NOT NULL,
  `amount_left` bigint(20) NOT NULL,
  `price_per_btc` decimal(18,4) NOT NULL,
  `created` datetime NOT NULL,
  `ends` datetime NOT NULL,
  PRIMARY KEY (`id`),
  KEY `owner_uuid` (`owner_uuid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

