-- Торговая площадка (MAJESTIC MARKETPLACE / EternalDev). Выполнять в основной базе (REDAGE_DB_NAME, по умолчанию ra3_main).
-- Отличие от авторских файлов: без DROP TABLE, повторный запуск не удаляет лоты.
SET NAMES utf8mb4;

CREATE TABLE IF NOT EXISTS `e-dev_marketplace-auction`  (
  `id` int NOT NULL,
  `type` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
  `cost` int NULL DEFAULT NULL,
  `data` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
  `betStep` int NULL DEFAULT NULL,
  `bets` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
  `endDate` datetime NULL DEFAULT NULL,
  `views` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
  `favourites` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci ROW_FORMAT = Dynamic;

CREATE TABLE IF NOT EXISTS `e-dev_marketplace-items`  (
  `id` int NOT NULL,
  `owner` int NULL DEFAULT NULL,
  `type` int NULL DEFAULT NULL,
  `data` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
  `cost` bigint NULL DEFAULT NULL,
  `views` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
  `favourites` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
  `photos` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
  `comment` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
  `createDate` datetime NULL DEFAULT NULL,
  `endDate` datetime NULL DEFAULT NULL,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci ROW_FORMAT = Dynamic;

CREATE TABLE IF NOT EXISTS `e-dev_marketplace-profiles`  (
  `uuid` int NOT NULL,
  `storage` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
  PRIMARY KEY (`uuid`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci ROW_FORMAT = Dynamic;

