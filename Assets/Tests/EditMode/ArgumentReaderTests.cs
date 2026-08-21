using NUnit.Framework;

public class ArgumentReaderTests
{
    [Test]
    public void ReadsTheValueFollowingAName()
    {
        var args = new[] { "game.exe", "-dsn", "https://example.com", "-demo" };

        Assert.AreEqual("https://example.com", ArgumentReader.GetArg(args, "dsn"));
    }

    [Test]
    public void ReturnsNullForAnAbsentName()
    {
        var args = new[] { "game.exe", "-demo" };

        Assert.IsNull(ArgumentReader.GetArg(args, "dsn"));
    }

    [Test]
    public void ReturnsNullWhenTheNameIsLastAndHasNoValue()
    {
        // The trailing-argument guard: without it this indexes past the end of the array.
        var args = new[] { "game.exe", "-dsn" };

        Assert.IsNull(ArgumentReader.GetArg(args, "dsn"));
    }

    [Test]
    public void MatchesOnTheLeadingDashOnly()
    {
        var args = new[] { "game.exe", "dsn", "https://example.com" };

        Assert.IsNull(ArgumentReader.GetArg(args, "dsn"));
    }

    [Test]
    public void ReturnsTheFirstOccurrenceWhenANameRepeats()
    {
        var args = new[] { "game.exe", "-dsn", "first", "-dsn", "second" };

        Assert.AreEqual("first", ArgumentReader.GetArg(args, "dsn"));
    }

    [Test]
    public void TreatsAFollowingFlagAsTheValue()
    {
        // Documents current behaviour rather than endorsing it: the reader does not know
        // which names take values, so "-demo" is consumed as the value of "-dsn".
        var args = new[] { "game.exe", "-dsn", "-demo" };

        Assert.AreEqual("-demo", ArgumentReader.GetArg(args, "dsn"));
    }

    [Test]
    public void DetectsAFlagAnywhereInTheArguments()
    {
        var args = new[] { "game.exe", "-batchmode", "-demo", "-nographics" };

        Assert.IsTrue(ArgumentReader.HasFlag(args, "demo"));
        Assert.IsTrue(ArgumentReader.HasFlag(args, "batchmode"));
    }

    [Test]
    public void ReportsAnAbsentFlagAsFalse()
    {
        var args = new[] { "game.exe", "-batchmode" };

        Assert.IsFalse(ArgumentReader.HasFlag(args, "demo"));
    }

    [Test]
    public void FlagMatchingIsExactSoPrefixesDoNotCount()
    {
        // "-demolition" must not enable the demo build.
        var args = new[] { "game.exe", "-demolition" };

        Assert.IsFalse(ArgumentReader.HasFlag(args, "demo"));
    }

    [Test]
    public void FlagMatchingIsCaseSensitive()
    {
        var args = new[] { "game.exe", "-DEMO" };

        Assert.IsFalse(ArgumentReader.HasFlag(args, "demo"));
    }

    [Test]
    public void HandlesAnEmptyArgumentList()
    {
        var args = new string[0];

        Assert.IsNull(ArgumentReader.GetArg(args, "dsn"));
        Assert.IsFalse(ArgumentReader.HasFlag(args, "demo"));
    }

    [Test]
    public void HandlesANullArgumentList()
    {
        Assert.IsNull(ArgumentReader.GetArg(null, "dsn"));
        Assert.IsFalse(ArgumentReader.HasFlag(null, "demo"));
    }
}
