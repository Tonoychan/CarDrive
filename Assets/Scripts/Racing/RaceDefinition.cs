using UnityEngine;

namespace Racing
{
    [CreateAssetMenu(fileName = "RaceDefinition", menuName = "Racing/Race Definition")]
    public class RaceDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string raceName = "New Race";
        [TextArea] public string description;

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
    }
}
