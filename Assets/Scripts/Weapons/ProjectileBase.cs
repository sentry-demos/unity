using System.Collections.Generic;
using UnityEngine;

public class ProjectileBase : MonoBehaviour
{
    // Reused across every projectile: the non-allocating CalculateFrustumPlanes overload
    // needs a six-element buffer, and the results are consumed before the next call.
    private static readonly Plane[] _frustumPlanes = new Plane[6];

    // Camera.main is a tagged search on every call. Projectiles leave the screen constantly,
    // so it is resolved once and re-resolved only if the camera goes away.
    private static Camera _gameCamera;

    protected Rigidbody2D _rigidbody2D;
    private Renderer _renderer;

    protected void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _renderer = GetComponentInChildren<Renderer>();
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.TryGetComponent<Hitbox>(out var hitbox))
        {
            var enemy = hitbox.Enemy;

            SoundEffects.Instance.PlayHitSound();

            OnDamage(enemy);
            OnHit();
        }
        else if (other.gameObject.CompareTag(Tags.Barrier))
        {
            OnHit();
        }
    }

    protected virtual void OnDamage(Enemy enemy)
    {
        DamageEnemy(enemy);
    }

    protected virtual void OnHit()
    {
        Destroy(gameObject);
    }

    // Deal damage to the enemy because they were hit by a projectile
    protected virtual void DamageEnemy(Enemy enemy) { }

    // Fires for any camera, including the editor's Scene view, so check the game camera.
    private void OnBecameInvisible()
    {
        if (!IsVisibleFromGameCamera())
        {
            Destroy(gameObject);
        }
    }

    private bool IsVisibleFromGameCamera()
    {
        if (_gameCamera == null)
        {
            _gameCamera = Camera.main;
        }

        if (_gameCamera == null)
        {
            return false; // Scene teardown: treat as off-screen so it still gets cleaned up.
        }

        if (_renderer == null)
        {
            // Resolved in Awake, but a subclass that declares its own Awake would hide the
            // base one and leave this unset -- so fall back rather than leak the projectile.
            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer == null)
            {
                return false;
            }
        }

        GeometryUtility.CalculateFrustumPlanes(_gameCamera, _frustumPlanes);
        return GeometryUtility.TestPlanesAABB(_frustumPlanes, _renderer.bounds);
    }

    protected void SplashDamage(
        Vector2 origin,
        float radius,
        int damage,
        HashSet<Enemy> ignoreList = null
    )
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, radius, Vector2.zero);

#if SENTAUR_PERF_METRICS
        // How much this allocating cast is actually returning. Off by default, for the same
        // reason as the AutoAim scan timing: it is per projectile hit.
        GameMetrics.RecordSplashHits(hits.Length);
#endif

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != null && hit.collider.gameObject.CompareTag(Tags.Enemy))
            {
                Enemy enemyTarget = hit.collider.gameObject.GetComponent<Enemy>();
                if (ignoreList == null || !ignoreList.Contains(enemyTarget))
                {
                    enemyTarget.TakeDamage(damage);
                }
            }
        }
    }
}
