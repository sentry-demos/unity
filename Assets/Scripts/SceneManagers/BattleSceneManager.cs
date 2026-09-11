using UnityEngine;
using UnityEngine.InputSystem;

public class BattleSceneManager : MonoBehaviour
{
    [Header("Game Properties")]
    [SerializeField]
    [Tooltip("Designer-tunable numbers for this battle: spawn rates, ramps, formation, milestones")]
    private BattleTuning _tuning;

    [SerializeField]
    [Tooltip("The current level")]
    private int _currentLevel = 0;

    [SerializeField]
    [Tooltip("Starting XP")]
    private float _xp = 0;

    [SerializeField]
    [Tooltip("The HUD for this scene; every other UI element hangs off it")]
    private HUD _hud;

    [Header("Components")]
    [SerializeField]
    [Tooltip("Spawns enemies; driven from this manager's Update")]
    private EnemySpawner _enemySpawner;

    [SerializeField]
    [Tooltip("Spawns pickups; driven from this manager's Update")]
    private PickupSpawner _pickupSpawner;

    [SerializeField]
    [Tooltip("Background music, started and stopped with the game state")]
    private BattleAudioManager _audio;

    [SerializeField]
    [Tooltip("How long the board sits still before the demo run starts behind it")]
    private float _attractDelay = 1.6f;

    private DemoConfiguration _demoConfig;

    // the player's accumulated score so far
    private int _score = 0;

    public int GetScore() => _score;


    private LevelProgression _progression;
    private DifficultyCurve _difficulty;
    private SpawnDirector _spawnDirector;
    private BattleMetrics _metrics;

    private enum GameState
    {
        Playing,
        GameOver,
        Paused
    }

    public bool IsPlaying => _gameState == GameState.Playing;

    private GameState _gameState;

    private float _gameStartTime;
    private bool _isDeathEnemyPresent = false;

    // The frame the last accepted pause press landed on. See OnPause: the same press reaches
    // this from every action map that happens to be enabled.
    private int _lastPauseFrame = -1;

    // Pause is bound in both maps, and which of them is live depends on whether a menu is up,
    // so both are listened to. Held rather than looked up per press.
    private InputAction _playerPause;
    private InputAction _uiPause;

    private void Awake()
    {
        _demoConfig = DemoConfiguration.Load();

        if (_tuning == null)
        {
            // Every spawn rule reads from this, so there is no sensible partial behaviour to
            // fall back to. Fail here rather than as a NullReferenceException mid-run.
            Debug.LogError(
                $"{nameof(BattleSceneManager)} on '{name}' has no {nameof(BattleTuning)} "
                    + "assigned. Assign the BattleTuning asset in the inspector.",
                this
            );
            enabled = false;
            return;
        }

        _progression = new LevelProgression(_tuning.LevelMilestones, _currentLevel, _xp);
        _difficulty = new DifficultyCurve(DifficultyCurve.Settings.From(_tuning));
        _spawnDirector = new SpawnDirector();
        _metrics = new BattleMetrics();
        _enemySpawner.Initialize(_difficulty, new WaveFormation(_tuning));

        // Every connected device drives this scene. A PlayerInput component used to own these
        // actions, and such a component narrows the asset to the devices of the one control
        // scheme it paired -- a set that comes back empty after a scene reload and leaves the
        // game deaf to the pad and the keyboard alike. Nothing here wants that: a cabinet has a
        // pad, a desk has a keyboard, and both should work without either claiming the asset.
        InputSystem.actions.devices = null;

        _playerPause = InputSystem.actions.FindActionMap("Player").FindAction("Pause");
        _uiPause = InputSystem.actions.FindActionMap("UI").FindAction("Pause");

        InputSystem.actions.FindActionMap("Player").Enable();
        InputSystem.actions.FindActionMap("UI").Disable();
    }

    // Start is called before the first frame update
    private void Start()
    {
        _gameState = GameState.Playing;
        Time.timeScale = 1; // in case time scale was set to 0 (e.g. on death)

        _hud.SetXp(_progression.XpProgress);

        _spawnDirector.Start(Time.time);
        _gameStartTime = Time.time;

        // A demo run is nobody's run. Counting it would put attract loops in with the runs
        // people actually played, and every one of them would start a trace of its own.
        if (!AttractMode.Active)
        {
            _metrics.RunStarted(Time.time, _progression.CurrentLevel);
        }

        _hud.SetCurrentLevel(_progression.CurrentLevel);

        if (AttractMode.Active)
        {
            EnterAttract();
        }
    }

    /// <summary>
    /// The board stays up with the game playing itself behind it. Both input maps are live: the
    /// virtual gamepad drives the player through the Player map, while the human needs the UI
    /// map to press Again.
    /// </summary>
    private void EnterAttract()
    {
        InputSystem.actions.FindActionMap("Player").Enable();
        InputSystem.actions.FindActionMap("UI").Enable();

        _hud.ShowAttract(AttractMode.JustPosted);
    }

