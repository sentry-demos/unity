using Sentry.Unity;
using UnityEngine;

public class SentryOptionConfiguration : SentryOptionsConfiguration
{
    public override void Configure(SentryUnityOptions options)
    {
        var dsn = ArgumentReader.GetCommandLineArg("dsn");
        if (string.IsNullOrEmpty(dsn))
        {
            options.Dsn = dsn;
        }
        else
        {
            Debug.LogError("Failed to fetch `dsn` from the command line arguments");
        }
    }
}