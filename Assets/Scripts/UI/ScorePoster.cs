using System;
using System.Threading.Tasks;
using Sentry.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The score entry panel on the game-over screen.
/// </summary>
/// <remarks>
/// A presenter, and nothing more: it reveals the panel, keeps the submit button in step with
/// what has been entered, and reports the outcome on that button. How a score is kept belongs
/// to <see cref="IScoreStore"/>, and how a name is entered to <see cref="INameEntry"/> -- of
/// which the store picks one, through <see cref="ChooseEntry"/>. It is revealed by
/// <see cref="HUD"/> and navigated by <c>HUDManager</c>, the same as every other part of this
/// screen.
/// </remarks>
public class ScorePoster : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private NameEntryField _nameEntry;

    [Tooltip("Optional: the letter picker, for machines with no keyboard to type a name on")]
    [SerializeField] private ArcadeNameEntry _arcadeEntry;

    [SerializeField] private Button _submitButton;

    [Tooltip("Optional: the light saying whether the score has anywhere to go")]
    [SerializeField] private ConnectionIndicator _connectionIndicator;

    [Tooltip("Optional: the on-device top ten, shown once a run has been recorded")]
    [SerializeField] private LeaderboardBoard _board;

    private TextMeshProUGUI _buttonText;

    /// <summary>Where the score goes. Null when this mode keeps no scores at all.</summary>
    private IScoreStore _store;

    /// <summary>Which of the two ways in the player is being offered. Settled at reveal.</summary>
    private INameEntry _entry;

    private bool _isUploading;
    private bool _uploadSucceeded;

    // Handed in by the HUD at reveal time, which already tracks it for the game-over display.
    private int _finalScore;

    private void Awake()
    {
        _buttonText = _submitButton.GetComponentInChildren<TextMeshProUGUI>();
        _store = CreateStore();

        _submitButton.onClick.AddListener(OnSubmit);

        // Nothing to post until a name is typed.
        _submitButton.interactable = false;

        // Safe before either one's own Awake: this only registers a delegate on the instance.
        // Both are listened to because which one is used is not known until the panel is
        // revealed, and the one that is not used never raises it.
        _nameEntry.TextChanged += OnNameChanged;
        _entry = _nameEntry;

        if (_arcadeEntry != null)
        {
            _arcadeEntry.TextChanged += OnNameChanged;
        }
    }

    private void OnDestroy()
    {
        if (_nameEntry != null)
        {
            _nameEntry.TextChanged -= OnNameChanged;
        }

        if (_arcadeEntry != null)
        {
            _arcadeEntry.TextChanged -= OnNameChanged;
        }

        _store?.Dispose();
        _store = null;
    }

    private static IScoreStore CreateStore()
    {
        return LeaderboardConfiguration.Mode switch
        {
            ScoreMode.Remote => new RemoteScoreStore(
                LeaderboardConfiguration.ApiUrl,
                LeaderboardConfiguration.Credentials
            ),
            ScoreMode.Local => new LocalScoreStore(),
            // None never keeps anything, so there is nothing to show a panel for.
            _ => null,
        };
    }

    /// <summary>
    /// Reveals the panel for a finished run. Whether the backend answers is not known yet and
    /// deliberately not waited for: the panel appears if there is somewhere to post to at all,
    /// and connecting happens when the player actually submits.
    /// </summary>
    public void Enable(int finalScore)
    {
        if (_store == null || !_store.CanSubmit)
        {
            return;
        }

        _finalScore = finalScore;

        // Before this the whole panel is inactive, so neither name entry has woken up yet and
        // anything reaching into one would be reading a component that has not run Awake. Both
        // are active in the scene for that reason, and the unused one is switched off below --
        // after its Awake, so it can still answer for itself.
        _root.SetActive(true);

        _entry = ChooseEntry();
        if (_arcadeEntry != null)
        {
            _arcadeEntry.gameObject.SetActive(ReferenceEquals(_entry, _arcadeEntry));
        }
        _nameEntry.gameObject.SetActive(ReferenceEquals(_entry, _nameEntry));

        _entry.SetLengthLimit(_store.NameLengthLimit);
        _submitButton.interactable = !_uploadSucceeded && !string.IsNullOrEmpty(_entry.Text);

        // Starts reaching for the backend now, so the light has answered by the time a name is
        // typed. A store with nothing to reach hides it instead.
        if (_connectionIndicator != null)
        {
            _connectionIndicator.Bind(_store);
        }
    }

    /// <summary>
    /// Shows the board with no entry form, for the demo run that follows a recorded score. The
    /// form has already done its job by then, and this side of a reload there is nothing to fill
    /// in anyway.
    /// </summary>
    public void ShowBoardOnly(string highlightKey)
    {
        if (_store == null || _board == null)
        {
            return;
        }

        _root.SetActive(false);
        _ = _board.ShowAsync(_store, highlightKey);
    }

    /// <summary>
    /// Puts the panel away. The overlay is shared with the pause screen, so it has to be able
    /// to say what is not showing as well as what is.
    /// </summary>
    public void Hide()
    {
        _root.SetActive(false);

        if (_connectionIndicator != null)
        {
            _connectionIndicator.Unbind();
        }

        if (_board != null)
        {
            _board.Hide();
        }
    }

    /// <summary>
    /// Which way this board asks for a name. The store says; this only obeys.
    /// </summary>
    /// <remarks>
    /// Local scores are an arcade board and take initials, which the picker enters with nothing
    /// but a stick -- the only thing that works on a WebGL build or a kiosk cabinet, neither of
    /// which has a keyboard to open or plug in. Remote scores take a full name, which has to be
    /// typed. A scene with no picker wired falls back to the text box either way.
    /// </remarks>
    private INameEntry ChooseEntry()
    {
        var wantsInitials = _store.NameEntry == NameEntryStyle.Initials;
        return wantsInitials && _arcadeEntry != null ? _arcadeEntry : _nameEntry;
    }

    private void OnNameChanged()
    {
        if (_uploadSucceeded || _isUploading)
        {
            return;
        }

        _submitButton.interactable = !string.IsNullOrEmpty(_entry.Text);
    }

    private void OnSubmit()
    {
        // Button callbacks are synchronous, so fire-and-forget via SubmitAsync, which
        // reports anything that escapes rather than failing silently.
        _ = SubmitAsync();
    }

    private async Task SubmitAsync()
    {
        try
        {
            await UploadScoreAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Score submission failed: {ex.Message}");
            SentrySdk.CaptureException(ex);
        }
    }

    private async Task UploadScoreAsync()
    {
        if (_isUploading)
        {
            return;
        }

        _isUploading = true;
        _submitButton.interactable = false;

        try
        {
            _uploadSucceeded = await SubmitToStoreAsync();
        }
        finally
        {
            _isUploading = false;

            if (_uploadSucceeded)
            {
                // Lock the entry so navigation and typing can't re-submit or edit the name.
                _submitButton.interactable = false;
                _entry.Lock();
            }
            else
            {
                _submitButton.interactable = !string.IsNullOrEmpty(_entry.Text);
            }
        }
    }

    /// <summary>
    /// Hands the run to the store and reports the outcome on the button. Everything about how
    /// the score is kept -- connecting, retrying, where it ends up -- belongs to the store.
    /// </summary>
    private async Task<bool> SubmitToStoreAsync()
    {
        if (_store == null)
        {
            return false;
        }

        var entry = new ScoreEntry
        {
            Key = Guid.NewGuid().ToString(),
            Name = _entry.Text,
            Duration = TimeSpan.FromSeconds(Time.timeSinceLevelLoad).ToString(),
            Score = _finalScore,
            Timestamp = DateTime.Now.ToString("o"),
            Platform = Application.platform.ToString()
        };

        var stored = await _store.SubmitAsync(entry);
        _buttonText.text = stored ? "Posted!" : "Retry";

        if (stored && _board != null && await _board.ShowAsync(_store, entry.Key))
        {
            // The board takes the screen over from the entry form, which has done its job.
            _root.SetActive(false);
            _connectionIndicator?.Unbind();

            // Only once there is a board to sit behind. A store with nothing to show keeps the
            // ordinary game-over screen, form and all, which is the remote case: its board is on
            // the web and there is nothing here to watch a demo run behind.
            GameEvents.RaiseScoreSubmitted(entry.Key);
        }

        return stored;
    }
}
