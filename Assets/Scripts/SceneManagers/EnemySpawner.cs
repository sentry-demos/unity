using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Instantiates enemies: which type, how many, and where.
/// </summary>
/// <remarks>
/// Driven by <see cref="BattleSceneManager"/> rather than running its own clock -- the manager
/// owns the ordering between spawning, ramping and levelling up. The rules live in
/// <see cref="DifficultyCurve"/> and <see cref="WaveFormation"/>; this only does the placing.
/// </remarks>
public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    [SerializeField]
    [Tooltip("The sentaur enemy prefab to spawn")]
    private GameObject _sentaurEnemyPrefab;

    [SerializeField]
    [Tooltip("The ant enemy prefab to spawn")]
    private GameObject _antEnemyPrefab;

    [SerializeField]
    [Tooltip("The head enemy prefab to spawn")]
    private GameObject _headEnemyPrefab;

    [SerializeField]
    [Tooltip("The mantis enemy prefab to spawn")]
    private GameObject _mantisEnemyPrefab;

    [SerializeField]
    [Tooltip("The death enemy prefab to spawn")]
    private GameObject _deathEnemyPrefab;

    [SerializeField]
    [Tooltip("The linear enemy prefab to spawn")]
    private GameObject _linearEnemyPrefab;

    [SerializeField]
    [Tooltip("The random enemy prefab to spawn")]
    private GameObject _randomEnemyPrefab;

    [Header("Scene References")]
    [SerializeField]
    [Tooltip("Where to spawn -- shared with the pickup spawner")]
    private SpawnArea _spawnArea;

    [SerializeField]
    [Tooltip("Parent transform that spawned enemies are placed under")]
    private Transform _enemiesParentTransform;

    [SerializeField]
    [Tooltip("Parent transform that XP drops are placed under, handed to each enemy spawned")]
    private Transform _xpDropParentTransform;

    [SerializeField]
    [Tooltip("The Level container that the death enemy is placed under")]
    private Transform _levelContainer;

    private DifficultyCurve _difficulty;
    private WaveFormation _waveFormation;

    /// <summary>
    /// Hands over the rules objects the manager owns. Called before any spawning; the
    /// spawner has no useful behaviour until it is.
    /// </summary>
    public void Initialize(DifficultyCurve difficulty, WaveFormation waveFormation)
    {
        _difficulty = difficulty;
        _waveFormation = waveFormation;
    }

    /// <summary>Spawns a wave of random enemies appropriate to the level.</summary>
    public void SpawnRandomWave(int level)
    {
        var highestUnlocked = DifficultyCurve.HighestUnlocked(level);
        var spawnChoice = (DifficultyCurve.EnemyType)
            Random.Range((int)DifficultyCurve.EnemyType.Sentaur, (int)highestUnlocked + 1);

        var waveSize = DifficultyCurve.AdjustWaveSizeForType(
            Random.Range(1, _difficulty.MaxWaveSize(level)),
            spawnChoice
        );

        SpawnFannedOut(PrefabFor(spawnChoice), waveSize);
    }

    /// <summary>Spawns a line of linear enemies converging on the player.</summary>
    public void SpawnLinearWave(int count, bool doubleWave)
    {
        var direction = (LinearEnemy.Direction)Random.Range(0, 4);
        SpawnLinearWave(count, direction);

        if (doubleWave)
        {
            SpawnLinearWave(count, WaveFormation.Opposite(direction));
        }
    }

    /// <summary>Spawns the death enemy at a random point off-screen.</summary>
    public void SpawnDeath()
    {
        var death = Instantiate(_deathEnemyPrefab, _levelContainer, true);
        death.GetComponent<Enemy>().SetXpDropParent(_xpDropParentTransform);
        death.transform.position = _spawnArea.RandomPointOutsideViewport();
    }

    private void SpawnLinearWave(int count, LinearEnemy.Direction direction)
    {
        var initialPosition =
            Player.Instance.transform.position
            + _waveFormation.LinearWaveStartOffset(count, direction);
        var step = WaveFormation.LinearWaveStep(direction);

        for (var i = 0; i < count; i++)
        {
            var enemy = InstantiateEnemy(_linearEnemyPrefab);

            // from initial position, fan out enemies
            enemy.transform.position = initialPosition + i * step;
            enemy.GetComponent<LinearEnemy>().SetDirection(direction);
        }
    }

    private void SpawnFannedOut(GameObject prefab, int count)
    {
        var initialPosition = _spawnArea.RandomPointOutsideViewport();

        for (var i = 0; i < count; i++)
        {
            var enemy = InstantiateEnemy(prefab);

            // from initial position, fan out enemies to the left and right
            enemy.transform.position =
                initialPosition
                + new Vector3(
                    _waveFormation.FanOffsetX(i),
                    Random.Range(0, _waveFormation.FanRangeY),
                    0
                );
        }
    }

    private GameObject InstantiateEnemy(GameObject enemyPrefab)
    {
        var enemy = Instantiate(enemyPrefab, _enemiesParentTransform, true);

        var enemyComponent = enemy.GetComponent<Enemy>();
        enemyComponent.hitpoints += _difficulty.EnemyHitPointModifier;

        // Spawned prefabs cannot be wired in the inspector, so the spawner hands over the
        // scene reference it already holds rather than each enemy searching for it.
        enemyComponent.SetXpDropParent(_xpDropParentTransform);

        return enemy;
    }

    private GameObject PrefabFor(DifficultyCurve.EnemyType type)
    {
        switch (type)
        {
            case DifficultyCurve.EnemyType.Sentaur:
                return _sentaurEnemyPrefab;
            case DifficultyCurve.EnemyType.Ant:
                return _antEnemyPrefab;
            case DifficultyCurve.EnemyType.RandomHead:
                return _randomEnemyPrefab;
            case DifficultyCurve.EnemyType.DiagonalHead:
                return _headEnemyPrefab;
            case DifficultyCurve.EnemyType.Mantis:
                return _mantisEnemyPrefab;
            case DifficultyCurve.EnemyType.LinearHead:
                return _linearEnemyPrefab;
            default:
                throw new System.Exception("EnemySpawner.PrefabFor: Invalid enemy type");
        }
    }
}
