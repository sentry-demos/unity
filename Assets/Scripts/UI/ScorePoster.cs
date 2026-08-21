using System;
#if !UNITY_SWITCH
using System.Net.Http;
#endif
using System.Threading.Tasks;
using Sentry;
using Sentry.Unity;
using TMPro;
using UnityEngine;
#if UNITY_SWITCH
using System.Collections.Generic;
using UnityEngine.Networking;
#endif
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
#if !UNITY_SWITCH
    private HttpClient _httpClient;
#endif

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
#if !UNITY_SWITCH
            _httpClient = new HttpClient(new SentryHttpMessageHandler());
#endif
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
#if !UNITY_SWITCH
        // "Try Again" reloads the scene, so this would otherwise leak per reload.
        _httpClient?.Dispose();
        _httpClient = null;
#endif
    }

    private async Task LoginAsync()
    {
        // On the run's trace: the score being posted is the last act of the run that earned it.
        var transaction = RunTrace.StartTransaction("scoreposter", "login");
        RunTrace.SetScopeTransaction(transaction);

        try
        {
            var json = JsonUtility.ToJson(_demoConfig.User);
            var url = _demoConfig.ApiUrl + "/token";

#if UNITY_SWITCH
            // HttpClient + SentryHttpMessageHandler do not work on Switch, so the request goes
            // through UnityWebRequest and this block reproduces what the handler would have
            // done: the http.client child span, trace-header propagation, the breadcrumb, and
            // the failed-request event.
            var span = transaction.StartChild("http.client", $"POST {url}");
            span.SetExtra("http.request.method", "POST");
            var uri = new Uri(url);
            if (!string.IsNullOrWhiteSpace(uri.Host))
            {
                span.SetExtra("server.address", uri.Host);
            }

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                PropagateTraceHeaders(request, span);

                await request.SendWebRequest();

                var statusCode = (int)request.responseCode;
                AddHttpBreadcrumb(url, statusCode);
                span.SetExtra("http.response.status_code", statusCode);
                span.Finish(GetSpanStatusFromHttpCode(statusCode));

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("Login to leaderboard successful.");
                    GameMetrics.Count(GameMetrics.ScoreLogin, 1, (GameMetrics.ResultKey, "ok"));
                    transaction.Finish(SpanStatus.Ok);
                    _jwtToken = request.downloadHandler.text.Replace("\"", "");
                }
                else
                {
                    Debug.Log("Login to leaderboard failed.");
                    GameMetrics.Count(
                        GameMetrics.ScoreLogin,
                        1,
                        (GameMetrics.ResultKey, statusCode.ToString())
                    );
                    CaptureFailedRequest("POST", url, statusCode, request.error);
                    transaction.Finish(SpanStatus.Unavailable);
                    _jwtToken = null;
                }
            }
#else
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
            {
                Debug.Log("Login to leaderboard successful.");
                GameMetrics.Count(GameMetrics.ScoreLogin, 1, (GameMetrics.ResultKey, "ok"));
                transaction.Finish(SpanStatus.Ok);
                _jwtToken = (await response.Content.ReadAsStringAsync()).Replace("\"", "");
            }
            else
            {
                Debug.Log("Login to leaderboard failed.");
                GameMetrics.Count(
                    GameMetrics.ScoreLogin,
                    1,
                    (GameMetrics.ResultKey, ((int)response.StatusCode).ToString())
                );
                transaction.Finish(SpanStatus.Unavailable);
                _jwtToken = null;
            }
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"Login failed: {ex.Message}");
            GameMetrics.Count(GameMetrics.ScoreLogin, 1, (GameMetrics.ResultKey, "error"));
            transaction.Finish(SpanStatus.InternalError);
            _jwtToken = null;
        }
        finally
        {
            RunTrace.ClearScopeTransaction();
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
            Debug.Log("Not uploading the score: no leaderboard session.");
            GameMetrics.Count(GameMetrics.ScoreUpload, 1, (GameMetrics.ResultKey, "no_session"));
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

        var uploadTransaction = RunTrace.StartTransaction("scoreposter", "upload");
        RunTrace.SetScopeTransaction(uploadTransaction);

        // Inside the transaction, so a spike in the failure count leads straight to a trace.
        var started = System.Diagnostics.Stopwatch.StartNew();
        var result = "error";

        try
        {
            var url = _demoConfig.ApiUrl + "/score";

#if UNITY_SWITCH
            // Same manual UnityWebRequest path as LoginAsync (HttpClient does not work on
            // Switch): http.client span, trace headers, breadcrumb, failed-request event.
            var span = uploadTransaction.StartChild("http.client", $"POST {url}");
            span.SetExtra("http.request.method", "POST");
            var uri = new Uri(url);
            if (!string.IsNullOrWhiteSpace(uri.Host))
            {
                span.SetExtra("server.address", uri.Host);
            }

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + _jwtToken);
                PropagateTraceHeaders(request, span);

                await request.SendWebRequest();

                var statusCode = (int)request.responseCode;
                AddHttpBreadcrumb(url, statusCode);
                span.SetExtra("http.response.status_code", statusCode);
                span.Finish(GetSpanStatusFromHttpCode(statusCode));

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log("Uploading score to leaderboard failed.");
                    CaptureFailedRequest("POST", url, statusCode, request.error);
                    result = statusCode.ToString();
                    _buttonText.text = "Retry";
                    uploadTransaction.Finish(SpanStatus.Unavailable);
                    return false;
                }

                Debug.Log("Uploading score to leaderboard was successful.");
                result = "ok";
                _buttonText.text = "Posted!";
                uploadTransaction.Finish(SpanStatus.Ok);
                return true;
            }
