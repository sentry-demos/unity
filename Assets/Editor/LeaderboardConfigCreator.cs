using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the per-machine leaderboard config, since it is deliberately not in source control.
/// </summary>
/// <remarks>
/// Cloning the repo onto a new machine gets you no asset, which is the intended state: no asset
/// means local scores, and the on-device board and the demo run behind it work with no setup at
/// all. This is for the other case, where a machine should post to the backend and needs a URL
/// and a login of its own.
/// </remarks>
public static class LeaderboardConfigCreator
{
    private const string AssetPath = "Assets/Resources/LeaderboardConfig.asset";

    [MenuItem("Sentaur/Leaderboard Config", priority = 100)]
    private static void CreateOrSelect()
    {
        var existing = AssetDatabase.LoadAssetAtPath<LeaderboardConfiguration>(AssetPath);
        if (existing != null)
        {
            Debug.Log($"Leaderboard config already exists at {AssetPath}.");
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        var directory = Path.GetDirectoryName(AssetPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
        }

        // Local by default, so a machine that only wants the on-device board is already done.
        // Remote needs the URL and login filling in by hand; they are not in the repo and are
        // not meant to be.
        var config = ScriptableObject.CreateInstance<LeaderboardConfiguration>();
        AssetDatabase.CreateAsset(config, AssetPath);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"Created {AssetPath}, set to Local.\n"
                + "For a machine that builds for consoles or handhelds, set Score Handling to "
                + "Remote and fill in the API URL, Username and Password on the asset now "
                + "selected in the Project window.\n"
                + "It is gitignored on purpose, so it stays on this machine. Note the asset "
                + "ships inside every player build made here, credentials and all."
        );

        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
    }
}
