using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class LocalScoreStoreTests
{
    private string _directory;
    private LocalScoreStore _store;

    private string BoardPath => Path.Combine(_directory, "leaderboard.json");

    [SetUp]
    public void SetUp()
    {
        // A directory per test, so nothing leaks between them or onto the real board.
        _directory = Path.Combine(Path.GetTempPath(), "sentaur-scores-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_directory);
        _store = new LocalScoreStore(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private static ScoreEntry Entry(string name, int score)
    {
        return new ScoreEntry
        {
            Key = System.Guid.NewGuid().ToString(),
            Name = name,
            Score = score,
            Duration = "00:01:00",
            Timestamp = "2026-01-01T00:00:00.0000000",
            Platform = "Test"
        };
    }

    private void Submit(string name, int score)
    {
        Assert.IsTrue(_store.SubmitAsync(Entry(name, score)).Result, $"submitting {name} failed");
    }

    private ScoreEntry[] Top(int count) => _store.TopAsync(count).Result.ToArray();

    [Test]
    public void NeedsNoConfigurationAndHasNothingToConnectTo()
    {
        Assert.IsTrue(_store.CanSubmit);
        Assert.AreEqual(ConnectionState.NotApplicable, _store.Connection);
    }

    [Test]
    public void AsksForAThreeCharacterName()
    {
        Assert.AreEqual(3, _store.NameLengthLimit);
    }

    [Test]
    public void ReadsAsEmptyBeforeAnythingIsStored()
    {
        Assert.IsEmpty(Top(10));
        Assert.IsFalse(File.Exists(BoardPath), "reading must not create the board");
    }

    [Test]
    public void StoresARunAndReadsItBack()
    {
        Submit("ABC", 1234);

        var top = Top(10);
        Assert.AreEqual(1, top.Length);
        Assert.AreEqual("ABC", top[0].Name);
        Assert.AreEqual(1234, top[0].Score);
    }

    [Test]
    public void SurvivesTheStoreBeingRecreated()
    {
        Submit("ABC", 10);

        // The board is the file, not anything the instance is holding.
        var reopened = new LocalScoreStore(_directory);
        Assert.AreEqual(1, reopened.TopAsync(10).Result.Count);
        reopened.Dispose();
    }

    [Test]
    public void RanksTheHighestScoreFirst()
    {
        Submit("LOW", 1);
        Submit("TOP", 999);
        Submit("MID", 500);

        Assert.AreEqual(new[] { "TOP", "MID", "LOW" }, Top(10).Select(e => e.Name).ToArray());
    }

    [Test]
    public void ReturnsAtMostTheRequestedCount()
    {
        Submit("AAA", 3);
        Submit("BBB", 2);
        Submit("CCC", 1);

        Assert.AreEqual(new[] { "AAA", "BBB" }, Top(2).Select(e => e.Name).ToArray());
    }

    [Test]
    public void ReturnsNothingForANonPositiveCount()
    {
        Submit("AAA", 3);

        Assert.IsEmpty(Top(0));
        Assert.IsEmpty(Top(-1));
    }

    [Test]
    public void KeepsOnlyTheBestRunsOnceTheBoardIsFull()
    {
        for (var i = 1; i <= LocalScoreStore.MaxEntries + 5; i++)
        {
            Submit("R" + i, i);
        }

        var top = Top(100);
        Assert.AreEqual(LocalScoreStore.MaxEntries, top.Length, "the board must not grow forever");
        Assert.AreEqual(LocalScoreStore.MaxEntries + 5, top[0].Score);
        Assert.AreEqual(6, top[LocalScoreStore.MaxEntries - 1].Score, "the weakest runs are dropped");
    }

    [Test]
    public void ALowScoreDoesNotDisplaceAFullBoard()
    {
        for (var i = 1; i <= LocalScoreStore.MaxEntries; i++)
        {
            Submit("R" + i, 100 + i);
        }

        Submit("BAD", 1);

        Assert.IsFalse(Top(100).Any(e => e.Name == "BAD"));
    }

    [Test]
    public void TrimsANameThatIsTooLongEvenIfTheFieldDidNot()
    {
        Submit("LONGNAME", 5);

        Assert.AreEqual("LON", Top(1)[0].Name);
    }

    [Test]
    public void StartsOverWhenTheBoardCannotBeRead()
    {
        Submit("ABC", 10);
        File.WriteAllText(BoardPath, "{ this is not json");

        // Reported rather than thrown: a corrupt board must not take the game down. Expected
        // rather than ignored, so that the report quietly going away fails this test too --
        // which ignoring every failing message could not tell the difference from. The tail of
        // the message is whatever the JSON reader called it, so only the stable half is matched.
        var unreadable = new Regex("^The local score board could not be read: ");

        LogAssert.Expect(LogType.Error, unreadable);
        Assert.IsEmpty(Top(10));

        // Once more: writing the next board reads the old one first, through the same Load.
        LogAssert.Expect(LogType.Error, unreadable);
        Submit("NEW", 20);

        // And the board written over it is readable again, so this last read expects nothing.
        Assert.AreEqual(new[] { "NEW" }, Top(10).Select(e => e.Name).ToArray());
    }

    [Test]
    public void IgnoresALeftOverTemporaryFileFromAnInterruptedWrite()
    {
        Submit("ABC", 10);
        File.WriteAllText(BoardPath + ".tmp", "{ half written");

        Assert.AreEqual(new[] { "ABC" }, Top(10).Select(e => e.Name).ToArray());

        // And the next write still succeeds over it.
        Submit("DEF", 20);
        Assert.AreEqual(new[] { "DEF", "ABC" }, Top(10).Select(e => e.Name).ToArray());
    }

    [Test]
    public void CreatesTheDirectoryIfItIsNotThereYet()
    {
        var nested = Path.Combine(_directory, "nope", "still-nope");
        var store = new LocalScoreStore(nested);

        Assert.IsTrue(store.SubmitAsync(Entry("ABC", 1)).Result);
        Assert.AreEqual(1, store.TopAsync(10).Result.Count);
        store.Dispose();
    }
}
