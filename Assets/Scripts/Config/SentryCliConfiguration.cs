using System;
using Sentry.Unity;

public class SentryCliConfiguration : SentryCliOptionsConfiguration
{
    public override void Configure(SentryCliOptions cliOptions)
    {
        if (cliOptions.Auth is not null)
        {
            return;
        }
        
        var token = ArgumentReader.GetCommandLineArg("auth_token");
        if (!string.IsNullOrEmpty(token))
        {
            cliOptions.Auth = token;
        }
        else
        {
            throw new InvalidOperationException("Failed to fetch `auth_token` from the command line arguments");
        }

        cliOptions.Organization = "demo";
        cliOptions.Project = "unity";
        cliOptions.UrlOverride = "https://sentry.io";
    }
}