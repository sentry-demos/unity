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
        }
        else
        {
            Debug.Log("Getting the 'AUTH TOKEN' from the environment.");

            var token = System.Environment.GetEnvironmentVariable("SENTRY_AUTH_TOKEN");
            if (!string.IsNullOrEmpty(token))
            {
                Debug.Log("Setting the 'AUTH TOKEN' from environment.");
                cliOptions.Auth = token;
            }
            else
            {
                Debug.LogError("Failed to get the 'AUTH TOKEN' from environment.");
            }
        }

#if UNITY_ANDROID
        // TODO: sentry-cli seems to choke in CI trying to upload
        cliOptions.UploadSources = false;
#endif
        
        var organization = System.Environment.GetEnvironmentVariable("SENTRY_ORG");
        if (!string.IsNullOrEmpty(organization))
        {
            cliOptions.Organization = organization;
        }

        var project = System.Environment.GetEnvironmentVariable("SENTRY_PROJECT");
        if (!string.IsNullOrEmpty(project))
        {
            cliOptions.Project = project;
        }

        cliOptions.UrlOverride = "https://sentry.io";
    }
}
