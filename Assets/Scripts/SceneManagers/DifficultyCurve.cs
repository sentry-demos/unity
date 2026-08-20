using System.Collections.Generic;

/// <summary>
/// How the game gets harder over time: which enemies are unlocked, how big a wave is, and
/// how the spawn rates and enemy hitpoints ramp. Plain C# -- time is passed in rather than
/// read from <c>Time.time</c>, so every rule here is testable without a scene.
/// </summary>
public class DifficultyCurve
{
    public enum EnemyType
    {
        Sentaur = 0,
        Ant = 1,
        RandomHead = 2,
        DiagonalHead = 3,
        Mantis = 4,
        LinearHead = 5,
    }

    // What level each enemy type starts appearing at.
    private static readonly Dictionary<EnemyType, int> LevelEnemyGate = new Dictionary<EnemyType, int>
    {
        { EnemyType.Sentaur, 0 }, // start
        { EnemyType.Ant, 2 }, // level 3
        { EnemyType.RandomHead, 3 }, // level 4
        { EnemyType.DiagonalHead, 4 }, // level 5
        { EnemyType.Mantis, 6 }, // level 7
        { EnemyType.LinearHead, 7 }, // level 8
    };

    private readonly Settings _settings;

    public DifficultyCurve(Settings settings)
    {
        _settings = settings;

        EnemySpawnRate = settings.EnemySpawnRate;
        LinearHeadSpawnRate = settings.LinearHeadSpawnRate;
    }

    public float EnemySpawnRate { get; private set; }
    public float LinearHeadSpawnRate { get; private set; }
    public int EnemyHitPointModifier { get; private set; }

    public static int UnlockLevel(EnemyType type) => LevelEnemyGate[type];

    /// <summary>The hardest enemy type unlocked at this level; spawns roll within Sentaur..this.</summary>
    public static EnemyType HighestUnlocked(int level)
    {
        var highest = EnemyType.Sentaur;

        // LinearHead is excluded on purpose: it only arrives through timed waves, never
        // through the regular spawn roll.
        if (level >= LevelEnemyGate[EnemyType.Ant])
        {
            highest = EnemyType.Ant;
        }
        if (level >= LevelEnemyGate[EnemyType.RandomHead])
        {
            highest = EnemyType.RandomHead;
        }
        if (level >= LevelEnemyGate[EnemyType.DiagonalHead])
        {
            highest = EnemyType.DiagonalHead;
        }
        if (level >= LevelEnemyGate[EnemyType.Mantis])
        {
            highest = EnemyType.Mantis;
        }

        return highest;
    }

    /// <summary>
    /// Upper bound (exclusive) for the wave-size roll. With the default 0.67 scale factor:
    /// level 1-2 gives 1 enemy, level 3-4 gives 1-2, level 5 gives 1-3, and so on.
    /// Clamped to at least 1 -- at levels 0-1 the scaled value is 0, which would give a
    /// reversed Range(1, 0).
    /// </summary>
    public int MaxWaveSize(int level)
    {
        var scaled = (int)(level * _settings.MaxWaveSizeScaleFactor);
        return scaled < 1 ? 1 : scaled;
    }

    /// <summary>Heads come in half-sized waves, and never fewer than one.</summary>
    public static int AdjustWaveSizeForType(int waveSize, EnemyType type)
    {
        if (type != EnemyType.DiagonalHead)
        {
            return waveSize;
        }

        var halved = waveSize / 2;
        return halved < 1 ? 1 : halved;
    }

    public bool AreLinearHeadWavesUnlocked(int level) => level >= _settings.LinearHeadSpawnLevelFloor;

    /// <summary>Both waves spawn at once in the last minute before Death shows up.</summary>
    public bool IsDoubleWave(float elapsedSinceStart) =>
        elapsedSinceStart > _settings.DeathAppearanceTime - _settings.DoubleWaveLeadTime;

    public int WaveSize(int level) => (level + 1) / 2;

    /// <summary>Nudges both spawn rates one interval faster, down to their floors.</summary>
    public void RampUpSpawnRates()
    {
        EnemySpawnRate = Max(
            EnemySpawnRate - _settings.EnemySpawnRateRampUp,
            _settings.EnemySpawnRateFloor
        );
        LinearHeadSpawnRate = Max(
            LinearHeadSpawnRate - _settings.LinearHeadSpawnRateRampUp,
            _settings.LinearHeadSpawnRateFloor
        );
    }

    public void RampUpHitPoints()
    {
        EnemyHitPointModifier += _settings.HpRampUpValue;
    }

    private static float Max(float a, float b) => a > b ? a : b;

    /// <summary>
    /// The tuning numbers the curve reads, as plain values. Deliberately not the asset
    /// itself: the curve stays testable without one, and cannot write back to it.
    /// </summary>
    public struct Settings
    {
        public float EnemySpawnRate;
        public float EnemySpawnRateFloor;
        public float EnemySpawnRateRampUp;
        public float LinearHeadSpawnRate;
        public float LinearHeadSpawnRateFloor;
        public float LinearHeadSpawnRateRampUp;
        public int LinearHeadSpawnLevelFloor;
        public float MaxWaveSizeScaleFactor;
        public float DeathAppearanceTime;
        public float DoubleWaveLeadTime;
        public int HpRampUpValue;

        /// <summary>Reads the tuning asset into the plain values the curve works with.</summary>
        public static Settings From(BattleTuning tuning) =>
            new Settings
            {
                EnemySpawnRate = tuning.EnemySpawnRate,
                EnemySpawnRateFloor = tuning.EnemySpawnRateFloor,
                EnemySpawnRateRampUp = tuning.EnemySpawnRateRampUp,
                LinearHeadSpawnRate = tuning.LinearHeadSpawnRate,
                LinearHeadSpawnRateFloor = tuning.LinearHeadSpawnRateFloor,
                LinearHeadSpawnRateRampUp = tuning.LinearHeadSpawnRateRampUp,
                LinearHeadSpawnLevelFloor = tuning.LinearHeadSpawnLevelFloor,
                MaxWaveSizeScaleFactor = tuning.MaxWaveSizeScaleFactor,
                DeathAppearanceTime = tuning.DeathAppearanceTime,
                DoubleWaveLeadTime = tuning.DoubleWaveLeadTime,
                HpRampUpValue = tuning.HpRampUpValue,
            };
    }
}
