namespace Racing
{
    /// One shop category (Engine/Brakes/Handling) as an ordered set of 4
    /// progressive tiers. Shared by ShopSceneController (buy) and
    /// GarageSceneController (read-only level display) so both read the exact
    /// same RacePart assets/ownership data -- there's one Upgrade button per
    /// category (not one per tier), so it always targets the next un-owned tier.
    [System.Serializable]
    public class UpgradeCategory
    {
        public string displayName = "Engine";
        public RacePart[] tiers = new RacePart[4];
    }
}
