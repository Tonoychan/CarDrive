using System;

namespace Racing
{
    /// Decouples RaceManager (simulation) from RaceUI (presentation).
    public static class RaceEvents
    {
        public static event Action<float> CountdownStarted;
        public static event Action<float> CountdownTick;
        public static event Action RaceStarted;
        public static event Action<int, int> PositionChanged;
        public static event Action<int> PlayerFinished;
        public static event Action<RaceResult[]> RaceFinished;

        public static void RaiseCountdownStarted(float seconds) => CountdownStarted?.Invoke(seconds);
        public static void RaiseCountdownTick(float remaining) => CountdownTick?.Invoke(remaining);
        public static void RaiseRaceStarted() => RaceStarted?.Invoke();
        public static void RaisePositionChanged(int position, int totalRacers) => PositionChanged?.Invoke(position, totalRacers);
        public static void RaisePlayerFinished(int finishPosition) => PlayerFinished?.Invoke(finishPosition);
        public static void RaiseRaceFinished(RaceResult[] results) => RaceFinished?.Invoke(results);
    }

    public struct RaceResult
    {
        public string participantName;
        public int position;
        public bool isPlayer;
    }
}
