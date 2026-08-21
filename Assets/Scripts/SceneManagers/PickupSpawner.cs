using UnityEngine;

/// <summary>
/// Instantiates pickups at random points in the spawn area.
/// </summary>
/// <remarks>
/// Driven by <see cref="BattleSceneManager"/> rather than running its own clock, for the same
/// reason as <see cref="EnemySpawner"/>.
/// </remarks>
public class PickupSpawner : MonoBehaviour
{
    [SerializeField]
    [Tooltip("List of pickup prefabs to randomly spawn")]
    private GameObject[] _pickupPrefabs;

    [SerializeField]
    [Tooltip("Where to spawn -- shared with the enemy spawner")]
    private SpawnArea _spawnArea;

    [SerializeField]
    [Tooltip("Parent transform that spawned pickups are placed under")]
    private Transform _pickupParentTransform;

    private static readonly System.Random Random = new System.Random();

    /// <summary>
    /// How many pickups are currently on screen. Derived from the parent's child count, not
    /// tracked: a counter leaks a slot whenever a pickup is destroyed without being collected.
    /// Destroy() lags a frame, which the spawn interval absorbs.
    /// </summary>
    public int OnScreen => _pickupParentTransform.childCount;

    public void Spawn()
    {
        var index = Random.Next(_pickupPrefabs.Length);
        var pickup = Instantiate(_pickupPrefabs[index], _pickupParentTransform, true);

        pickup.transform.position = _spawnArea.RandomPoint();
    }
}
