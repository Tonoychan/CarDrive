using UnityEngine;
using TMPro;

namespace Racing.UI
{
    public class RaceUIController : MonoBehaviour
    {
        [Header("Countdown")]
        public GameObject countdownPanel;
        public TMP_Text countdownText;

        [Header("HUD")]
        public GameObject hudPanel;
        public TMP_Text positionText;

        [Header("Results")]
        public GameObject resultsPanel;
        public TMP_Text resultsText;

        void OnEnable()
        {
            RaceEvents.CountdownStarted += OnCountdownStarted;
            RaceEvents.CountdownTick += OnCountdownTick;
            RaceEvents.RaceStarted += OnRaceStarted;
            RaceEvents.PositionChanged += OnPositionChanged;
            RaceEvents.RaceFinished += OnRaceFinished;

            SetPanelActive(countdownPanel, false);
            SetPanelActive(hudPanel, false);
            SetPanelActive(resultsPanel, false);
        }

        void OnDisable()
        {
            RaceEvents.CountdownStarted -= OnCountdownStarted;
            RaceEvents.CountdownTick -= OnCountdownTick;
            RaceEvents.RaceStarted -= OnRaceStarted;
            RaceEvents.PositionChanged -= OnPositionChanged;
            RaceEvents.RaceFinished -= OnRaceFinished;
        }

        void OnCountdownStarted(float seconds)
        {
            SetPanelActive(resultsPanel, false);
            SetPanelActive(countdownPanel, true);
        }

        void OnCountdownTick(float remaining)
        {
            if (countdownText == null) return;
            countdownText.text = remaining > 0.05f ? Mathf.CeilToInt(remaining).ToString() : "GO!";
        }

        void OnRaceStarted()
        {
            SetPanelActive(countdownPanel, false);
            SetPanelActive(hudPanel, true);
        }

        void OnPositionChanged(int position, int totalRacers)
        {
            if (positionText == null) return;
            positionText.text = Ordinal(position) + " / " + totalRacers;
        }

        void OnRaceFinished(RaceResult[] results)
        {
            SetPanelActive(hudPanel, false);
            SetPanelActive(resultsPanel, true);

            if (resultsText == null) return;
            var sb = new System.Text.StringBuilder();
            foreach (var r in results)
            {
                sb.Append(r.position).Append(". ").Append(r.participantName);
                if (r.isPlayer) sb.Append("  (You)");
                sb.AppendLine();
            }
            resultsText.text = sb.ToString();
        }

        static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null) panel.SetActive(active);
        }

        static string Ordinal(int n)
        {
            int rem100 = n % 100;
            if (rem100 == 11 || rem100 == 12 || rem100 == 13) return n + "th";
            switch (n % 10)
            {
                case 1: return n + "st";
                case 2: return n + "nd";
                case 3: return n + "rd";
                default: return n + "th";
            }
        }
    }
}
