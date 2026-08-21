using Sentry;

/// <summary>
/// The shape of one battle, as metrics: when it started, how it went, and what the frame
/// pacing looked like while it did.
/// </summary>
/// <remarks>
/// <para>
/// Plain C# and driven from <see cref="BattleSceneManager"/>, for the same reason
/// <see cref="SpawnDirector"/> is: time is passed in rather than read, so nothing here needs
/// a scene to be exercised, and the manager keeps the ordering between what it does and what
/// gets measured.
/// </para>
/// <para>
/// It subscribes to nothing. <see cref="GameEvents"/> is static, so a listener that outlives
/// a scene reload keeps counting -- the bug <see cref="BattleSceneManager"/> documents at its
/// own subscriptions. The manager already handles every one of those events, so it calls in
/// here instead and there is no subscription to leak.
/// </para>
/// </remarks>
public class BattleMetrics
{
    /// <summary>How often the accumulated metrics are drained and the gauges sampled.</summary>
    private const float SampleInterval = 1.0f;

    /// <summary>
    /// One frame in this many is emitted as a frame-time sample. Every frame would be 30
    /// metrics a second on the kiosk build; every fifteenth is two, which is plenty to build
    /// a distribution from over a run.
    /// </summary>
    private const int FrameSampleInterval = 15;

    // The scene reloads for "Try Again" but the process does not, so this counts attempts
    // within one session -- which is the interesting number when a run ends in a crash.
    private static int _attempts;

    private float _runStartTime;
    private float _lastSampleTime;
    private float _lastLevelTime;
    private float _worstFrameTime;
    private int _frameCount;
    private bool _runEnded;

    public void RunStarted(float now, int startingLevel)
    {
        // First, so that everything below -- and everything the run goes on to report -- lands
        // on the run's own trace rather than whatever the scope was holding.
        RunTrace.Begin();

        GameMetrics.ResetRun();
        GameMetrics.SetLevel(startingLevel);

        _attempts++;
        _runStartTime = now;
        _lastSampleTime = now;
        _lastLevelTime = now;
        _worstFrameTime = 0f;
        _frameCount = 0;
        _runEnded = false;

        GameMetrics.Count(GameMetrics.RunStarted, 1, (GameMetrics.AttemptKey, _attempts));
    }

    /// <summary>
    /// Samples the frame clock, and once a second drains everything the hot paths recorded.
    /// Called from the manager's Update while the game is actually playing, so a pause or a
    /// game-over screen does not land in the frame-time distribution.
    /// </summary>
    public void Tick(float now, float unscaledDeltaTime, int enemiesAlive, int pickupsOnScreen)
    {
        if (unscaledDeltaTime > _worstFrameTime)
        {
            _worstFrameTime = unscaledDeltaTime;
        }

        _frameCount++;

        if (_frameCount % FrameSampleInterval == 0)
        {
            // Attributed with the enemy count it was rendered under: the whole point of the
            // frame-rate cap is that pacing holds up when the field fills, and this is what
            // shows whether it does.
            GameMetrics.Distribution(
                GameMetrics.FrameTime,
                unscaledDeltaTime * 1000.0,
                MeasurementUnit.Duration.Millisecond,
                (GameMetrics.EnemiesKey, GameMetrics.CountBucket(enemiesAlive))
            );
        }

        if (now - _lastSampleTime < SampleInterval)
        {
            return;
        }

        _lastSampleTime = now;

        GameMetrics.Gauge(GameMetrics.EnemiesAlive, enemiesAlive, MeasurementUnit.None);
        GameMetrics.Gauge(GameMetrics.PickupsOnScreen, pickupsOnScreen, MeasurementUnit.None);

        // The worst frame of the second, not the average: uneven pacing is what reads as
        // stutter, and an average of 30 healthy frames hides the one that stalled.
        GameMetrics.Distribution(
            GameMetrics.FrameTimeWorst,
            _worstFrameTime * 1000.0,
            MeasurementUnit.Duration.Millisecond,
            (GameMetrics.EnemiesKey, GameMetrics.CountBucket(enemiesAlive))
        );

        _worstFrameTime = 0f;

        GameMetrics.Flush();
    }

    public void LevelUp(int level, float now)
    {
        GameMetrics.SetLevel(level);

        GameMetrics.Count(GameMetrics.LevelUp, 1, (GameMetrics.NewLevelKey, level));
        GameMetrics.Distribution(
            GameMetrics.TimeToLevel,
            now - _lastLevelTime,
            MeasurementUnit.Duration.Second,
            (GameMetrics.NewLevelKey, level)
        );

        _lastLevelTime = now;
    }

    public void DifficultyRamped(float enemySpawnRate, float waveSpawnRate)
    {
        GameMetrics.Gauge(
            GameMetrics.EnemySpawnRate,
            enemySpawnRate,
            MeasurementUnit.Duration.Second
        );
        GameMetrics.Gauge(
            GameMetrics.WaveSpawnRate,
            waveSpawnRate,
            MeasurementUnit.Duration.Second
        );
    }

    public void HitPointsRamped(int hitPointModifier)
    {
        GameMetrics.Gauge(GameMetrics.EnemyHitPointModifier, hitPointModifier, MeasurementUnit.None);
    }

    public void DeathEnemySpawned(float now)
    {
        GameMetrics.Count(GameMetrics.DeathEnemySpawned, 1);
        GameMetrics.Distribution(
            GameMetrics.DeathEnemySpawnedAt,
            now - _runStartTime,
            MeasurementUnit.Duration.Second
        );
    }

    /// <summary>
    /// Ends the run: drains the last interval, then reports how it went.
    /// </summary>
    /// <remarks>
    /// Guarded, because a run can end more than once from the game's point of view -- the
    /// player can quit from the game-over screen, which raises Quit after PlayerDeath. Only
    /// the first outcome is the real one.
    /// </remarks>
    public void RunEnded(string outcome, float now, int score, int level)
    {
        if (_runEnded)
        {
            return;
        }

        _runEnded = true;

        GameMetrics.Flush();

        GameMetrics.Count(GameMetrics.RunEnded, 1, (GameMetrics.OutcomeKey, outcome));
        GameMetrics.Distribution(
            GameMetrics.RunDuration,
            now - _runStartTime,
            MeasurementUnit.Duration.Second,
            (GameMetrics.OutcomeKey, outcome)
        );
        GameMetrics.Distribution(GameMetrics.RunScore, score, MeasurementUnit.None);
        GameMetrics.Distribution(GameMetrics.RunLevelReached, level, MeasurementUnit.None);
    }
}
