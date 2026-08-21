using System.Collections.Generic;
using Sentry;
using Sentry.Unity;
using UnityEngine;

/// <summary>
/// The game's Sentry metrics: what they are called, the attributes every one of them
/// carries, and the aggregation that keeps the hot paths off the wire.
/// </summary>
/// <remarks>
/// <para>
/// Always on. There is no <see cref="DemoConfiguration"/> gate here: instrumenting the real
/// game is itself part of what this demo shows. It costs nothing in a build without a DSN --
/// <c>SentrySdk.IsEnabled</c> is false there and every call returns immediately.
/// </para>
/// <para>
/// Two ways in. <c>Emit*</c> goes straight out, and is for things that happen a handful of
/// times a run: a level-up, a run ending, an HTTP call. <c>Record*</c> accumulates, and is
/// for anything that can fire several times a second -- kills, damage, XP. Those are drained
/// once a second by <see cref="BattleMetrics"/>. A metric per projectile hit would put
/// thousands of items a minute on the wire from a 30fps kiosk build, without adding any
/// resolution that a one-second bucket does not already have.
/// </para>
/// <para>
/// Attribute values stay within bounded sets -- an enemy prefab, a pickup type, an upgrade
/// path, a level, an outcome word. Never an instance name or an id: attributes are indexed
/// per distinct value, and an unbounded one makes the metric unqueryable.
/// </para>
/// </remarks>
public static class GameMetrics
{
    private const string Prefix = "sentaur.";

    // The run as a whole.
    public const string RunStarted = Prefix + "run.started";
    public const string RunDuration = Prefix + "run.duration";
    public const string RunScore = Prefix + "run.score";
    public const string RunLevelReached = Prefix + "run.level_reached";
    public const string RunEnded = Prefix + "run.ended";
    public const string RunCrashPathEntered = Prefix + "run.crash_path_entered";

    // Progression.
    public const string LevelUp = Prefix + "level.up";
    public const string TimeToLevel = Prefix + "level.time_to_level";
    public const string UpgradeSelected = Prefix + "upgrade.selected";
    public const string UpgradePoolExhausted = Prefix + "upgrade.pool_exhausted";

    // How hard the game is being right now.
    public const string EnemyHitPointModifier = Prefix + "difficulty.enemy_hp_modifier";
    public const string EnemySpawnRate = Prefix + "difficulty.enemy_spawn_rate";
    public const string WaveSpawnRate = Prefix + "difficulty.wave_spawn_rate";
    public const string EnemySpawned = Prefix + "enemy.spawned";
    public const string EnemiesAlive = Prefix + "enemy.alive";
    public const string DeathEnemySpawned = Prefix + "enemy.death_spawned";
    public const string DeathEnemySpawnedAt = Prefix + "enemy.death_spawned_at";

    // Combat.
    public const string EnemyKilled = Prefix + "combat.enemy_killed";
    public const string DamageDealt = Prefix + "combat.damage_dealt";
    public const string Hits = Prefix + "combat.hits";
    public const string HitDamageMax = Prefix + "combat.hit_damage_max";

    // The player.
    public const string DamageTaken = Prefix + "player.damage_taken";
    public const string HitsTaken = Prefix + "player.hits_taken";
    public const string PlayerHealth = Prefix + "player.health";
    public const string FirstDamageAt = Prefix + "player.first_damage_at";

    // Pickups and XP.
    public const string PickupCollected = Prefix + "pickup.collected";
    public const string PickupsOnScreen = Prefix + "pickup.on_screen";
    public const string XpEarned = Prefix + "xp.earned";
    public const string XpDropsCollected = Prefix + "xp.drops_collected";

    // Frame pacing and the scans that scale with the enemy count.
    public const string FrameTime = Prefix + "perf.frame_time";
    public const string FrameTimeWorst = Prefix + "perf.frame_time_worst";
    public const string AutoAimScan = Prefix + "perf.autoaim_scan";
    public const string AutoAimScans = Prefix + "perf.autoaim_scans";
    public const string SplashHits = Prefix + "perf.splash_hits";

    // The demo's deliberate network paths. See CONTRIBUTING.md.
    public const string UpgradeFetch = Prefix + "upgrade.fetch";
    public const string UpgradeFetchDuration = Prefix + "upgrade.fetch_duration";
    public const string ScoreLogin = Prefix + "score.login";
    public const string ScoreUpload = Prefix + "score.upload";
    public const string ScoreUploadDuration = Prefix + "score.upload_duration";
    public const string BundleDownload = Prefix + "bundle.download";
    public const string BundleDownloadDuration = Prefix + "bundle.download_duration";

