/// <summary>
/// Decides *when* things spawn. It owns no Unity objects and does no instantiating -- the
/// caller passes the current time in and acts on the answers, which keeps the scheduling
/// rules testable while the Instantiate calls stay in <see cref="BattleSceneManager"/>.
/// </summary>
public class SpawnDirector
{
    private readonly Timer _enemyTimer = new Timer();
    private readonly Timer _waveTimer = new Timer();
    private readonly Timer _pickupTimer = new Timer();
    private readonly Timer _spawnRampUpTimer = new Timer();
    private readonly Timer _hpRampUpTimer = new Timer();

    /// <summary>Anchors every timer to the given time. Call once the clock is meaningful.</summary>
    public void Start(float now)
    {
        _enemyTimer.Reset(now);
        _waveTimer.Reset(now);
        _pickupTimer.Reset(now);
        _spawnRampUpTimer.Reset(now);
        _hpRampUpTimer.Reset(now);
    }

    /// <summary>A rate of 0 or less disables enemy spawning entirely.</summary>
    public bool ShouldSpawnEnemy(float now, float spawnRate) =>
        spawnRate > 0 && _enemyTimer.TryConsume(now, spawnRate);

    /// <summary>
    /// While waves are still level-locked the timer is left untouched rather than consumed,
    /// so the first wave lands on the frame the gate opens.
    /// </summary>
    public bool ShouldSpawnWave(float now, float spawnRate, bool unlocked) =>
        unlocked && _waveTimer.TryConsume(now, spawnRate);

    public bool ShouldSpawnPickup(float now, float spawnRate, int onScreen, int maxOnScreen) =>
        onScreen < maxOnScreen && _pickupTimer.TryConsume(now, spawnRate);

    public bool ShouldRampUpSpawnRate(float now, float interval) =>
        _spawnRampUpTimer.TryConsume(now, interval);

    public bool ShouldRampUpHitPoints(float now, float interval) =>
        _hpRampUpTimer.TryConsume(now, interval);

    /// <summary>Elapsed-time gate that rearms itself once it fires.</summary>
    private class Timer
    {
        private float _last;

        public void Reset(float now) => _last = now;

        public bool TryConsume(float now, float interval)
        {
            if (now - _last <= interval)
            {
                return false;
            }

            _last = now;
            return true;
        }
    }
}
