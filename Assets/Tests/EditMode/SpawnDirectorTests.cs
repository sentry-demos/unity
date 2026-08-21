using NUnit.Framework;

public class SpawnDirectorTests
{
    private SpawnDirector _director;

    [SetUp]
    public void SetUp()
    {
        _director = new SpawnDirector();
        _director.Start(0f);
    }

    [Test]
    public void DoesNotSpawnBeforeTheIntervalHasElapsed()
    {
        Assert.IsFalse(_director.ShouldSpawnEnemy(1.9f, 2f));
        Assert.IsFalse(_director.ShouldSpawnEnemy(2f, 2f));
        Assert.IsTrue(_director.ShouldSpawnEnemy(2.1f, 2f));
    }

    [Test]
    public void RearmsAfterFiringSoTheIntervalIsMeasuredFromTheLastSpawn()
    {
        Assert.IsTrue(_director.ShouldSpawnEnemy(2.1f, 2f));
        Assert.IsFalse(_director.ShouldSpawnEnemy(3f, 2f));
        Assert.IsTrue(_director.ShouldSpawnEnemy(4.2f, 2f));
    }

    [Test]
    public void ANonPositiveRateDisablesEnemySpawning()
    {
        Assert.IsFalse(_director.ShouldSpawnEnemy(1000f, 0f));
        Assert.IsFalse(_director.ShouldSpawnEnemy(1000f, -1f));
    }

    [Test]
    public void HoldsTheWaveTimerWhileWavesAreStillLevelLocked()
    {
        // Locked: the timer must not be consumed, or the wait restarts every frame and the
        // first wave lands a full interval after the gate opens instead of at the gate.
        Assert.IsFalse(_director.ShouldSpawnWave(100f, 30f, unlocked: false));
        Assert.IsTrue(_director.ShouldSpawnWave(101f, 30f, unlocked: true));
    }

    [Test]
    public void SpawnsWavesOnTheIntervalOnceUnlocked()
    {
        Assert.IsFalse(_director.ShouldSpawnWave(29f, 30f, unlocked: true));
        Assert.IsTrue(_director.ShouldSpawnWave(31f, 30f, unlocked: true));
        Assert.IsFalse(_director.ShouldSpawnWave(40f, 30f, unlocked: true));
    }

    [Test]
    public void DoesNotSpawnPickupsAtTheOnScreenCap()
    {
        Assert.IsFalse(_director.ShouldSpawnPickup(100f, 15f, onScreen: 5, maxOnScreen: 5));
        Assert.IsFalse(_director.ShouldSpawnPickup(100f, 15f, onScreen: 6, maxOnScreen: 5));
    }

    [Test]
    public void SpawnsPickupsBelowTheCapOnceTheIntervalElapses()
    {
        Assert.IsFalse(_director.ShouldSpawnPickup(10f, 15f, onScreen: 0, maxOnScreen: 5));
        Assert.IsTrue(_director.ShouldSpawnPickup(16f, 15f, onScreen: 0, maxOnScreen: 5));
    }

    [Test]
    public void DoesNotConsumeThePickupTimerWhileAtTheCap()
    {
        // The cap is checked first, so a full run at the cap leaves the timer ready to fire
        // as soon as a slot frees up.
        Assert.IsFalse(_director.ShouldSpawnPickup(100f, 15f, onScreen: 5, maxOnScreen: 5));
        Assert.IsTrue(_director.ShouldSpawnPickup(100f, 15f, onScreen: 4, maxOnScreen: 5));
    }

    [Test]
    public void TimersAreIndependentOfOneAnother()
    {
        Assert.IsTrue(_director.ShouldSpawnEnemy(11f, 2f));

        // consuming the enemy timer must not disturb the ramp-up timer
        Assert.IsTrue(_director.ShouldRampUpSpawnRate(11f, 10f));
        Assert.IsTrue(_director.ShouldRampUpHitPoints(61f, 60f));
    }

    [Test]
    public void StartRebasesEveryTimerToTheGivenTime()
    {
        // Time.time is already well past zero by the time Start() runs; without rebasing,
        // every timer would fire on the first frame.
        _director.Start(500f);

        Assert.IsFalse(_director.ShouldSpawnEnemy(501f, 2f));
        Assert.IsFalse(_director.ShouldRampUpSpawnRate(505f, 10f));
        Assert.IsTrue(_director.ShouldSpawnEnemy(503f, 2f));
    }
}
