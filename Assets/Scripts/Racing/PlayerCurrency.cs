using UnityEngine;

namespace Racing
{
    /// Mock currency wallet backing the Shop. PlayerPrefs-persisted, starts with a
    /// seed balance so there's something to spend from day one. No earning-from-races
    /// logic yet -- Earn() is exposed for whoever wires that up later.
    public class PlayerCurrency : MonoBehaviour
    {
        public static PlayerCurrency Instance { get; private set; }

        const string BalanceKey = "RaceGarage.Currency";

        [SerializeField] int startingBalance = 1000;

        public int Balance { get; private set; }

        public event System.Action<int> BalanceChanged;

        void Awake()
        {
            Instance = this;
            Balance = PlayerPrefs.HasKey(BalanceKey) ? PlayerPrefs.GetInt(BalanceKey) : startingBalance;
        }

        public bool CanAfford(int cost) => Balance >= cost;

        public bool TrySpend(int cost)
        {
            if (!CanAfford(cost)) return false;
            Balance -= cost;
            Save();
            return true;
        }

        public void Earn(int amount)
        {
            if (amount <= 0) return;
            Balance += amount;
            Save();
        }

        /// Wipes the saved balance back to an exact amount (0 for the debug reset
        /// shortcut) rather than falling back to startingBalance like a missing save
        /// would.
        public void ResetBalance(int newBalance = 0)
        {
            Balance = newBalance;
            Save();
        }

        void Save()
        {
            PlayerPrefs.SetInt(BalanceKey, Balance);
            PlayerPrefs.Save();
            BalanceChanged?.Invoke(Balance);
        }
    }
}
