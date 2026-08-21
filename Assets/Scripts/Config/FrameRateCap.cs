using UnityEngine;

/// <summary>
/// Pins the Web build to a frame rate the Raspberry Pi kiosk can hold every frame.
/// </summary>
/// <remarks>
/// <para>
/// The Pi renders a gameplay frame in roughly 22ms against a 60Hz panel. Uncapped, that
/// lands between one and two vsync intervals, so frames alternate 16.7ms / 33.3ms and the
/// uneven pacing reads as stutter -- the complaint is the jitter, not the average rate.
/// Pinning to 30 gives every frame exactly two intervals, and leaves ~11ms of headroom for
/// the frames where enemy counts spike.
/// </para>
/// <para>
/// Web only: desktop targets have the budget for 60 and should not inherit the cap.
/// </para>
/// </remarks>
public static class FrameRateCap
{
    private const int WebTargetFrameRate = 30;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // vSyncCount must be cleared first: while it is non-zero Unity paces off the
        // display's refresh and ignores targetFrameRate entirely, so setting the target
        // alone silently does nothing.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = WebTargetFrameRate;
#endif
    }
}
