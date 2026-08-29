using System.Collections.Generic;
using UnityEngine;
using MVC.AI;

namespace Racing
{
    [DisallowMultipleComponent]
    public class RaceCourse : MonoBehaviour
    {
        [Header("Definition")]
        public RaceDefinition definition;

        [Header("Grid")]
        [Tooltip("Slot 0 is always the player's start position.")]
        public Transform[] gridSlots;
        public Transform finishLine;
        [Min(0.1f)] public float finishLineRadius = 6f;

        [Header("Paths")]
        [Tooltip("Main racing line, used for progress/position tracking.")]
        public VehicleAIPath mainPath;
        [Tooltip("Optional alternate lines so AI can pass instead of forming a single-file line.")]
        public VehicleAIPath[] alternatePaths;

        [Header("Wrong-way detection")]
        [Range(-1f, 1f)] public float wrongWayDotThreshold = -0.3f;

        public IReadOnlyList<VehicleAIPath> AllPaths
        {
            get
            {
                var list = new List<VehicleAIPath>();
                if (mainPath != null) list.Add(mainPath);
                if (alternatePaths != null) list.AddRange(alternatePaths);
                return list;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            if (gridSlots != null)
            {
                foreach (var slot in gridSlots)
                {
                    if (slot == null) continue;
                    Gizmos.DrawWireSphere(slot.position, 1f);
                    Gizmos.DrawLine(slot.position, slot.position + slot.forward * 2f);
                }
            }

            Gizmos.color = Color.yellow;
            if (finishLine != null)
                Gizmos.DrawWireSphere(finishLine.position, finishLineRadius);
        }
    }
}
