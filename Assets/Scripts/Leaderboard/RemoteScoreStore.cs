using System;
#if !UNITY_SWITCH && !UNITY_SWITCH2
using System.Net.Http;
#endif
using System.Threading.Tasks;
using Sentry;
using Sentry.Unity;
using UnityEngine;
#if UNITY_SWITCH || UNITY_SWITCH2
using UnityEngine.Networking;
#endif

/// <summary>
/// Posts a run to the leaderboard backend.
/// </summary>
/// <remarks>
/// <para>
/// The session is established lazily, on the first submit that needs it. It used to be
/// established when the battle scene started, which meant a connection that was slow or absent
/// at that moment left the player unable to post a score they had already earned, several
/// minutes later, over a connection that had since come back.
/// </para>
/// <para>
/// A failed attempt is deliberately not cached: the next submit tries again from scratch. That
/// is what makes a retry meaningful.
/// </para>
/// <para>
/// Switch and Switch 2 cannot use <c>HttpClient</c> or <c>SentryHttpMessageHandler</c>, so they
/// send through <see cref="UnityWebRequest"/> and hand the instrumentation to
/// <see cref="SentryWebRequest"/>. Only those branches are platform-specific; everything around
/// them is shared.
/// </para>
/// </remarks>
public sealed class RemoteScoreStore : IScoreStore
{
    private readonly string _apiUrl;
    private readonly LeaderboardCredentials _credentials;

    private string _jwtToken;
    private Task _loginTask;

#if !UNITY_SWITCH && !UNITY_SWITCH2
    private HttpClient _httpClient;
#endif

    public RemoteScoreStore(string apiUrl, LeaderboardCredentials credentials)
    {
        _apiUrl = apiUrl;
        _credentials = credentials;
    }

    /// <summary>Configured is enough. Whether the backend answers is found out at submit time.</summary>
    public bool CanSubmit => !string.IsNullOrEmpty(_apiUrl);

    /// <summary>The backend takes a full name.</summary>
    public int NameLengthLimit => 0;

    public ConnectionState Connection { get; private set; } = ConnectionState.Disconnected;

    public async Task<bool> SubmitAsync(ScoreEntry entry)
    {
        if (!await EnsureSessionAsync())
        {
            Debug.Log("Not uploading the score: no leaderboard session.");
            GameMetrics.Count(GameMetrics.ScoreUpload, 1, (GameMetrics.ResultKey, "no_session"));
            return false;
        }

        return await UploadAsync(entry);
    }

    public void Dispose()
    {
#if !UNITY_SWITCH && !UNITY_SWITCH2
        // "Try Again" reloads the scene, so this would otherwise leak per reload.
        _httpClient?.Dispose();
        _httpClient = null;
#endif
    }

    /// <summary>
    /// Logs in if there is no usable session yet, awaiting a login already in flight rather than
    /// starting a second one.
    /// </summary>
    private async Task<bool> EnsureSessionAsync()
    {
        if (!string.IsNullOrEmpty(_jwtToken))
        {
            return true;
        }

        // A finished task with no token is a failed attempt, and it must not be cached: the
        // player pressing Submit again is exactly the retry that should get a fresh try.
        if (_loginTask == null || _loginTask.IsCompleted)
        {
            _loginTask = LoginAsync();
        }

        await _loginTask;
        return !string.IsNullOrEmpty(_jwtToken);
    }

#if !UNITY_SWITCH && !UNITY_SWITCH2
    private HttpClient Client => _httpClient ??= new HttpClient(new SentryHttpMessageHandler());
#endif

