using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Picks what the directed weapons shoot at, so the player never aims by hand.
/// </summary>
/// <remarks>
/// <para>
/// Nearest-enemy alone takes all control away: it locks onto whatever happens to be
/// closest, which is regularly the mob the player is trying to run away from. So the
/// score weights the direction the player is running in as well as the distance --
/// steering becomes the aim, and the player can pick a target by heading towards it.
/// </para>
/// <para>
/// Scoring, per candidate: <c>(1 - distance / range) * (1 + bias * dot(moveDir, toEnemy))</c>.
/// The dot product runs -1 behind to +1 dead ahead, so with the default bias an enemy the
/// player is running at outscores a somewhat closer one at their back. Standing still
/// zeroes the movement term and the whole thing degrades to nearest-enemy.
/// </para>
/// <para>
/// One scan per <see cref="_retargetInterval"/> serves every weapon that asks, and the
/// previous target keeps a small bonus so aim doesn't flicker between two near-equal
/// candidates mid-burst.
/// </para>
/// </remarks>
public class AutoAim : MonoBehaviour
{
    [SerializeField]
    [Tooltip("How far from the player to look for targets")]
    private float _range = 12.0f;

    [SerializeField]
    [Range(0.0f, 1.0f)]
    [Tooltip("How much the direction the player is running in outweighs plain distance. "
        + "0 is nearest-enemy; 1 all but ignores anything behind the player.")]
    private float _movementBias = 0.6f;

    [SerializeField]
    [Range(1.0f, 2.0f)]
    [Tooltip("Score bonus the current target keeps, to stop the aim flickering between "
        + "two enemies that score about the same")]
    private float _targetStickiness = 1.15f;

    [SerializeField]
    [Tooltip("Seconds between target scans. Aiming every frame is neither cheap nor steady.")]
    private float _retargetInterval = 0.1f;

    [SerializeField]
    [Tooltip("Below this speed the player counts as standing still and only distance matters")]
    private float _minimumSpeed = 0.1f;

    /// <summary>The enemy currently aimed at, or null when none is in range.</summary>
    public Transform CurrentTarget { get; private set; }

    private Enemy _currentTargetEnemy;

    /// <summary>
    /// Where the directed weapons should fire, normalised. Holds its last value while no
    /// enemy is in range, so weapons firing into an empty field keep shooting the way the
    /// player was last aiming rather than snapping to zero.
    /// </summary>
    /// <remarks>
    /// Rescans on the spot if the target died since the last scan. Bursts read this between
    /// shots from a coroutine, which resumes independently of <c>Update</c>, so validating on
    /// read is what actually guarantees no shot is aimed at a corpse.
    /// </remarks>
    public Vector3 AimDirection
    {
        get
        {
            if (!IsTargetAlive())
            {
                _timeSinceLastScan = 0.0f;
                Scan();
            }

            return _aimDirection;
        }
    }

    private Vector3 _aimDirection = Vector3.right;

    /// <summary>
    /// Where a rear-firing weapon should shoot: the best target on the far side of the player
    /// from <see cref="AimDirection"/>, so the backwards shot covers the ground the player is
    /// leaving rather than firing at nothing.
    /// </summary>
    /// <remarks>
    /// Straight opposite the aim is what a rear shot used to mean, back when the player picked
    /// the axis with the mouse and lined both ends up themselves. With the aim chosen for them
    /// the negated direction is just wherever the front target is not, so this picks a real
    /// target instead -- and only falls back to the negation when the field behind is empty.
    /// </remarks>
    public Vector3 RearAimDirection
    {
        get
        {
            if (!IsTargetAlive() || !IsAlive(_rearTarget, _rearTargetEnemy))
            {
                _timeSinceLastScan = 0.0f;
                Scan();
            }

            return _rearAimDirection;
        }
    }

    private Vector3 _rearAimDirection = Vector3.left;
    private Transform _rearTarget;
    private Enemy _rearTargetEnemy;

    private Rigidbody2D _rigidBody;
    private float _timeSinceLastScan;

    // Reused across scans; the count varies per scan, so treat only the returned length as
    // meaningful. Sized well past what fits in _range to keep the scan allocation-free.
    private readonly Collider2D[] _candidates = new Collider2D[128];

    private readonly List<Transform> _rankedTargets = new List<Transform>();
    private readonly List<float> _scores = new List<float>();

    private void Awake()
    {
        _rigidBody = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        _timeSinceLastScan += Time.deltaTime;

        // A dead target is retargeted the same frame it dies. Waiting out the interval would
        // leave the rest of a burst firing at a corpse: an enemy keeps its transform for the
        // whole death animation, so the aim would track it as it shrinks into nothing.
        if (_timeSinceLastScan < _retargetInterval && IsTargetAlive())
        {
            return;
        }

        _timeSinceLastScan = 0.0f;
        Scan();
    }

    /// <summary>Whether the target the aim direction points at is still alive.</summary>
    private bool IsTargetAlive()
    {
        return IsAlive(CurrentTarget, _currentTargetEnemy);
    }

