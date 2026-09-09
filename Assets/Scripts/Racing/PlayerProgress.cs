using System.Collections.Generic;
using UnityEngine;

namespace Racing
{
    /// PlayerPrefs-persisted level/XP + per-race first-clear tracking (same pattern as
    /// PlayerCurrency's balance and RaceGarageController's owned-parts set). Level is
    /// derived from total XP via LevelCurve, never stored directly, so re-tuning the
    /// curve later re-derives everyone's level from their existing XP instead of
    /// needing a migration.
    public class PlayerProgress : MonoBehaviour
    {
        public static PlayerProgress Instance { get; private set; }

        const string XPKey = "RaceGarage.XP";
        const string CompletedRacesKey = "RaceGarage.CompletedRaces";
        const char Separator = ';';

        [SerializeField] LevelCurve levelCurve;

        readonly HashSet<string> completedRaces = new HashSet<string>();

        public int XP { get; private set; }
        public int Level { get; private set; } = 1;
        public int XPIntoLevel => levelCurve != null ? XP - levelCurve.TotalXPForLevel(Level) : 0;
        public int XPForNextLevel => levelCurve != null ? levelCurve.XPToNextLevel(Level) : 0;

        /// (newXPTotal)
        public event System.Action<int> XPChanged;
        /// (newLevel)
        public event System.Action<int> LevelChanged;

        void Awake()
        {
            Instance = this;
            XP = PlayerPrefs.GetInt(XPKey, 0);
            Level = levelCurve != null ? levelCurve.LevelForXP(XP) : 1;
            LoadCompletedRaces();
        }

        public bool HasCompleted(RaceDefinition race) => race != null && completedRaces.Contains(RaceKey(race));

        public void MarkCompleted(RaceDefinition race)
        {
            if (race == null || !completedRaces.Add(RaceKey(race))) return;
            SaveCompletedRaces();
        }

        public void GainXP(int amount)
        {
            if (amount <= 0) return;
            XP += amount;
            PlayerPrefs.SetInt(XPKey, XP);
            PlayerPrefs.Save();
            XPChanged?.Invoke(XP);

            int newLevel = levelCurve != null ? levelCurve.LevelForXP(XP) : 1;
            if (newLevel != Level)
            {
                Level = newLevel;
                LevelChanged?.Invoke(Level);
            }
        }

        /// Wipes saved XP/level and first-clear tracking back to a fresh start (used by
        /// the debug reset shortcut so the full level 1->10 unlock chain can be replayed
        /// without manually clearing PlayerPrefs).
        public void ResetProgress()
        {
            XP = 0;
            PlayerPrefs.SetInt(XPKey, 0);
            completedRaces.Clear();
            PlayerPrefs.SetString(CompletedRacesKey, string.Empty);
            PlayerPrefs.Save();

            Level = levelCurve != null ? levelCurve.LevelForXP(0) : 1;
            XPChanged?.Invoke(XP);
            LevelChanged?.Invoke(Level);
        }

        static string RaceKey(RaceDefinition race) =>
            !string.IsNullOrEmpty(race.raceId) ? race.raceId : race.name;

        void LoadCompletedRaces()
        {
            completedRaces.Clear();
            string raw = PlayerPrefs.GetString(CompletedRacesKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return;
            foreach (var id in raw.Split(Separator))
                if (!string.IsNullOrEmpty(id)) completedRaces.Add(id);
        }

        void SaveCompletedRaces()
        {
            PlayerPrefs.SetString(CompletedRacesKey, string.Join(Separator.ToString(), completedRaces));
            PlayerPrefs.Save();
        }
    }
}
