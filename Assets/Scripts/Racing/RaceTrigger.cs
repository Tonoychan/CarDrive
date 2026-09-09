using UnityEngine;
using Racing.UI;

namespace Racing
{
    /// Lives on its own GameObject (typically a child of the RaceCourse, e.g.
    /// "TriggerArea") and auto-wires to the nearest RaceCourse up the hierarchy via
    /// GetComponentInParent -- no manual dragging. This keeps the trigger volume free
    /// to be placed/sized independently of the course's root object while the whole
    /// race (course data, path, opponent presets, trigger) still ships as one
    /// self-contained prefab.
    public class RaceTrigger : WorldPromptTrigger
    {
        public RaceCourse Course { get; private set; }

        /// The trigger's world marker (e.g. "Area_star_ellow") -- isEnabled alone only
        /// gates interaction, so without this a locked race's marker stayed visible in
        /// the world and made every race look available regardless of player level.
        ParticleSystem marker;

        /// World-space name/type/reward card hovering over the trigger -- shares the
        /// marker's lock/mid-race visibility and additionally colors itself by
        /// first-clear status (see RaceInfoBillboard.SetCompleted).
        RaceInfoBillboard billboard;

        void Awake()
        {
            Course = GetComponentInParent<RaceCourse>();
            marker = GetComponentInChildren<ParticleSystem>(true);
            billboard = GetComponentInChildren<RaceInfoBillboard>(true);
        }

        void Start()
        {
            // Subscribing here rather than OnEnable(): Start() runs only after every
            // Awake() in the scene has completed, so PlayerProgress.Instance (set in
            // its own Awake, on a different GameObject) is guaranteed to exist by now.
            if (PlayerProgress.Instance != null) PlayerProgress.Instance.LevelChanged += OnLevelChanged;
            // Every trigger disables itself the moment ANY race starts (only one race
            // can run at a time) and re-checks once RaceManager.ResetToIdle() fires --
            // RaceAgain() goes straight from Finished back to Countdown without ever
            // touching Idle, so triggers correctly stay disabled through that too.
            RaceEvents.CountdownStarted += OnCountdownStarted;
            RaceEvents.RaceReset += OnRaceReset;
            // A completed race's own trigger is the one whose color needs to flip, but
            // RewardGranted doesn't say which RaceDefinition just got marked complete --
            // every trigger just re-checks its own, which is a cheap no-op for the ones
            // that didn't change.
            RaceEvents.RewardGranted += OnRewardGranted;

            billboard?.SetInfo(Course != null ? Course.definition : null);
            RefreshUnlockState();
        }

        void OnDestroy()
        {
            if (PlayerProgress.Instance != null) PlayerProgress.Instance.LevelChanged -= OnLevelChanged;
            RaceEvents.CountdownStarted -= OnCountdownStarted;
            RaceEvents.RaceReset -= OnRaceReset;
            RaceEvents.RewardGranted -= OnRewardGranted;
        }

        void OnLevelChanged(int newLevel) => RefreshUnlockState();
        void OnCountdownStarted(float seconds) => RefreshUnlockState();
        void OnRaceReset() => RefreshUnlockState();
        void OnRewardGranted(RaceReward reward) => RefreshUnlockState();

        /// isEnabled is WorldPromptTrigger's existing "master gate" field (see its own
        /// doc comment) -- this just drives it from the player's level and current race
        /// state instead of hand-flipping it in the Inspector. Unlocked-by-default
        /// (requiredLevel<=1) races stay interactable even before PlayerProgress exists
        /// in the scene. Quick Race/Multiplayer (GameMode.BypassesLevelGate) skip the
        /// level check entirely -- same career map/RaceDefinitions, just no level
        /// requirement -- but still respect the race-in-progress gate below, since only
        /// one race can run at a time regardless of mode.
        void RefreshUnlockState()
        {
            var def = Course != null ? Course.definition : null;
            if (def == null) return;

            bool unlocked = GameMode.BypassesLevelGate
                || def.IsUnlocked(PlayerProgress.Instance != null ? PlayerProgress.Instance.Level : 1);
            bool raceInProgress = RaceManager.Instance != null && RaceManager.Instance.State != RaceState.Idle;

            isEnabled = unlocked && !raceInProgress;
            SetMarkerVisible(isEnabled);

            bool completed = PlayerProgress.Instance != null && PlayerProgress.Instance.HasCompleted(def);
            billboard?.SetCompleted(completed);
        }

        void SetMarkerVisible(bool visible)
        {
            if (marker != null) marker.gameObject.SetActive(visible);
            if (billboard != null) billboard.gameObject.SetActive(visible);
        }

        public override string PromptLabel
        {
            get
            {
                var def = Course != null ? Course.definition : null;
                return def != null && !string.IsNullOrEmpty(def.raceName) ? def.raceName : promptLabel;
            }
        }

        public override string PromptDetails
        {
            get
            {
                var def = Course != null ? Course.definition : null;
                int opponents = def != null ? def.opponentCount : 0;
                float countdown = def != null ? def.countdownSeconds : 3f;
                string desc = def != null && !string.IsNullOrEmpty(def.description) ? def.description + "\n\n" : "";
                return $"{desc}{opponents} opponent{(opponents == 1 ? "" : "s")} - {countdown:0}s countdown";
            }
        }

        public override string TriggerKind => "RACE TRIGGER";

        protected override void OnConfirmed()
        {
            if (Course == null || RaceManager.Instance == null) return;
            RaceManager.Instance.BeginRace(Course);
        }
    }
}
