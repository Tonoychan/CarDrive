namespace Racing
{
    /// Implemented by world triggers that show a hover-then-confirm popup via
    /// RacePromptUI (RaceTrigger, GarageTrigger, ShopTrigger, ...).
    public interface IWorldPromptTrigger
    {
        string PromptLabel { get; }
        string PromptDetails { get; }
        /// Small kicker line ("RACE TRIGGER", "GARAGE TRIGGER", ...) shown above the
        /// title, or inside the header bar when HeaderRight is set.
        string TriggerKind { get; }
        /// Footer primary-button label, e.g. "START" or "ENTER".
        string ActionLabel { get; }
        /// Optional live stat shown right-aligned in a dark header bar above the title
        /// ("3 CARS OWNED", "4,250 COINS"). Null hides the header bar entirely.
        string HeaderRight { get; }
        void Confirm();
        void Cancel();
    }
}
