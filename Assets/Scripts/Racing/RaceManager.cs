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

        /// Non-null from BeginRace() until ResetToIdle() -- used by MinimapController
        /// to point a beacon at RaceCourse.finishLine.
        public RaceCourse ActiveCourse => activeCourse;

        RaceCourse activeCourse;
        RaceCourse lastCourse;
        readonly List<RaceParticipant> participants = new List<RaceParticipant>();
        /// Deactivated-but-alive opponent instances, keyed by source prefab, reused
        /// across races instead of Destroy()+Instantiate() every time -- a full car
        /// (mesh, materials, physics, textures) is expensive to tear down and rebuild,
        /// especially with several opponents per race.
        readonly Dictionary<GameObject, Queue<GameObject>> opponentPool = new Dictionary<GameObject, Queue<GameObject>>();
        float countdownRemaining;
        float raceStartTime;

        class RaceParticipant
        {
            public Vehicle vehicle;
            public Rigidbody rigidbody;
            public SimpleAIPathFollower aiFollower;
            public GameObject sourcePrefab;
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
            /// World-space distance actually driven since the last time this
            /// participant left the finish zone -- reset on every zone entry (counted
            /// or not). Required to clear a threshold before a false->true zone
            /// re-entry counts as a completed lap. On a circuit the grid sits close
            /// enough to double as the finish line (a handful of units away, well
            /// inside finishLineRadius) that ordinary physics settling right as the
            /// countdown ends, or a stuck/idling car merely jittering near that same
            /// spot, can flicker in and out of the zone on its own -- without this
            /// guard that reads as a free lap for a car that never actually drove one.
            /// Plain waypoint-index progress isn't reliable enough for this on a small
            /// loop (closest-waypoint can stay pinned near one corner depending on the
            /// exact line taken), so this tracks actual driven distance instead.
            public float distanceSinceZoneExit;
            public Vector3 lastTrackedPosition;
        }

        /// Moves both the Transform AND the Rigidbody to the given pose, then forces an
        /// immediate physics sync. Setting transform.position alone is NOT enough for a
        /// Rigidbody with interpolation enabled (the norm for a smooth-looking vehicle):
        /// Unity renders interpolated bodies from the physics engine's own buffered
        /// position, not straight off the Transform, so the visible car can keep
        /// showing its old spot -- exactly where it was sitting on the trigger -- even
        /// though transform.position already reads the correct grid slot. Writing
        /// through Rigidbody.position/rotation too, plus Physics.SyncTransforms(),
        /// updates that buffer immediately instead of waiting on/hoping for the next
        /// FixedUpdate to catch up.
        static void TeleportVehicle(Transform t, Rigidbody rb, Vector3 position, Quaternion rotation)
        {
            t.SetPositionAndRotation(position, rotation);
            if (rb != null)
            {
                rb.position = position;
                rb.rotation = rotation;
            }
            Physics.SyncTransforms();
        }

        GameObject RentOpponent(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (opponentPool.TryGetValue(prefab, out var queue))
            {
                while (queue.Count > 0)
                {
                    var pooled = queue.Dequeue();
                    if (pooled == null) continue; // destroyed some other way -- skip, don't return it
                    var prb = pooled.GetComponent<Rigidbody>();
                    TeleportVehicle(pooled.transform, prb, position, rotation);
                    pooled.SetActive(true);
                    if (prb != null)
                    {
                        prb.linearVelocity = Vector3.zero;
                        prb.angularVelocity = Vector3.zero;
                    }
                    return pooled;
                }
            }
            return Instantiate(prefab, position, rotation);
        }

        void ReturnOpponent(GameObject prefab, GameObject instance)
        {
            if (prefab == null || instance == null) return;
            instance.SetActive(false);
            if (!opponentPool.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                opponentPool[prefab] = queue;
            }
            queue.Enqueue(instance);
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
                    ReturnOpponent(p.sourcePrefab, p.vehicle.gameObject);
            }
            participants.Clear();

            var vm = FindFirstObjectByType<VehicleManager>();
            var player = vm != null ? vm.PlayerVehicle : null;
            var slots = activeCourse.gridSlots;

            if (player == null || slots == null || slots.Length == 0 || slots[0] == null)
            {
                Debug.LogWarning($"[RaceManager] Player NOT repositioned for '{activeCourse.name}' -- " +
                    $"vm={(vm != null)} player={(player != null)} slots={(slots != null ? slots.Length.ToString() : "null")} " +
                    $"slot0={(slots != null && slots.Length > 0 ? (slots[0] != null ? "ok" : "null") : "n/a")}");
            }

            if (player != null && slots != null && slots.Length > 0 && slots[0] != null)
            {
                var rb = player.GetComponent<Rigidbody>();
                TeleportVehicle(player.transform, rb, slots[0].position, slots[0].rotation);
                Debug.Log($"[RaceManager] Player repositioned to grid slot 0 '{slots[0].name}' @ {slots[0].position} (actual transform now @ {player.transform.position})");
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
                    inFinishZone = IsInFinishZone(slots[0].position),
                    lastTrackedPosition = slots[0].position
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

                var go = RentOpponent(preset.vehiclePrefab, slot.position, slot.rotation);
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
                    sourcePrefab = preset.vehiclePrefab,
                    isPlayer = false,
                    gridPosition = slot.position,
                    gridRotation = slot.rotation,
                    inFinishZone = IsInFinishZone(slot.position),
                    lastTrackedPosition = slot.position
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
                TeleportVehicle(p.vehicle.transform, p.rigidbody, p.gridPosition, p.gridRotation);
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
                var playerAtStart = participants.FirstOrDefault(p => p.isPlayer);
                if (playerAtStart != null)
                    Debug.Log($"[RaceManager] Race starting -- player @ {playerAtStart.vehicle.transform.position} (grid was {playerAtStart.gridPosition})");
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

                Vector3 pos = p.vehicle.transform.position;
                p.distanceSinceZoneExit += Vector3.Distance(pos, p.lastTrackedPosition);
                p.lastTrackedPosition = pos;
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
            // Require at least 30% of one lap's length actually driven since the last
            // zone visit before a re-entry counts -- comfortably above anything a
            // stationary/jittering car could rack up (sub-meter noise vs. tens of
            // meters), while still tolerant of a car cutting corners tighter than the
            // nominal racing line.
            float minLapDistance = activeCourse.mainPath != null ? activeCourse.mainPath.TotalLength * 0.3f : 0f;

            foreach (var p in participants)
            {
                if (p.finished || p.vehicle == null) continue;

                bool inZone = IsInFinishZone(p.vehicle.transform.position);
                if (inZone && !p.inFinishZone)
                {
                    bool realLap = p.distanceSinceZoneExit >= minLapDistance;
                    p.distanceSinceZoneExit = 0f;
                    if (!realLap) { p.inFinishZone = inZone; continue; }

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
            GrantRewardIfEarned();
        }

        /// Only the player's own finish counts -- a DNF (race ended because every OTHER
        /// participant finished/despawned while the player never crossed the line)
        /// earns nothing. First-ever clear of a given RaceDefinition pays full reward;
        /// every clear after that pays repeatRewardMultiplier of it (see
        /// RaceDefinition.GetReward). Needs both PlayerProgress and PlayerCurrency in
        /// the scene -- silently skips if either is missing rather than throwing, same
        /// as every other Instance-optional lookup in this codebase. Quick Race/
        /// Multiplayer (GameMode.BypassesLevelGate) never earns anything -- it's meant
        /// to be a no-stakes "just race" mode, isolated from Career progression.
        void GrantRewardIfEarned()
        {
            if (GameMode.BypassesLevelGate) return;

            var def = activeCourse != null ? activeCourse.definition : null;
            var playerParticipant = participants.FirstOrDefault(p => p.isPlayer);
            if (def == null || playerParticipant == null || !playerParticipant.finished) return;

            bool firstClear = PlayerProgress.Instance == null || !PlayerProgress.Instance.HasCompleted(def);
            var (xp, coins) = def.GetReward(firstClear);

            int levelBefore = PlayerProgress.Instance != null ? PlayerProgress.Instance.Level : 1;
            PlayerProgress.Instance?.GainXP(xp);
            PlayerCurrency.Instance?.Earn(coins);
            PlayerProgress.Instance?.MarkCompleted(def);
            int levelAfter = PlayerProgress.Instance != null ? PlayerProgress.Instance.Level : levelBefore;

            RaceEvents.RaiseRewardGranted(new RaceReward
            {
                xpGained = xp,
                coinsGained = coins,
                firstClear = firstClear,
                leveledUp = levelAfter > levelBefore,
                newLevel = levelAfter
            });
        }

        public void ResetToIdle()
        {
            foreach (var p in participants)
            {
                if (!p.isPlayer && p.vehicle != null)
                {
                    ReturnOpponent(p.sourcePrefab, p.vehicle.gameObject);
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
            RaceEvents.RaiseRaceReset();
        }
    }
}
