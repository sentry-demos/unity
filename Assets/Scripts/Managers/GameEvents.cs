using System;
using UnityEngine;

/// <summary>
/// The payload for <see cref="GameEvents.PickupGrabbed"/>.
/// </summary>
/// <remarks>
/// Carries the icon rather than the pickup's <c>GameObject</c>: the pickup destroys itself
/// as it raises the event, so anything reaching back through the object would be relying on
/// <c>Destroy</c> being deferred to the end of the frame.
/// </remarks>
public readonly struct PickupCollected
{
    public readonly int ScoreValue;
    public readonly Sprite Icon;

    /// <summary>How long the effect lasts. Zero for instant pickups.</summary>
    public readonly float EffectDuration;

    public PickupCollected(int scoreValue, Sprite icon, float effectDuration)
    {
        ScoreValue = scoreValue;
        Icon = icon;
        EffectDuration = effectDuration;
    }
}

/// <summary>
/// The game's events, as compile-time checked delegates.
/// </summary>
/// <remarks>
/// Subscribe in <c>OnEnable</c> and unsubscribe in <c>OnDisable</c>. These events are static,
/// so a listener that never unsubscribes stays registered across a scene reload and its
/// handler runs once more per reload.
/// </remarks>
public static class GameEvents
{
    public static event Action<int> EnemyDestroyed;
    public static event Action<PickupCollected> PickupGrabbed;
    public static event Action<int> XpEarned;
    public static event Action PlayerDeath;
    public static event Action TryAgain;
    public static event Action Quit;

    public static void RaiseEnemyDestroyed(int scoreValue) => EnemyDestroyed?.Invoke(scoreValue);

    public static void RaisePickupGrabbed(PickupCollected pickup) => PickupGrabbed?.Invoke(pickup);

    public static void RaiseXpEarned(int xp) => XpEarned?.Invoke(xp);

    public static void RaisePlayerDeath() => PlayerDeath?.Invoke();

    public static void RaiseTryAgain() => TryAgain?.Invoke();

    public static void RaiseQuit() => Quit?.Invoke();
}
