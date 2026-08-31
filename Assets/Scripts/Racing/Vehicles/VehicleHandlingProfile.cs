using UnityEngine;
using MVC.Core;

namespace Racing.Vehicles
{
    /// Thin tuning layer over MVC's Vehicle. MVC exposes ~100+ raw handling fields
    /// spread across Stability/Steering/Suspension modules; most of the "feel" issues
    /// we hit (excess drift on turn-in, snap-spins at high speed) turned out to be a
    /// handful of those defaults, not physics bugs. This collects the ones that matter
    /// for gameplay feel into a small set of sliders and (re)applies them on demand,
    /// so tuning lives in one readable place instead of buried in MVC's inspector.
    ///
    /// Note: MVC's Stability/Steering properties return structs by value with a
    /// back-reference to the owning Vehicle -- assign the property to a local first,
    /// mutate the local, the setters write through to the real vehicle. Writing
    /// straight through the property chain (vehicle.Stability.X = y) won't compile.
    [RequireComponent(typeof(Vehicle))]
    public class VehicleHandlingProfile : MonoBehaviour
    {
        [Header("Drift / Rotation Control")]
        [Tooltip("How strongly the car resists spinning out (yaw) once the tires start " +
                 "to slip. 0 = raw physics, free to spin. 1 = heavily corrected, hard " +
                 "to spin out even under hard steering input.")]
        [Range(0f, 1f)] public float driftResistance = 0.4f;

        [Tooltip("Rear anti-sway bar stiffness (MVC's 'antiSwayRear'). Raising this " +
                 "toward the front value reduces the car's tendency to oversteer/spin.")]
        [Min(0f)] public float rearAntiSwayStiffness = 13000f;

        [Tooltip("Electronic Stability Program correction strength (MVC's 'ESPStrength'). " +
                 "Can go above 1 for extra correction on top of the angular helper above.")]
        [Min(0f)] public float espStrength = 1f;

        // 'ESPAllowDonuts' has no working runtime setter on MVC's StabilityModule (no
        // set_ESPAllowDonuts exists) -- writing to it through the struct silently
        // no-ops instead of reaching the real vehicle, so it's tuned via the MVC
        // Vehicle inspector directly (Stability foldout) rather than through here.

        [Header("High-Speed Steering")]
        [Tooltip("Smooths how fast the steering angle catches up to your input at " +
                 "speed, so a quick tap doesn't snap the car sideways. 0 = instant " +
                 "(raw input), higher = more lag before the wheels reach full angle.")]
        [Range(0f, 1f)] public float highSpeedSteeringSmoothing = 0.35f;

        [Tooltip("Speed above which steering angle is clamped down to its minimum " +
                 "(MVC's 'lowSteerAngleSpeed'). Lower this to taper off steering " +
                 "authority earlier at high speed, closer to how a real car behaves.")]
        [Min(0f)] public float steeringTaperSpeed = 130f;

        [Tooltip("Full-lock steering angle at low speed (MVC's 'maximumSteerAngle'). " +
                 "Lower this if even low-speed steering feels too twitchy.")]
        [Range(1f, 45f)] public float maximumSteerAngle = 20f;

        Vehicle vehicle;

        void Awake()
        {
            vehicle = GetComponent<Vehicle>();
            Apply();
        }

        void OnValidate()
        {
            if (vehicle == null)
                vehicle = GetComponent<Vehicle>();
            if (vehicle != null)
                Apply();
        }

        public void Apply()
        {
            var stability = vehicle.Stability;
            stability.ArcadeAngularSteerHelperIntensity = driftResistance;
            stability.AntiSwayRear = rearAntiSwayStiffness;
            stability.ESPStrength = espStrength;

            var steering = vehicle.Steering;
            steering.UseDynamicSteering = highSpeedSteeringSmoothing > 0f;
            steering.DynamicSteeringIntensity = highSpeedSteeringSmoothing;
            steering.LowSteerAngleSpeed = steeringTaperSpeed;
            steering.MaximumSteerAngle = maximumSteerAngle;
        }
    }
}
