using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// The on-device top ten, shown once a run has been recorded.
/// </summary>
/// <remarks>
/// <para>
/// Three text columns rather than one block with tab stops: each column aligns itself, so the
/// numbers line up without depending on the font being monospaced or on rich-text positioning
/// that would need retuning every time the type changes.
/// </para>
/// <para>
/// Always draws its full height, padding with placeholder rows, so the board looks like a board
/// on a fresh install rather than like one entry floating in space.
/// </para>
/// <para>
/// A store with nothing to read hides it. That is the remote case, whose board lives on the web.
/// </para>
/// </remarks>
public class LeaderboardBoard : MonoBehaviour
{
    [SerializeField] private GameObject _root;

    [Tooltip("Rank and initials, left aligned")]
    [SerializeField] private TextMeshProUGUI _ranks;

    [Tooltip("Scores, right aligned")]
    [SerializeField] private TextMeshProUGUI _scores;

    [Tooltip("Dates, right aligned")]
    [SerializeField] private TextMeshProUGUI _dates;

    [SerializeField] private int _rows = 10;

    [Tooltip("Colour for the run that was just recorded")]
    [SerializeField] private Color _highlight = new Color(1f, 0.72f, 0.15f);

    /// <summary>
    /// Fills the board from the store and shows it. <paramref name="highlightKey"/> marks the
    /// run just submitted, so the player can find themselves without reading every row.
    /// </summary>
    /// <returns>Whether there was a board to show at all.</returns>
    public async Task<bool> ShowAsync(IScoreStore store, string highlightKey)
    {
        if (store == null)
        {
            Hide();
            return false;
        }

        var top = await store.TopAsync(_rows);
        if (top == null || top.Count == 0)
        {
            Hide();
            return false;
        }

        var ranks = new StringBuilder();
        var scores = new StringBuilder();
        var dates = new StringBuilder();
        var tint = ColorUtility.ToHtmlStringRGB(_highlight);

        for (var i = 0; i < _rows; i++)
        {
            if (i > 0)
            {
                ranks.Append('\n');
                scores.Append('\n');
                dates.Append('\n');
            }

            var entry = i < top.Count ? top[i] : null;
            var mine = entry != null && !string.IsNullOrEmpty(highlightKey) && entry.Key == highlightKey;

            var open = mine ? $"<color=#{tint}>" : string.Empty;
            var close = mine ? "</color>" : string.Empty;

            ranks.Append(open).Append((i + 1).ToString().PadLeft(2)).Append("  ")
                 .Append(entry != null ? Initials(entry.Name) : "---").Append(close);
            scores.Append(open).Append(entry != null ? entry.Score.ToString("N0") : "-----").Append(close);
            dates.Append(open).Append(entry != null ? Date(entry.Timestamp) : "--").Append(close);
        }

        _ranks.text = ranks.ToString();
        _scores.text = scores.ToString();
        _dates.text = dates.ToString();

        _root.SetActive(true);
        return true;
    }

    public void Hide()
    {
        _root.SetActive(false);
    }

    /// <summary>Three characters, always. The store trims, but a hand-edited file might not.</summary>
    private static string Initials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "???";
        }

        var trimmed = name.Trim().ToUpperInvariant();
        return trimmed.Length <= 3 ? trimmed.PadRight(3) : trimmed.Substring(0, 3);
    }

    /// <summary>
    /// The day the run happened. Anything unparseable shows as unknown rather than throwing:
    /// the file is on disk and can have been through a crash or an editor.
    /// </summary>
    private static string Date(string timestamp)
    {
        if (DateTime.TryParse(timestamp, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var when))
        {
            return when.ToString("dd MMM yy", CultureInfo.InvariantCulture).ToUpperInvariant();
        }

        return "--";
    }
}
