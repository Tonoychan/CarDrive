using UnityEngine;

namespace Racing
{
    [CreateAssetMenu(fileName = "RaceDefinition", menuName = "Racing/Race Definition")]
    public class RaceDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable save-data key for completion tracking/rewards -- unlike the " +
                 "asset name, renaming the asset in the Project window won't lose a " +
                 "player's \"already completed this race\" record. Falls back to the " +
                 "asset name if left blank.")]
        public string raceId = "";
        public string raceName = "New Race";
        [TextArea] public string description;

        [Header("Progression")]
        [Tooltip("Player level required for this race's trigger to be interactable.")]
        [Min(1)] public int requiredLevel = 1;
        [Tooltip("XP awarded on a first-ever clear of this race.")]
        [Min(0)] public int baseXPReward = 100;
        [Tooltip("Coins awarded on a first-ever clear of this race.")]
        [Min(0)] public int baseCoinReward = 150;
        [Tooltip("Reward multiplier applied on every clear after the first (0.5 = 50% less).")]
        [Range(0f, 1f)] public float repeatRewardMultiplier = 0.5f;

        [Header("Opponents")]
        [Min(0)] public int opponentCount = 6;
        public RaceOpponentPreset[] opponentPresets;

        [Header("Countdown")]
        [Min(0f)] public float countdownSeconds = 3f;

        [Header("Laps")]
        [Tooltip("How many times a participant must cross the finish line to finish. " +
                 "1 = point-to-point/sprint race (finish line reached once). For a " +
                 "circuit course, set this higher and make sure the course's looped " +
                 "SimpleAIPath wraps back to waypoint 0 so AI keeps circulating.")]
        [Min(1)] public int laps = 1;

        public RaceOpponentPreset GetOpponentPreset(int opponentIndex)
        {
            if (opponentPresets == null || opponentPresets.Length == 0)
                return null;
            return opponentPresets[opponentIndex % opponentPresets.Length];
        }

        public bool IsUnlocked(int playerLevel) => playerLevel >= requiredLevel;

        public (int xp, int coins) GetReward(bool firstClear)
        {
            float mult = firstClear ? 1f : repeatRewardMultiplier;
            return (Mathf.RoundToInt(baseXPReward * mult), Mathf.RoundToInt(baseCoinReward * mult));
        }
    }
}
