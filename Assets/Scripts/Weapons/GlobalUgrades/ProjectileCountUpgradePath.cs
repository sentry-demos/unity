using UnityEngine;

internal class ProjectileCountUpgradePath : UpgradePathBase
{
    [SerializeField]
    private string[] _descriptions =
    {
        "+1 of each projectile",
        "+1 of each projectile",
        "+2 of each projectile!"
    };
    protected override string[] Descriptions => _descriptions;

    // Additive, matching the descriptions. These used to be absolute assignments
    // (2, 3, 5) that only produced the advertised "+1, +1, +2" because the modifier
    // happens to start at 1 -- and the level-3 text said "+2" while assigning 5.
    [SerializeField]
    private int[] _projectilesAddedPerLevel = { 1, 1, 2 };

    public override void UpgradeToLevel(int level)
    {
        if (level < 1 || level > _projectilesAddedPerLevel.Length)
        {
            return;
        }

        Player.Instance.WeaponManager.Stats.AddProjectiles(_projectilesAddedPerLevel[level - 1]);
    }
}
