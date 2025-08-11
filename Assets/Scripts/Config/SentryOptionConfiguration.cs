using Sentry.Unity;
using UnityEditor.Build;
using UnityEngine;

public class SentryOptionConfiguration : SentryOptionsConfiguration
{
    public override void Configure(SentryUnityOptions options)
    {
        var dsn = ArgumentReader.GetCommandLineArg("dsn");
        if (!string.IsNullOrEmpty(dsn))
        {
            options.Dsn = dsn;
        }
        else
        {
            throw new BuildFailedException("Failed to fetch `dsn` from the command line arguments");
        }
    }
}