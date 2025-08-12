using Sentry.Unity;
using UnityEngine;

public class SentryCliConfiguration : SentryCliOptionsConfiguration
{
    public override void Configure(SentryCliOptions cliOptions)
    {
        Debug.Log("Calling into the 'Configure' callback for CLI options.");
        
        if (!string.IsNullOrEmpty(cliOptions.Auth))
        {
            Debug.Log("The 'AUTH TOKEN' is already set.");
            return;
        }
        
        Debug.Log("Getting the 'AUTH TOKEN' from the  commandline arguments.");
        
        var token = ArgumentReader.GetCommandLineArg("auth_token");
        if (!string.IsNullOrEmpty(token))
        {
            Debug.Log("Setting the 'AUTH TOKEN' from command line arguments.");
            cliOptions.Auth = token;
        }
        else
        {
            Debug.LogError("Failed to get the 'AUTH TOKEN' from both environment variable and command line arguments.");
        }

        cliOptions.Organization = "demo";
        cliOptions.Project = "unity";
        cliOptions.UrlOverride = "https://sentry.io";
    }
}