    // GameEvents is static, so subscriptions outlive the scene. "Try Again" reloads
    // BattleScene, and subscribing in Start() without ever unsubscribing left the previous
    // instance registered -- score, XP and pickups all counted once per attempt made.
    private void OnEnable()
    {
        GameEvents.EnemyDestroyed += OnEnemyDestroyed;
        GameEvents.PickupGrabbed += OnPickupGrabbed;
        GameEvents.PlayerDeath += OnPlayerDeath;
        GameEvents.XpEarned += OnXpEarned;
        GameEvents.TryAgain += OnTryAgain;
        GameEvents.Quit += OnQuit;
        GameEvents.ScoreSubmitted += OnScoreSubmitted;

        _playerPause.performed += OnPause;
        _uiPause.performed += OnPause;
    }

    private void OnDisable()
    {
        GameEvents.EnemyDestroyed -= OnEnemyDestroyed;
        GameEvents.PickupGrabbed -= OnPickupGrabbed;
        GameEvents.PlayerDeath -= OnPlayerDeath;
        GameEvents.XpEarned -= OnXpEarned;
        GameEvents.TryAgain -= OnTryAgain;
        GameEvents.Quit -= OnQuit;
        GameEvents.ScoreSubmitted -= OnScoreSubmitted;

        _playerPause.performed -= OnPause;
        _uiPause.performed -= OnPause;
    }

    private void OnPickupGrabbed(PickupCollected pickup)
    {
        SetScore(_score + pickup.ScoreValue);

        // active effects get denoted in the UI
        if (pickup.EffectDuration > 0)
        {
            _hud.AddActivePickup(pickup.Icon, pickup.EffectDuration);
        }
    }

    private void OnXpEarned(int xp)
    {
        GameMetrics.RecordXpEarned(xp);

        _progression.AddXp(xp);
        _hud.SetXp(_progression.XpProgress);

        // xp picked up also adds to score
        // this is important late game -- even if xp doesn't help you level up,
        // at least it adds to score
        AddScore(xp);
    }

    private void OnQuit()
    {
        // A demo run never started as far as metrics are concerned, so ending one would report
        // a duration measured from a clock that was never set.
        if (!AttractMode.Active)
        {
            _metrics.RunEnded("quit", Time.time, _score, _progression.CurrentLevel);
        }

        Application.Quit();
    }

    private void OnTryAgain()
    {
        // A human pressed Again, so whatever happens next is a real run rather than the demo
        // that may have been playing behind the board.
        AttractMode.End();
        ReloadScene();
    }

    /// <summary>
    /// A score was recorded. The board is already up; hand the screen over to a demo run behind
    /// it, after a beat so the player can see where they landed.
    /// </summary>
    private void OnScoreSubmitted(string key)
    {
        AttractMode.Begin(key);
        StartCoroutine(StartAttractAfterAPause());
    }

    private System.Collections.IEnumerator StartAttractAfterAPause()
    {
        // Realtime: the game-over screen has the clock stopped.
        yield return new WaitForSecondsRealtime(_attractDelay);
        ReloadScene();
    }

