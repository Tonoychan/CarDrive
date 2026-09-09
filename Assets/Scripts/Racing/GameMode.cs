namespace Racing
{
    public enum PlayMode { Career, QuickRace, Multiplayer }

    /// Session-only (not PlayerPrefs-persisted) flag set by MainMenuController when the
    /// player picks a mode from the title screen, read by Drive_Scene to decide how
    /// race triggers/RaceSelectUI behave: Career respects PlayerProgress.Level (normal
    /// free-roam progression); Quick Race and Multiplayer share the exact same map and
    /// RaceDefinitions but bypass the level gate entirely. Multiplayer has no
    /// implementation yet -- it behaves identically to Quick Race for now.
    /// Defaults to Career so entering Play Mode directly on Drive_Scene (skipping the
    /// menu, e.g. while testing in the Editor) behaves like normal career progression.
    public static class GameMode
    {
        public static PlayMode Current { get; private set; } = PlayMode.Career;

        public static void SetCareer() => Current = PlayMode.Career;
        public static void SetQuickRace() => Current = PlayMode.QuickRace;
        public static void SetMultiplayer() => Current = PlayMode.Multiplayer;

        public static bool BypassesLevelGate => Current != PlayMode.Career;
    }
}
