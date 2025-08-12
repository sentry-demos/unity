using Sentry.Unity;
using UnityEngine;

public class SentryCliConfiguration : SentryCliOptionsConfiguration
{
    public override void Configure(SentryCliOptions cliOptions)
    {
        Debug.Log("Calling into the 'Configure' callback for CLI options.");
        
        if (!string.IsNullOrEmpty(cliOptions.Auth))
        {
            Debug.Log("The 'AUTH TOKEN' is already set and taken from local cli options.");
            return;
        }
        
        Debug.Log("Getting the 'AUTH TOKEN' from the commandline arguments.");
        var token = ArgumentReader.GetCommandLineArg("auth_token");
        if (!string.IsNullOrEmpty(token))
        {
            Debug.Log("Setting the 'AUTH TOKEN'.");
            cliOptions.Auth = token;
        }
        else
        {
            Debug.LogError("Failed to get the 'AUTH TOKEN'.");
        }

        cliOptions.Organization = "demo";
        cliOptions.Project = "unity";
        cliOptions.UrlOverride = "https://sentry.io";
    }
}