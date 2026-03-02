using Sentry;
using Sentry.Unity;
using System;
using System.Collections.Generic;
#if !UNITY_SWITCH
using System.Net.Http;
#endif
#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[Serializable]
public class ScoreEntry
{
    public Guid Key;
    public string Name;
    public string Email;
    public string Duration;
    public int Score;
    public string Timestamp;
}

public class ScorePoster : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_InputField _nameField;
    [SerializeField] private Button _submitButton;

    private DemoConfiguration _demoConfig;
    private BattleSceneManager _gameManager;
    private TextMeshProUGUI _buttonText;

    public TMP_InputField NameField => _nameField;
    public Button SubmitButton => _submitButton;

    private string _jwtToken;
#if !UNITY_SWITCH
    private HttpClient _httpClient;
#endif

    private TouchScreenKeyboard _keyboard;
    private bool _keyboardWasActive = false;
    private bool _submitted = false;
    private string _savedName = "";

#if UNITY_STANDALONE_WIN
    [DllImport("HandheldHelper")] private static extern bool ShowVirtualKeyboard();
    [DllImport("HandheldHelper")] private static extern bool HideVirtualKeyboard();
    [DllImport("HandheldHelper")] private static extern bool IsDeviceHandheld();
#endif

    private void Awake()
    {
        _demoConfig = DemoConfiguration.Load();
        _gameManager = GameObject.Find("BattleSceneManager").GetComponent<BattleSceneManager>();
        _buttonText = _submitButton.GetComponentInChildren<TextMeshProUGUI>();

        _submitButton.onClick.AddListener(OnSubmit);
        _submitButton.interactable = false;
        _nameField.onValueChanged.AddListener(OnNameValueChanged);
        _nameField.onEndEdit.AddListener(OnNameEndEdit);

        // Configure input field for touch devices
        // For devices with touch keyboards (mobile, Switch, Windows tablets):
        // - shouldHideMobileInput = false (default): Shows both Unity input field and native keyboard
        // - shouldHideMobileInput = true: Hides Unity input field, shows only native keyboard (saves screen space)
        // We use false to let Unity handle keyboard automatically on supported platforms
        _nameField.shouldHideMobileInput = false;

        // On some platforms (like Nintendo Switch), we may need to manually trigger the keyboard
        // Add listener as a fallback for platforms that need explicit keyboard management
        if (TouchScreenKeyboard.isSupported)
        {
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

    private void OnDestroy()
    {
        // Clean up the listener to avoid memory leaks
        if (_nameField != null)
        {
            _nameField.onSelect.RemoveListener(OnInputFieldSelected);
            _nameField.onValueChanged.RemoveListener(OnNameValueChanged);
            _nameField.onEndEdit.RemoveListener(OnNameEndEdit);
#if UNITY_STANDALONE_WIN
            _nameField.onDeselect.RemoveListener(OnInputFieldDeselected);
#endif
        }
    }

    private void OnNameValueChanged(string text)
    {
        if (_submitted) return;
        if (!string.IsNullOrEmpty(text))
            _savedName = text;
        _submitButton.interactable = !string.IsNullOrEmpty(text);
    }

    private void OnNameEndEdit(string text)
    {
        if (_submitted) return;
        // TMP_InputField reverts to m_OriginalText when the user presses ESC/Cancel.
        // If the field is now empty but we have a previously typed name, restore it.
        if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(_savedName))
        {
            _nameField.SetTextWithoutNotify(_savedName);
            _submitButton.interactable = true;
        }
    }

    public void Start()
    {
        if (_demoConfig != null && _demoConfig.Enabled && !string.IsNullOrEmpty(_demoConfig.ApiUrl))
        {
#if !UNITY_SWITCH
            _httpClient = new HttpClient(new SentryHttpMessageHandler());
#endif
            _ = LoginAsync();
        }
    }

    public void Enable()
    {
        // If we did not manage to login during `Awake` (which means scene loading) then we do not display the upload screen
        if (_jwtToken != null)
        {
            _root.SetActive(true);
            _submitButton.interactable = !string.IsNullOrEmpty(_nameField.text);
        }
    }

    private void Update()
    {
        // Update the input field with keyboard text if the keyboard is active
        if (_keyboard != null && _keyboard.active)
        {
            _nameField.text = _keyboard.text;
            _keyboardWasActive = true;
        }
        else if (_keyboardWasActive)
        {
            // Keyboard was just closed, ensure final text is synchronized
            if (_keyboard != null)
            {
                _nameField.text = _keyboard.text;
            }
            _keyboardWasActive = false;
        }
    }

    private void OnInputFieldSelected(string text)
    {
        // Open native keyboard on devices without physical keyboards
        // This works on mobile (iOS/Android), Nintendo Switch, and Windows touch devices
        if (TouchScreenKeyboard.isSupported)
        {
            _keyboard = TouchScreenKeyboard.Open(
                _nameField.text,
                TouchScreenKeyboardType.Default,
                false, // autocorrection
                false, // multiline
                false, // secure
                false, // alert
                _nameField.placeholder.GetComponent<TextMeshProUGUI>().text // placeholder text
            );
        }
#if UNITY_STANDALONE_WIN
        else
        {
            // Windows handheld (e.g. ROG Ally X) — show the WinRT gamepad keyboard.
            // Only reachable when IsDeviceHandheld() was true at startup (listener not added otherwise).
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
    }
#endif

    private async Task LoginAsync()
    {
        var transaction = SentrySdk.StartTransaction("scoreposter", "login");
        SentrySdk.ConfigureScope(scope => scope.Transaction = transaction);

        try
        {
            var json = JsonUtility.ToJson(_demoConfig.User);
            var url = _demoConfig.ApiUrl + "/token";
            var method = "POST";

#if UNITY_SWITCH
            // Start a child span for this HTTP request (mimics SentryHttpMessageHandler)
            var span = transaction.StartChild("http.client", $"{method} {url}");
            span?.SetExtra("http.request.method", method);

            var uri = new Uri(url);
            if (!string.IsNullOrWhiteSpace(uri.Host))
            {
                span?.SetExtra("server.address", uri.Host);
            }

            using (UnityWebRequest request = new UnityWebRequest(url, method))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                // Propagate trace headers for distributed tracing
                PropagateTraceHeaders(request, span);

                await request.SendWebRequest();

                var statusCode = (int)request.responseCode;

                // Add breadcrumb (mimics SentryHttpMessageHandler)
                SentrySdk.AddBreadcrumb(
                    message: string.Empty,
                    category: "http",
                    type: "http",
                    data: new Dictionary<string, string>
                    {
                        {"url", url},
                        {"method", method},
                        {"status_code", statusCode.ToString()}
                    }
                );

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("Login to leaderboard successful.");
                    _jwtToken = request.downloadHandler.text.Replace("\"", "");

                    // Finish span with status code info
                    span?.SetExtra("http.response.status_code", statusCode);
                    span?.Finish(GetSpanStatusFromHttpCode(statusCode));
                    transaction.Finish(SpanStatus.Ok);
                }
                else
                {
                    Debug.Log("Login to leaderboard failed.");
                    _jwtToken = null;

                    // Finish span with error status
                    span?.SetExtra("http.response.status_code", statusCode);
                    var spanStatus = GetSpanStatusFromHttpCode(statusCode);
                    span?.Finish(spanStatus);

                    // Capture failed request as event (mimics SentryHttpFailedRequestHandler)
                    if (statusCode >= 400)
                    {
                        CaptureFailedRequest(method, url, statusCode, request.error);
                    }

                    transaction.Finish(SpanStatus.Unavailable);
                }
            }
#else
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
            {
                Debug.Log("Login to leaderboard successful.");
                transaction.Finish(SpanStatus.Ok);
                _jwtToken = (await response.Content.ReadAsStringAsync()).Replace("\"", "");
            }
            else
            {
                Debug.Log("Login to leaderboard failed.");
                transaction.Finish(SpanStatus.Unavailable);
                _jwtToken = null;
            }
#endif
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
        // Disable immediately to prevent a second submission while the request is in-flight.
        // Re-enabled only on failure so the player can retry.
        _submitButton.interactable = false;
        _ = UploadScoreAsync();
    }

    private async Task UploadScoreAsync()
    {
        var score = new ScoreEntry
        {
            Key = Guid.NewGuid(),
            Name = _nameField.text,
            Duration = TimeSpan.FromSeconds(Time.timeSinceLevelLoad).ToString(),
            Score = _gameManager.GetScore(),
            Timestamp = DateTime.Now.ToString("o")
        };

        var json = JsonUtility.ToJson(score);

        var uploadTransaction = SentrySdk.StartTransaction("scoreposter", "upload");
        SentrySdk.ConfigureScope(scope => scope.Transaction = uploadTransaction);

        try
        {
            var url = _demoConfig.ApiUrl + "/score";
            var method = "POST";

#if UNITY_SWITCH
            // Start a child span for this HTTP request (mimics SentryHttpMessageHandler)
            var span = uploadTransaction.StartChild("http.client", $"{method} {url}");
            span?.SetExtra("http.request.method", method);

            var uri = new Uri(url);
            if (!string.IsNullOrWhiteSpace(uri.Host))
            {
                span?.SetExtra("server.address", uri.Host);
            }

            using (UnityWebRequest request = new UnityWebRequest(url, method))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + _jwtToken);

                // Propagate trace headers for distributed tracing
                PropagateTraceHeaders(request, span);

                await request.SendWebRequest();

                var statusCode = (int)request.responseCode;

                // Add breadcrumb (mimics SentryHttpMessageHandler)
                SentrySdk.AddBreadcrumb(
                    message: string.Empty,
                    category: "http",
                    type: "http",
                    data: new Dictionary<string, string>
                    {
                        {"url", url},
                        {"method", method},
                        {"status_code", statusCode.ToString()}
                    }
                );

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log("Uploading score to leaderboard failed.");
                    _submitButton.interactable = true;
                    _buttonText.text = "Retry";

                    // Finish span with error status
                    span?.SetExtra("http.response.status_code", statusCode);
                    var spanStatus = GetSpanStatusFromHttpCode(statusCode);
                    span?.Finish(spanStatus);

                    // Capture failed request as event (mimics SentryHttpFailedRequestHandler)
                    if (statusCode >= 400)
                    {
                        CaptureFailedRequest(method, url, statusCode, request.error);
                    }

                    uploadTransaction.Finish(SpanStatus.Unavailable);
                }
                else
                {
                    Debug.Log("Uploading score to leaderboard was successful.");
                    _submitted = true;
                    _submitButton.interactable = false;
                    _buttonText.text = "Posted!";
                    _nameField.interactable = false;

                    // Finish span with success status
                    span?.SetExtra("http.response.status_code", statusCode);
                    span?.Finish(GetSpanStatusFromHttpCode(statusCode));

                    uploadTransaction.Finish(SpanStatus.Ok);
                }
            }
