using System;
using NUnit.Framework;

public class LevelProgressionTests
{
    private static readonly int[] Milestones = { 50, 150, 300 };

    private static LevelProgression Create() => new LevelProgression(Milestones);

    [Test]
    public void StartsAtLevelZeroTargetingTheFirstMilestone()
    {
        var progression = Create();

        Assert.AreEqual(0, progression.CurrentLevel);
        Assert.AreEqual(0f, progression.Xp);
        Assert.AreEqual(50, progression.NextLevelXpMilestone);
        Assert.IsFalse(progression.IsMaxLevel);
    }

    [Test]
    public void RejectsAnEmptyMilestoneTable()
    {
        Assert.Throws<ArgumentException>(() => new LevelProgression(new int[0]));
        Assert.Throws<ArgumentException>(() => new LevelProgression(null));
    }

    [Test]
    public void DoesNotLevelUpBelowTheMilestone()
    {
        var progression = Create();

        progression.AddXp(49);

        Assert.IsFalse(progression.TryLevelUp());
        Assert.AreEqual(0, progression.CurrentLevel);
    }

    [Test]
    public void LevelsUpOnReachingTheMilestoneExactly()
    {
        var progression = Create();

        progression.AddXp(50);

        Assert.IsTrue(progression.TryLevelUp());
        Assert.AreEqual(1, progression.CurrentLevel);
        Assert.AreEqual(150, progression.NextLevelXpMilestone);
    }

    [Test]
    public void ConsumesOneLevelPerCallSoTheCallerCanDriveTheUiEachTime()
    {
        var progression = Create();

        // enough for two milestones at once
        progression.AddXp(150);

        Assert.IsTrue(progression.TryLevelUp());
        Assert.AreEqual(1, progression.CurrentLevel);

        Assert.IsTrue(progression.TryLevelUp());
        Assert.AreEqual(2, progression.CurrentLevel);

        Assert.IsFalse(progression.TryLevelUp());
    }

    [Test]
    public void StopsAtTheLastMilestoneInsteadOfIndexingPastIt()
    {
        var progression = Create();

        progression.AddXp(10000);

        while (progression.TryLevelUp())
        {
        }

        Assert.AreEqual(Milestones.Length, progression.CurrentLevel);
        Assert.IsTrue(progression.IsMaxLevel);

        // The original code guarded this with an explicit bounds check in Update; the
        // regression it prevents is an IndexOutOfRangeException on the final level up.
        Assert.DoesNotThrow(() => progression.TryLevelUp());
    }

    [Test]
    public void XpProgressIsMeasuredFromTheStartOfTheCurrentLevel()
    {
        var progression = Create();

        progression.AddXp(25);
        Assert.AreEqual(0.5f, progression.XpProgress, 0.0001f);

        progression.AddXp(25);
        progression.TryLevelUp();

        // level 1 spans 50..150, so 100 xp is halfway
        progression.AddXp(50);
        Assert.AreEqual(0.5f, progression.XpProgress, 0.0001f);
    }

    [Test]
    public void XpProgressIsClampedToTheUnitRange()
    {
        var progression = Create();

        progression.AddXp(10000);

        Assert.AreEqual(1f, progression.XpProgress, 0.0001f);
    }

    [Test]
    public void HonoursAnInspectorSetStartingLevelAndXp()
    {
        var progression = new LevelProgression(Milestones, startingLevel: 2, startingXp: 200f);

        Assert.AreEqual(2, progression.CurrentLevel);
        Assert.AreEqual(200f, progression.Xp);
    }

    [Test]
    public void SeedsTheMilestoneWindowToMatchAStartingLevel()
    {
        // Starting at level 2 must target the level-3 milestone. Seeding from milestone[0]
        // instead would report the player as already past it and level them up instantly.
        var progression = new LevelProgression(Milestones, startingLevel: 2, startingXp: 200f);

        Assert.AreEqual(300, progression.NextLevelXpMilestone);
        Assert.IsFalse(progression.TryLevelUp());

        // level 2 spans 150..300, so 200 xp is a third of the way in
        Assert.AreEqual(1f / 3f, progression.XpProgress, 0.0001f);
    }

    [Test]
    public void AStartingLevelAtOrPastTheTableIsAlreadyMaxed()
    {
        var progression = new LevelProgression(Milestones, startingLevel: Milestones.Length);

        Assert.IsTrue(progression.IsMaxLevel);
        Assert.IsFalse(progression.TryLevelUp());
        Assert.AreEqual(1f, progression.XpProgress, 0.0001f);
    }
}
