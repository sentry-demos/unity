using System.Collections.Generic;
using NUnit.Framework;

public class UpgradeManagerTests
{
    // Always draws index 0, so the result is the pool in order -- enough to prove the
    // draw is without replacement.
    private static int First(int max) => 0;

    // Always draws the last entry, exercising the other end of the index range.
    private static int Last(int max) => max - 1;

    private static List<string> Pool() => new List<string> { "a", "b", "c", "d" };

    [Test]
    public void DrawsTheRequestedNumberOfEntries()
    {
        var chosen = UpgradeManager.PickDistinct(Pool(), 2, First);

        Assert.AreEqual(2, chosen.Count);
    }

    [Test]
    public void NeverRepeatsAnEntry()
    {
        var chosen = UpgradeManager.PickDistinct(Pool(), 4, First);

        CollectionAssert.AllItemsAreUnique(chosen);
        CollectionAssert.AreEquivalent(Pool(), chosen);
    }

    [Test]
    public void RemovesTheDrawnEntryFromTheRemainingPool()
    {
        // Drawing index 0 four times can only yield the pool in order if each draw removes
        // what it took.
        var chosen = UpgradeManager.PickDistinct(Pool(), 4, First);

        CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, chosen);
    }

    [Test]
    public void IndexesAgainstTheShrinkingPoolNotTheOriginal()
    {
        var chosen = UpgradeManager.PickDistinct(Pool(), 4, Last);

        CollectionAssert.AreEqual(new[] { "d", "c", "b", "a" }, chosen);
    }

    [Test]
    public void ReturnsTheWholePoolWhenAskedForMoreThanItHolds()
    {
        // The level-up UI asks for 2; late in a run the pool can be down to 1 maxed-out
        // path, and it must not loop forever or throw.
        var chosen = UpgradeManager.PickDistinct(Pool(), 99, First);

        Assert.AreEqual(4, chosen.Count);
    }

    [Test]
    public void ReturnsEmptyForAnExhaustedPool()
    {
        var chosen = UpgradeManager.PickDistinct(new List<string>(), 2, First);

        Assert.IsEmpty(chosen);
    }

    [Test]
    public void ReturnsEmptyForANonPositiveCount()
    {
        Assert.IsEmpty(UpgradeManager.PickDistinct(Pool(), 0, First));
        Assert.IsEmpty(UpgradeManager.PickDistinct(Pool(), -1, First));
    }

    [Test]
    public void LeavesTheSourcePoolUntouched()
    {
        var pool = Pool();

        UpgradeManager.PickDistinct(pool, 3, First);

        CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, pool);
    }
}
