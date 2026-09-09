using UnityEngine;
using UnityEngine.InputSystem;

namespace Racing
{
    /// Debug-only shortcut (Ctrl+Shift+R) that wipes saved career progress -- XP/level,
    /// completed-race first-clear tracking, and coin balance -- back to a fresh start,
    /// so the full level 1->10 unlock chain can be replayed without manually clearing
    /// PlayerPrefs. Compiled out of release (non-development) builds.
    public class DebugProgressReset : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (ctrl && shift && kb.rKey.wasPressedThisFrame)
                ResetSavedProgress();
        }

        // Deliberately NOT named "Reset" -- that's a reserved Unity message Editor
        // invokes automatically on AddComponent and on the Inspector's "Reset" context
        // option, which would silently wipe save data the moment this component is
        // added to a GameObject.
        void ResetSavedProgress()
        {
            PlayerProgress.Instance?.ResetProgress();
            PlayerCurrency.Instance?.ResetBalance(0);
            Debug.Log("[Debug] Progress reset -- Level 1, 0 XP, 0 coins, no races marked complete.");
        }
#endif
    }
}
