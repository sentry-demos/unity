using System;
using System.Runtime.InteropServices;
using Sentry.Unity;
using UnityEngine;

/// <summary>
/// INTENTIONAL: the native crash this demo exists to show off.
/// </summary>
/// <remarks>
/// <para>
/// <c>save_score_to_disk</c> (NativeSaver.c) crashes on purpose, so Sentry can be seen catching
/// a native crash with a clean, named frame. Two paths reach it: the game-over crash, gated on
/// <see cref="DemoConfiguration.CrashOnGameOver"/>, and the d-pad sequence in
/// <see cref="ForceCrashSequence"/>, which is armed by default so the crash can be demoed on a
/// console build. See CONTRIBUTING.md.
/// </para>
/// <para>
/// Despite the name, this never saves anything. The real on-device score board is a separate
/// thing entirely, and the two must stay easy to tell apart in a stack trace.
/// </para>
/// <para>
/// Only crashes in a player build. In the Editor the native call is compiled out and this just
/// logs.
/// </para>
/// </remarks>
public static class NativeScoreSaver
{
    public static void SaveScoreToDisk(int score)
    {
        // Emitted and flushed before the process goes away, so the metric arrives even
        // though nothing after the native call ever runs.
        GameMetrics.Count(GameMetrics.RunCrashPathEntered, 1);
        SentrySdk.Flush(TimeSpan.FromSeconds(2));

#if !UNITY_EDITOR
        Debug.Log("Calling into Native Save Utils.");

        // The log lines below are mirrored in .github/scripts/lib/DemoRun.psm1: they are how
        // the demo run tells a real crash from one the process survived.
        try
        {
            Debug.Log("Attempting save_score_to_disk...");
            save_score_to_disk(score);
            Debug.Log("save_score_to_disk completed without crash - this should not happen!");
        }
        catch (System.Exception e)
        {
            Debug.Log("save_score_to_disk threw exception: " + e.Message);
        }

        Debug.Log("ForceCrash also failed - this should not be reached!");
#else
        Debug.Log("If this was not the Editor, the score would be saved 'natively'.");
#endif
    }

    // NativeSaver.c
    [DllImport("__Internal")]
    private static extern void save_score_to_disk(int score);
}