    private static void ReloadScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            "BattleScene",
            UnityEngine.SceneManagement.LoadSceneMode.Single
        );
    }

    private void OnEnemyDestroyed(int scoreValue)
    {
        SetScore(_score + scoreValue);
    }

    /// <summary>
    /// Hands control to the player or to the menus. Setting timeScale to 0 stops every
    /// time-based operation, which is what actually freezes the game.
    /// </summary>
    private void SetPlayerInControl(bool playerInControl)
    {
        var playerMap = InputSystem.actions.FindActionMap("Player");
        var uiMap = InputSystem.actions.FindActionMap("UI");

        Time.timeScale = playerInControl ? 1 : 0;

        if (playerInControl)
        {
            playerMap.Enable();
            uiMap.Disable();
            _audio.PlayMusic();
        }
        else
        {
            playerMap.Disable();
            uiMap.Enable();
            _audio.StopMusic();
        }
    }

    public void PauseGame()
    {
        _gameState = GameState.Paused;
        SetPlayerInControl(false);

        _hud.ShowPause();
    }

    public void UnpauseGame()
    {
        _gameState = GameState.Playing;
        SetPlayerInControl(true);

        _hud.HidePause();
    }

    private void OnPlayerDeath()
    {
        if (AttractMode.Active)
        {
            // The demo run ended. Start another rather than showing a game-over screen for a
            // run nobody played; the board is already up and stays up across the reload.
            ReloadScene();
            return;
        }

        _gameState = GameState.GameOver;
        SetPlayerInControl(false);

        _hud.ShowGameOver();

        _metrics.RunEnded("death", Time.time, _score, _progression.CurrentLevel);

        if (_demoConfig != null && _demoConfig.CrashOnGameOver)
        {
            Debug.Log("Saving score to disk.");
            NativeScoreSaver.SaveScoreToDisk(_score);
        }
    }

    /// <summary>
    /// Picks an upgrade without showing the choice, so a demo run still gets stronger and still
    /// looks like a run worth watching.
    /// </summary>
    private static void TakeUpgradeUnattended()
    {
        var paths = UpgradeManager.Instance.GetRandomUpgradePaths(1);
        if (paths == null || paths.Count == 0)
        {
            return;
        }

        UpgradeManager.Instance.LevelUpUpgradePath(paths[0]);
    }

    private void SetScore(int score)
    {
        _score = score;
        _hud.SetScore(_score);
    }

    private void AddScore(int score)
    {
        SetScore(_score + score);
    }

    // _currentLevel and _xp are the inspector-set starting values; LevelProgression owns
    // both once Awake has handed them over.
    public int GetCurrentLevel() => _progression.CurrentLevel;

    // Only 'performed' is subscribed, which matters because pausing disables the Player map:
    // that cancels the very action being handled, and a handler that also took 'canceled' would
    // toggle the game straight back.
    private void OnPause(InputAction.CallbackContext context)
    {
        // Pause is bound in both the Player and the UI map and both are listened to, so one
        // press arrives twice wherever both maps are enabled -- which is the attract board.
        // Taking only the first leaves the toggle to the press rather than to how many maps
        // happen to be live.
        if (_lastPauseFrame == Time.frameCount)
        {
            return;
        }

        _lastPauseFrame = Time.frameCount;

        // Don't allow pausing if the level up UI is active (it already pauses the game)
        if (_hud.IsLevelUpOpen)
        {
            return;
        }

        // A demo run is nobody's game to pause, and the board it plays behind is the same
        // overlay a pause screen would take over: pausing replaces the board with PAUSED, and
        // unpausing switches the overlay off entirely, leaving the attract loop with no menu at
        // all until the demo player next dies. The board already offers Again and Quit.
        if (AttractMode.Active)
        {
            return;
        }

        if (_gameState == GameState.Playing)
        {
            PauseGame();
        }
        else if (_gameState == GameState.Paused)
        {
            UnpauseGame();
        }
    }

    // Update is called once per frame
    private void Update()
    {
        if (_gameState != GameState.Playing)
        {
            return;
        }

        var now = Time.time;
        var level = _progression.CurrentLevel;

        if (!AttractMode.Active)
        {
            _metrics.Tick(
                now,
                Time.unscaledDeltaTime,
                _enemySpawner.EnemiesAlive,
                _pickupSpawner.OnScreen
            );
        }

        if (!_isDeathEnemyPresent && (now - _gameStartTime > _tuning.DeathAppearanceTime))
        {
            _isDeathEnemyPresent = true;
            _enemySpawner.SpawnDeath();
            _metrics.DeathEnemySpawned(now);

            // the death enemy resets the clock, so waves double again ahead of the next one
            _gameStartTime = now;
        }

        if (_spawnDirector.ShouldSpawnEnemy(now, _difficulty.EnemySpawnRate))
        {
            _enemySpawner.SpawnRandomWave(level);
        }

        if (
            _spawnDirector.ShouldSpawnWave(
                now,
                _difficulty.LinearHeadSpawnRate,
                _difficulty.AreLinearHeadWavesUnlocked(level)
            )
        )
        {
            _enemySpawner.SpawnLinearWave(
                _difficulty.WaveSize(level),
                _difficulty.IsDoubleWave(now - _gameStartTime)
            );
        }

        if (_spawnDirector.ShouldRampUpSpawnRate(now, _tuning.SpawnRampUpInterval))
        {
            _difficulty.RampUpSpawnRates();
            _metrics.DifficultyRamped(_difficulty.EnemySpawnRate, _difficulty.LinearHeadSpawnRate);
        }

        if (_spawnDirector.ShouldRampUpHitPoints(now, _tuning.HpRampUpInterval))
        {
            _difficulty.RampUpHitPoints();
            _metrics.HitPointsRamped(_difficulty.EnemyHitPointModifier);

            Debug.Log("Enemy HP modifier is now " + _difficulty.EnemyHitPointModifier);
        }

        if (
            _spawnDirector.ShouldSpawnPickup(
                now,
                _tuning.PickupSpawnRate,
                _pickupSpawner.OnScreen,
                _tuning.MaxPickupsOnScreen
            )
        )
        {
            _pickupSpawner.Spawn();
        }

        if (_progression.TryLevelUp())
        {
            Debug.Log("GameManager.Update: Level Up!");

            _metrics.LevelUp(_progression.CurrentLevel, now);

            _hud.SetCurrentLevel(_progression.CurrentLevel);

            // reset xp bar to 0 after leveling up
            _hud.SetXp(0);

            if (AttractMode.Active)
            {
                // A demo run has nobody to ask. Offering the choice would put a panel over the
                // leaderboard and stop the clock on the run the board is sitting in front of.
                TakeUpgradeUnattended();
            }
            else
            {
                _hud.ShowLevelUp();
            }
        }
    }
}