#else
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                Debug.Log("Uploading score to leaderboard failed.");
                SentrySdk.CaptureException(new HttpRequestException("Failed to upload score."));
                _submitButton.interactable = true;
                _buttonText.text = "Retry";
                uploadTransaction.Finish(SpanStatus.Unavailable);
            }
            else
            {
                Debug.Log("Uploading score to leaderboard was successful.");
                _submitted = true;
                _submitButton.interactable = false;
                _buttonText.text = "Posted!";
                _nameField.interactable = false;
                uploadTransaction.Finish(SpanStatus.Ok);
            }
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"Score upload failed: {ex.Message}");
            _submitButton.interactable = true;
            _buttonText.text = "Retry";
            uploadTransaction.Finish(SpanStatus.InternalError);
        }
    }

#if UNITY_SWITCH
    /// <summary>
    /// Propagates Sentry trace headers to the UnityWebRequest for distributed tracing.
    /// Mimics the behavior of SentryMessageHandler.PropagateTraceHeaders.
    /// </summary>
    private void PropagateTraceHeaders(UnityWebRequest request, ISpan span)
    {
        // Add sentry-trace header
        var traceHeader = span?.GetTraceHeader() ?? SentrySdk.GetTraceHeader();
        if (traceHeader != null)
        {
            request.SetRequestHeader("sentry-trace", traceHeader.ToString());
        }

        // Add baggage header
        var baggage = SentrySdk.GetBaggage();
        if (baggage != null)
        {
            request.SetRequestHeader("baggage", baggage.ToString());
        }
    }

    /// <summary>
    /// Maps HTTP status codes to Sentry span statuses.
    /// Mimics the behavior of SpanStatusConverter.FromHttpStatusCode.
    /// </summary>
    private SpanStatus GetSpanStatusFromHttpCode(int code)
    {
        return code switch
        {
            < 400 => SpanStatus.Ok,
            400 => SpanStatus.FailedPrecondition,
            401 => SpanStatus.Unauthenticated,
            403 => SpanStatus.PermissionDenied,
            404 => SpanStatus.NotFound,
            409 => SpanStatus.AlreadyExists,
            429 => SpanStatus.ResourceExhausted,
            499 => SpanStatus.Cancelled,
            < 500 => SpanStatus.FailedPrecondition,
            500 => SpanStatus.InternalError,
            501 => SpanStatus.Unimplemented,
            503 => SpanStatus.Unavailable,
            504 => SpanStatus.DeadlineExceeded,
            < 600 => SpanStatus.InternalError,
            _ => SpanStatus.UnknownError
        };
    }

    /// <summary>
    /// Captures failed HTTP requests as Sentry events.
    /// Mimics the behavior of SentryHttpFailedRequestHandler.
    /// </summary>
    private void CaptureFailedRequest(string method, string url, int statusCode, string error)
    {
        // Only capture 4xx and 5xx errors
        if (statusCode < 400)
        {
            return;
        }

        // Create an exception for the failed request
        var exception = new System.Net.Http.HttpRequestException(
            $"Response status code does not indicate success: {statusCode} ({error})"
        );

        // Create a Sentry event
        var sentryEvent = new SentryEvent(exception);

        // Add request context
        var uri = new Uri(url);
        sentryEvent.Request = new SentryRequest
        {
            Url = url,
            Method = method,
            QueryString = uri.Query
        };

        // Add response context
        sentryEvent.Contexts["response"] = new Dictionary<string, object>
        {
            {"status_code", statusCode}
        };

        // Capture the event
        SentrySdk.CaptureEvent(sentryEvent);
    }
#endif
}

#if UNITY_SWITCH
// Extension method to make UnityWebRequest awaitable
public static class UnityWebRequestExtensions
{
    public static Task SendWebRequest(this UnityWebRequest request)
    {
        var tcs = new TaskCompletionSource<bool>();
        request.SendWebRequest().completed += _ => tcs.SetResult(true);
        return tcs.Task;
    }
}
#endif
