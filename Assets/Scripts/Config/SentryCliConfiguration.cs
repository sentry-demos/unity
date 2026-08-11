using Sentry.Unity;
using UnityEngine;

public class SentryCliConfiguration : SentryCliOptionsConfiguration
{
    public override void Configure(SentryCliOptions cliOptions)
    {
        Debug.Log("Calling into the 'Configure' callback for CLI options.");

        var auth = ArgumentReader.GetCommandLineArg("sentryAuthToken");
        if (!string.IsNullOrEmpty(auth))
        {
            Debug.Log("Setting the 'AUTH TOKEN' from command line arguments.");
            cliOptions.Auth = auth;
        }
        else if (!string.IsNullOrEmpty(cliOptions.Auth))
        {
            Debug.Log("The 'AUTH TOKEN' is already set.");
        }
        else
        {
            Debug.LogError("Failed to get the 'AUTH TOKEN' from command line arguments.");
        }

#if UNITY_ANDROID
        cliOptions.UploadSources = false;
#endif

        var organization = ArgumentReader.GetCommandLineArg("sentryOrg");
        if (!string.IsNullOrEmpty(organization))
        {
            cliOptions.Organization = organization;
        }

        var project = ArgumentReader.GetCommandLineArg("sentryProject");
        if (!string.IsNullOrEmpty(project))
        {
            cliOptions.Project = project;
        }

        Debug.Log($"Uploading debug symbols to '{cliOptions.Organization}/{cliOptions.Project}'.");

        // Deliberately not setting UrlOverride. The SDK already resolves the upload URL from the DSN and
        // only writes 'defaults.url' for self-hosted instances. Hardcoding it here would put a URL into
        // sentry.properties that sentry-cli then discards as a cross-source conflict.
    }
}
