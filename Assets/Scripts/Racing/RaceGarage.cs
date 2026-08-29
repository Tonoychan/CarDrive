using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MVC.Core;

namespace Racing
{
    [CreateAssetMenu(fileName = "RacePart", menuName = "Racing/Part")]
    public class RacePart : ScriptableObject
    {
        public string partId = "part_id";
        public string displayName = "New Part";
        [TextArea] public string description;
        public int cost = 100;

        [Header("Additive engine stat deltas")]
        public float powerDelta = 0f;
        public float torqueDelta = 0f;
        public float maximumRpmDelta = 0f;
        public float massDelta = 0f;
    }

    /// Placeholder garage: tracks owned parts via PlayerPrefs and applies their
    /// cumulative stat deltas onto the player vehicle's engine. No economy/spending
    /// logic yet -- Purchase() just unlocks a part unconditionally.
    public class RaceGarageController : MonoBehaviour
    {
        const string OwnedPartsKey = "RaceGarage.OwnedParts";
        const char Separator = ';';

        public RacePart[] availableParts;

        readonly HashSet<string> owned = new HashSet<string>();

        void Awake()
        {
            LoadOwnedParts();
        }

        void LoadOwnedParts()
        {
            owned.Clear();
            string raw = PlayerPrefs.GetString(OwnedPartsKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return;

            foreach (var id in raw.Split(Separator))
            {
                if (!string.IsNullOrEmpty(id)) owned.Add(id);
            }
        }

        void SaveOwnedParts()
        {
            PlayerPrefs.SetString(OwnedPartsKey, string.Join(Separator.ToString(), owned));
            PlayerPrefs.Save();
        }

        public bool IsOwned(RacePart part) => part != null && owned.Contains(part.partId);

        public void Purchase(RacePart part)
        {
            if (part == null || IsOwned(part)) return;
            owned.Add(part.partId);
            SaveOwnedParts();
        }

        public void ApplyOwnedPartsToVehicle(Vehicle vehicle)
        {
            if (vehicle == null || vehicle.Engine == null) return;

            var engine = vehicle.Engine;
            float power = engine.Power;
            float torque = engine.Torque;
            float maxRpm = engine.MaximumRPM;
            float mass = engine.Mass;

            foreach (var part in availableParts.Where(p => p != null && owned.Contains(p.partId)))
            {
                power += part.powerDelta;
                torque += part.torqueDelta;
                maxRpm += part.maximumRpmDelta;
                mass += part.massDelta;
            }

            engine.Power = power;
            engine.Torque = torque;
            engine.MaximumRPM = maxRpm;
            engine.Mass = mass;
        }
    }
}
