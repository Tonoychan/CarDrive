namespace Racing
{
    /// World trigger that sends the player into Garage_Scene (choose which car
    /// to drive). Same hover-then-confirm flow as RaceTrigger; place anywhere on
    /// the map.
    public class GarageTrigger : WorldPromptTrigger
    {
        public override string PromptDetails => "Swap car - review engine, brakes and handling levels.";
        public override string TriggerKind => "GARAGE TRIGGER";
        public override string ActionLabel => "ENTER";

        public override string HeaderRight
        {
            get
            {
                var garage = FindFirstObjectByType<CarGarageController>();
                int count = garage != null && garage.roster != null ? garage.roster.Length : 0;
                return count + " CAR" + (count == 1 ? "" : "S") + " OWNED";
            }
        }

        protected override void OnConfirmed()
        {
            SceneFlow.LoadGarage();
        }
    }
}
