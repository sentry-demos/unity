using UnityEngine;

internal class CooldownUpgradePath : UpgradePathBase
{
    [SerializeField]
    private string[] _descriptions =
    {
        "-20% cooldown time",
        "-25% cooldown time",
        "-50% cooldown time!"
    };

    protected override string[] Descriptions => _descriptions;

    [SerializeField]
    private float[] _cooldownModifiersPerLevel = { 0.8f, 0.75f, 0.5f };

    public override void UpgradeToLevel(int level)
    {
        if (level < 1 || level > _cooldownModifiersPerLevel.Length)
        {
            return;
        }

        Player.Instance.WeaponManager.Stats.ApplyCooldownMultiplier(
            _cooldownModifiersPerLevel[level - 1]
        );
    }
}
