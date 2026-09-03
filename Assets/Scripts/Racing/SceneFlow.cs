namespace Racing
{
    /// Central place naming the three top-level scenes and routing transitions
    /// between them through LoadingScreen, so every trigger/button uses the same
    /// loading-screen-wrapped path instead of calling SceneManager directly.
    public static class SceneFlow
    {
        public const string Drive = "Drive_Scene";
        public const string Garage = "Garage_Scene";
        public const string Shop = "Shop_Scene";

        public static void LoadDrive() => UI.LoadingScreen.Load(Drive);
        public static void LoadGarage() => UI.LoadingScreen.Load(Garage);
        public static void LoadShop() => UI.LoadingScreen.Load(Shop);
    }
}
