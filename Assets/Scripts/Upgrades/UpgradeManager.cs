using System.Collections.Generic;
using Sentry;
using UnityEngine;

internal class UpgradeManager : SceneSingleton<UpgradeManager>
{
    private List<UpgradePathBase> _availableUpgrades = new List<UpgradePathBase>();

    protected override void Awake()
    {
        base.Awake();

        _availableUpgrades.AddRange(GetComponentsInChildren<UpgradePathBase>());

        // dart starts at level 1
        GetComponentInChildren<DartUpgradePath>()
            .LevelUp();
    }

    /**
      * Returns up to count distinct random upgrade paths from the available pool
      */
    public List<UpgradePathBase> GetRandomUpgradePaths(int count) =>
        PickDistinct(_availableUpgrades, count, max => Random.Range(0, max));

    /**
      * Draws count distinct entries from pool without replacement, or the whole pool if it
      * holds fewer. The source is left untouched.
      *
      * Static and index-source-injected so the draw can be tested with a deterministic
      * sequence instead of UnityEngine.Random.
      */
    public static List<T> PickDistinct<T>(List<T> pool, int count, System.Func<int, int> nextIndex)
    {
        var remaining = new List<T>(pool);
        var chosen = new List<T>();

        while (chosen.Count < count && remaining.Count > 0)
        {
            var option = nextIndex(remaining.Count);
            chosen.Add(remaining[option]);
            remaining.RemoveAt(option);
        }

        return chosen;
    }

    public void LevelUpUpgradePath(UpgradePathBase upgradePath)
    {
        upgradePath.LevelUp(); // level up the selected upgrade

        if (upgradePath.IsMaxLevel())
        {
            // take the upgrade out of the pool if it's maxed out
            _availableUpgrades.Remove(upgradePath);
        }
    }
}
