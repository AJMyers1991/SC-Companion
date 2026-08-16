using SCCompanion.Data.Hangar;

namespace SCCompanion.Tests.Data;

[TestClass]
public sealed class HangarTimerCalculatorTests
{
    private const long CycleStart = 1_700_000_000;

    [TestMethod]
    public void Calculate_AtCycleStart_IsClosedWithAllRedLights()
    {
        HangarTimerSnapshot snapshot = HangarTimerCalculator.Calculate(CycleStart, CycleStart);

        Assert.AreEqual(HangarPhase.Closed, snapshot.Phase);
        Assert.AreEqual(2 * 60 * 60, snapshot.SecondsRemaining);
        Assert.IsTrue(snapshot.Lights.All(light => light == HangarLightColor.Red));
    }

    [TestMethod]
    public void Calculate_DuringClosedPhase_TurnsOnOneGreenLightEveryTwentyFourMinutes()
    {
        HangarTimerSnapshot snapshot = HangarTimerCalculator.Calculate(
            CycleStart,
            CycleStart + (24 * 60));

        Assert.AreEqual(HangarPhase.Closed, snapshot.Phase);
        CollectionAssert.AreEqual(
            new[]
            {
                HangarLightColor.Green,
                HangarLightColor.Red,
                HangarLightColor.Red,
                HangarLightColor.Red,
                HangarLightColor.Red
            },
            snapshot.Lights.ToArray());
    }

    [TestMethod]
    public void Calculate_AtOpenPhaseStart_IsOpenWithAllGreenLights()
    {
        HangarTimerSnapshot snapshot = HangarTimerCalculator.Calculate(
            CycleStart,
            CycleStart + (2 * 60 * 60));

        Assert.AreEqual(HangarPhase.Open, snapshot.Phase);
        Assert.AreEqual(60 * 60, snapshot.SecondsRemaining);
        Assert.IsTrue(snapshot.Lights.All(light => light == HangarLightColor.Green));
    }

    [TestMethod]
    public void Calculate_DuringOpenPhase_TurnsOffOneLightEveryTwelveMinutes()
    {
        HangarTimerSnapshot snapshot = HangarTimerCalculator.Calculate(
            CycleStart,
            CycleStart + (2 * 60 * 60) + (12 * 60));

        Assert.AreEqual(HangarPhase.Open, snapshot.Phase);
        CollectionAssert.AreEqual(
            new[]
            {
                HangarLightColor.Dark,
                HangarLightColor.Green,
                HangarLightColor.Green,
                HangarLightColor.Green,
                HangarLightColor.Green
            },
            snapshot.Lights.ToArray());
    }

    [TestMethod]
    public void Calculate_AtResetPhaseStart_IsResettingWithAllDarkLights()
    {
        HangarTimerSnapshot snapshot = HangarTimerCalculator.Calculate(
            CycleStart,
            CycleStart + (3 * 60 * 60));

        Assert.AreEqual(HangarPhase.Resetting, snapshot.Phase);
        Assert.AreEqual(5 * 60, snapshot.SecondsRemaining);
        Assert.IsTrue(snapshot.Lights.All(light => light == HangarLightColor.Dark));
    }

    [TestMethod]
    public void Calculate_AtNextCycleStart_WrapsBackToClosed()
    {
        HangarTimerSnapshot snapshot = HangarTimerCalculator.Calculate(
            CycleStart,
            CycleStart + HangarTimerCalculator.TotalCycleSeconds);

        Assert.AreEqual(HangarPhase.Closed, snapshot.Phase);
        Assert.AreEqual(2 * 60 * 60, snapshot.SecondsRemaining);
        Assert.IsTrue(snapshot.Lights.All(light => light == HangarLightColor.Red));
    }

    [TestMethod]
    [DataRow(7200, "2h 0m 0s")]
    [DataRow(3661, "1h 1m 1s")]
    [DataRow(3599, "59m 59s")]
    [DataRow(0, "0m 0s")]
    public void FormatCountdown_UsesCompactHourMinuteSecondFormat(int seconds, string expected)
    {
        Assert.AreEqual(expected, HangarTimerCalculator.FormatCountdown(seconds));
    }
}
