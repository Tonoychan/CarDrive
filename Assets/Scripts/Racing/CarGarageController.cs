using UnityEngine;
using MVC;
using MVC.Core;

namespace Racing
{
    [System.Serializable]
    public class CarRosterEntry
    {
        public string displayName = "Car";
        public GameObject vehiclePrefab;
        /// The "- Empty" prefab variant (mesh/wheels only, no Vehicle/Rigidbody) --
        /// used by Garage_Scene/Shop_Scene's static preview display. Instantiating the
        /// full driveable vehiclePrefab there throws: Vehicle.Awake() tries to
        /// auto-bootstrap an MVC.VehicleManager + VehicleFollower camera rig, which
        /// don't exist in those scenes. Falls back to vehiclePrefab if unset.
        public GameObject previewPrefab;
    }

    /// Mock garage: lets the player swap which car they drive by instantiating the
    /// chosen roster prefab and handing it to VehicleManager as the new PlayerTarget.
    /// All roster cars are freely selectable for now -- no per-car unlock cost, only
    /// parts (RaceGarageController) cost currency.
    public class CarGarageController : MonoBehaviour
    {
        public CarRosterEntry[] roster;

        public int SelectedIndex { get; private set; } = -1;
        public Vehicle CurrentVehicle { get; private set; }

        public event System.Action<int> CarSelected;

        void Start()
        {
            var vm = FindFirstObjectByType<VehicleManager>();
            CurrentVehicle = vm != null ? vm.PlayerVehicle : null;

            // Drive_Scene reloads fresh every time the player returns from
            // Garage_Scene, so the roster choice made there (CarSelection,
            // PlayerPrefs-backed) has to be re-applied here on load.
            if (roster != null && roster.Length > 0)
                SelectCar(Mathf.Clamp(CarSelection.LoadIndex(), 0, roster.Length - 1));
        }

        public void SelectCar(int index)
        {
            if (roster == null || index < 0 || index >= roster.Length) return;
            if (index == SelectedIndex) return;

            var entry = roster[index];
            if (entry.vehiclePrefab == null) return;

            var vm = FindFirstObjectByType<VehicleManager>();
            if (vm == null) return;

            var previous = vm.PlayerVehicle;
            Transform spawnPoint = previous != null ? previous.transform : transform;

            var go = Instantiate(entry.vehiclePrefab, spawnPoint.position, spawnPoint.rotation);
            var vehicle = go.GetComponent<Vehicle>();
            if (vehicle == null)
            {
                Destroy(go);
                return;
            }

            vm.PlayerTarget = vehicle;
            vm.RefreshPlayer();

            // NOT calling RaceGarageController.ApplyOwnedPartsToVehicle() here anymore --
            // confirmed (2026-09-03) that vehicle.Engine returns the SAME VehicleEngine
            // instance for every clone of a given prefab, not a per-instance copy.
            // Writing engine.Power/Torque there permanently mutates the prefab asset
            // itself (visible immediately via AssetDatabase, no Instantiate boundary),
            // and since this ran on every Drive_Scene load, the deltas stacked without
            // bound across repeated loads until MVC's own curve-mismatch validator
            // disabled the vehicle entirely. See RaceGarage.cs for the same landmine.

            var camera = FindFirstObjectByType<VehicleFollower>();
            if (camera != null) camera.Restart();

            if (previous != null) Destroy(previous.gameObject);

            CurrentVehicle = vehicle;
            SelectedIndex = index;
            CarSelected?.Invoke(index);
        }
    }
}
