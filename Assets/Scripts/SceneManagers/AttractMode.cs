/// <summary>
/// Whether the battle currently running is a background demo rather than a real run.
/// </summary>
/// <remarks>
/// <para>
/// After a score is recorded the board goes up and the game keeps playing itself behind it,
/// the way an arcade cabinet does, until someone presses Again.
/// </para>
/// <para>
/// The demo run is a genuine reload rather than the dead player being brought back. Reviving in
/// place would mean resetting hit points, the death animation, the enemies already on screen and
/// the difficulty the last run had climbed to -- which is most of what loading the scene does
/// anyway, only by hand and with more to get wrong.
/// </para>
/// <para>
/// Static because it has to survive that reload. The scene goes away between runs; the process
/// does not.
/// </para>
/// </remarks>
public static class AttractMode
{
    /// <summary>True while the battle on screen is playing itself.</summary>
    public static bool Active;

    /// <summary>
    /// The run just recorded, so the board can still pick it out after the reload that starts
    /// the demo. Null once a real run begins.
    /// </summary>
    public static string JustPosted;

    /// <summary>Begins a demo run. The caller reloads the scene.</summary>
    public static void Begin(string justPostedKey)
    {
        Active = true;
        JustPosted = justPostedKey;
    }

    /// <summary>A human took over. The next run counts.</summary>
    public static void End()
    {
        Active = false;
        JustPosted = null;
    }
}
