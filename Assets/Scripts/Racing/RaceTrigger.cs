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

        void Awake()
        {
            Course = GetComponentInParent<RaceCourse>();
        }

        void Start()
        {
            // Subscribing here rather than OnEnable(): Start() runs only after every
            // Awake() in the scene has completed, so PlayerProgress.Instance (set in
            // its own Awake, on a different GameObject) is guaranteed to exist by now.
            if (PlayerProgress.Instance != null) PlayerProgress.Instance.LevelChanged += OnLevelChanged;
            RefreshUnlockState();
        }

        void OnDestroy()
        {
            if (PlayerProgress.Instance != null) PlayerProgress.Instance.LevelChanged -= OnLevelChanged;
        }

        void OnLevelChanged(int newLevel) => RefreshUnlockState();

        /// isEnabled is WorldPromptTrigger's existing "master gate" field (see its own
        /// doc comment) -- this just drives it from the player's level instead of
        /// hand-flipping it in the Inspector. Unlocked-by-default (requiredLevel<=1)
        /// races stay interactable even before PlayerProgress exists in the scene.
        void RefreshUnlockState()
        {
            var def = Course != null ? Course.definition : null;
            if (def == null) return;
            int level = PlayerProgress.Instance != null ? PlayerProgress.Instance.Level : 1;
            isEnabled = def.IsUnlocked(level);
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
