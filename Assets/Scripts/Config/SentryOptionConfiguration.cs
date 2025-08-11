using System;
using Sentry.Unity;

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
            throw new InvalidOperationException("Failed to fetch `dsn` from the command line arguments");
        }
    }
}