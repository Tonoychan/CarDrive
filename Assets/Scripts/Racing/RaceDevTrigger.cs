using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Racing
{
    /// Manual test helper: press a key in Play mode to start a race without needing a
    /// RaceTrigger volume wired up yet (Phase 3). Not meant to ship -- once RaceTrigger
    /// is placed in the scene, this can be removed or left disabled.
    public class RaceDevTrigger : MonoBehaviour
    {
        public RaceCourse course;

        void Update()
        {
            if (RaceManager.Instance == null || course == null) return;
            if (RaceManager.Instance.State != RaceState.Idle) return;

            bool pressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                pressed = Keyboard.current[Key.R].wasPressedThisFrame;
#else
            pressed = Input.GetKeyDown(KeyCode.R);
#endif
            if (pressed)
                RaceManager.Instance.BeginRace(course);
        }
    }
}
