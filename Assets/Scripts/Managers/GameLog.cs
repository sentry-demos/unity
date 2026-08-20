using System.Diagnostics;
using Debug = UnityEngine.Debug;

/// <summary>
/// Verbose gameplay tracing that must not reach a shipped build.
/// </summary>
/// <remarks>
/// <para>
/// Calls compile away entirely outside the Editor: <see cref="ConditionalAttribute"/> removes
/// the call *and its argument expressions* at the call site, so a string concatenation in a
/// per-frame path costs nothing in a player build. That is why this is a method rather than an
/// <c>if (enabled)</c> guard -- the guard would still allocate the string.
/// </para>
/// <para>
/// For anything a player hitting a bug should see, use <c>Debug.LogWarning</c> or
/// <c>Debug.LogError</c> directly. Those stay in builds by design, and the Sentry SDK turns
/// them into breadcrumbs.
/// </para>
/// <para>
/// The demo-gated paths (the native crash, the asset-bundle download, the upgrade fetch) also
/// keep plain <c>Debug.Log</c>: their output is part of what the demo shows -- the trail
/// leading up to an intentional failure -- so it has to survive into a player build.
/// See CONTRIBUTING.md.
/// </para>
/// </remarks>
public static class GameLog
{
    [Conditional("UNITY_EDITOR")]
    public static void Trace(string message)
    {
        Debug.Log(message);
    }

    [Conditional("UNITY_EDITOR")]
    public static void Trace(string message, UnityEngine.Object context)
    {
        Debug.Log(message, context);
    }
}
