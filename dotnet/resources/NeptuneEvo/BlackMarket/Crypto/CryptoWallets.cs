using System;
using System.Collections.Generic;
using System.Data;

namespace NeptuneEvo.BlackMarket.Crypto
{
    public class CryptoWallet
    {
        public int Uuid;
        public long Balance;
        /// <summary>Заблокировано P2P-заявками: тратить и переводить нельзя.</summary>
        public long Reserved;
        public long Available => Balance - Reserved;
    }

    /// <summary>
    /// Игровая криптовалюта (BTC): личные кошельки игроков, кошельки криминальных фракций и системный
    /// кошелёк (сюда идут комиссии). Отдельна от Money/BankMoney/RedBucks/FractionData.Money.
    /// Все изменения — только под <see cref="BlackMarketCore.Sync"/> (методы берут блокировку сами, она реентерабельна).
    /// Курс к $ пока фиксированный (конфиг), но кошельки хранят только количество BTC, так что курс/биржа добавляются снаружи.
    /// </summary>
    public static class CryptoWallets
    {
        private static readonly Dictionary<int, CryptoWallet> Personal = new Dictionary<int, CryptoWallet>();
        private static readonly Dictionary<int, long> Fraction = new Dictionary<int, long>();
        private static long _system = 0;

        public static void Load()
        {
            lock (BlackMarketCore.Sync)
            {
                Personal.Clear();
                Fraction.Clear();

                using (var data = BlackMarketRepository.Read("SELECT * FROM `crypto_wallets`"))
                {
                    if (data != null)
                        foreach (DataRow row in data.Rows)
                        {
                            var wallet = new CryptoWallet
                            {
                                Uuid = Convert.ToInt32(row["uuid"]),
                                Balance = Convert.ToInt64(row["balance"]),
                                Reserved = Convert.ToInt64(row["reserved"]),
                            };
                            Personal[wallet.Uuid] = wallet;
                        }
                }

                using (var data = BlackMarketRepository.Read("SELECT * FROM `crypto_fraction_wallets`"))
                {
                    if (data != null)
                        foreach (DataRow row in data.Rows)
                            Fraction[Convert.ToInt32(row["fraction_id"])] = Convert.ToInt64(row["balance"]);
                }

                using (var data = BlackMarketRepository.Read("SELECT `balance` FROM `crypto_system` WHERE `id`=1"))
                {
                    _system = data != null && data.Rows.Count > 0 ? Convert.ToInt64(data.Rows[0]["balance"]) : 0;
                }

                BlackMarketCore.Log.Write($"Crypto: {Personal.Count} личных кошельков, {Fraction.Count} кошельков фракций, системный {_system}");
            }
        }

        #region Личный кошелёк
        /// <summary>Копия состояния кошелька (для отображения).</summary>
        public static CryptoWallet Get(int uuid)
        {
            lock (BlackMarketCore.Sync)
            {
                return Personal.TryGetValue(uuid, out var wallet)
                    ? new CryptoWallet { Uuid = uuid, Balance = wallet.Balance, Reserved = wallet.Reserved }
                    : new CryptoWallet { Uuid = uuid };
            }
        }

        public static long Available(int uuid)
        {
            lock (BlackMarketCore.Sync)
                return Personal.TryGetValue(uuid, out var wallet) ? wallet.Available : 0;
        }

        private static CryptoWallet GetOrCreate(int uuid)
        {
            if (!Personal.TryGetValue(uuid, out var wallet))
            {
                wallet = new CryptoWallet { Uuid = uuid };
                Personal[uuid] = wallet;
            }
            return wallet;
        }

        /// <summary>
        /// Изменить личный баланс. Списание не может залезть в зарезервированное P2P и увести баланс в минус.
        /// </summary>
        public static bool ChangePersonal(int uuid, long delta)
        {
            if (uuid <= 0)
                return false;
            lock (BlackMarketCore.Sync)
            {
                var wallet = GetOrCreate(uuid);
                if (delta < 0 && wallet.Available + delta < 0)
                    return false;
                wallet.Balance += delta;
                SavePersonal(wallet);
                return true;
            }
        }

