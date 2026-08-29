using System.Collections.Generic;
using UnityEngine;

namespace Racing.AI
{
    /// Community-tier stand-in for MVC's (Pro-only) VehicleAIPath. Field/method names
    /// deliberately mirror VehicleAIPath's public surface (TotalLength, IsValid,
    /// ClosestPointIndex, loopedPath) so this is a drop-in-shaped replacement if the
    /// project ever upgrades to MVC Pro and switches back to the real component.
    [DisallowMultipleComponent]
    public class SimpleAIPath : MonoBehaviour
    {
        [Tooltip("Ordered waypoints. Populate via child Transforms using the editor tool, " +
                 "or assign directly.")]
        public Transform[] waypoints;

        public bool loopedPath = false;

        [Tooltip("Gizmo/debug path width only -- does not affect steering.")]
        public float drawWidth = 6f;

        public int PointCount => waypoints?.Length ?? 0;

        public bool IsValid => waypoints != null && waypoints.Length >= 2;

        public float TotalLength
        {
            get
            {
                if (!IsValid) return 0f;
                float length = 0f;
                for (int i = 0; i < waypoints.Length - 1; i++)
                    length += Vector3.Distance(waypoints[i].position, waypoints[i + 1].position);
                if (loopedPath)
                    length += Vector3.Distance(waypoints[waypoints.Length - 1].position, waypoints[0].position);
                return length;
            }
        }

        public Vector3 GetPoint(int index)
        {
            if (!IsValid) return transform.position;
            index = WrapIndex(index);
            return waypoints[index].position;
        }

        public int NextIndex(int index)
        {
            if (!IsValid) return 0;
            int next = index + 1;
            if (next >= waypoints.Length)
                return loopedPath ? 0 : waypoints.Length - 1;
            return next;
        }

        int WrapIndex(int index)
        {
            int count = waypoints.Length;
            if (loopedPath) return ((index % count) + count) % count;
            return Mathf.Clamp(index, 0, count - 1);
        }

        /// Nearest waypoint index to a world position. Used to pick a sane starting
        /// target for a follower and, combined with NextIndex, gives a rough
        /// progress-along-path measure for race position ordering.
        public int ClosestPointIndex(Vector3 worldPosition)
        {
            if (!IsValid) return 0;
            int best = 0;
            float bestDistSqr = float.MaxValue;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                float d = (waypoints[i].position - worldPosition).sqrMagnitude;
                if (d < bestDistSqr) { bestDistSqr = d; best = i; }
            }
            return best;
        }

        void OnDrawGizmosSelected()
        {
            if (waypoints == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawWireSphere(waypoints[i].position, 1f);
                int next = i + 1;
                if (next < waypoints.Length && waypoints[next] != null)
                    Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
            }
            if (loopedPath && waypoints.Length > 1 && waypoints[0] != null && waypoints[waypoints.Length - 1] != null)
                Gizmos.DrawLine(waypoints[waypoints.Length - 1].position, waypoints[0].position);
        }
    }
}
