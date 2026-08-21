using System;
using System.Net.Http;
using System.Threading.Tasks;
using Sentry;
using Sentry.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class ScoreEntry
{
    public string Key;
    public string Name;
    public string Email;
    public string Duration;
    public int Score;
    public string Timestamp;
    public string Platform;
}

public class ScorePoster : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_InputField _nameField;
    [SerializeField] private Button _submitButton;
    [SerializeField] private BattleSceneManager _gameManager;

    private DemoConfiguration _demoConfig;
    private TextMeshProUGUI _buttonText;

    private string _jwtToken;
    private HttpClient _httpClient;

    // Awaited before uploading: the player can submit before login resolves.
    private Task _loginTask;
    private bool _isUploading;
    private bool _uploadSucceeded;

    private void Awake()
    {
        _demoConfig = DemoConfiguration.Load();
        _buttonText = _submitButton.GetComponentInChildren<TextMeshProUGUI>();

        _submitButton.onClick.AddListener(OnSubmit);
    }

    private void Start()
    {
        if (_demoConfig != null && _demoConfig.Enabled && !string.IsNullOrEmpty(_demoConfig.ApiUrl))
        {
            _httpClient = new HttpClient(new SentryHttpMessageHandler());
            _loginTask = LoginAsync();
        }
    }

    public void Enable()
    {
        // Nothing to upload to if the login failed.
        if (_jwtToken != null)
        {
            _root.SetActive(true);
        }
    }

    private void OnDestroy()
    {
        // "Try Again" reloads the scene, so this would otherwise leak per reload.
        _httpClient?.Dispose();
        _httpClient = null;
    }

    private async Task LoginAsync()
    {
        var transaction = SentrySdk.StartTransaction("scoreposter", "login");
        SentrySdk.ConfigureScope(scope => scope.Transaction = transaction);

        try
        {
            var json = JsonUtility.ToJson(_demoConfig.User);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_demoConfig.ApiUrl + "/token", content);
            if (response.IsSuccessStatusCode)
            {
                GameLog.Trace("Login to leaderboard successful.");
                transaction.Finish(SpanStatus.Ok);
                _jwtToken = (await response.Content.ReadAsStringAsync()).Replace("\"", "");
            }
            else
            {
                GameLog.Trace("Login to leaderboard failed.");
                transaction.Finish(SpanStatus.Unavailable);
                _jwtToken = null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Login failed: {ex.Message}");
            transaction.Finish(SpanStatus.InternalError);
            _jwtToken = null;
        }
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
            _uploadSucceeded = await UploadScoreCoreAsync();
        }
        finally
        {
            _isUploading = false;

            _submitButton.interactable = !_uploadSucceeded;
        }
    }

    private async Task<bool> UploadScoreCoreAsync()
    {
        if (_loginTask != null)
        {
            await _loginTask;
        }

        if (string.IsNullOrEmpty(_jwtToken))
        {
            GameLog.Trace("Not uploading the score: no leaderboard session.");
            _buttonText.text = "Retry";
            return false;
        }

        var score = new ScoreEntry
        {
            Key = Guid.NewGuid().ToString(),
            Name = _nameField.text,
            Duration = TimeSpan.FromSeconds(Time.timeSinceLevelLoad).ToString(),
            Score = _gameManager.GetScore(),
            Timestamp = DateTime.Now.ToString("o"),
            Platform = Application.platform.ToString()
        };

        var json = JsonUtility.ToJson(score);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var uploadTransaction = SentrySdk.StartTransaction("scoreposter", "upload");
        SentrySdk.ConfigureScope(scope => scope.Transaction = uploadTransaction);

        try
        {
            // Per-request: the client is shared, so mutating its defaults is global state.
            using var request = new HttpRequestMessage(HttpMethod.Post, _demoConfig.ApiUrl + "/score")
            {
                Content = content,
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                GameLog.Trace("Uploading score to leaderboard failed.");
                SentrySdk.CaptureException(new HttpRequestException("Failed to upload score."));
                _buttonText.text = "Retry";
                uploadTransaction.Finish(SpanStatus.Unavailable);
                return false;
            }

            GameLog.Trace("Uploading score to leaderboard was successful.");
            _buttonText.text = "Posted!";
            uploadTransaction.Finish(SpanStatus.Ok);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Score upload failed: {ex.Message}");
            _buttonText.text = "Retry";
            uploadTransaction.Finish(SpanStatus.InternalError);
            return false;
        }
    }
}
