using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Racing.UI
{
    /// World-space info card hovering above a race trigger -- name, type, and reward --
    /// that billboards to face the camera every frame so it reads correctly regardless
    /// of approach angle. Lives on the trigger's own hierarchy (built once, alongside
    /// the particle marker); RaceTrigger drives its content, completed-state color, and
    /// visibility (shared with the marker's -- locked/mid-race = both hidden).
    public class RaceInfoBillboard : MonoBehaviour
    {
        [SerializeField] Image frameImage;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text typeText;
        [SerializeField] TMP_Text rewardText;

        Transform cam;

        public void Bind(Image frame, TMP_Text name, TMP_Text type, TMP_Text reward)
        {
            frameImage = frame;
            nameText = name;
            typeText = type;
            rewardText = reward;
        }

        void LateUpdate()
        {
            if (cam == null)
            {
                if (Camera.main == null) return;
                cam = Camera.main.transform;
            }
            // Face the camera, holding upright -- yaw only, no pitch/roll from the
            // camera's own tilt, so the card never leans or flips.
            Vector3 away = transform.position - cam.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(away);
        }

        public void SetInfo(RaceDefinition def)
        {
            if (def == null) return;
            if (nameText != null) nameText.text = def.raceName.ToUpperInvariant();
            if (typeText != null)
                typeText.text = def.laps > 1 ? $"CIRCUIT  ·  {def.laps} LAPS" : "SPRINT";
            if (rewardText != null)
                rewardText.text = $"+{def.baseXPReward} XP   ·   +{def.baseCoinReward} COINS";
        }

        /// Swaps the card's border + reward-text color between the default ink/accent
        /// look (not yet cleared) and green (already cleared at least once) -- a glance
        /// tells you which races still owe a first-clear bonus.
        public void SetCompleted(bool completed)
        {
            var tint = completed ? RacingTheme.Success : RacingTheme.Ink;
            if (frameImage != null) frameImage.color = tint;
            if (rewardText != null) rewardText.color = completed ? RacingTheme.Success : RacingTheme.Accent;
        }
    }
}
