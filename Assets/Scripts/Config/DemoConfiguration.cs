using UnityEngine;

/// <summary>
/// The deliberate faults this demo exists to show off, and nothing else.
/// </summary>
/// <remarks>
/// Leaderboard settings used to live here too. They moved to
/// <see cref="LeaderboardConfiguration"/>, whose asset is deliberately not in source control:
/// this one is checked in and describes what CI does, that one is per-machine and holds
/// credentials. See CONTRIBUTING.md.
/// </remarks>
[CreateAssetMenu(fileName = "Assets/Resources/DemoConfig.asset", menuName = "DemoConfig", order = 999)]
public class DemoConfiguration : ScriptableObject
{
    [Header("Master Switch")]
    [Tooltip("Turns on everything in CI Demo below. Set by -demo or SENTRY_DEMO at startup.")]
    [SerializeField] private bool _enabled;

    [Header("CI Demo")]
    [Tooltip("Plays the game on its own, so an unattended run reaches gameplay and dies.")]
    [SerializeField] private bool _autoPlay;

    [Tooltip("Downloads an asset bundle with no guard, so the failure surfaces in Sentry.")]
    [SerializeField] private bool _notHotDogParticleEffect;

    [Tooltip("Fetches the upgrade choice from a server that is expected to fail.")]
    [SerializeField] private bool _fetchUpgradeFromServer;

    [Tooltip("Crashes natively on game over, to demo native crash capture.")]
    [SerializeField] private bool _crashOnGameOver;

    [Header("Konami Crash")]
    [Tooltip(
        "The d-pad force-crash: Up Up Down Down Left Right Left Right. On by default and NOT "
            + "covered by the master switch, so it still fires on a console build that could "
            + "not be launched with -demo. Turn it off only to rule it out while debugging."
    )]
    [SerializeField] private bool _konamiCrash = true;

    private bool _overridesApplied;

    public bool Enabled => _enabled;

    // Each of these is ANDed with the master switch: they are the faults CI turns on together.
    public bool AutoPlay => _enabled && _autoPlay;
    public bool NotHotDogParticleEffect => _enabled && _notHotDogParticleEffect;
    public bool FetchUpgradeFromServer => _enabled && _fetchUpgradeFromServer;
    public bool CrashOnGameOver => _enabled && _crashOnGameOver;

    /// <summary>
    /// Whether the d-pad force-crash sequence is armed. Deliberately NOT ANDed with
    /// <see cref="Enabled"/>.
    /// </summary>
    /// <remarks>
    /// The whole point of this crash is that it works on a console build, and a console has no
    /// practical way to pass -demo or set SENTRY_DEMO. Gating it behind the master switch would
    /// make it unreachable exactly where it is needed. The sequence is obscure enough that it
    /// cannot be entered by accident, which is what makes shipping it armed safe.
    /// </remarks>
    public bool KonamiCrash => _konamiCrash;

    public void ApplyRuntimeOverrides()
    {
        if (_overridesApplied)
        {
            return;
        }

        _overridesApplied = true;

        // iOS players don't expose the launch arguments through GetCommandLineArgs, so the
        // simulator passes the flag as an environment variable (SIMCTL_CHILD_SENTRY_DEMO)
        // the same way the DSN is picked up.
        var demoFromEnvironment = !string.IsNullOrEmpty(
            System.Environment.GetEnvironmentVariable("SENTRY_DEMO")
        );

        if (ArgumentReader.HasCommandLineFlag("demo") || demoFromEnvironment)
        {
            // Only the CI Demo section. The Konami crash is already armed, and the score mode
            // lives in LeaderboardConfiguration, which CI does not ship an asset for.
            _enabled = true;
            _autoPlay = true;
            _crashOnGameOver = true;
            _notHotDogParticleEffect = true;
            _fetchUpgradeFromServer = true;
        }
    }

    private static DemoConfiguration _instance;

    public static DemoConfiguration Load()
    {
        if (_instance == null)
        {
            _instance = Resources.Load("DemoConfig") as DemoConfiguration;
            _instance?.ApplyRuntimeOverrides();
        }
        return _instance;
    }
}
