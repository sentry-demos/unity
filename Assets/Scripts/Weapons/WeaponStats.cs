/// <summary>
/// The player's global weapon modifiers: the accumulated effect of every upgrade taken this
/// run, plus any temporary pickup buff.
/// </summary>
/// <remarks>
/// Plain C# so the stat math is testable without a scene. Values are read-only from the
/// outside and change only through the named methods, which is what stops an upgrade from
/// assigning where it meant to multiply -- the bug that had
/// <c>ProjectileCountUpgradePath</c> setting an absolute 5 for a "+2" description.
/// </remarks>
public class WeaponStats
{
    /// <summary>Multiplies weapon base damage. 1 = unmodified.</summary>
    public float DamageModifier { get; private set; } = 1.0f;

    /// <summary>Multiplies weapon cooldown. Below 1 = faster.</summary>
    public float CooldownModifier { get; private set; } = 1.0f;

    /// <summary>How many projectiles each weapon fires.</summary>
    public int ProjectileCount { get; private set; } = 1;

    /// <summary>
    /// A temporary cooldown multiplier from a pickup, stacked on top of
    /// <see cref="CooldownModifier"/>. Unlike the others this is not permanent -- it is set
    /// for a duration and then restored, so it is assigned rather than accumulated.
    /// </summary>
    public float EffectCooldownModifier { get; set; } = 1.0f;

    /// <summary>The cooldown multiplier actually applied to a weapon: upgrades and buff.</summary>
    public float TotalCooldownModifier => CooldownModifier * EffectCooldownModifier;

    public void ApplyDamageMultiplier(float multiplier)
    {
        DamageModifier *= multiplier;
    }

    public void ApplyCooldownMultiplier(float multiplier)
    {
        CooldownModifier *= multiplier;
    }

    public void AddProjectiles(int count)
    {
        ProjectileCount += count;
    }
}
