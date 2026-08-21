using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class UpgradePathBaseTests
{
    /// <summary>
    /// Minimal concrete path. UpgradePathBase is abstract and a MonoBehaviour, so the level
    /// transitions can only be exercised through a real component on a throwaway GameObject.
    /// </summary>
    private class TestUpgradePath : UpgradePathBase
    {
        protected override string[] Descriptions { get; } =
            new[] { "level 1", "level 2", "level 3" };

        public readonly List<int> UpgradedToLevels = new List<int>();

        public override void UpgradeToLevel(int level)
        {
            UpgradedToLevels.Add(level);
        }
    }

    private GameObject _gameObject;
    private TestUpgradePath _path;

    [SetUp]
    public void SetUp()
    {
        _gameObject = new GameObject("TestUpgradePath");
        _path = _gameObject.AddComponent<TestUpgradePath>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_gameObject);
    }

    [Test]
    public void StartsInactiveAtLevelZero()
    {
        Assert.AreEqual(0, _path.Level);
        Assert.AreEqual(1, _path.NextLevel);
        Assert.IsFalse(_path.IsMaxLevel());
    }

    [Test]
    public void LevelUpAdvancesOneLevelAtATime()
    {
        _path.LevelUp();
        Assert.AreEqual(1, _path.Level);
        Assert.AreEqual(2, _path.NextLevel);

        _path.LevelUp();
        Assert.AreEqual(2, _path.Level);
    }

    [Test]
    public void LevelUpPassesTheNewLevelToTheSubclass()
    {
        // The paths switch on this parameter, so it must be the level just reached -- not
        // the previous one, and not something the subclass has to read off the field.
        _path.LevelUp();
        _path.LevelUp();

        CollectionAssert.AreEqual(new[] { 1, 2 }, _path.UpgradedToLevels);
    }

    [Test]
    public void ReportsMaxLevelAtTheTopOfTheTable()
    {
        _path.LevelUp();
        _path.LevelUp();
        _path.LevelUp();

        Assert.AreEqual(3, _path.Level);
        Assert.IsTrue(_path.IsMaxLevel());
    }

    [Test]
    public void LevelUpIsIgnoredOnceMaxed()
    {
        for (var i = 0; i < 3; i++)
        {
            _path.LevelUp();
        }

        _path.LevelUp();
        _path.LevelUp();

        Assert.AreEqual(3, _path.Level);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, _path.UpgradedToLevels);
    }

    [Test]
    public void NextDescriptionTracksTheCurrentLevel()
    {
        // Descriptions is 0-indexed while levels start at 1, so the description for the
        // *next* level sits at index _level.
        Assert.AreEqual("level 1", _path.NextDescription);

        _path.LevelUp();
        Assert.AreEqual("level 2", _path.NextDescription);

        _path.LevelUp();
        Assert.AreEqual("level 3", _path.NextDescription);
    }
}
