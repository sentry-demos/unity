using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Where a finished run's score goes. One implementation per <see cref="ScoreMode"/>.
/// </summary>
/// <remarks>
/// The view talks only to this, which is what lets the same game-over screen serve an upload to
/// a backend and a board kept on the device. In particular <see cref="NameEntry"/> and
/// <see cref="NameLengthLimit"/> live here rather than on the name field: the mode decides how a
/// name is entered and how long it may be, and the screen shows whichever widget it is told to
/// instead of asking which mode it is in.
/// </remarks>
public interface IScoreStore : IDisposable
{
    /// <summary>
    /// Whether this store is usable at all -- configured, not necessarily reachable. Reaching
    /// the backend is discovered during <see cref="SubmitAsync"/>, not before.
    /// </summary>
    bool CanSubmit { get; }

    /// <summary>Which way this store asks for a name. See <see cref="NameEntryStyle"/>.</summary>
    NameEntryStyle NameEntry { get; }

    /// <summary>Longest name this store accepts, or 0 for no limit (as TMP_InputField reads it).</summary>
    int NameLengthLimit { get; }

    /// <summary>What to tell the player about the backend. See <see cref="ConnectionState"/>.</summary>
    ConnectionState Connection { get; }

    /// <summary>
    /// Raised on the main thread whenever <see cref="Connection"/> changes, so the indicator
    /// does not have to poll for it.
    /// </summary>
    event Action<ConnectionState> ConnectionChanged;

    /// <summary>
    /// Reaches the backend without submitting anything, so the player can be told whether a
    /// score will go anywhere before they type a name. Does nothing for a store with no backend.
    /// </summary>
    Task TryConnectAsync(CancellationToken cancellationToken);

    /// <summary>Records one run. Returns whether it was stored; never throws for an expected failure.</summary>
    Task<bool> SubmitAsync(ScoreEntry entry);

    /// <summary>
    /// The best runs this store knows about, highest first, at most <paramref name="count"/> of
    /// them. Empty rather than throwing when there is nothing to read.
    /// </summary>
    Task<IReadOnlyList<ScoreEntry>> TopAsync(int count);
}
