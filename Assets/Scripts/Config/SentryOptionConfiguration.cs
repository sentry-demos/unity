using Sentry.Unity;
using UnityEngine;

public class SentryOptionConfiguration : SentryOptionsConfiguration
{
    public override void Configure(SentryUnityOptions options)
    {
        Debug.Log("Calling into the 'Configure' callback.");

        if (!string.IsNullOrEmpty(options.Dsn))
        {
            Debug.Log("The 'DSN' is already set.");
            return;
        }

        Debug.Log("Getting the 'DSN' from the environment.");

        var dsn = System.Environment.GetEnvironmentVariable("SENTRY_DSN");
        if (!string.IsNullOrEmpty(dsn))
        {
            Debug.Log("Setting the 'DSN' from environment variable.");
            options.Dsn = dsn;
            return;
        }

        // On Android, try to get DSN from Intent extras
#if UNITY_ANDROID && !UNITY_EDITOR
        Debug.Log("Getting the 'DSN' from Android Intent extras.");

        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject intent = currentActivity.Call<AndroidJavaObject>("getIntent"))
            {
                dsn = intent.Call<string>("getStringExtra", "dsn");
                if (!string.IsNullOrEmpty(dsn))
                {
                    Debug.Log("Setting the 'DSN' from Android Intent extras.");
                    options.Dsn = dsn;
                    return;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to get DSN from Android Intent: {e.Message}");
        }
#endif

        Debug.Log("Getting the 'DSN' from the commandline arguments.");

        dsn = ArgumentReader.GetCommandLineArg("dsn");
        if (!string.IsNullOrEmpty(dsn))
        {
            Debug.Log("Setting the 'DSN' from command line arguments.");
            options.Dsn = dsn;
        }
        else
        {
            Debug.LogError("Failed to get the 'DSN' from environment variable, Intent extras, and command line arguments.");
        }
    }
}
