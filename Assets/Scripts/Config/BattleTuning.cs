using UnityEngine;

/// <summary>
/// Designer-tunable numbers for a battle run. Lives as an asset rather than on the scene
/// object so values can be edited during play mode and survive stopping -- the normal way
/// to feel out a spawn curve.
/// </summary>
/// <remarks>
/// Tuning only. Everything here is read-only at runtime: the rules classes that consume it
/// (<see cref="DifficultyCurve"/>, <see cref="SpawnDirector"/>, <see cref="WaveFormation"/>)
/// own their own mutable state and are rebuilt each run. Writing back would persist a single
/// run's ramped-down spawn rate into the asset and leak it into every run after.
/// </remarks>
[CreateAssetMenu(fileName = "BattleTuning", menuName = "Sentaur/Battle Tuning")]
public class BattleTuning : ScriptableObject
{
    [Header("Enemy Spawning")]
    [SerializeField]
    [Tooltip("How frequently enemies spawn (in seconds)")]
    private float _enemySpawnRate = 2.0f;

    [SerializeField]
    [Tooltip("The fastest possible spawn rate for enemies (in seconds)")]
    private float _enemySpawnRateFloor = 0.5f;

    [SerializeField]
    [Tooltip("How much the enemy spawn interval shortens each ramp-up (in seconds)")]
    private float _enemySpawnRateRampUp = 0.05f;

    [SerializeField]
    [Tooltip("How frequently the spawn rates ramp up (in seconds)")]
    private float _spawnRampUpInterval = 10f;

    [SerializeField]
    [Tooltip("Scales how many enemies a single spawn can produce, as a factor of the level")]
    private float _maxWaveSizeScaleFactor = 0.6f;

    [Header("Linear Head Waves")]
    [SerializeField]
    [Tooltip("How frequently waves of linear heads spawn (in seconds)")]
    private float _linearHeadSpawnRate = 30.0f;

    [SerializeField]
    [Tooltip("The fastest possible spawn rate for waves of linear heads (in seconds)")]
    private float _linearHeadSpawnRateFloor = 8.0f;

    [SerializeField]
    [Tooltip("How much the wave interval shortens each ramp-up (in seconds)")]
    private float _linearHeadSpawnRateRampUp = 1.5f;

    [SerializeField]
    [Tooltip("What level linear head waves start spawning")]
    private int _linearHeadSpawnLevelFloor = 3;

    [Header("Wave Formation")]
    [SerializeField]
    [Tooltip("How far off-screen a linear wave starts, along its axis of travel (in units)")]
    private float _linearWaveDistance = 10f;

    [SerializeField]
    [Tooltip("Spacing between enemies fanned out from a spawn point (in units)")]
    private float _fanSpacingX = 0.75f;

    [SerializeField]
    [Tooltip("Height of the random band a fanned-out spawn scatters within (in units)")]
    private float _fanRangeY = 2.0f;

    [Header("Enemy Hitpoints")]
    [SerializeField]
    [Tooltip("How frequently the max hitpoints of enemies ramp up (in seconds)")]
    private float _hpRampUpInterval = 60f;

    [SerializeField]
    [Tooltip("How much to increase the max hitpoints of enemies by each interval")]
    private int _hpRampUpValue = 5;

    [Header("Pickups")]
    [SerializeField]
    [Tooltip("How frequently pickups spawn (in seconds)")]
    private float _pickupSpawnRate = 2.0f;

    [SerializeField]
    [Tooltip("The maximum number of pickups allowed on screen at any given moment")]
    private int _maxPickupsOnScreen = 5;

    [Header("Progression")]
    [SerializeField]
    [Tooltip("Total XP required to reach each level. One entry per level above the first.")]
    private int[] _levelMilestones =
    {
        50, // level 2
        150,
        300,
        550,
        900,
        1400,
        2050,
        2900,
        3950, // level 10
        5250,
        6850,
        8800,
        11150,
        13950,
        17250,
        21150,
        25800,
        31350,
        38000, // level 20
        46000,
        55600, // level 22 (max)
    };

    [SerializeField]
    [Tooltip("Time when death appears (in seconds)")]
    private float _deathAppearanceTime = 600f;

    [SerializeField]
    [Tooltip("How long before death appears that waves start spawning doubled (in seconds)")]
    private float _doubleWaveLeadTime = 60f;

    public float EnemySpawnRate => _enemySpawnRate;
    public float EnemySpawnRateFloor => _enemySpawnRateFloor;
    public float EnemySpawnRateRampUp => _enemySpawnRateRampUp;
    public float SpawnRampUpInterval => _spawnRampUpInterval;
    public float MaxWaveSizeScaleFactor => _maxWaveSizeScaleFactor;

    public float LinearHeadSpawnRate => _linearHeadSpawnRate;
    public float LinearHeadSpawnRateFloor => _linearHeadSpawnRateFloor;
    public float LinearHeadSpawnRateRampUp => _linearHeadSpawnRateRampUp;
    public int LinearHeadSpawnLevelFloor => _linearHeadSpawnLevelFloor;

    public float LinearWaveDistance => _linearWaveDistance;
    public float FanSpacingX => _fanSpacingX;
    public float FanRangeY => _fanRangeY;

    public float HpRampUpInterval => _hpRampUpInterval;
    public int HpRampUpValue => _hpRampUpValue;

    public float PickupSpawnRate => _pickupSpawnRate;
    public int MaxPickupsOnScreen => _maxPickupsOnScreen;

    public int[] LevelMilestones => _levelMilestones;
    public float DeathAppearanceTime => _deathAppearanceTime;
    public float DoubleWaveLeadTime => _doubleWaveLeadTime;
}