    private async Task LoginAsync()
    {
        Connection = ConnectionState.Connecting;

        // On the run's trace: the score being posted is the last act of the run that earned it.
        var transaction = RunTrace.StartTransaction("scoreposter", "login");
        RunTrace.SetScopeTransaction(transaction);

        try
        {
            var json = JsonUtility.ToJson(_credentials);
            var url = _apiUrl + "/token";

#if UNITY_SWITCH || UNITY_SWITCH2
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
                SentryWebRequest.PropagateTraceHeaders(request, span);

                await request.SendWebRequest();

                var statusCode = (int)request.responseCode;
                SentryWebRequest.AddHttpBreadcrumb(url, statusCode);
                span.SetExtra("http.response.status_code", statusCode);
                span.Finish(SentryWebRequest.GetSpanStatusFromHttpCode(statusCode));

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("Login to leaderboard successful.");
                    GameMetrics.Count(GameMetrics.ScoreLogin, 1, (GameMetrics.ResultKey, "ok"));
                    transaction.Finish(SpanStatus.Ok);
                    _jwtToken = request.downloadHandler.text.Replace("\"", "");
                    Connection = ConnectionState.Connected;
                }
                else
                {
                    Debug.Log("Login to leaderboard failed.");
                    GameMetrics.Count(
                        GameMetrics.ScoreLogin,
                        1,
                        (GameMetrics.ResultKey, statusCode.ToString())
                    );
                    SentryWebRequest.CaptureFailedRequest("POST", url, statusCode, request.error);
                    transaction.Finish(SpanStatus.Unavailable);
                    _jwtToken = null;
                    Connection = ConnectionState.Failed;
                }
            }
#else
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await Client.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
            {
                Debug.Log("Login to leaderboard successful.");
                GameMetrics.Count(GameMetrics.ScoreLogin, 1, (GameMetrics.ResultKey, "ok"));
                transaction.Finish(SpanStatus.Ok);
                _jwtToken = (await response.Content.ReadAsStringAsync()).Replace("\"", "");
                Connection = ConnectionState.Connected;
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
                Connection = ConnectionState.Failed;
            }
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"Login failed: {ex.Message}");
            GameMetrics.Count(GameMetrics.ScoreLogin, 1, (GameMetrics.ResultKey, "error"));
            transaction.Finish(SpanStatus.InternalError);
            _jwtToken = null;
            Connection = ConnectionState.Failed;
        }
        finally
        {
            RunTrace.ClearScopeTransaction();
        }
    }

    private async Task<bool> UploadAsync(ScoreEntry entry)
    {
        var json = JsonUtility.ToJson(entry);

        var uploadTransaction = RunTrace.StartTransaction("scoreposter", "upload");
        RunTrace.SetScopeTransaction(uploadTransaction);

        // Inside the transaction, so a spike in the failure count leads straight to a trace.
        var started = System.Diagnostics.Stopwatch.StartNew();
        var result = "error";

        try
        {
            var url = _apiUrl + "/score";

#if UNITY_SWITCH || UNITY_SWITCH2
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
                SentryWebRequest.PropagateTraceHeaders(request, span);

                await request.SendWebRequest();

                var statusCode = (int)request.responseCode;
                SentryWebRequest.AddHttpBreadcrumb(url, statusCode);
                span.SetExtra("http.response.status_code", statusCode);
                span.Finish(SentryWebRequest.GetSpanStatusFromHttpCode(statusCode));

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log("Uploading score to leaderboard failed.");
                    SentryWebRequest.CaptureFailedRequest("POST", url, statusCode, request.error);
                    result = statusCode.ToString();
                    uploadTransaction.Finish(SpanStatus.Unavailable);
                    return false;
                }

                Debug.Log("Uploading score to leaderboard was successful.");
                result = "ok";
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

            var response = await Client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                Debug.Log("Uploading score to leaderboard failed.");
                SentrySdk.CaptureException(new HttpRequestException("Failed to upload score."));
                result = ((int)response.StatusCode).ToString();
                uploadTransaction.Finish(SpanStatus.Unavailable);
                return false;
            }

            Debug.Log("Uploading score to leaderboard was successful.");
            result = "ok";
            uploadTransaction.Finish(SpanStatus.Ok);
            return true;
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"Score upload failed: {ex.Message}");
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
}
