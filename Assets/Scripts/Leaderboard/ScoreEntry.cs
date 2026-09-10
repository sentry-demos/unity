using System;

/// <summary>
/// One finished run, as the leaderboard records it.
/// </summary>
/// <remarks>
/// Serialized with <c>JsonUtility</c> both on the wire and, once local scores land, on disk,
/// so these field names are a format. Renaming one breaks the backend contract, existing save
/// files, or both.
/// </remarks>
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
