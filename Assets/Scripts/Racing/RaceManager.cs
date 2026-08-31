using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MVC;
using MVC.Core;
using Racing.AI;

namespace Racing
{
    public enum RaceState { Idle, Countdown, Racing, Finished }

    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        public RaceState State { get; private set; } = RaceState.Idle;

        RaceCourse activeCourse;
        readonly List<RaceParticipant> participants = new List<RaceParticipant>();
        float countdownRemaining;

        class RaceParticipant
        {
            public Vehicle vehicle;
            public Rigidbody rigidbody;
            public SimpleAIPathFollower aiFollower;
            public Vector3 gridPosition;
            public Quaternion gridRotation;
            public bool isPlayer;
            public bool finished;
            public float finishTime;
            public int progressIndex;
            public int position;
        }

        void Awake()
        {
            Instance = this;
        }

        public void BeginRace(RaceCourse course)
        {
            if (State != RaceState.Idle || course == null) return;

            activeCourse = course;
            SetupParticipants();
            countdownRemaining = course.definition != null ? course.definition.countdownSeconds : 3f;
            State = RaceState.Countdown;
            RaceEvents.RaiseCountdownStarted(countdownRemaining);
        }

        void SetupParticipants()
        {
            foreach (var p in participants)
            {
                if (!p.isPlayer && p.vehicle != null)
                    Destroy(p.vehicle.gameObject);
            }
            participants.Clear();

            var vm = FindFirstObjectByType<VehicleManager>();
            var player = vm != null ? vm.PlayerVehicle : null;
            var slots = activeCourse.gridSlots;

            if (player != null && slots != null && slots.Length > 0 && slots[0] != null)
            {
                player.transform.SetPositionAndRotation(slots[0].position, slots[0].rotation);
                var rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }
                // Deliberately NOT disabling the player's Vehicle component here: the
                // follow camera's own update appears gated on Vehicle.enabled, so
                // disabling it froze the camera at whatever it was looking at when the
                // countdown started (e.g. the trigger popup view) for the whole
                // countdown. The kinematic Rigidbody plus the per-frame grid-pose snap
                // in TickCountdown() below are what actually keep the car from moving;
                // Vehicle staying enabled just lets the camera keep tracking it.
                var camera = FindFirstObjectByType<VehicleFollower>();
                if (camera != null) camera.Restart();
                participants.Add(new RaceParticipant
                {
                    vehicle = player,
                    rigidbody = rb,
                    isPlayer = true,
                    gridPosition = slots[0].position,
                    gridRotation = slots[0].rotation
                });
            }

