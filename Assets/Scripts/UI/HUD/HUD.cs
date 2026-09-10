using System.Collections;
using System.Runtime.InteropServices;
using SceneManagers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/**
 * Heads-up display (HUD) for the game.
 */
public class HUD : MonoBehaviour
{
    private TextMeshProUGUI _scoreText;
    private TextMeshProUGUI _timeElapsedText;
    private TextMeshProUGUI _gameOverText;
    private TextMeshProUGUI _currentLevelText;

    [SerializeField] private ScorePoster _scorePoster;
    [Tooltip("Optional: shows the final score during the game-over reveal")]
    [SerializeField] private TextMeshProUGUI _gameOverScoreText;
    [SerializeField] private GameObject _tryAgain;
    [SerializeField] private GameObject _quit;
    [SerializeField] private HUDManager _hudManager;

    [Tooltip("The level up UI prefab to show when a level is reached")]
    [SerializeField] private GameObject _levelUpUI;

    [Tooltip("The parent UI element containing the active pickups")]
    [SerializeField] private ActivePickupsUI _activePickupsUI;

    private int _lastScore;

    private XpBar _xpBar;

    private void Awake()
    {
        // get score text component from child
        _scoreText = transform.Find("Score").GetComponent<TextMeshProUGUI>();
        _timeElapsedText = transform.Find("TimeElapsed").GetComponent<TextMeshProUGUI>();
        _gameOverText = transform.Find("GameOver").GetComponent<TextMeshProUGUI>();
        _currentLevelText = transform.Find("XpBar").GetComponentInChildren<TextMeshProUGUI>();

        _xpBar = transform.Find("XpBar").GetComponent<XpBar>();

        var tryAgainButton = _tryAgain.GetComponent<Button>();
        tryAgainButton.onClick.AddListener(GameEvents.RaiseTryAgain);

        var quitButton = _quit.GetComponent<Button>();
        quitButton.onClick.AddListener(GameEvents.RaiseQuit);
    }

    private void Update()
    {
        // get time elapsed since game start in mm:ss format
        var timeElapsed = Time.timeSinceLevelLoad;
        var minutes = Mathf.FloorToInt(timeElapsed / 60.0f);
        var seconds = Mathf.FloorToInt(timeElapsed % 60.0f);
        _timeElapsedText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public void SetScore(int score)
    {
        _lastScore = score;
        _scoreText.text = score.ToString();
    }

    public void SetXp(float xp)
    {
        _xpBar.SetXp(xp);
    }

    public void ShowPause()
    {
        _gameOverText.text = "PAUSED";
        _gameOverText.enabled = true;

        _tryAgain.SetActive(true);
        _quit.SetActive(true);
    }

    public void HidePause()
    {
        _gameOverText.enabled = false;
        _tryAgain.SetActive(false);
        _quit.SetActive(false);

        // Clear the highlighted button to prevent accidental clicks
        if (_hudManager != null)
        {
            _hudManager.ClearHighlightedButton();
        }
    }

    public void ShowGameOver()
    {
        StartCoroutine(ShowGameOverSequence());
    }

    // Staged reveal. Realtime waits, because the game-over screen runs at Time.timeScale = 0.
    private IEnumerator ShowGameOverSequence()
    {
        // 1. Show "GAME OVER"
        _gameOverText.text = "GAME OVER";
        _gameOverText.enabled = true;

        yield return new WaitForSecondsRealtime(1.0f);

        // 2. Show the final score (optional, skipped when not wired in the scene)
        if (_gameOverScoreText != null)
        {
            _gameOverScoreText.text = _lastScore.ToString();
            _gameOverScoreText.enabled = true;
        }

        yield return new WaitForSecondsRealtime(1.0f);

        // 3. Show the score poster
        _scorePoster.Enable(_lastScore);

        yield return new WaitForSecondsRealtime(1.0f);

        // 4. Show Try Again / Quit
        _tryAgain.SetActive(true);
        _quit.SetActive(true);

        yield return new WaitForSecondsRealtime(0.1f);

        // Pre-select the name field so the player can immediately type / navigate with a
        // controller.
        if (_hudManager != null)
        {
            _hudManager.FocusNameField();
        }
    }

    public void SetCurrentLevel(int level)
    {
        _currentLevelText.text = "Level " + (level + 1);
    }

    /// <summary>Marks a timed pickup effect as running, with the icon it was collected as.</summary>
    public void AddActivePickup(Sprite icon, float duration)
    {
        _activePickupsUI.Add(icon, duration);
    }

    /// <summary>Offers the level-up choice. The panel pauses the game itself while it is open.</summary>
    public void ShowLevelUp()
    {
        _levelUpUI.SetActive(true);
    }

    /// <summary>
    /// Whether the level-up choice is up. The battle manager asks so that pausing on top of it
    /// is refused; that panel has already stopped the clock.
    /// </summary>
    public bool IsLevelUpOpen => _levelUpUI.activeSelf;
}
