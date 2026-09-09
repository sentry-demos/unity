using System;
using UnityEngine;

/// <summary>Where a finished run's score goes.</summary>
public enum ScoreMode
{
    /// <summary>Nothing is kept. The game-over screen shows no score entry at all.</summary>
    None,

    /// <summary>Kept on this device, for the offline leaderboard. Needs no configuration.</summary>
    Local,

    /// <summary>Uploaded to the leaderboard backend. Needs a URL and credentials.</summary>
    Remote,
}

/// <summary>The credentials the leaderboard backend expects. Field names are the wire format.</summary>
[Serializable]
public class LeaderboardCredentials
{
    public string Username;
    public string Password;
}

/// <summary>
/// How this machine handles scores. Unlike <see cref="DemoConfiguration"/>, the asset behind
/// this is deliberately NOT in source control.
/// </summary>
/// <remarks>
/// <para>
/// There are two use cases and they want different things. CI builds and plays the game with no
/// leaderboard at all, and a kiosk build posts to a real backend with real credentials. Keeping
/// the asset out of the repo means neither has to carry the other's settings, and no credential
/// is ever committed.
/// </para>
/// <para>
/// So a missing asset is the normal case, not an error, and it means <see cref="ScoreMode.Local"/>:
/// local scores need no configuration, remote scores need it by definition. Read this through the
/// static members below, which all answer sensibly with no asset present.
/// </para>
/// <para>
/// Reach it only through <c>Resources.Load</c>, never a serialized inspector reference. A
/// serialized reference resolves to null on any machine without the asset, and makes the
/// referencing scene or prefab churn depending on who opened it.
/// </para>
/// </remarks>
[CreateAssetMenu(
    fileName = "Assets/Resources/LeaderboardConfig.asset",
    menuName = "LeaderboardConfig",
    order = 1000
)]
public class LeaderboardConfiguration : ScriptableObject
{
    [Header("Score Handling")]
    [Tooltip("Where a finished run's score goes. Remote also needs the section below filled in.")]
    [SerializeField] private ScoreMode _mode = ScoreMode.Local;

    [Header("Remote Leaderboard")]
    [Tooltip("Base URL of the leaderboard backend, with no trailing slash.")]
    [SerializeField] private string _apiUrl = string.Empty;

    [Tooltip("Login for the leaderboard backend. Ships inside the player build, so keep it throwaway.")]
    [SerializeField] private LeaderboardCredentials _credentials;

    /// <summary>The configured mode, or <see cref="ScoreMode.Local"/> when there is no asset.</summary>
    public static ScoreMode Mode
    {
        get
        {
            var config = Load();
            return config == null ? ScoreMode.Local : config._mode;
        }
    }

    /// <summary>The backend's base URL, or empty when unset or when there is no asset.</summary>
    public static string ApiUrl
    {
        get
        {
            var config = Load();
            return config == null ? string.Empty : config._apiUrl;
        }
    }

    /// <summary>The backend login, or null when unset or when there is no asset.</summary>
    public static LeaderboardCredentials Credentials
    {
        get
        {
            var config = Load();
            return config == null ? null : config._credentials;
        }
    }

    private static LeaderboardConfiguration _instance;

    // Separate from the null check below because a missing asset is expected here. Without it,
    // every read would hit Resources.Load again on the machines that have no asset -- which is
    // CI and a fresh checkout.
    private static bool _loadAttempted;

    public static LeaderboardConfiguration Load()
    {
        if (!_loadAttempted)
        {
            _loadAttempted = true;
            _instance = Resources.Load("LeaderboardConfig") as LeaderboardConfiguration;
        }
        return _instance;
    }
}
