namespace Racing
{
    /// World trigger that sends the player into Shop_Scene (buy parts). Same
    /// hover-then-confirm flow as RaceTrigger; place anywhere on the map.
    public class ShopTrigger : WorldPromptTrigger
    {
        public override string PromptDetails => "Upgrade your car's parts.";
        public override string TriggerKind => "SHOP TRIGGER";
        public override string ActionLabel => "ENTER";

        public override string HeaderRight
        {
            get
            {
                int balance = PlayerCurrency.Instance != null ? PlayerCurrency.Instance.Balance : 0;
                return balance.ToString("N0") + " COINS";
            }
        }

        protected override void OnConfirmed()
        {
            SceneFlow.LoadShop();
        }
    }
}
