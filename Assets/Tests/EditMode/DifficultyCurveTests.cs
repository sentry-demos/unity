using NUnit.Framework;

public class DifficultyCurveTests
{
    private static DifficultyCurve.Settings DefaultSettings() =>
        new DifficultyCurve.Settings
        {
            EnemySpawnRate = 2.0f,
            EnemySpawnRateFloor = 0.5f,
            EnemySpawnRateRampUp = 0.05f,
            LinearHeadSpawnRate = 30.0f,
            LinearHeadSpawnRateFloor = 8.0f,
            LinearHeadSpawnRateRampUp = 1.5f,
            LinearHeadSpawnLevelFloor = 3,
            MaxWaveSizeScaleFactor = 0.67f,
            DeathAppearanceTime = 600f,
            DoubleWaveLeadTime = 60f,
            HpRampUpValue = 10,
        };

    private static DifficultyCurve Create() => new DifficultyCurve(DefaultSettings());

    [Test]
    public void UnlocksEnemyTypesInLevelOrder()
    {
        Assert.AreEqual(DifficultyCurve.EnemyType.Sentaur, DifficultyCurve.HighestUnlocked(0));
        Assert.AreEqual(DifficultyCurve.EnemyType.Sentaur, DifficultyCurve.HighestUnlocked(1));
        Assert.AreEqual(DifficultyCurve.EnemyType.Ant, DifficultyCurve.HighestUnlocked(2));
        Assert.AreEqual(DifficultyCurve.EnemyType.RandomHead, DifficultyCurve.HighestUnlocked(3));
        Assert.AreEqual(DifficultyCurve.EnemyType.DiagonalHead, DifficultyCurve.HighestUnlocked(4));
        Assert.AreEqual(DifficultyCurve.EnemyType.DiagonalHead, DifficultyCurve.HighestUnlocked(5));
        Assert.AreEqual(DifficultyCurve.EnemyType.Mantis, DifficultyCurve.HighestUnlocked(6));
    }

    [Test]
    public void NeverUnlocksLinearHeadsThroughTheRegularSpawnRoll()
    {
        // Linear heads only ever arrive as a timed wave. If the roll could pick them the
        // scene would spawn them without a direction set.
        for (var level = 0; level < 50; level++)
        {
            Assert.AreNotEqual(
                DifficultyCurve.EnemyType.LinearHead,
                DifficultyCurve.HighestUnlocked(level)
            );
        }
    }

    [Test]
    public void MaxWaveSizeIsAtLeastOneOnEarlyLevels()
    {
        var curve = Create();

        // level * 0.67 truncates to 0 here; an unclamped value produces Random.Range(1, 0)
        Assert.AreEqual(1, curve.MaxWaveSize(0));
        Assert.AreEqual(1, curve.MaxWaveSize(1));
    }

    [Test]
    public void MaxWaveSizeGrowsWithLevel()
    {
        var curve = Create();

        Assert.AreEqual(2, curve.MaxWaveSize(3));
        Assert.AreEqual(3, curve.MaxWaveSize(5));
        Assert.AreEqual(5, curve.MaxWaveSize(8));
    }

    [Test]
    public void HalvesHeadWavesButNeverBelowOne()
    {
        Assert.AreEqual(
            2,
            DifficultyCurve.AdjustWaveSizeForType(4, DifficultyCurve.EnemyType.DiagonalHead)
        );
        Assert.AreEqual(
            1,
            DifficultyCurve.AdjustWaveSizeForType(1, DifficultyCurve.EnemyType.DiagonalHead)
        );
    }

    [Test]
    public void LeavesWaveSizeAloneForOtherEnemyTypes()
    {
        Assert.AreEqual(
            4,
            DifficultyCurve.AdjustWaveSizeForType(4, DifficultyCurve.EnemyType.Sentaur)
        );
    }

    [Test]
    public void GatesLinearHeadWavesOnTheConfiguredLevelFloor()
    {
        var curve = Create();

        Assert.IsFalse(curve.AreLinearHeadWavesUnlocked(2));
        Assert.IsTrue(curve.AreLinearHeadWavesUnlocked(3));
    }

    [Test]
    public void DoublesWavesOnlyInTheLastMinuteBeforeDeathArrives()
    {
        var curve = Create();

        Assert.IsFalse(curve.IsDoubleWave(500f));
        Assert.IsTrue(curve.IsDoubleWave(560f));
    }

    [Test]
    public void RampsSpawnRatesDownToTheirFloorsAndNoFurther()
    {
        var curve = Create();

        curve.RampUpSpawnRates();
        Assert.AreEqual(1.95f, curve.EnemySpawnRate, 0.0001f);
        Assert.AreEqual(28.5f, curve.LinearHeadSpawnRate, 0.0001f);

        for (var i = 0; i < 1000; i++)
        {
            curve.RampUpSpawnRates();
        }

        Assert.AreEqual(0.5f, curve.EnemySpawnRate, 0.0001f);
        Assert.AreEqual(8.0f, curve.LinearHeadSpawnRate, 0.0001f);
    }

    [Test]
    public void AccumulatesTheEnemyHitPointModifier()
    {
        var curve = Create();

        Assert.AreEqual(0, curve.EnemyHitPointModifier);

        curve.RampUpHitPoints();
        curve.RampUpHitPoints();

        Assert.AreEqual(20, curve.EnemyHitPointModifier);
    }

    [Test]
    public void WaveSizeGrowsEveryOtherLevel()
    {
        var curve = Create();

        Assert.AreEqual(2, curve.WaveSize(3));
        Assert.AreEqual(2, curve.WaveSize(4));
        Assert.AreEqual(3, curve.WaveSize(5));
    }
}
