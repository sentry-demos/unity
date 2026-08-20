using System.Collections;
using UnityEngine;

public class Schnitzel : WeaponBase
{
    [SerializeField]
    private float _speed = 5.0f;

    [SerializeField]
    private float _spawnDistanceOutsidePlayer = 1.25f;

    [SerializeField]
    private float _shootingInterval = 0.25f; // time between consecutive schnitzel

    [SerializeField]
    public float Scale = 1.0f;

    [SerializeField]
    private SchnitzelProjectile _schnitzelProjectilePrefab;

    private Vector3 _shootingDirection = Vector3.right;
    private AutoAim _autoAim;

    private void Start()
    {
        _autoAim = Player.Instance.GetComponent<AutoAim>();
    }

    public override void Fire()
    {
        base.Fire();

        var player = Player.Instance.gameObject;
        StartCoroutine(ShootSchnitzels(player));
    }

    public IEnumerator ShootSchnitzels(GameObject player)
    {
        SchnitzelProjectile schnitzelProjectilePrefab = _schnitzelProjectilePrefab;
        _shootingDirection = CalculateDirection();

        // shoot the base number of schnitzel
        for (int i = 0; i < Count; i++)
        {
            ShootOneSchnitzel(schnitzelProjectilePrefab, player, _shootingDirection);

            yield return new WaitForSeconds(_shootingInterval);
            // re-aim between shots, so a burst tracks a target that is still moving
            _shootingDirection = CalculateDirection();
        }

        // reset cooldown after all schnitzels fired
        ResetCooldown();

        yield return null;
    }

    /// <summary>Where to fire, holding the last direction until the auto-aim is available.</summary>
    private Vector3 CalculateDirection()
    {
        return _autoAim != null ? _autoAim.AimDirection : _shootingDirection;
    }

    private void ShootOneSchnitzel(SchnitzelProjectile prefab, GameObject player, Vector3 direction)
    {
        SchnitzelProjectile schnitzel = Instantiate(prefab);
        schnitzel.Initialize(Damage, _speed);
        schnitzel.transform.parent = player.transform.parent;

        // initial position
        schnitzel.transform.position =
            player.transform.position + direction.normalized * _spawnDistanceOutsidePlayer;

        // adjust scale
        schnitzel.transform.localScale = new Vector3(Scale, Scale, Scale);

        schnitzel.SetDirection(direction);
    }
}
