using UnityEngine;

public abstract class WeaponBase : MonoBehaviour
{
    [SerializeField]
    protected bool _isEnabled = false;

    [SerializeField]
    public float BaseCooldown;

    [SerializeField]
    public float BaseDamage;

    private static WeaponStats Stats => Player.Instance.WeaponManager.Stats;

    public float Cooldown => BaseCooldown * Stats.TotalCooldownModifier;
    public int Damage => (int)(Stats.DamageModifier * BaseDamage);
    public int Count => Stats.ProjectileCount;

    protected float _timeElapsedSinceLastFire = 0.0f;

    // Scaled time on purpose: cooldowns must freeze while paused. UI that runs during a
    // pause (the level-up popup) uses WaitForSecondsRealtime instead.
    protected virtual void Update()
    {
        if (_isEnabled)
        {
            _timeElapsedSinceLastFire += Time.deltaTime;
        }
    }

    public virtual bool CanFire()
    {
        return _isEnabled && _timeElapsedSinceLastFire >= Cooldown;
    }

    public virtual void Fire()
    {
        ResetCooldown();
    }

    public virtual void ResetCooldown()
    {
        _timeElapsedSinceLastFire = 0.0f;
    }

    public void Enable()
    {
        _isEnabled = true;
    }
}
