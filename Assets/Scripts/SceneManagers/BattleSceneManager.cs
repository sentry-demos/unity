using System;
using System.Runtime.InteropServices;
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
    [Tooltip("The level up UI prefab to spawn")]
    private GameObject _levelUpUI;

    [SerializeField]
    [Tooltip("The parent UI element containing the active pickups")]
    private ActivePickupsUI _activePickupsUI;

    [SerializeField]
    [Tooltip("The HUD for this scene")]
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

    private DemoConfiguration _demoConfig;

    // the player's accumulated score so far
    private int _score = 0;

    public int GetScore() => _score;


    private LevelProgression _progression;
    private DifficultyCurve _difficulty;
    private SpawnDirector _spawnDirector;

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
        _enemySpawner.Initialize(_difficulty, new WaveFormation(_tuning));

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

        _hud.SetCurrentLevel(_progression.CurrentLevel);
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
    }

    private void OnDisable()
    {
        GameEvents.EnemyDestroyed -= OnEnemyDestroyed;
        GameEvents.PickupGrabbed -= OnPickupGrabbed;
        GameEvents.PlayerDeath -= OnPlayerDeath;
        GameEvents.XpEarned -= OnXpEarned;
        GameEvents.TryAgain -= OnTryAgain;
        GameEvents.Quit -= OnQuit;
    }

    private void OnPickupGrabbed(PickupCollected pickup)
    {
        SetScore(_score + pickup.ScoreValue);

        // active effects get denoted in the UI
        if (pickup.EffectDuration > 0)
        {
            _activePickupsUI.Add(pickup.Icon, pickup.EffectDuration);
        }
    }

    private void OnXpEarned(int xp)
    {
        _progression.AddXp(xp);
        _hud.SetXp(_progression.XpProgress);

        // xp picked up also adds to score
        // this is important late game -- even if xp doesn't help you level up,
        // at least it adds to score
        AddScore(xp);
    }

    private void OnQuit()
    {
        Application.Quit();
    }

    private void OnTryAgain()
    {
        // reload this scene
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
        _gameState = GameState.GameOver;
        SetPlayerInControl(false);

        _hud.ShowGameOver();

        if (_demoConfig != null && _demoConfig.CrashOnGameOver)
        {
            Debug.Log("Saving score to disk.");
            SaveScoreToDisk();
        }
    }

    // INTENTIONAL: save_score_to_disk crashes on purpose, to demo native crash capture.
    // Gated on DemoConfiguration.CrashOnGameOver. See CONTRIBUTING.md.
    private void SaveScoreToDisk()
    {

#if !UNITY_EDITOR
        Debug.Log("Calling into Native Save Utils.");

        try
        {
            Debug.Log("Attempting save_score_to_disk...");
            save_score_to_disk(_score);
            Debug.Log("save_score_to_disk completed without crash - this should not happen!");
        }
        catch (System.Exception e)
        {
            Debug.Log("save_score_to_disk threw exception: " + e.Message);
        }

        Debug.Log("ForceCrash also failed - this should not be reached!");
#else
        Debug.Log("If this was not the Editor, the score would be saved 'natively'.");
#endif
    }

    // NativeSaver.c
    [DllImport("__Internal")]
    private static extern void save_score_to_disk(int score);

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

    // PlayerInput wires its UnityEvents to started, performed *and* canceled, so this runs
    // three times per press unless it filters. That matters here because pausing disables the
    // Player map, which cancels the very action being handled and would toggle straight back.
    public void OnPause(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        // Don't allow pausing if the level up UI is active (it already pauses the game)
        if (_levelUpUI.activeSelf)
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

        if (!_isDeathEnemyPresent && (now - _gameStartTime > _tuning.DeathAppearanceTime))
        {
            _isDeathEnemyPresent = true;
            _enemySpawner.SpawnDeath();

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
        }

        if (_spawnDirector.ShouldRampUpHitPoints(now, _tuning.HpRampUpInterval))
        {
            _difficulty.RampUpHitPoints();

            GameLog.Trace("Enemy HP modifier is now " + _difficulty.EnemyHitPointModifier);
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
            GameLog.Trace("GameManager.Update: Level Up!");

            _hud.SetCurrentLevel(_progression.CurrentLevel);

            // reset xp bar to 0 after leveling up
            _hud.SetXp(0);

            _levelUpUI.SetActive(true);
        }
    }
}
