using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// The region enemies and pickups spawn in, and the queries against it.
/// </summary>
/// <remarks>
/// Shared by <see cref="EnemySpawner"/> and <see cref="PickupSpawner"/>: pickups drop anywhere
/// in the area, enemies only outside the viewport. Owning the spawn plane here keeps the two
/// spawners independent of each other.
/// </remarks>
public class SpawnArea : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The plane to spawn enemies and pickups on")]
    private GameObject _spawnPlane;

    /// <summary>A random position anywhere within the spawn area.</summary>
    public Vector3 RandomPoint()
    {
        var rectTransform = _spawnPlane.GetComponent<RectTransform>();
        var worldBounds = new Bounds(rectTransform.position, rectTransform.rect.size);

        return new Vector3(
            Random.Range(worldBounds.min.x, worldBounds.max.x),
            Random.Range(worldBounds.min.y, worldBounds.max.y),
            0.0f
        );
    }

    /// <summary>
    /// A random position within the spawn area but outside the camera's view, so enemies
    /// walk on rather than appearing in front of the player.
    /// </summary>
    public Vector3 RandomPointOutsideViewport()
    {
        var viewportBounds = ViewportBounds();

        Vector3 point;
        do
        {
            point = RandomPoint();
        } while (viewportBounds.Contains(point));

        return point;
    }

    /// <summary>The camera viewport as a bounding box in world space.</summary>
    private static Bounds ViewportBounds()
    {
        // viewport coordinates run (0, 0) bottom-left to (1, 1) top-right
        var center = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0.0f));
        center.z = 0; // reset z to 0 so on same plane as enemies

        var bottomLeft = Camera.main.ViewportToWorldPoint(new Vector3(0f, 0f, 0.0f));
        var topRight = Camera.main.ViewportToWorldPoint(new Vector3(1.0f, 1.0f, 0.0f));

        var extents = new Vector3(topRight.x - bottomLeft.x, topRight.y - bottomLeft.y, 0f);

        return new Bounds(center, extents);
    }
}