        /// <summary>Заблокировать часть свободного баланса (P2P-заявка).</summary>
        public static bool Reserve(int uuid, long amount)
        {
            if (amount <= 0)
                return false;
            lock (BlackMarketCore.Sync)
            {
                var wallet = GetOrCreate(uuid);
                if (wallet.Available < amount)
                    return false;
                wallet.Reserved += amount;
                SavePersonal(wallet);
                return true;
            }
        }

        /// <summary>Снять блокировку (отмена/истечение заявки).</summary>
        public static void Release(int uuid, long amount)
        {
            if (amount <= 0)
                return;
            lock (BlackMarketCore.Sync)
            {
                var wallet = GetOrCreate(uuid);
                wallet.Reserved = Math.Max(0, wallet.Reserved - amount);
                SavePersonal(wallet);
            }
        }

        /// <summary>Списать заблокированное (покупка по P2P-заявке).</summary>
        public static bool ConsumeReserved(int uuid, long amount)
        {
            if (amount <= 0)
                return false;
            lock (BlackMarketCore.Sync)
            {
                var wallet = GetOrCreate(uuid);
                if (wallet.Reserved < amount || wallet.Balance < amount)
                    return false;
                wallet.Reserved -= amount;
                wallet.Balance -= amount;
                SavePersonal(wallet);
                return true;
            }
        }

        /// <summary>Перевод между игроками одним шагом (обе стороны под одной блокировкой).</summary>
        public static bool Transfer(int fromUuid, int toUuid, long amount)
        {
            if (amount <= 0 || fromUuid <= 0 || toUuid <= 0 || fromUuid == toUuid)
                return false;
            lock (BlackMarketCore.Sync)
            {
                var from = GetOrCreate(fromUuid);
                if (from.Available < amount)
                    return false;
                var to = GetOrCreate(toUuid);
                from.Balance -= amount;
                to.Balance += amount;
                SavePersonal(from);
                SavePersonal(to);
                return true;
            }
        }

        private static void SavePersonal(CryptoWallet wallet)
        {
            BlackMarketRepository.Enqueue(
                "INSERT INTO `crypto_wallets` (`uuid`, `balance`, `reserved`) VALUES (@uuid, @balance, @reserved) " +
                "ON DUPLICATE KEY UPDATE `balance`=VALUES(`balance`), `reserved`=VALUES(`reserved`)",
                ("@uuid", wallet.Uuid), ("@balance", wallet.Balance), ("@reserved", wallet.Reserved));
        }
        #endregion

        #region Кошелёк фракции
        public static long GetFraction(int fractionId)
        {
            lock (BlackMarketCore.Sync)
                return Fraction.TryGetValue(fractionId, out var balance) ? balance : 0;
        }

        public static bool ChangeFraction(int fractionId, long delta)
        {
            if (!BlackMarketCore.IsCriminalFraction(fractionId))
                return false;
            lock (BlackMarketCore.Sync)
            {
                Fraction.TryGetValue(fractionId, out var balance);
                if (delta < 0 && balance + delta < 0)
                    return false;
                balance += delta;
                Fraction[fractionId] = balance;
                BlackMarketRepository.Enqueue(
                    "INSERT INTO `crypto_fraction_wallets` (`fraction_id`, `balance`) VALUES (@id, @balance) " +
                    "ON DUPLICATE KEY UPDATE `balance`=VALUES(`balance`)",
                    ("@id", fractionId), ("@balance", balance));
                return true;
            }
        }
        #endregion

        #region Системный кошелёк
        public static long SystemBalance
        {
            get { lock (BlackMarketCore.Sync) return _system; }
        }

        public static void ChangeSystem(long delta)
        {
            if (delta == 0)
                return;
            lock (BlackMarketCore.Sync)
            {
                _system += delta;
                BlackMarketRepository.Enqueue(
                    "INSERT INTO `crypto_system` (`id`, `balance`) VALUES (1, @balance) ON DUPLICATE KEY UPDATE `balance`=VALUES(`balance`)",
                    ("@balance", _system));
            }
        }
        #endregion
    }
}
