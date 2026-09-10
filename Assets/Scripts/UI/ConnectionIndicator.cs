using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The little light in the corner of the game-over screen saying whether the score has anywhere
/// to go.
/// </summary>
/// <remarks>
/// <para>
/// It retries on its own, widening the gap each time, because a connection that was down when
/// the run started is often back by the time the run ends. Giving up leaves the light red rather
/// than hiding it: "we tried and could not" is worth telling the player.
/// </para>
/// <para>
/// A store with no backend hides it entirely instead of showing failure. There is nothing wrong
/// with keeping a score on the device, so there is nothing to report.
/// </para>
/// <para>
/// Waits are real-time, which matters: this screen runs at <c>Time.timeScale = 0</c>.
/// </para>
/// </remarks>
public class ConnectionIndicator : MonoBehaviour
{
    [Tooltip("The dot that changes colour")]
    [SerializeField] private Image _light;

    [Header("States")]
    [SerializeField] private Color _connecting = new Color(0.96f, 0.71f, 0.24f);
    [SerializeField] private Color _connected = new Color(0.29f, 0.78f, 0.45f);
    [SerializeField] private Color _failed = new Color(0.85f, 0.34f, 0.30f);

    [Header("Retry")]
    [Tooltip("Seconds before the first retry. Each further wait is twice the last")]
    [SerializeField] private float _firstRetryDelay = 1f;

    [Tooltip("How many attempts before giving up and leaving the light red")]
    [SerializeField] private int _maxAttempts = 5;

    private IScoreStore _store;
    private CancellationTokenSource _cancellation;

    /// <summary>
    /// Starts reporting on a store, and starts trying to reach it. Safe to call again; the
    /// previous attempt is abandoned first.
    /// </summary>
    public void Bind(IScoreStore store)
    {
        Unbind();

        if (store == null || store.Connection == ConnectionState.NotApplicable)
        {
            gameObject.SetActive(false);
            return;
        }

        _store = store;
        _store.ConnectionChanged += Render;

        gameObject.SetActive(true);
        Render(_store.Connection);

        _cancellation = new CancellationTokenSource();
        _ = RetryUntilConnectedAsync(_cancellation.Token);
    }

    /// <summary>Stops reporting and abandons any attempt in flight.</summary>
    public void Unbind()
    {
        if (_store != null)
        {
            _store.ConnectionChanged -= Render;
            _store = null;
        }

        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        // "Try Again" reloads the scene, and the store is disposed with it. A retry still
        // sleeping here would wake up holding a disposed client.
        Unbind();
    }

    private async Task RetryUntilConnectedAsync(CancellationToken cancellationToken)
    {
        var wait = _firstRetryDelay;

        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            try
            {
                await _store.TryConnectAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // The store reports its own failures; this is only for something unforeseen.
                Debug.LogWarning($"Connection attempt {attempt} failed: {ex.Message}");
            }

            if (cancellationToken.IsCancellationRequested || _store == null)
            {
                return;
            }

            if (_store.Connection == ConnectionState.Connected || attempt == _maxAttempts)
            {
                return;
            }

            try
            {
                // Real time on purpose: the game-over screen has the clock stopped.
                await Task.Delay(TimeSpan.FromSeconds(wait), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            wait *= 2f;
        }
    }

    private void Render(ConnectionState state)
    {
        if (_light == null)
        {
            return;
        }

        switch (state)
        {
            case ConnectionState.NotApplicable:
                gameObject.SetActive(false);
                return;
            case ConnectionState.Connected:
                _light.color = _connected;
                break;
            case ConnectionState.Failed:
                _light.color = _failed;
                break;
            default:
                _light.color = _connecting;
                break;
        }
    }
}
