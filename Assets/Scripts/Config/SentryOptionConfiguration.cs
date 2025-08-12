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
        
        Debug.Log("Getting the 'DSN' from the commandline arguments.");
        
        dsn = ArgumentReader.GetCommandLineArg("dsn");
        if (!string.IsNullOrEmpty(dsn))
        {
            Debug.Log("Setting the 'DSN' from command line arguments.");
            options.Dsn = dsn;
        }
        else
        {
            Debug.LogError("Failed to get the 'DSN' from both environment variable and command line arguments.");
        }
    }
}