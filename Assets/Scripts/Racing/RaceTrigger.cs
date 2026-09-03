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
