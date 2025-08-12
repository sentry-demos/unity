using Sentry.Unity;
using UnityEngine;

public class SentryOptionConfiguration : SentryOptionsConfiguration
{
    public override void Configure(SentryUnityOptions options)
    {
        Debug.Log("Calling into the 'Configure' callback.");
        
        if (!string.IsNullOrEmpty(options.Dsn))
        {
            Debug.Log("The 'DSN' is already set and taken from local options.");
            return;
        }
        
        Debug.Log("Getting the 'DSN' from the commandline arguments.");
        var dsn = ArgumentReader.GetCommandLineArg("dsn");
        if (!string.IsNullOrEmpty(dsn))
        {
            Debug.Log("Setting the 'DSN'.");
            options.Dsn = dsn;
        }
        else
        {
            Debug.LogError("Failed to get the 'DSN'.");
        }
    }
}