    // Attribute keys, so a typo cannot split one metric across two attribute names.
    public const string LevelKey = "level";
    public const string PlatformKey = "platform";
    public const string AttemptKey = "attempt";
    public const string OutcomeKey = "outcome";
    public const string NewLevelKey = "new_level";
    public const string PathKey = "path";
    public const string TypeKey = "type";
    public const string SizeKey = "size";
    public const string ResultKey = "result";
    public const string EnemiesKey = "enemies";
    public const string PickupKey = "pickup";

    private static string _platformName = "unknown";

    /// <summary>
    /// Resolves the values that go on every metric, on the main thread and before any scene
    /// runs. Deliberately not a field initializer: that would run on whichever thread touched
    /// this class first, and the score upload reaches it from a task.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        _platformName = Application.platform.ToString();
    }

    private static int _level;

    private static readonly Dictionary<string, long> _kills = new Dictionary<string, long>();
    private static readonly Dictionary<string, long> _pickups = new Dictionary<string, long>();

    private static Bucket _damageDealt;
    private static Bucket _damageTaken;
    private static Bucket _autoAimScans;
    private static Bucket _splashHits;
    private static long _xpEarned;
    private static long _xpDrops;

    private static float _playerHealth;
    private static bool _hasPlayerHealth;
    private static bool _firstDamageReported;

    /// <summary>The level attached to every metric from here on.</summary>
    public static void SetLevel(int level) => _level = level;

    /// <summary>
    /// Drops everything accumulated so far. "Try Again" reloads the scene rather than the
    /// process, so without this the first flush of a new run would carry the tail of the
    /// previous one.
    /// </summary>
    public static void ResetRun()
    {
        _kills.Clear();
        _pickups.Clear();
        _damageDealt = default;
        _damageTaken = default;
        _autoAimScans = default;
        _splashHits = default;
        _xpEarned = 0;
        _xpDrops = 0;
        _playerHealth = 0f;
        _hasPlayerHealth = false;
        _firstDamageReported = false;
        _level = 0;
    }

    public static void RecordEnemyKilled(string kind) => Increment(_kills, kind);

    public static void RecordPickupCollected(string kind) => Increment(_pickups, kind);

    public static void RecordDamageDealt(int damage) => _damageDealt.Add(damage);

    public static void RecordDamageTaken(int damage) => _damageTaken.Add(damage);

    public static void RecordXpEarned(int xp)
    {
        _xpEarned += xp;
        _xpDrops++;
    }

    /// <summary>The latest health fraction. A gauge, so only the last value in the interval matters.</summary>
    public static void RecordPlayerHealth(float fraction)
    {
        _playerHealth = fraction;
        _hasPlayerHealth = true;
    }

    /// <summary>
    /// How long the player lasted before taking a hit, once per run. A run where this is
    /// small is a run where the opening wave is too much.
    /// </summary>
    public static void RecordFirstDamage(float secondsSinceRunStart)
    {
        if (_firstDamageReported)
        {
            return;
        }

        _firstDamageReported = true;
        Distribution(FirstDamageAt, secondsSinceRunStart, MeasurementUnit.Duration.Second);
    }

    public static void RecordAutoAimScan(double microseconds) => _autoAimScans.Add(microseconds);

    public static void RecordSplashHits(int hits) => _splashHits.Add(hits);

    /// <summary>
    /// Sends everything accumulated since the last call and starts the next interval empty.
    /// Driven from <see cref="BattleMetrics"/>, once a second.
    /// </summary>
    public static void Flush()
    {
        if (!SentrySdk.IsEnabled)
        {
            // Still clear, or a build without a DSN would accumulate for the whole run.
            ResetInterval();
            return;
        }

        foreach (var kill in _kills)
        {
            Count(EnemyKilled, kill.Value, (TypeKey, kill.Key));
        }

        foreach (var pickup in _pickups)
        {
            Count(PickupCollected, pickup.Value, (PickupKey, pickup.Key));
        }

        if (_damageDealt.Count > 0)
        {
            Count(DamageDealt, (long)_damageDealt.Total);
            Count(Hits, _damageDealt.Count);
            Distribution(HitDamageMax, _damageDealt.Max, MeasurementUnit.None);
        }

        if (_damageTaken.Count > 0)
        {
            Count(DamageTaken, (long)_damageTaken.Total);
            Count(HitsTaken, _damageTaken.Count);
        }

        if (_xpDrops > 0)
        {
            Count(XpEarned, _xpEarned);
            Count(XpDropsCollected, _xpDrops);
        }

        if (_hasPlayerHealth)
        {
            Gauge(PlayerHealth, _playerHealth, MeasurementUnit.Fraction.Ratio);
        }

        if (_autoAimScans.Count > 0)
        {
            Count(AutoAimScans, _autoAimScans.Count);
            Distribution(AutoAimScan, _autoAimScans.Max, MeasurementUnit.Duration.Microsecond);
        }

        if (_splashHits.Count > 0)
        {
            Distribution(SplashHits, _splashHits.Max, MeasurementUnit.None);
        }

        ResetInterval();
    }

    public static void Count(string name, long value, params (string Key, object Value)[] attributes)
    {
        if (!SentrySdk.IsEnabled || value == 0)
        {
            return;
        }

        SentrySdk.Metrics.EmitCounter(name, value, Attributes(attributes));
    }

    public static void Distribution(
        string name,
        double value,
        MeasurementUnit unit,
        params (string Key, object Value)[] attributes
    )
    {
        if (!SentrySdk.IsEnabled)
        {
            return;
        }

        SentrySdk.Metrics.EmitDistribution(name, value, unit, Attributes(attributes));
    }

    public static void Gauge(
        string name,
        double value,
        MeasurementUnit unit,
        params (string Key, object Value)[] attributes
    )
    {
        if (!SentrySdk.IsEnabled)
        {
            return;
        }

        SentrySdk.Metrics.EmitGauge(name, value, unit, Attributes(attributes));
    }

    /// <summary>
    /// Buckets a count into one of a handful of labels. An exact enemy count as an attribute
    /// would be a new attribute value for every number the game ever reaches.
    /// </summary>
    public static string CountBucket(int count)
    {
        if (count == 0)
        {
            return "0";
        }
        if (count <= 5)
        {
            return "1-5";
        }
        if (count <= 15)
        {
            return "6-15";
        }
        if (count <= 30)
        {
            return "16-30";
        }

        return "31+";
    }

    /// <summary>
    /// Unity's instantiated objects are named "<c>Prefab(Clone)</c>". The prefab name is a
    /// fixed, small set and makes a good attribute; the suffix would only ever be noise.
    /// </summary>
    public static string PrefabKind(string objectName)
    {
        const string cloneSuffix = "(Clone)";

        return objectName.EndsWith(cloneSuffix)
            ? objectName.Substring(0, objectName.Length - cloneSuffix.Length)
            : objectName;
    }

    /// <summary>
    /// The caller's attributes plus the two every metric carries. Typed as the interface the
    /// SDK takes, so the array does not also match its <c>ReadOnlySpan</c> overload.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, object>> Attributes(
        (string Key, object Value)[] attributes
    )
    {
        var all = new KeyValuePair<string, object>[attributes.Length + 2];

        for (var i = 0; i < attributes.Length; i++)
        {
            all[i] = new KeyValuePair<string, object>(attributes[i].Key, attributes[i].Value);
        }

        all[attributes.Length] = new KeyValuePair<string, object>(LevelKey, _level);
        all[attributes.Length + 1] = new KeyValuePair<string, object>(PlatformKey, _platformName);

        return all;
    }

    private static void Increment(Dictionary<string, long> counts, string key)
    {
        counts.TryGetValue(key, out var count);
        counts[key] = count + 1;
    }

    private static void ResetInterval()
    {
        _kills.Clear();
        _pickups.Clear();
        _damageDealt = default;
        _damageTaken = default;
        _autoAimScans = default;
        _splashHits = default;
        _xpEarned = 0;
        _xpDrops = 0;
        _hasPlayerHealth = false;
    }

    /// <summary>One interval's worth of one measurement: how many, how much, and the worst one.</summary>
    private struct Bucket
    {
        public long Count;
        public double Total;
        public double Max;

        public void Add(double value)
        {
            Count++;
            Total += value;

            if (value > Max)
            {
                Max = value;
            }
        }
    }
}
