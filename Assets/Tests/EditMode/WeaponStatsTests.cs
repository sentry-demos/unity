using NUnit.Framework;

public class WeaponStatsTests
{
    [Test]
    public void StartsUnmodified()
    {
        var stats = new WeaponStats();

        Assert.AreEqual(1f, stats.DamageModifier, 0.0001f);
        Assert.AreEqual(1f, stats.CooldownModifier, 0.0001f);
        Assert.AreEqual(1f, stats.EffectCooldownModifier, 0.0001f);
        Assert.AreEqual(1, stats.ProjectileCount);
    }

    [Test]
    public void DamageMultipliersCompound()
    {
        var stats = new WeaponStats();

        stats.ApplyDamageMultiplier(1.3f);
        stats.ApplyDamageMultiplier(1.25f);
        stats.ApplyDamageMultiplier(1.25f);

        // the shipped damage path: 1.3 -> 1.625 -> 2.03125
        Assert.AreEqual(2.03125f, stats.DamageModifier, 0.0001f);
    }

    [Test]
    public void CooldownMultipliersCompound()
    {
        var stats = new WeaponStats();

        stats.ApplyCooldownMultiplier(0.8f);
        stats.ApplyCooldownMultiplier(0.75f);
        stats.ApplyCooldownMultiplier(0.5f);

        // the shipped cooldown path: 0.8 -> 0.6 -> 0.3
        Assert.AreEqual(0.3f, stats.CooldownModifier, 0.0001f);
    }

    [Test]
    public void ProjectilesAccumulateFromTheStartingOne()
    {
        var stats = new WeaponStats();

        // the shipped count path is "+1, +1, +2", reaching 2 -> 3 -> 5
        stats.AddProjectiles(1);
        Assert.AreEqual(2, stats.ProjectileCount);

        stats.AddProjectiles(1);
        Assert.AreEqual(3, stats.ProjectileCount);

        stats.AddProjectiles(2);
        Assert.AreEqual(5, stats.ProjectileCount);
    }

    [Test]
    public void TotalCooldownStacksTheTemporaryBuffOnTopOfUpgrades()
    {
        var stats = new WeaponStats();

        stats.ApplyCooldownMultiplier(0.8f);
        stats.EffectCooldownModifier = 0.5f;

        Assert.AreEqual(0.4f, stats.TotalCooldownModifier, 0.0001f);
    }

    [Test]
    public void TheTemporaryBuffIsAssignedNotAccumulated()
    {
        // Pickups save the previous value and restore it when the effect expires, so this one
        // must not compound the way the upgrade modifiers do.
        var stats = new WeaponStats();

        stats.EffectCooldownModifier = 0.5f;
        stats.EffectCooldownModifier = 0.5f;

        Assert.AreEqual(0.5f, stats.EffectCooldownModifier, 0.0001f);
    }

    [Test]
    public void RestoringTheBuffLeavesPermanentUpgradesIntact()
    {
        var stats = new WeaponStats();

        stats.ApplyCooldownMultiplier(0.8f);

        var saved = stats.EffectCooldownModifier;
        stats.EffectCooldownModifier = 0.25f;
        stats.EffectCooldownModifier = saved;

        Assert.AreEqual(0.8f, stats.TotalCooldownModifier, 0.0001f);
    }
}