            var def = activeCourse.definition;
            int opponentCount = def != null ? def.opponentCount : 0;
            for (int i = 0; i < opponentCount; i++)
            {
                var preset = def.GetOpponentPreset(i);
                if (preset == null || preset.vehiclePrefab == null) continue;

                int slotIndex = i + 1;
                Transform slot = (slots != null && slotIndex < slots.Length && slots[slotIndex] != null)
                    ? slots[slotIndex]
                    : activeCourse.transform;

                var go = Instantiate(preset.vehiclePrefab, slot.position, slot.rotation);
                var vehicle = go.GetComponent<Vehicle>();
                if (vehicle == null)
                {
                    Destroy(go);
                    continue;
                }

                ApplyEnginePreset(vehicle, preset);

                var follower = go.GetComponent<SimpleAIPathFollower>();
                if (follower == null) follower = go.AddComponent<SimpleAIPathFollower>();
                follower.path = ChoosePathForOpponent(i);
                follower.trafficPolicy = preset.trafficPolicy;
                follower.targetSpeedMultiplier = preset.targetSpeedMultiplier;
                follower.followTimeGap = preset.followTimeGap;
                follower.followMinimumGap = preset.followMinimumGap;
                follower.canDrive = false;

                var aiRb = go.GetComponent<Rigidbody>();
                if (aiRb != null) aiRb.isKinematic = true;
                vehicle.enabled = false;

                participants.Add(new RaceParticipant
                {
                    vehicle = vehicle,
                    rigidbody = aiRb,
                    aiFollower = follower,
                    isPlayer = false,
                    gridPosition = slot.position,
                    gridRotation = slot.rotation
                });
            }
        }

        SimpleAIPath ChoosePathForOpponent(int opponentIndex)
        {
            var all = activeCourse.AllPaths;
            if (all.Count == 0) return null;
            return all[opponentIndex % all.Count];
        }

        static void ApplyEnginePreset(Vehicle vehicle, RaceOpponentPreset preset)
        {
            if (!preset.overrideEngine) return;
            var engine = vehicle.Engine;
            if (engine == null) return;
            engine.Power = preset.power;
            engine.Torque = preset.torque;
            engine.MaximumRPM = preset.maximumRPM;
            engine.RedlineRPM = preset.redlineRPM;
            engine.Mass = preset.mass;
        }

        void Update()
        {
            switch (State)
            {
                case RaceState.Countdown:
                    TickCountdown();
                    break;
                case RaceState.Racing:
                    UpdatePositions();
                    CheckFinishes();
                    break;
            }
        }

        void TickCountdown()
        {
            // Belt-and-suspenders hold: force every car back to its grid pose every
            // frame, regardless of what might be nudging it (kinematic Rigidbody +
            // disabled Vehicle component should already prevent movement, but this
            // guarantees it even if something else writes to the transform directly).
            foreach (var p in participants)
            {
                if (p.vehicle == null) continue;
                p.vehicle.transform.SetPositionAndRotation(p.gridPosition, p.gridRotation);
                // Kinematic rigidbodies ignore velocity entirely -- writing to it just
                // logs "not supported" warnings every frame, for every car, for the
                // whole countdown.
                if (p.rigidbody != null && !p.rigidbody.isKinematic)
                {
                    p.rigidbody.linearVelocity = Vector3.zero;
                    p.rigidbody.angularVelocity = Vector3.zero;
                }
            }

            countdownRemaining -= Time.deltaTime;
            RaceEvents.RaiseCountdownTick(Mathf.Max(0f, countdownRemaining));
            if (countdownRemaining <= 0f)
            {
                State = RaceState.Racing;
                foreach (var p in participants)
                {
                    if (p.vehicle != null) p.vehicle.enabled = true;
                    if (p.rigidbody != null) p.rigidbody.isKinematic = false;
                    if (p.aiFollower != null) p.aiFollower.canDrive = true;
                }
                RaceEvents.RaiseRaceStarted();
            }
        }

        void UpdatePositions()
        {
            var path = activeCourse.mainPath;
            if (path == null || !path.IsValid) return;

            foreach (var p in participants)
            {
                if (p.finished || p.vehicle == null) continue;
                p.progressIndex = path.ClosestPointIndex(p.vehicle.transform.position);
            }

            var ordered = participants
                .Where(p => p.vehicle != null)
                .OrderByDescending(p => p.finished ? int.MaxValue : p.progressIndex)
                .ThenBy(p => p.finished ? p.finishTime : 0f)
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
                ordered[i].position = i + 1;

            var playerParticipant = participants.FirstOrDefault(p => p.isPlayer);
            if (playerParticipant != null)
                RaceEvents.RaisePositionChanged(playerParticipant.position, ordered.Count);
        }

        void CheckFinishes()
        {
            if (activeCourse.finishLine == null) return;

            float radiusSqr = activeCourse.finishLineRadius * activeCourse.finishLineRadius;
            foreach (var p in participants)
            {
                if (p.finished || p.vehicle == null) continue;

                float distSqr = (p.vehicle.transform.position - activeCourse.finishLine.position).sqrMagnitude;
                if (distSqr > radiusSqr) continue;

                p.finished = true;
                p.finishTime = Time.time;
                if (p.isPlayer) RaceEvents.RaisePlayerFinished(p.position);
            }

            if (participants.Count > 0 && participants.All(p => p.vehicle == null || p.finished))
                FinishRace();
        }

        void FinishRace()
        {
            State = RaceState.Finished;
            var results = participants
                .Where(p => p.vehicle != null)
                .OrderBy(p => p.position)
                .Select(p => new RaceResult
                {
                    participantName = p.isPlayer ? "Player" : p.vehicle.name,
                    position = p.position,
                    isPlayer = p.isPlayer
                })
                .ToArray();
            RaceEvents.RaiseRaceFinished(results);
        }

        public void ResetToIdle()
        {
            foreach (var p in participants)
            {
                if (!p.isPlayer && p.vehicle != null)
                {
                    Destroy(p.vehicle.gameObject);
                }
                else if (p.isPlayer && p.vehicle != null)
                {
                    p.vehicle.enabled = true;
                    if (p.rigidbody != null) p.rigidbody.isKinematic = false;
                }
            }
            participants.Clear();
            activeCourse = null;
            State = RaceState.Idle;
        }
    }
}
