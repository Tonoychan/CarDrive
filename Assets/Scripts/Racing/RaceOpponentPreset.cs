using UnityEngine;
using Racing.AI;

namespace Racing
{
    [CreateAssetMenu(fileName = "RaceOpponentPreset", menuName = "Racing/Opponent Preset")]
    public class RaceOpponentPreset : ScriptableObject
    {
        [Header("Vehicle")]
        public GameObject vehiclePrefab;
        public string displayName = "Opponent";

        [Header("Engine performance override")]
        [Tooltip("If false, the vehicle prefab's own engine values are used as-is. Note: " +
                 "AI opponents currently drive via SimpleAIPathFollower (Rigidbody physics), " +
                 "so this only visibly affects the player's own car if reused there -- see " +
                 "PLANNING.md's MVC Pro note.")]
        public bool overrideEngine = false;
        public float power = 300f;
        public float torque = 350f;
        public float maximumRPM = 7000f;
        public float redlineRPM = 6500f;
        public float mass = 1400f;

        [Header("AI behaviour")]
        public SimpleAIPathFollower.TrafficPolicy trafficPolicy = SimpleAIPathFollower.TrafficPolicy.Racing;
        [Range(0.5f, 1.5f)] public float targetSpeedMultiplier = 1f;
        [Min(0f)] public float followTimeGap = 1.2f;
        [Min(0f)] public float followMinimumGap = 4f;
    }
}
