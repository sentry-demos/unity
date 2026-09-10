/// <summary>
/// What a score store can currently say about its backend, for the connection indicator.
/// </summary>
/// <remarks>
/// <see cref="NotApplicable"/> is a real answer, not a missing one: a store that keeps scores on
/// the device has no backend to reach, so the indicator is hidden rather than shown as failed.
/// <see cref="Disconnected"/> and <see cref="Failed"/> are also different answers -- never tried
/// versus tried and could not -- and the indicator is entitled to draw them differently.
/// </remarks>
public enum ConnectionState
{
    /// <summary>Nothing to connect to. Show no indicator at all.</summary>
    NotApplicable,

    /// <summary>There is a backend, but no attempt has been made yet.</summary>
    Disconnected,

    /// <summary>An attempt is in flight.</summary>
    Connecting,

    /// <summary>There is a usable session.</summary>
    Connected,

    /// <summary>The last attempt failed. Another one may still succeed.</summary>
    Failed,
}
