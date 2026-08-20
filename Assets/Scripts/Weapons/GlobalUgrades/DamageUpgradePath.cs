using UnityEngine;

public class DamageUpgradePath : UpgradePathBase
{
    [SerializeField]
    private string[] _descriptions = { "+30% damage", "+25% damage", "+25% damage!" };
    protected override string[] Descriptions => _descriptions;

    [SerializeField]
    private float[] _damageModifiersPerLevel = { 1.3f, 1.25f, 1.25f };

    public override void UpgradeToLevel(int level)
    {
        if (level < 1 || level > _damageModifiersPerLevel.Length)
        {
            return;
        }

        Player.Instance.WeaponManager.Stats.ApplyDamageMultiplier(
            _damageModifiersPerLevel[level - 1]
        );
    }
}
