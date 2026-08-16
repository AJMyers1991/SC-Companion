namespace SCCompanion.Data.Hangar;

public enum HangarPhase
{
    Closed,
    Open,
    Resetting
}

public enum HangarLightColor
{
    Red,
    Green,
    Dark
}

public sealed record HangarTimerSnapshot(
    HangarPhase Phase,
    int SecondsRemaining,
    IReadOnlyList<HangarLightColor> Lights);

/// <summary>
/// Calculates the current Executive Hangar phase from the authoritative cycle timestamp.
/// </summary>
public static class HangarTimerCalculator
{
    public const int ClosedPhaseSeconds = 2 * 60 * 60;
    public const int OpenPhaseSeconds = 60 * 60;
    public const int ResetPhaseSeconds = 5 * 60;
    public const int TotalCycleSeconds =
        ClosedPhaseSeconds + OpenPhaseSeconds + ResetPhaseSeconds;

    private const int LightCount = 5;
    private const int ClosedLightIntervalSeconds = ClosedPhaseSeconds / LightCount;
    private const int OpenLightIntervalSeconds = OpenPhaseSeconds / LightCount;

    public static HangarTimerSnapshot Calculate(
        long cycleStartUnixSeconds,
        long currentUnixSeconds)
    {
        long elapsedSeconds = Math.Max(0, currentUnixSeconds - cycleStartUnixSeconds);
        int cycleElapsedSeconds = (int)(elapsedSeconds % TotalCycleSeconds);

        if (cycleElapsedSeconds < ClosedPhaseSeconds)
        {
            return new HangarTimerSnapshot(
                HangarPhase.Closed,
                ClosedPhaseSeconds - cycleElapsedSeconds,
                BuildClosedLights(cycleElapsedSeconds));
        }

        int openElapsedSeconds = cycleElapsedSeconds - ClosedPhaseSeconds;
        if (openElapsedSeconds < OpenPhaseSeconds)
        {
            return new HangarTimerSnapshot(
                HangarPhase.Open,
                OpenPhaseSeconds - openElapsedSeconds,
                BuildOpenLights(openElapsedSeconds));
        }

        int resetElapsedSeconds = openElapsedSeconds - OpenPhaseSeconds;
        return new HangarTimerSnapshot(
            HangarPhase.Resetting,
            ResetPhaseSeconds - resetElapsedSeconds,
            Enumerable.Repeat(HangarLightColor.Dark, LightCount).ToArray());
    }

    public static string FormatCountdown(int seconds)
    {
        int clampedSeconds = Math.Max(0, seconds);
        int hours = clampedSeconds / 3600;
        int minutes = (clampedSeconds % 3600) / 60;
        int remainingSeconds = clampedSeconds % 60;

        return hours > 0
            ? $"{hours}h {minutes}m {remainingSeconds}s"
            : $"{minutes}m {remainingSeconds}s";
    }

    private static HangarLightColor[] BuildClosedLights(int phaseElapsedSeconds)
    {
        return Enumerable.Range(1, LightCount)
            .Select(index => phaseElapsedSeconds >= index * ClosedLightIntervalSeconds
                ? HangarLightColor.Green
                : HangarLightColor.Red)
            .ToArray();
    }

    private static HangarLightColor[] BuildOpenLights(int phaseElapsedSeconds)
    {
        return Enumerable.Range(1, LightCount)
            .Select(index => phaseElapsedSeconds >= index * OpenLightIntervalSeconds
                ? HangarLightColor.Dark
                : HangarLightColor.Green)
            .ToArray();
    }
}
