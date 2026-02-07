using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChainLightningEffect : MonoBehaviour
{
    private float _duration;
    private int _damage;
    private float _fireInterval;
    private int _chainCount;
    private float _detectRadius;
    private float _chainJumpRange;

    private float _expirationTime;
    private Coroutine _effectCoroutine;

    [SerializeField]
    [Tooltip("Optional LineRenderer to show lightning bolt (child object).")]
    private LineRenderer _lineRenderer;

    [SerializeField]
    [Tooltip("How long the lightning line is visible in seconds.")]
    private float _lightningDisplayDuration = 0.15f;

    /// <summary>
    /// Configure the effect. Call after instantiating, before the effect runs.
    /// </summary>
    public void Initialize(
        float duration,
        int damage,
        float fireInterval,
        int chainCount,
        float detectRadius,
        float chainJumpRange)
    {
        _duration = duration;
        _damage = damage;
        _fireInterval = fireInterval;
        _chainCount = chainCount;
        _detectRadius = detectRadius;
        _chainJumpRange = chainJumpRange;
    }

    /// <summary>
    /// Add more time to the effect (e.g. when same pickup is collected again).
    /// </summary>
    public void ExtendDuration(float extraSeconds)
    {
        _expirationTime += extraSeconds;
    }

    private void Start()
    {
        _expirationTime = Time.time + _duration;
        _effectCoroutine = StartCoroutine(EffectLoop());
    }

    private void OnDisable()
    {
        if (_effectCoroutine != null)
        {
            StopCoroutine(_effectCoroutine);
        }
    }

    private IEnumerator EffectLoop()
    {
        // Wait for first strike after one interval (or strike immediately - plan says "each fireInterval")
        yield return new WaitForSeconds(_fireInterval);

        while (Time.time < _expirationTime)
        {
            StrikeChainLightning();
            yield return new WaitForSeconds(_fireInterval);
        }

        Destroy(gameObject);
    }

    private void StrikeChainLightning()
    {
        Transform playerTransform = transform.parent != null ? transform.parent : Player.Instance.transform;
        Vector3 origin = playerTransform.position;

        List<Enemy> chain = BuildChain(origin);
        if (chain.Count == 0)
            return;

        foreach (Enemy enemy in chain)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                enemy.TakeDamage(_damage);
                enemy.Flash();
                if (SoundManager.Instance != null)
                    SoundManager.Instance.PlayHitSound();
            }
        }

        ShowLightningBolt(origin, chain);
    }

    private List<Enemy> BuildChain(Vector3 origin)
    {
        List<Enemy> chain = new List<Enemy>();
        List<Enemy> inRange = GetEnemiesInRange(origin, _detectRadius);
        Enemy current = GetClosestEnemy(inRange, origin);

        if (current == null)
            return chain;

        chain.Add(current);
        Vector3 currentPos = current.transform.position;

        while (chain.Count < _chainCount)
        {
            inRange = GetEnemiesInRange(currentPos, _chainJumpRange);
            Enemy next = GetClosestEnemyExcluding(inRange, currentPos, chain);
            if (next == null)
                break;
            chain.Add(next);
            currentPos = next.transform.position;
        }

        return chain;
    }

    private List<Enemy> GetEnemiesInRange(Vector3 position, float range)
    {
        List<Enemy> result = new List<Enemy>();
        RaycastHit2D[] hits = Physics2D.CircleCastAll(position, range, Vector2.zero);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || !hit.collider.gameObject.CompareTag("Enemy"))
                continue;

            var enemy = hit.collider.gameObject.GetComponent<Enemy>();
            if (enemy != null && enemy.gameObject.activeInHierarchy)
                result.Add(enemy);
        }

        return result;
    }

    private Enemy GetClosestEnemy(List<Enemy> enemies, Vector3 fromPosition)
    {
        if (enemies == null || enemies.Count == 0)
            return null;

        Enemy closest = null;
        float minSqrDist = float.MaxValue;

        foreach (Enemy e in enemies)
        {
            if (e == null || !e.gameObject.activeInHierarchy)
                continue;

            float sqrDist = (e.transform.position - fromPosition).sqrMagnitude;
            if (sqrDist < minSqrDist)
            {
                minSqrDist = sqrDist;
                closest = e;
            }
        }

        return closest;
    }

    private Enemy GetClosestEnemyExcluding(List<Enemy> enemies, Vector3 fromPosition, List<Enemy> exclude)
    {
        if (enemies == null || enemies.Count == 0)
            return null;

        var excludeSet = new HashSet<Enemy>(exclude);
        Enemy closest = null;
        float minSqrDist = float.MaxValue;

        foreach (Enemy e in enemies)
        {
            if (e == null || !e.gameObject.activeInHierarchy || excludeSet.Contains(e))
                continue;

            float sqrDist = (e.transform.position - fromPosition).sqrMagnitude;
            if (sqrDist < minSqrDist)
            {
                minSqrDist = sqrDist;
                closest = e;
            }
        }

        return closest;
    }

    private void ShowLightningBolt(Vector3 origin, List<Enemy> chain)
    {
        if (_lineRenderer == null || chain.Count == 0)
            return;

        StartCoroutine(ShowLightningCoroutine(origin, chain));
    }

    private IEnumerator ShowLightningCoroutine(Vector3 origin, List<Enemy> chain)
    {
        _lineRenderer.positionCount = chain.Count + 1;
        _lineRenderer.SetPosition(0, origin);

        for (int i = 0; i < chain.Count; i++)
        {
            if (chain[i] != null && chain[i].gameObject.activeInHierarchy)
                _lineRenderer.SetPosition(i + 1, chain[i].transform.position);
            else
                _lineRenderer.SetPosition(i + 1, i > 0 ? _lineRenderer.GetPosition(i) : origin);
        }

        _lineRenderer.enabled = true;
        yield return new WaitForSeconds(_lightningDisplayDuration);
        if (_lineRenderer != null)
            _lineRenderer.enabled = false;
    }
}
