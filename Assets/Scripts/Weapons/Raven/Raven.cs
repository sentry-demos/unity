using UnityEngine;

public class Raven : WeaponBase
{
    [SerializeField]
    [Tooltip("Projectile speed as it leaves player")]
    private float _speed = 12.0f;

    [SerializeField]
    [Tooltip("Modifies AOE of emitted projectiles")]
    public float AreaOfEffectRadius = 1.0f;

    [SerializeField]
    [Tooltip("Distance from player to spawn raven")]
    private float _spawnDistanceOutsidePlayer = 1.25f;

    [SerializeField]
    private RavenProjectile _ravenProjectilePrefab;

    private GameObject _player;
    private AutoAim _autoAim;

    public void Start()
    {
        _player = Player.Instance.gameObject;
        _autoAim = Player.Instance.GetComponent<AutoAim>();
    }

    public override void Fire()
    {
        base.Fire();

        if (_autoAim == null)
        {
            return;
        }

        // Shares the auto-aim's ranking, so every weapon agrees on which enemies are worth
        // hitting -- the ravens spread over the best Count of them rather than the nearest.
        var targets = _autoAim.GetTargets(Count);

        foreach (var target in targets)
        {
            var projectile = Instantiate(_ravenProjectilePrefab);

            projectile.Initialize(Damage, _speed, AreaOfEffectRadius);
            projectile.transform.parent = _player.transform.parent;

            Vector3 direction = target.transform.position - _player.transform.position;
            projectile.SetDirection(direction);

            // initial position
            projectile.transform.position =
                _player.transform.position + direction.normalized * _spawnDistanceOutsidePlayer;
        }
    }
}
