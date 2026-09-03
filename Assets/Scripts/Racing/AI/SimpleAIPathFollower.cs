using UnityEngine;
using MVC.Core;

namespace Racing.AI
{
    /// Community-tier stand-in for MVC's (Pro-only) VehicleAIPathFollower. Drives the
    /// car entirely through the Rigidbody (force for propulsion, damped yaw rate for
    /// steering) rather than through MVC's Vehicle/wheel simulation: Vehicle.Inputs'
    /// Fuel/Brake/Direction/etc. setters exist but are non-public (confirmed via
    /// reflection -- MVC.Core.Vehicle+InputsAccess only exposes public getters), so
    /// they cannot be driven from outside code without MVC Pro's internal AI access.
    /// Field names mirror VehicleAIPathFollower (path, followDirection,
    /// targetSpeedMultiplier, trafficPolicy, followTimeGap, followMinimumGap) so this
    /// reads the same way and is a conceptually similar drop-in if the project
    /// upgrades to MVC Pro later. Trade-off: the AI car does not get MVC's engine
    /// sound/RPM/torque-curve simulation -- it is a straightforward arcade physics
    /// follower.
    [RequireComponent(typeof(Rigidbody))]
    public class SimpleAIPathFollower : MonoBehaviour
    {
        public enum FollowDirection { Default, Inverse }
        public enum TrafficPolicy { Racing, Traffic }

        [Header("Path")]
        public SimpleAIPath path;
        public FollowDirection followDirection = FollowDirection.Default;

        [Tooltip("While false, the car sits idle (no steering or throttle) -- used to " +
                 "hold AI opponents at the grid during the race countdown, and to park " +
                 "them once they've finished. A car with residual speed when this goes " +
                 "false brakes to a stop rather than just coasting on drag.")]
        public bool canDrive = true;

        [Header("Speed")]
        [Range(0.3f, 1.5f)] public float targetSpeedMultiplier = 1f;
        [Tooltip("Throttle is reduced proportionally to steering sharpness above this angle (degrees).")]
        public float slowForTurnAngle = 20f;

        [Header("Steering")]
        [Tooltip("Max yaw turn rate in degrees/second.")]
        public float maxTurnRateDegPerSec = 90f;
        [Tooltip("How quickly the yaw rate approaches its target -- higher is snappier, lower is smoother.")]
        public float turnRateDamping = 4f;
        public float waypointReachDistance = 6f;

        [Header("Traffic (simplified)")]
        public TrafficPolicy trafficPolicy = TrafficPolicy.Racing;
        [Min(0f)] public float followTimeGap = 1.2f;
        [Min(0f)] public float followMinimumGap = 4f;
        public LayerMask obstacleLayerMask = ~0;

        [Header("Collision Avoidance / Overtaking")]
        [Tooltip("How far ahead (m) to scan for another vehicle via a forward sphere-cast.")]
        public float obstacleScanDistance = 30f;
        [Tooltip("Radius of the forward obstacle scan -- wider than a thin raycast so a car " +
                 "slightly off-center (e.g. mid-overtake) still gets detected before impact.")]
        public float obstacleScanRadius = 1.4f;
        [Tooltip("Active braking deceleration (m/s^2) applied when a car ahead is too close " +
                 "to safely pass -- stronger than coastDeceleration so the AI actually slows " +
                 "down for traffic instead of gently rolling off throttle.")]
        public float brakeDeceleration = 14f;
        [Tooltip("Only Racing-policy cars attempt this: sideways aim offset (m) added to the " +
                 "steering target when trying to pass a slower car ahead with room on one side.")]
        public float overtakeLateralOffset = 4f;
        [Tooltip("How far ahead to probe left/right for clearance before committing to an overtake.")]
        public float overtakeProbeDistance = 10f;
        [Tooltip("Sideways offset (m) from center used when probing left/right for overtake clearance.")]
        public float overtakeProbeWidth = 2.2f;

        [Header("Propulsion (direct Rigidbody drive)")]
        [Tooltip("Forward acceleration applied at full throttle, in m/s^2.")]
        public float driveAcceleration = 8f;
        [Tooltip("Deceleration applied when throttle is cut, in m/s^2 (on top of drag).")]
        public float coastDeceleration = 4f;
        public float maxSpeed = 30f;

