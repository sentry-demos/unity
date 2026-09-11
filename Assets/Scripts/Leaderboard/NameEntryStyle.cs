/// <summary>
/// How a score store asks for the name that goes with a run.
/// </summary>
/// <remarks>
/// It belongs to the store rather than to the game-over screen because it is the board that
/// differs, not the machine: a board on the web carries whatever name a player gives it, and an
/// on-device board is an arcade board with three characters per line. The screen reads this and
/// shows the matching widget, instead of inferring one from how long a name may be.
/// </remarks>
public enum NameEntryStyle
{
    /// <summary>
    /// A text box, typed on whatever keyboard the platform offers. Needs one, which is the
    /// trade for a name longer than three characters.
    /// </summary>
    FullName,

    /// <summary>
    /// Letter slots wound with a stick, the way a cabinet asks for initials. Needs no keyboard,
    /// so it is the one that works on a WebGL build or a kiosk cabinet.
    /// </summary>
    Initials,
}
