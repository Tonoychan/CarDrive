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

        public RaceOpponentPreset GetOpponentPreset(int opponentIndex)
        {
            if (opponentPresets == null || opponentPresets.Length == 0)
                return null;
            return opponentPresets[opponentIndex % opponentPresets.Length];
        }
    }
}
