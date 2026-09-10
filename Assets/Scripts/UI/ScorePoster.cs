using System;
#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif
using System.Threading.Tasks;
using Sentry.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScorePoster : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_InputField _nameField;
    [SerializeField] private Button _submitButton;
    [SerializeField] private BattleSceneManager _gameManager;

    private TextMeshProUGUI _buttonText;

    // For HUDManager, which drives controller navigation across the game-over screen.
    public TMP_InputField NameField => _nameField;
    public Button SubmitButton => _submitButton;

    /// <summary>Raised when the on-screen keyboard closes and the name field has text.</summary>
    public event Action OnVirtualKeyboardClosedWithText;

    /// <summary>Where the score goes. Null when this mode keeps no scores at all.</summary>
    private IScoreStore _store;

    private bool _isUploading;
    private bool _uploadSucceeded;

    private TouchScreenKeyboard _keyboard;
    private bool _keyboardWasActive;
    // TMP_InputField reverts to its original text when the user cancels (ESC / keyboard
    // dismissed), which threw away a typed name. Kept here to restore it.
    private string _savedName = "";

#if UNITY_STANDALONE_WIN
    // Windows handhelds (e.g. ROG Ally) report no touch keyboard support, so the WinRT
    // gamepad keyboard is driven explicitly through this helper. The DLL is optional;
    // every call is wrapped so its absence just means no on-screen keyboard.
    [DllImport("HandheldHelper")] private static extern bool ShowVirtualKeyboard();
    [DllImport("HandheldHelper")] private static extern bool HideVirtualKeyboard();
    [DllImport("HandheldHelper")] private static extern bool IsDeviceHandheld();
#endif

    private void Awake()
    {
        _buttonText = _submitButton.GetComponentInChildren<TextMeshProUGUI>();
        _store = CreateStore();

        _submitButton.onClick.AddListener(OnSubmit);

        // Nothing to post until a name is typed.
        _submitButton.interactable = false;
        _nameField.onValueChanged.AddListener(OnNameValueChanged);
        _nameField.onEndEdit.AddListener(OnNameEndEdit);

        // Keep the Unity input field visible next to the native keyboard on platforms
        // that show one (mobile, Switch).
        _nameField.shouldHideMobileInput = false;

        if (TouchScreenKeyboard.isSupported)
        {
            // Platforms with a native on-screen keyboard (mobile, Switch) need it opened
            // explicitly when the field is selected via controller navigation.
            _nameField.onSelect.AddListener(OnInputFieldSelected);
        }
#if UNITY_STANDALONE_WIN
        else
        {
            try
            {
                if (IsDeviceHandheld())
                {
                    _nameField.onSelect.AddListener(OnInputFieldSelected);
                    _nameField.onDeselect.AddListener(OnInputFieldDeselected);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"HandheldHelper unavailable: {ex.Message}");
            }
        }
#endif
    }

    private static IScoreStore CreateStore()
    {
        return LeaderboardConfiguration.Mode switch
        {
            ScoreMode.Remote => new RemoteScoreStore(
                LeaderboardConfiguration.ApiUrl,
                LeaderboardConfiguration.Credentials
            ),
            // Local gets its store when the on-device board lands. None never keeps anything.
            _ => null,
        };
    }

    public void Enable()
    {
        // Whether the backend answers is not known yet and deliberately not waited for: the
        // panel appears if there is somewhere to post to at all, and connecting happens when
        // the player actually submits.
        if (_store == null || !_store.CanSubmit)
        {
            return;
        }

        _root.SetActive(true);
        _submitButton.interactable = !_uploadSucceeded && !string.IsNullOrEmpty(_nameField.text);
    }

    private void Update()
    {
        // Mirror the native on-screen keyboard's text into the input field while it is open,
        // and detect the close so navigation can move on to Submit.
        if (_keyboard != null && _keyboard.active)
        {
            _nameField.text = _keyboard.text;
            _keyboardWasActive = true;
        }
        else if (_keyboardWasActive)
        {
            if (_keyboard != null)
            {
                _nameField.text = _keyboard.text;
            }
            _keyboardWasActive = false;
            if (!string.IsNullOrEmpty(_nameField.text))
            {
                OnVirtualKeyboardClosedWithText?.Invoke();
            }
        }
    }

    private void OnDestroy()
    {
        if (_nameField != null)
        {
            _nameField.onSelect.RemoveListener(OnInputFieldSelected);
            _nameField.onValueChanged.RemoveListener(OnNameValueChanged);
            _nameField.onEndEdit.RemoveListener(OnNameEndEdit);
#if UNITY_STANDALONE_WIN
            _nameField.onDeselect.RemoveListener(OnInputFieldDeselected);
#endif
        }

        _store?.Dispose();
        _store = null;
    }

    private void OnNameValueChanged(string text)
    {
        if (_uploadSucceeded || _isUploading)
        {
            return;
        }
        if (!string.IsNullOrEmpty(text))
        {
            _savedName = text;
        }
        _submitButton.interactable = !string.IsNullOrEmpty(text);
    }

    private void OnNameEndEdit(string text)
    {
        if (_uploadSucceeded)
        {
            return;
        }
        // TMP_InputField reverts to its original text when the user cancels. If the field is
        // now empty but a name was typed earlier, restore it.
        if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(_savedName))
        {
            _nameField.SetTextWithoutNotify(_savedName);
            _submitButton.interactable = !_isUploading;
        }
    }

    private void OnInputFieldSelected(string text)
    {
        if (TouchScreenKeyboard.isSupported)
        {
            _keyboard = TouchScreenKeyboard.Open(
                _nameField.text,
                TouchScreenKeyboardType.Default,
                false, // autocorrection
                false, // multiline
                false, // secure
                false, // alert
                _nameField.placeholder.GetComponent<TextMeshProUGUI>().text
            );
        }
#if UNITY_STANDALONE_WIN
        else
        {
            // Only reachable on a Windows handheld: the listener is not added otherwise.
            try
            {
                ShowVirtualKeyboard();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to show virtual keyboard: {ex.Message}");
            }
        }
#endif
    }

#if UNITY_STANDALONE_WIN
    private void OnInputFieldDeselected(string text)
    {
        try
        {
            HideVirtualKeyboard();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to hide virtual keyboard: {ex.Message}");
        }
        if (!string.IsNullOrEmpty(_nameField.text))
        {
            OnVirtualKeyboardClosedWithText?.Invoke();
        }
    }
#endif

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
                _nameField.interactable = false;
            }
            else
            {
                _submitButton.interactable = !string.IsNullOrEmpty(_nameField.text);
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
            Name = _nameField.text,
            Duration = TimeSpan.FromSeconds(Time.timeSinceLevelLoad).ToString(),
            Score = _gameManager.GetScore(),
            Timestamp = DateTime.Now.ToString("o"),
            Platform = Application.platform.ToString()
        };

        var stored = await _store.SubmitAsync(entry);
        _buttonText.text = stored ? "Posted!" : "Retry";
        return stored;
    }
}