#else
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            // Per-request: the client is shared, so mutating its defaults is global state.
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content,
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                Debug.Log("Uploading score to leaderboard failed.");
                SentrySdk.CaptureException(new HttpRequestException("Failed to upload score."));
                result = ((int)response.StatusCode).ToString();
                _buttonText.text = "Retry";
                uploadTransaction.Finish(SpanStatus.Unavailable);
                return false;
            }

            Debug.Log("Uploading score to leaderboard was successful.");
            result = "ok";
            _buttonText.text = "Posted!";
            uploadTransaction.Finish(SpanStatus.Ok);
            return true;
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"Score upload failed: {ex.Message}");
            _buttonText.text = "Retry";
            uploadTransaction.Finish(SpanStatus.InternalError);
            return false;
        }
        finally
        {
            GameMetrics.Count(GameMetrics.ScoreUpload, 1, (GameMetrics.ResultKey, result));
            GameMetrics.Distribution(
                GameMetrics.ScoreUploadDuration,
                started.Elapsed.TotalMilliseconds,
                MeasurementUnit.Duration.Millisecond,
                (GameMetrics.ResultKey, result)
            );

            RunTrace.ClearScopeTransaction();
        }
    }

#if UNITY_SWITCH
    /// <summary>
    /// Propagates the sentry-trace and baggage headers to the outgoing request, so the
    /// leaderboard backend joins the distributed trace. Mimics
    /// SentryMessageHandler.PropagateTraceHeaders.
    /// </summary>
    private static void PropagateTraceHeaders(UnityWebRequest request, ISpan span)
    {
        var traceHeader = span?.GetTraceHeader() ?? SentrySdk.GetTraceHeader();
        if (traceHeader != null)
        {
            request.SetRequestHeader("sentry-trace", traceHeader.ToString());
        }

        var baggage = SentrySdk.GetBaggage();
        if (baggage != null)
        {
            request.SetRequestHeader("baggage", baggage.ToString());
        }
    }

    /// <summary>
    /// The breadcrumb SentryHttpMessageHandler would have left for the request.
    /// </summary>
    private static void AddHttpBreadcrumb(string url, int statusCode)
    {
        SentrySdk.AddBreadcrumb(
            message: string.Empty,
            category: "http",
            type: "http",
            data: new Dictionary<string, string>
            {
                { "url", url },
                { "method", "POST" },
                { "status_code", statusCode.ToString() },
            }
        );
    }

    /// <summary>
    /// Maps HTTP status codes to span statuses. Mimics SpanStatusConverter.FromHttpStatusCode.
    /// </summary>
    private static SpanStatus GetSpanStatusFromHttpCode(int code)
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
            _ => SpanStatus.UnknownError,
        };
    }

    /// <summary>
    /// Captures a 4xx/5xx response as an event. Mimics SentryHttpFailedRequestHandler.
    /// </summary>
    private static void CaptureFailedRequest(string method, string url, int statusCode, string error)
    {
        if (statusCode < 400)
        {
            return;
        }

        var exception = new System.Net.Http.HttpRequestException(
            $"Response status code does not indicate success: {statusCode} ({error})"
        );

        var sentryEvent = new SentryEvent(exception)
        {
            Request = new SentryRequest
            {
                Url = url,
                Method = method,
                QueryString = new Uri(url).Query,
            },
        };
        sentryEvent.Contexts["response"] = new Dictionary<string, object>
        {
            { "status_code", statusCode },
        };

        SentrySdk.CaptureEvent(sentryEvent);
    }
#endif
}