        [Header("Stuck / Flip Recovery")]
        [Tooltip("Forward speed (m/s) below which the car counts as 'not really moving' while it should be driving.")]
        public float stuckSpeedThreshold = 1.5f;
        [Tooltip("How long (s) the car can sit stuck or flipped before this script forcibly rights " +
                 "and repositions it. MVC has its own automatic flip-reset, but for a car driven " +
                 "externally like this one that reset can leave the Rigidbody kinematic (or otherwise " +
                 "stalled) with nothing left to push it again -- this is a backstop, not a replacement.")]
        public float stuckRecoveryTime = 2.5f;
        [Tooltip("Upright-ness (transform.up . Vector3.up) below which the car counts as rolled/flipped.")]
        [Range(-1f, 1f)] public float flippedUpDot = 0.4f;

        Vehicle vehicle;
        Rigidbody rb;
        int currentIndex;
        bool started;
        float stuckTimer;

        void Awake()
        {
            vehicle = GetComponent<Vehicle>();
            rb = GetComponent<Rigidbody>();
        }

        void OnEnable()
        {
            started = false;
            stuckTimer = 0f;
        }

        void FixedUpdate()
        {
            if (path == null || !path.IsValid || rb == null) return;

            // Guard against MVC's own automatic flip-reset leaving the Rigidbody
            // kinematic once it repositions the car -- that reset flow assumes a
            // player-input-driven vehicle, and without it this follower's AddForce
            // calls would silently no-op forever.
            if (canDrive && rb.isKinematic)
                rb.isKinematic = false;

            if (!started)
            {
                currentIndex = path.ClosestPointIndex(transform.position);
                started = true;
            }

            float currentForwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

            if (!canDrive)
            {
                stuckTimer = 0f;
                if (!rb.isKinematic && currentForwardSpeed > 0.1f)
                    rb.AddForce(-transform.forward * brakeDeceleration, ForceMode.Acceleration);
                return;
            }

            if (TryRecoverIfStuck(currentForwardSpeed))
                return;

            Vector3 targetPoint = path.GetPoint(currentIndex);
            Vector3 toTarget = targetPoint - transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude <= waypointReachDistance)
            {
                currentIndex = followDirection == FollowDirection.Default
                    ? path.NextIndex(currentIndex)
                    : PreviousIndex(currentIndex);
                targetPoint = path.GetPoint(currentIndex);
                toTarget = targetPoint - transform.position;
                toTarget.y = 0f;
            }

            if (toTarget.sqrMagnitude < 0.0001f) return;

            bool blocked = false;
            if (IsVehicleAhead(out float gapDistance))
            {
                float requiredGap = followMinimumGap + Mathf.Max(0f, currentForwardSpeed) * followTimeGap;
                if (gapDistance < requiredGap)
                {
                    bool roomToOvertake = false;
                    float lateralOffset = 0f;
                    if (trafficPolicy == TrafficPolicy.Racing && gapDistance > followMinimumGap * 0.6f)
                        roomToOvertake = TryFindOvertakeOffset(out lateralOffset);

                    if (roomToOvertake)
                        toTarget += transform.right * lateralOffset;
                    else
                        blocked = true;
                }
            }

            float signedAngle = Vector3.SignedAngle(transform.forward, toTarget.normalized, Vector3.up);
            float desiredYawRate = Mathf.Clamp(signedAngle * turnRateDamping, -maxTurnRateDegPerSec, maxTurnRateDegPerSec);

            Vector3 angularVelocity = rb.angularVelocity;
            angularVelocity.y = desiredYawRate * Mathf.Deg2Rad;
            rb.angularVelocity = angularVelocity;

            float turnSeverity = Mathf.Clamp01(Mathf.Abs(signedAngle) / Mathf.Max(1f, slowForTurnAngle));
            float throttle = blocked ? 0f : Mathf.Clamp01(targetSpeedMultiplier * (1f - 0.6f * turnSeverity));

