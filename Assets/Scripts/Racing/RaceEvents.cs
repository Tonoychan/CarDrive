using System;

namespace Racing
{
    /// Decouples RaceManager (simulation) from RaceUI (presentation).
    public static class RaceEvents
    {
        public static event Action<float> CountdownStarted;
        /// (courseName, opponentCount), raised once alongside CountdownStarted -- lets
        /// the countdown/results screens show the course name without needing a direct
        /// RaceManager/RaceCourse reference.
        public static event Action<string, int> RaceInfo;
        public static event Action<float> CountdownTick;
        public static event Action RaceStarted;
        public static event Action<int, int> PositionChanged;
        /// (currentLap, totalLaps) for the player -- currentLap is the lap they're now
        /// on (1-based), raised once at race start and again each time they cross the
        /// finish line without yet completing the race.
        public static event Action<int, int> LapChanged;
        public static event Action<int> PlayerFinished;
        public static event Action<RaceResult[]> RaceFinished;
        /// (remainingMeters, fraction 0-1 along the current lap) for the player, raised
        /// every frame during Racing. Drives the HUD's progress-map dot and "M" readout.
        public static event Action<float, float> ProgressChanged;

        public static void RaiseCountdownStarted(float seconds) => CountdownStarted?.Invoke(seconds);
        public static void RaiseRaceInfo(string courseName, int opponentCount) => RaceInfo?.Invoke(courseName, opponentCount);
        public static void RaiseCountdownTick(float remaining) => CountdownTick?.Invoke(remaining);
        public static void RaiseRaceStarted() => RaceStarted?.Invoke();
        public static void RaisePositionChanged(int position, int totalRacers) => PositionChanged?.Invoke(position, totalRacers);
        public static void RaiseLapChanged(int currentLap, int totalLaps) => LapChanged?.Invoke(currentLap, totalLaps);
        public static void RaiseProgressChanged(float remainingMeters, float fraction) => ProgressChanged?.Invoke(remainingMeters, fraction);
        public static void RaisePlayerFinished(int finishPosition) => PlayerFinished?.Invoke(finishPosition);
        public static void RaiseRaceFinished(RaceResult[] results) => RaceFinished?.Invoke(results);
    }

    public struct RaceResult
    {
        public string participantName;
        public int position;
        public bool isPlayer;
        /// Seconds from race start to this participant's finish (RaceManager's
        /// raceStartTime -> Time.time at finish), used for the results screen's
        /// absolute leader time + "+gap" rows.
        public float elapsedSeconds;
    }
}
