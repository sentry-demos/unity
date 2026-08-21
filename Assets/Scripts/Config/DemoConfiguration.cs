using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Assets/Resources/DemoConfig.asset", menuName = "DemoConfig", order = 999)]
public class DemoConfiguration : ScriptableObject
{
    [Header("Master Switch")]
    [SerializeField] private bool _enabled;
    
    [Header("Leaderboard Configuration")]
    [SerializeField] private string _apiUrl = string.Empty;
    [SerializeField] private User _user;

    [Header("Demo Settings")]
    [SerializeField] private bool _autoPlay;
    [SerializeField] private bool _notHotDogParticleEffect;
    [SerializeField] private bool _fetchUpgradeFromServer;
    [SerializeField] private bool _crashOnGameOver;

    private bool _overridesApplied;

    public bool Enabled => _enabled;
    public string ApiUrl => _apiUrl;
    public User User => _user;

    public bool AutoPlay => _enabled && _autoPlay;
    public bool NotHotDogParticleEffect => _enabled && _notHotDogParticleEffect;
    public bool FetchUpgradeFromServer => _enabled && _fetchUpgradeFromServer;
    public bool CrashOnGameOver => _enabled && _crashOnGameOver;

    public void ApplyRuntimeOverrides()
    {
        if (_overridesApplied)
            return;
        _overridesApplied = true;

        // iOS players don't expose the launch arguments through GetCommandLineArgs, so the
        // simulator passes the flag as an environment variable (SIMCTL_CHILD_SENTRY_DEMO)
        // the same way the DSN is picked up.
        var demoFromEnvironment = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SENTRY_DEMO"));

        if (ArgumentReader.HasCommandLineFlag("demo") || demoFromEnvironment)
        {
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

[Serializable]
public class User
{
    public string Username;
    public string Password;
}