            float targetSpeed = maxSpeed * targetSpeedMultiplier * (1f - 0.5f * turnSeverity);
            if (blocked)
            {
                if (currentForwardSpeed > 0.1f)
                    rb.AddForce(-transform.forward * brakeDeceleration, ForceMode.Acceleration);
            }
            else if (throttle > 0f && currentForwardSpeed < targetSpeed)
            {
                rb.AddForce(transform.forward * driveAcceleration * throttle, ForceMode.Acceleration);
            }
            else if (currentForwardSpeed > 0.1f)
            {
                rb.AddForce(-transform.forward * coastDeceleration, ForceMode.Acceleration);
            }
        }

        int PreviousIndex(int index)
        {
            if (!path.IsValid) return 0;
            int prev = index - 1;
            if (prev < 0)
                return path.loopedPath ? path.PointCount - 1 : 0;
            return prev;
        }

        // Shared scratch buffer for the NonAlloc casts below. Safe to share across every
        // AI car's instance because FixedUpdate calls are never concurrent in Unity.
        static readonly RaycastHit[] hitBuffer = new RaycastHit[8];

        /// The scan origin (transform.position + up) sits inside the car's own body
        /// collider, so a plain SphereCast's *first* hit is always itself -- which then
        /// gets filtered out by the self-exclusion check below, making the whole scan
        /// silently report "nothing ahead" no matter what's actually in front. Casting
        /// with SphereCastAll/NonAlloc and walking every hit (not just the first) is
        /// what actually lets this see past the car's own hull.
        bool IsVehicleAhead(out float distance)
        {
            distance = float.MaxValue;
            int count = Physics.SphereCastNonAlloc(transform.position + Vector3.up, obstacleScanRadius,
                transform.forward, hitBuffer, obstacleScanDistance, obstacleLayerMask);

            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var hit = hitBuffer[i];
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider.GetComponentInParent<Vehicle>() == null) continue;
                if (!found || hit.distance < distance)
                {
                    distance = hit.distance;
                    found = true;
                }
            }
            return found;
        }

        /// Probes short forward lanes to either side of the car for clearance. Picks
        /// whichever side is open (preferring the right, matching normal overtake-on-
        /// the-outside convention); returns false if both sides are blocked.
        bool TryFindOvertakeOffset(out float lateralOffset)
        {
            lateralOffset = 0f;
            Vector3 origin = transform.position + Vector3.up;
            float probeRadius = obstacleScanRadius * 0.6f;

            bool rightClear = !HasObstacleInLane(origin + transform.right * overtakeProbeWidth, probeRadius);
            bool leftClear = !HasObstacleInLane(origin - transform.right * overtakeProbeWidth, probeRadius);

            if (rightClear) { lateralOffset = overtakeLateralOffset; return true; }
            if (leftClear) { lateralOffset = -overtakeLateralOffset; return true; }
            return false;
        }

        bool HasObstacleInLane(Vector3 origin, float radius)
        {
            int count = Physics.SphereCastNonAlloc(origin, radius, transform.forward,
                hitBuffer, overtakeProbeDistance, obstacleLayerMask);
            for (int i = 0; i < count; i++)
            {
                if (!hitBuffer[i].collider.transform.IsChildOf(transform))
                    return true;
            }
            return false;
        }

        /// Tracks how long the car has been flipped or effectively stationary while it
        /// should be driving; past stuckRecoveryTime, forcibly rights and nudges it back
        /// onto the track rather than leaving it stalled indefinitely. Returns true if a
        /// recovery just happened (caller should skip normal driving this tick).
        bool TryRecoverIfStuck(float currentForwardSpeed)
        {
            bool flipped = Vector3.Dot(transform.up, Vector3.up) < flippedUpDot;
            bool crawling = Mathf.Abs(currentForwardSpeed) < stuckSpeedThreshold;

            stuckTimer = (flipped || crawling) ? stuckTimer + Time.fixedDeltaTime : 0f;
            if (stuckTimer < stuckRecoveryTime) return false;

            stuckTimer = 0f;
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.01f)
                flatForward = Vector3.ProjectOnPlane(path.GetPoint(currentIndex) - transform.position, Vector3.up);
            if (flatForward.sqrMagnitude < 0.01f)
                flatForward = Vector3.forward;

            transform.SetPositionAndRotation(
                transform.position + Vector3.up * 0.75f,
                Quaternion.LookRotation(flatForward.normalized, Vector3.up));

            return true;
        }
    }
}
