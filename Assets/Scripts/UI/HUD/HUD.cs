using SceneManagers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Heads-up display (HUD) for the game, and the one way anything reaches its elements.
/// </summary>
/// <remarks>
/// <para>
/// Two things sit on top of the game: the pause screen and the game-over screen. They are the
/// same overlay, differing only in what its title says and which of its children are shown.
/// </para>
/// <para>
/// One rule holds throughout: things appear and disappear with <c>SetActive</c>, never by
/// switching a component off. Mixing the two left elements that were live objects with a
/// disabled renderer sitting beside elements that were properly inactive, which read as the
/// same thing in the game and completely differently in the hierarchy.
/// </para>
/// <para>
/// Every element is a serialized reference. Finding them by name at runtime meant a rename or a
/// reparent failed as a null reference mid-run, with nothing to catch it beforehand.
/// </para>
/// </remarks>
public class HUD : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private TextMeshProUGUI _scoreText;
    [SerializeField] private TextMeshProUGUI _timeElapsedText;
    [SerializeField] private TextMeshProUGUI _currentLevelText;
    [SerializeField] private XpBar _xpBar;

    [Tooltip("The parent UI element containing the active pickups")]
    [SerializeField] private ActivePickupsUI _activePickupsUI;

    [Tooltip("The level up UI prefab to show when a level is reached")]
    [SerializeField] private GameObject _levelUpUI;

    [Header("Overlay")]
    [Tooltip("Everything that covers the game: shown for pause and for game over alike")]
    [SerializeField] private GameObject _overlay;

    [Tooltip("PAUSED or GAME OVER. A leaf, never a parent")]
    [SerializeField] private TextMeshProUGUI _title;

    [Tooltip("The final score, shown partway through the game-over reveal")]
    [SerializeField] private TextMeshProUGUI _finalScoreText;

    [SerializeField] private ScorePoster _scorePoster;

    [Tooltip("Try Again and Quit, shown together")]
    [SerializeField] private GameObject _choices;

    [SerializeField] private GameObject _tryAgain;
    [SerializeField] private GameObject _quit;
    [SerializeField] private HUDManager _hudManager;

    [Tooltip("Choreographs the game-over reveal. Pause has none: it should be instant")]
    [SerializeField] private GameOverReveal _reveal;

    private int _lastScore;

    private void Awake()
    {
        _tryAgain.GetComponent<Button>().onClick.AddListener(GameEvents.RaiseTryAgain);
        _quit.GetComponent<Button>().onClick.AddListener(GameEvents.RaiseQuit);
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

    public void SetCurrentLevel(int level)
    {
        _currentLevelText.text = "Level " + (level + 1);
    }

    public void ShowPause()
    {
        _title.text = "PAUSED";
        ShowOverlay(finalScore: false, choices: true);
    }

    public void HidePause()
    {
        _reveal.Stop();
        _overlay.SetActive(false);

        // Clear the highlighted button to prevent accidental clicks
        if (_hudManager != null)
        {
            _hudManager.ClearHighlightedButton();
        }
    }

    public void ShowGameOver()
    {
        _title.text = "GAME OVER";
        ShowOverlay(finalScore: false, choices: false);

        // The reveal owns the rest: what comes in, in what order, and how it moves. It also
        // reveals the poster and takes focus, once the screen has stopped moving.
        _reveal.Play(_lastScore);
    }

    /// <summary>
    /// Puts the overlay up with a known set of children showing, so neither screen can inherit
    /// leftovers from the other.
    /// </summary>
    private void ShowOverlay(bool finalScore, bool choices)
    {
        _overlay.SetActive(true);
        _title.gameObject.SetActive(true);
        _finalScoreText.gameObject.SetActive(finalScore);
        _choices.SetActive(choices);
        _scorePoster.Hide();

        // The reveal leaves things scaled and faded part-way through. Pause is instant, so it
        // has to put them back rather than inherit whatever the last animation was doing.
        _title.alpha = 1f;
        _title.transform.localScale = Vector3.one;
        _finalScoreText.alpha = 1f;
        _finalScoreText.transform.localScale = Vector3.one;
        _choices.transform.localScale = Vector3.one;
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
