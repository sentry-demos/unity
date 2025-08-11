using Sentry.Unity;
using UnityEngine;

public class SentryCliConfiguration : SentryCliOptionsConfiguration
{
    public override void Configure(SentryCliOptions cliOptions)
    {
        var token = ArgumentReader.GetCommandLineArg("auth_token");
        if (string.IsNullOrEmpty(token))
        {
            cliOptions.Auth = token;
        }
        else
        {
            Debug.LogError("Failed to fetch `auth_token` from the command line arguments");
        }
    }
}