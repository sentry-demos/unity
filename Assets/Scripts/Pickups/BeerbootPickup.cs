using System.Collections;
using UnityEngine;

public class BeerbootPickup : PickupBase
{
    [SerializeField] private float _cooldownModifier = 1.0f;

    protected override void OnCollect(Player player)
    {
        var stats = Player.Instance.WeaponManager.Stats;
        var currentEffectCooldownModifier = stats.EffectCooldownModifier;
        stats.EffectCooldownModifier = _cooldownModifier;

        // Start coroutine on Player so it survives this pickup being destroyed
        Player.Instance.StartCoroutine(ResetEffectCooldown(currentEffectCooldownModifier));
    }

    private IEnumerator ResetEffectCooldown(float cooldownModifier)
    {
        yield return new WaitForSeconds(_effectDuration);

        Player.Instance.WeaponManager.Stats.EffectCooldownModifier = cooldownModifier;
    }

    protected override string GetEffectText()
    {
        return $"+{_cooldownModifier}x speed!";
    }
}
