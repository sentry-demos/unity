using System;
using UnityEngine;

/// <summary>
/// How a player puts a name to a run, whichever way this machine asks for one.
/// </summary>
/// <remarks>
/// <para>
/// Two implementations, and the game-over screen does not care which it has.
/// <see cref="NameEntryField"/> is a text box, typed on whatever keyboard the platform offers.
/// <see cref="ArcadeNameEntry"/> is a row of letter slots wound up and down with a stick.
/// </para>
/// <para>
/// The picker exists because a keyboard is not a given. A WebGL build reports no on-screen
/// keyboard to open, and in a kiosk cabinet there is no physical one to fall back on either --
/// the text box that works on a desktop leaves a player there with no way to enter anything.
/// </para>
/// <para>
/// <see cref="Navigate"/> and <see cref="Confirm"/> are how the screen hands input over. Both
/// answer whether the entry used it, and a step it did not use is the screen's to act on. That
/// is what carries the highlight off the name and down onto the buttons.
/// </para>
/// </remarks>
public interface INameEntry
{
    /// <summary>The name as it stands. Empty until there is something worth posting.</summary>
    string Text { get; }

    /// <summary>Raised whenever <see cref="Text"/> changes.</summary>
    event Action TextChanged;

    /// <summary>
    /// Whether the entry is swallowing keystrokes. A text box with typing focus is; the picker
    /// never is, having nothing to type into.
    /// </summary>
    bool IsTyping { get; }

    /// <summary>Whether the player could enter a name right now.</summary>
    bool CanEdit { get; }

    /// <summary>What the screen bounces to say navigation has landed here.</summary>
    Transform Transform { get; }

    /// <summary>How many characters a name may have. 0 means no limit.</summary>
    void SetLengthLimit(int limit);

    /// <summary>Puts the player into the entry.</summary>
    void Focus();

    /// <summary>Takes them back out.</summary>
    void Blur();

    /// <summary>Stops further editing, once the name has been recorded.</summary>
    void Lock();

    /// <summary>
    /// Offers one step of navigation. <paramref name="fromKeyboard"/> says it came from a key
    /// rather than a stick, which the text box has to know: while typing, W and S are letters
    /// and must not also work the menu.
    /// </summary>
    /// <returns>Whether the entry used the step.</returns>
    bool Navigate(Vector2Int step, bool fromKeyboard);

    /// <summary>Offers the confirm button.</summary>
    /// <returns>Whether the entry used it, as opposed to being finished with it.</returns>
    bool Confirm();
}
