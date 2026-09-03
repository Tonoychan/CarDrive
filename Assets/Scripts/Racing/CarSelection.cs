using UnityEngine;

namespace Racing
{
    /// PlayerPrefs-backed selected-car index, shared across scenes: Garage_Scene
    /// writes it when Drive is pressed, Drive_Scene reads it on load to spawn the
    /// matching car. Same persistence pattern as PlayerCurrency/RaceGarageController.
    public static class CarSelection
    {
        const string Key = "RaceGarage.SelectedCarIndex";

        public static int LoadIndex() => PlayerPrefs.GetInt(Key, 0);

        public static void SaveIndex(int index)
        {
            PlayerPrefs.SetInt(Key, index);
            PlayerPrefs.Save();
        }
    }
}
