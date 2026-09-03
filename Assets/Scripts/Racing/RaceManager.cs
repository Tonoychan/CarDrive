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
        RaceCourse lastCourse;
        readonly List<RaceParticipant> participants = new List<RaceParticipant>();
        float countdownRemaining;
        float raceStartTime;

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
            public int lapsCompleted;
            /// Whether this participant was inside the finish-line radius as of the
            /// last check. A lap only counts on the false->true transition, so sitting
            /// in the zone (or starting the race there, on a circuit where grid ==
            /// start/finish) doesn't rack up free laps.
            public bool inFinishZone;
        }

        void Awake()
        {
            Instance = this;
        }

        public void BeginRace(RaceCourse course)
        {
            if (State != RaceState.Idle || course == null) return;

            activeCourse = course;
            lastCourse = course;
            SetupParticipants();
            countdownRemaining = course.definition != null ? course.definition.countdownSeconds : 3f;
            State = RaceState.Countdown;
            RaceEvents.RaiseCountdownStarted(countdownRemaining);

            string courseName = course.definition != null && !string.IsNullOrEmpty(course.definition.raceName)
                ? course.definition.raceName : course.name;
            int opponentCount = course.definition != null ? course.definition.opponentCount : 0;
            RaceEvents.RaiseRaceInfo(courseName, opponentCount);
        }

        /// Re-runs the race just finished/abandoned, same as walking up to the trigger
        /// again -- the results screen's "RACE AGAIN" button. Escape/"CLOSE" on the
        /// results screen uses ResetToIdle() instead and does NOT call this, so the two
        /// stay distinct: close-to-idle vs. actually restart.
        public void RaceAgain()
        {
            if (lastCourse == null) return;
            if (State != RaceState.Idle) ResetToIdle();
            BeginRace(lastCourse);
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
                    gridRotation = slots[0].rotation,
                    inFinishZone = IsInFinishZone(slots[0].position)
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
                    gridRotation = slot.rotation,
                    inFinishZone = IsInFinishZone(slot.position)
                });
            }
        }

        bool IsInFinishZone(Vector3 worldPosition)
        {
            if (activeCourse.finishLine == null) return false;
            float radiusSqr = activeCourse.finishLineRadius * activeCourse.finishLineRadius;
            return (worldPosition - activeCourse.finishLine.position).sqrMagnitude <= radiusSqr;
        }

        SimpleAIPath ChoosePathForOpponent(int opponentIndex)
        {
            var all = activeCourse.AllPaths;
            if (all.Count == 0) return null;
            return all[opponentIndex % all.Count];
        }

        // NOT applying preset.power/torque/etc to vehicle.Engine anymore -- confirmed
        // (2026-09-03) that Vehicle.Engine returns the SAME VehicleEngine instance for
        // every clone of a given prefab, not a per-instance copy. Writing to it here
        // permanently corrupted the source PREFAB ASSET (power/torque climbing without
        // bound across repeated races, eventually tripping MVC's own curve-mismatch
        // validator and disabling the vehicle -- including for the PLAYER's car, if an
        // opponent preset shares a prefab with the roster). Opponent stat variety needs
        // a redesign that doesn't write through this shared reference -- e.g. a
        // per-clone-safe override on our own AI follower, not MVC's Engine object.
        static void ApplyEnginePreset(Vehicle vehicle, RaceOpponentPreset preset) { }

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
                raceStartTime = Time.time;
                foreach (var p in participants)
                {
                    // Only the player's Vehicle component gets re-enabled -- AI cars are
                    // deliberately spawned with it disabled (see SetupParticipants) so
                    // MVC's own engine/stability/ABS simulation doesn't fight
                    // SimpleAIPathFollower for control of the same Rigidbody. Enabling it
                    // here for AI too was undoing that and fighting our AI driver for the
                    // whole race.
                    if (p.vehicle != null && p.isPlayer) p.vehicle.enabled = true;
                    if (p.rigidbody != null) p.rigidbody.isKinematic = false;
                    if (p.aiFollower != null) p.aiFollower.canDrive = true;
                }
                RaceEvents.RaiseRaceStarted();

                int totalLaps = activeCourse.definition != null ? Mathf.Max(1, activeCourse.definition.laps) : 1;
                RaceEvents.RaiseLapChanged(1, totalLaps);
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
                .OrderByDescending(p => p.finished)
                .ThenByDescending(p => p.lapsCompleted)
                .ThenByDescending(p => p.progressIndex)
                .ThenBy(p => p.finished ? p.finishTime : 0f)
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
                ordered[i].position = i + 1;

            var playerParticipant = participants.FirstOrDefault(p => p.isPlayer);
            if (playerParticipant != null)
            {
                RaceEvents.RaisePositionChanged(playerParticipant.position, ordered.Count);

                // Waypoint-index-based fraction -- same approximation progressIndex
                // already uses for position ordering above, just normalized to 0-1.
                int lastIndex = Mathf.Max(1, path.PointCount - 1);
                float fraction = Mathf.Clamp01((float)playerParticipant.progressIndex / lastIndex);
                float remainingMeters = path.TotalLength * (1f - fraction);
                RaceEvents.RaiseProgressChanged(remainingMeters, fraction);
            }
        }

        void CheckFinishes()
        {
            if (activeCourse.finishLine == null) return;

            int totalLaps = activeCourse.definition != null ? Mathf.Max(1, activeCourse.definition.laps) : 1;

            foreach (var p in participants)
            {
                if (p.finished || p.vehicle == null) continue;

                bool inZone = IsInFinishZone(p.vehicle.transform.position);
                if (inZone && !p.inFinishZone)
                {
                    p.lapsCompleted++;
                    if (p.lapsCompleted >= totalLaps)
                    {
                        p.finished = true;
                        p.finishTime = Time.time;
                        // AI cars have no reason to keep circulating once they've
                        // finished -- canDrive=false brakes them to a stop instead of
                        // looping the circuit forever on their last waypoint.
                        if (p.aiFollower != null) p.aiFollower.canDrive = false;
                        if (p.isPlayer) RaceEvents.RaisePlayerFinished(p.position);
                    }
                    else if (p.isPlayer)
                    {
                        RaceEvents.RaiseLapChanged(p.lapsCompleted + 1, totalLaps);
                    }
                }
                p.inFinishZone = inZone;
            }

            if (participants.Count > 0 && participants.All(p => p.vehicle == null || p.finished))
                FinishRace();
        }

        /// Instantiate() always appends "(Clone)"; AI variants carry a " - AI"/" - Drift"
        /// suffix that reads better as "· AI" in the results list.
        static string CleanVehicleName(string raw)
        {
            string s = raw.Replace("(Clone)", "").Trim();
            return s.Replace(" - AI", " · AI").Replace(" - Drift", " · Drift").Replace(" - Empty", "");
        }

        void FinishRace()
        {
            State = RaceState.Finished;
            var results = participants
                .Where(p => p.vehicle != null)
                .OrderBy(p => p.position)
                .Select(p => new RaceResult
                {
                    participantName = p.isPlayer ? "You" : CleanVehicleName(p.vehicle.name),
                    position = p.position,
                    isPlayer = p.isPlayer,
                    elapsedSeconds = p.finished ? p.finishTime - raceStartTime : Time.time - raceStartTime
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
