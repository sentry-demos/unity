using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sentry.Unity;
using UnityEngine;

/// <summary>
/// Keeps the best runs on this device, for the offline leaderboard.
/// </summary>
/// <remarks>
/// <para>
/// Needs no configuration, which is why it is what a machine with no leaderboard asset gets.
/// Names are short here on purpose: three characters, the way an arcade cabinet asks.
/// </para>
/// <para>
/// Nothing about this is a demo fault. It is a real feature and has to work.
/// </para>
/// <para>
/// The directory is injected so the whole thing is exercisable without a scene. That is also
/// why the file work is synchronous behind an async signature: the board is a few hundred
/// bytes, written once at the end of a run, and pretending otherwise would buy nothing.
/// </para>
/// </remarks>
public sealed class LocalScoreStore : IScoreStore
{
    /// <summary>How many runs the board keeps. Everything below the cut is dropped on write.</summary>
    public const int MaxEntries = 10;

    /// <summary>Arcade rules.</summary>
    public const int NameLimit = 3;

    private const string FileName = "leaderboard.json";

    private readonly string _path;

    public LocalScoreStore()
        : this(Application.persistentDataPath) { }

    public LocalScoreStore(string directory)
    {
        _path = Path.Combine(directory, FileName);
    }

    /// <summary>Always. There is no session to establish and nothing to be unreachable.</summary>
    public bool CanSubmit => true;

    public int NameLengthLimit => NameLimit;

    /// <summary>Nothing to connect to, so the indicator stays hidden rather than showing failure.</summary>
    public ConnectionState Connection => ConnectionState.NotApplicable;

    /// <summary>Never fires. This store is always in the one state it can be in.</summary>
    public event Action<ConnectionState> ConnectionChanged
    {
        add { }
        remove { }
    }

    /// <summary>Nothing to reach. Returns at once so the caller needs no special case.</summary>
    public Task TryConnectAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<bool> SubmitAsync(ScoreEntry entry)
    {
        try
        {
            // The store owns the rule, so it holds even if something bypasses the name field.
            if (!string.IsNullOrEmpty(entry.Name) && entry.Name.Length > NameLimit)
            {
                entry.Name = entry.Name.Substring(0, NameLimit);
            }

            var board = Load();
            board.Entries.Add(entry);
            board.Entries = Rank(board.Entries, MaxEntries);
            Save(board);

            GameMetrics.Count(GameMetrics.ScoreSaved, 1, (GameMetrics.ResultKey, "ok"));
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Saving the score to the local board failed: {ex.Message}");
            SentrySdk.CaptureException(ex);
            GameMetrics.Count(GameMetrics.ScoreSaved, 1, (GameMetrics.ResultKey, "error"));
            return Task.FromResult(false);
        }
    }

    public Task<IReadOnlyList<ScoreEntry>> TopAsync(int count)
    {
        IReadOnlyList<ScoreEntry> top = count <= 0
            ? Array.Empty<ScoreEntry>()
            : Rank(Load().Entries, count);

        return Task.FromResult(top);
    }

    public void Dispose()
    {
        // Nothing held open: each call reads and writes the file outright.
    }

    /// <summary>Highest first. OrderByDescending is stable, so equal scores keep their order.</summary>
    private static List<ScoreEntry> Rank(IEnumerable<ScoreEntry> entries, int count)
    {
        return entries
            .Where(e => e != null)
            .OrderByDescending(e => e.Score)
            .Take(count)
            .ToList();
    }

    private Board Load()
    {
        if (!File.Exists(_path))
        {
            return new Board();
        }

        try
        {
            var board = JsonUtility.FromJson<Board>(File.ReadAllText(_path));
            if (board?.Entries == null)
            {
                throw new InvalidDataException($"'{FileName}' holds no score list.");
            }

            return board;
        }
        catch (Exception ex)
        {
            // A board that cannot be read is not worth taking the game down for: the player
            // starts a fresh one. Reported so it is visible if it ever stops being rare, which
            // matters here more than most places -- this app crashes on purpose.
            Debug.LogError($"The local score board could not be read: {ex.Message}");
            SentrySdk.CaptureException(ex);
            GameMetrics.Count(GameMetrics.ScoreBoardUnreadable, 1);
            return new Board();
        }
    }

    private void Save(Board board)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Written beside the board and swapped in, so a crash partway through the write leaves
        // the previous board intact rather than a half-written one. This app crashes on purpose,
        // which makes that worth the extra step.
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonUtility.ToJson(board));
        Swap(temporary, _path);
    }

    /// <summary>
    /// Moves <paramref name="temporary"/> over <paramref name="destination"/>.
    /// </summary>
    /// <remarks>
    /// File.Replace does it in one filesystem operation, but it is not implemented everywhere
    /// this game ships and it requires the destination to already exist. Where it is unavailable
    /// the delete-then-move fallback leaves a window in which there is no board at all; that is
    /// worse than Replace and still much better than writing over the board in place.
    /// </remarks>
    private static void Swap(string temporary, string destination)
    {
        if (!File.Exists(destination))
        {
            File.Move(temporary, destination);
            return;
        }

        try
        {
            File.Replace(temporary, destination, null);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException || ex is IOException)
        {
            File.Delete(destination);
            File.Move(temporary, destination);
        }
    }

    /// <summary>JsonUtility will not serialize a bare list, so the file has a root object.</summary>
    [Serializable]
    private class Board
    {
        public List<ScoreEntry> Entries = new List<ScoreEntry>();
    }
}
