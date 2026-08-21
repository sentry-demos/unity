/// <summary>
/// The tags this code compares against, as constants.
/// </summary>
/// <remarks>
/// <para>
/// Unity has no runtime API for enumerating tags -- <c>UnityEngine.GameObject</c> can only
/// get, set, compare and search by one. The full list is editor-only
/// (<c>UnityEditorInternal.InternalEditorUtility.tags</c>), so it cannot be generated or
/// validated from gameplay code. Hence constants, kept in sync with
/// <c>ProjectSettings/TagManager.asset</c> by hand.
/// </para>
/// <para>
/// Only tags the code actually compares against belong here; the rest live in the
/// TagManager and are used from the inspector. Pair these with <c>CompareTag</c> rather
/// than <c>gameObject.tag == ...</c>: the property getter allocates a string,
/// <c>CompareTag</c> does not. Note a tag missing from the TagManager makes
/// <c>CompareTag</c> throw rather than quietly return false.
/// </para>
/// </remarks>
public static class Tags
{
    public const string PlayerHitbox = "PlayerHitbox";
    public const string Enemy = "Enemy";
    public const string Barrier = "Barrier";
    public const string XpDrop = "XpDrop";
}
