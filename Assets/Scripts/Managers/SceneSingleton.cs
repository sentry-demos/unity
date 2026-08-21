using UnityEngine;

/// <summary>
/// A MonoBehaviour with one instance per scene, reachable from code that cannot hold an
/// inspector reference.
/// </summary>
/// <remarks>
/// <para>
/// For spawned prefabs only -- enemies, pickups and projectiles are instantiated at runtime,
/// so they cannot be wired to a scene object ahead of time. Anything that lives in the scene
/// should take a <c>[SerializeField]</c> reference instead; it is checked at edit time and
/// does not depend on script execution order.
/// </para>
/// <para>
/// Scene-local by design: <see cref="Instance"/> is cleared on destroy, so it does not leak
/// across a scene load. Nothing here calls <c>DontDestroyOnLoad</c>. A subclass that needs to
/// survive a load wants a different base class, not an extra flag on this one.
/// </para>
/// <para>
/// A subclass overriding <c>Awake</c> or <c>OnDestroy</c> must call <c>base</c> -- otherwise
/// the instance is never claimed or never released.
/// </para>
/// </remarks>
public abstract class SceneSingleton<T> : MonoBehaviour
    where T : SceneSingleton<T>
{
    private static T _instance;

    /// <summary>
    /// The instance for the current scene, or null if the scene has none.
    /// </summary>
    /// <remarks>
    /// Falls back to a scene search when read before the instance's own <c>Awake</c> has run.
    /// Unity does not order <c>Awake</c> between objects, and this is reachable from one:
    /// <c>UpgradeManager.Awake</c> levels up the starting weapon, which reaches
    /// <c>Player.Instance.WeaponManager</c>. The fallback caches, so it costs one search at
    /// most -- but it is a safety net, not the normal path.
    /// </remarks>
    public static T Instance
    {
        get
        {
            // Unity's == overload: also re-searches if the cached instance was destroyed.
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<T>();
            }

            return _instance;
        }
    }

    /// <summary>True on the instance that owns <see cref="Instance"/>.</summary>
    /// <remarks>
    /// Reads the backing field directly, so a subclass can check it in <c>Awake</c> without
    /// tripping the lazy search in the property.
    /// </remarks>
    protected bool IsActiveInstance => _instance == this;

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning(
                $"{typeof(T).Name}: a second instance awoke on '{name}'. Destroying it -- "
                    + $"'{_instance.name}' claimed the scene first."
            );

            Destroy(gameObject);
            return;
        }

        _instance = (T)this;
    }

    protected virtual void OnDestroy()
    {
        // Guarded: a duplicate destroyed in Awake must not clear the real instance.
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
