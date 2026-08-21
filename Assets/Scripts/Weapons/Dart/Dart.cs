using System.Collections;
using UnityEngine;

public class Dart : WeaponBase
{
    [SerializeField]
    private float _speed = 10.0f;

    [SerializeField]
    public int RearFiringDartCount = 0; // NOTE: rear-shooting darts

    [SerializeField]
    private float _spawnDistanceOutsidePlayer = 1.25f;

    [SerializeField]
    private float _shootingInterval = 0.4f; // time between consecutive darts

    [SerializeField]
    private float _areaOfEffectRadius = 0.25f;

    private bool _isShooting = false;
    private GameObject _player;

    [SerializeField]
    private DartProjectile _dartProjectilePrefab;

    private Vector3 _shootingDirection = Vector3.right;
    private AutoAim _autoAim;

    // Override mechanism for external shooting direction control
    private bool _hasExternalShootingDirection = false;
    private Vector3 _externalShootingDirection;

    public void Start()
    {
        _isEnabled = true;
        _player = Player.Instance.gameObject;
        _autoAim = Player.Instance.GetComponent<AutoAim>();
    }

    /// <summary>
    /// Sets the shooting direction externally, overriding <see cref="AutoAim"/>.
    /// </summary>
    /// <param name="direction">The direction to shoot in</param>
    public void SetShootingDirection(Vector3 direction)
    {
        _hasExternalShootingDirection = true;
        _externalShootingDirection = direction.normalized;
    }

    /// <summary>
    /// Clears the external shooting direction override, returning to <see cref="AutoAim"/>.
    /// </summary>
    public void ClearShootingDirectionOverride()
    {
        _hasExternalShootingDirection = false;
    }

    /// <summary>Where to fire: the demo override if one is set, otherwise the auto-aim.</summary>
    private Vector3 CalculateDirection()
    {
        if (_hasExternalShootingDirection)
        {
            return _externalShootingDirection;
        }

        return _autoAim != null ? _autoAim.AimDirection : _shootingDirection;
    }

    /// <summary>
    /// Where the rear-firing darts go. The auto-aim picks a target behind the player rather
    /// than the bare opposite of the forward shot, which lands in empty space now that the
    /// player no longer chooses the axis themselves.
    /// </summary>
    private Vector3 CalculateRearDirection()
    {
        if (_hasExternalShootingDirection)
        {
            return -_externalShootingDirection;
        }

        return _autoAim != null ? _autoAim.RearAimDirection : -_shootingDirection;
    }

    public override void Fire()
    {
        base.Fire();

        if (_isShooting)
        {
            // if the dart is already firing, exit early (we only start counting
            // after the dart has CEASED firing)
            return;
        }

        StartCoroutine(ShootDarts());
    }

    public IEnumerator ShootDarts()
    {
        _isShooting = true;

        // shoot the base number of darts
        for (int i = 0; i < Count; i++)
        {
            // re-aim between darts, so a burst tracks a target that is still moving
            _shootingDirection = CalculateDirection();

            ShootADart(_dartProjectilePrefab, _player, _shootingDirection);
            if (RearFiringDartCount > i)
            {
                ShootADart(_dartProjectilePrefab, _player, CalculateRearDirection());
            }

            yield return new WaitForSeconds(_shootingInterval);
        }

        // accounting for case where # of backwards darts > # of forwards darts
        int remainingDarts = RearFiringDartCount - Count;
        for (int i = 0; i < remainingDarts; i++)
        {
            // re-aim between darts here too, for the same reason as the burst above
            ShootADart(_dartProjectilePrefab, _player, CalculateRearDirection());
            yield return new WaitForSeconds(_shootingInterval);
        }

        // reset cooldown after all darts have been shot
        ResetCooldown();

        _isShooting = false;
        yield return null;
    }

    private void ShootADart(DartProjectile prefab, GameObject player, Vector3 direction)
    {
        DartProjectile dart = Instantiate(prefab);
        dart.Initialize(Damage, _speed, _areaOfEffectRadius);
        dart.transform.parent = player.transform.parent;

        // initial position
        dart.transform.position =
            player.transform.position + direction.normalized * _spawnDistanceOutsidePlayer;

        dart.SetDirection(direction);
    }
}
