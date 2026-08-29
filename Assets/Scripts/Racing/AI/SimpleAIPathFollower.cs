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
                 "hold AI opponents at the grid during the race countdown. Defaults to " +
                 "true so a follower placed standalone (outside RaceManager) just works.")]
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

        [Header("Propulsion (direct Rigidbody drive)")]
        [Tooltip("Forward acceleration applied at full throttle, in m/s^2.")]
        public float driveAcceleration = 8f;
        [Tooltip("Deceleration applied when throttle is cut, in m/s^2 (on top of drag).")]
        public float coastDeceleration = 4f;
        public float maxSpeed = 30f;

        Vehicle vehicle;
        Rigidbody rb;
        int currentIndex;
        bool started;

        void Awake()
        {
            vehicle = GetComponent<Vehicle>();
            rb = GetComponent<Rigidbody>();
        }

        void OnEnable()
        {
            started = false;
        }

        void FixedUpdate()
        {
            if (!canDrive || path == null || !path.IsValid || rb == null) return;

            if (!started)
            {
                currentIndex = path.ClosestPointIndex(transform.position);
                started = true;
            }

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

            float signedAngle = Vector3.SignedAngle(transform.forward, toTarget.normalized, Vector3.up);
            float desiredYawRate = Mathf.Clamp(signedAngle * turnRateDamping, -maxTurnRateDegPerSec, maxTurnRateDegPerSec);

            Vector3 angularVelocity = rb.angularVelocity;
            angularVelocity.y = desiredYawRate * Mathf.Deg2Rad;
            rb.angularVelocity = angularVelocity;

            float turnSeverity = Mathf.Clamp01(Mathf.Abs(signedAngle) / Mathf.Max(1f, slowForTurnAngle));
            float throttle = Mathf.Clamp01(targetSpeedMultiplier * (1f - 0.6f * turnSeverity));

            float currentForwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

            if (trafficPolicy == TrafficPolicy.Traffic && IsVehicleAhead(out float gapDistance))
            {
                float requiredGap = followMinimumGap + Mathf.Max(0f, currentForwardSpeed) * followTimeGap;
                if (gapDistance < requiredGap)
                    throttle = 0f;
            }

            float targetSpeed = maxSpeed * targetSpeedMultiplier * (1f - 0.5f * turnSeverity);
            if (throttle > 0f && currentForwardSpeed < targetSpeed)
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

        bool IsVehicleAhead(out float distance)
        {
            distance = float.MaxValue;
            if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out var hit,
                followMinimumGap + 20f, obstacleLayerMask))
            {
                if (hit.collider.GetComponentInParent<Vehicle>() != null)
                {
                    distance = hit.distance;
                    return true;
                }
            }
            return false;
        }
    }
}
