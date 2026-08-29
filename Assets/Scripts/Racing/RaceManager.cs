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
            public SimpleAIPathFollower aiFollower;
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
                }
                participants.Add(new RaceParticipant { vehicle = player, isPlayer = true });
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

                participants.Add(new RaceParticipant { vehicle = vehicle, aiFollower = follower, isPlayer = false });
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
            countdownRemaining -= Time.deltaTime;
            RaceEvents.RaiseCountdownTick(Mathf.Max(0f, countdownRemaining));
            if (countdownRemaining <= 0f)
            {
                State = RaceState.Racing;
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
                    Destroy(p.vehicle.gameObject);
            }
            participants.Clear();
            activeCourse = null;
            State = RaceState.Idle;
        }
    }
}
