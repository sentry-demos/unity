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
/// It reaches for the backend exactly once, when the screen appears. It used to retry on its own
/// with a widening gap, which meant a player sitting on the game-over screen kept hammering a
/// backend that was not answering. Every attempt after the first belongs to the player: the
/// submit button reads "Retry" once an upload has failed, and pressing it logs in afresh.
/// </para>
/// <para>
/// Failure leaves the light red rather than hiding it: "we tried and could not" is worth telling
/// the player. A store with no backend hides it entirely instead, because there is nothing wrong
/// with keeping a score on the device, and so nothing to report.
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

    private IScoreStore _store;
    private CancellationTokenSource _cancellation;

    /// <summary>
    /// Starts reporting on a store, and makes the one attempt to reach it. Safe to call again;
    /// the previous attempt is abandoned first.
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
        _ = ConnectAsync(_cancellation.Token);
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
        // "Try Again" reloads the scene, and the store is disposed with it. An attempt still in
        // flight here would come back holding a disposed client.
        Unbind();
    }

    /// <summary>
    /// One attempt, so the light has an answer by the time a name is typed. Whether to try again
    /// is the player's call, made on the submit button.
    /// </summary>
    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _store.TryConnectAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The screen went away mid-attempt. Nothing left to draw on.
        }
        catch (Exception ex)
        {
            // The store reports its own failures; this is only for something unforeseen.
            Debug.LogWarning($"Connection attempt failed: {ex.Message}");
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