    /// <summary>
    /// Whether <paramref name="target"/> is still worth shooting at. False once it has been
    /// killed or destroyed, and true when there is no target at all -- nothing to invalidate.
    /// </summary>
    private static bool IsAlive(Transform target, Enemy enemy)
    {
        if (target == null)
        {
            return true;
        }

        return enemy != null && !enemy.IsDead;
    }

    /// <summary>
    /// The best <paramref name="count"/> targets by the same score the aim direction uses,
    /// closest-scoring first. Fewer than asked for when the field is thin, and empty when
    /// nothing is in range.
    /// </summary>
    /// <remarks>
    /// Rescans on demand rather than serving the cached ranking: weapons calling this fire
    /// on their own cooldown, which drifts against <see cref="_retargetInterval"/>.
    /// </remarks>
    public IReadOnlyList<Transform> GetTargets(int count)
    {
        Scan();
        _timeSinceLastScan = 0.0f;

        if (count < _rankedTargets.Count)
        {
            _rankedTargets.RemoveRange(count, _rankedTargets.Count - count);
        }

        return _rankedTargets;
    }

    /// <summary>
    /// Ranks everything in range by score, then points the aim at the winner.
    /// </summary>
    private void Scan()
    {
        _rankedTargets.Clear();

        var position = transform.position;
        var movementDirection = GetMovementDirection();
        var hitCount = Physics2D.OverlapCircleNonAlloc(position, _range, _candidates);

        // Insertion sort: the candidate list is short and mostly ordered from the previous
        // scan, and reusing the score buffer keeps the ranking allocation-free.
        _scores.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            var candidate = _candidates[i];
            if (candidate == null || !candidate.CompareTag(Tags.Enemy))
            {
                continue;
            }

            // Skip anything already dying. Its colliders are disabled the moment it dies, so
            // it usually will not be found here at all -- but the hitbox sits on a child, so
            // a corpse can still surface through that collider.
            var enemyComponent = candidate.GetComponentInParent<Enemy>();
            if (enemyComponent == null || enemyComponent.IsDead)
            {
                continue;
            }

            var enemy = enemyComponent.transform;
            var score = ScoreTarget(position, movementDirection, enemy);

            int insertAt = _scores.Count;
            while (insertAt > 0 && _scores[insertAt - 1] < score)
            {
                insertAt--;
            }

            _scores.Insert(insertAt, score);
            _rankedTargets.Insert(insertAt, enemy);
        }

        CurrentTarget = _rankedTargets.Count > 0 ? _rankedTargets[0] : null;
        _currentTargetEnemy = CurrentTarget != null
            ? CurrentTarget.GetComponent<Enemy>()
            : null;

        if (CurrentTarget != null)
        {
            _aimDirection = (CurrentTarget.position - position).normalized;
        }

        PickRearTarget(position);
    }

    /// <summary>
    /// Picks the best-scoring target behind the aim, walking the ranking that <see cref="Scan"/>
    /// just built. Leaves the rear aim straight opposite the front one when the field behind is
    /// empty, which is where a rear shot went before there was anything to aim it at.
    /// </summary>
    private void PickRearTarget(Vector3 position)
    {
        _rearTarget = null;
        _rearTargetEnemy = null;
        _rearAimDirection = -_aimDirection;

        // Ranked best-first, so the first one behind the aim is the best one behind the aim.
        for (int i = 0; i < _rankedTargets.Count; i++)
        {
            var toTarget = _rankedTargets[i].position - position;
            if (Vector3.Dot(toTarget, _aimDirection) >= 0.0f)
            {
                continue;
            }

            _rearTarget = _rankedTargets[i];
            _rearTargetEnemy = _rearTarget.GetComponent<Enemy>();
            _rearAimDirection = toTarget.normalized;
            break;
        }
    }

    private float ScoreTarget(Vector3 position, Vector3 movementDirection, Transform enemy)
    {
        var toEnemy = enemy.position - position;
        var distance = toEnemy.magnitude;

        // Nearer scores higher, and anything past the range scores nothing. Enemies land on
        // top of the player often enough that the zero-distance direction has to be handled.
        var distanceFactor = Mathf.Max(0.0f, 1.0f - distance / _range);
        if (distanceFactor <= 0.0f || distance <= Mathf.Epsilon)
        {
            return distanceFactor;
        }

        // Ahead of the player scores up to (1 + bias), behind down to (1 - bias). Zero while
        // standing still, which leaves distance to decide.
        var alignment = Vector3.Dot(movementDirection, toEnemy / distance);
        var directionFactor = 1.0f + _movementBias * alignment;

        var score = distanceFactor * directionFactor;
        if (enemy == CurrentTarget)
        {
            score *= _targetStickiness;
        }

        return score;
    }

    /// <summary>
    /// The direction the player is actually travelling, which is not the direction they are
    /// holding: walls and obstacles stop the body while the stick stays pushed.
    /// </summary>
    private Vector3 GetMovementDirection()
    {
        if (_rigidBody == null)
        {
            return Vector3.zero;
        }

        var velocity = _rigidBody.linearVelocity;
        return velocity.magnitude < _minimumSpeed ? Vector3.zero : ((Vector3)velocity).normalized;
    }
}
