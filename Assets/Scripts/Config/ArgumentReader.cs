using System;

public static class ArgumentReader
{
    public static string GetCommandLineArg(string name) =>
        GetArg(Environment.GetCommandLineArgs(), name);

    public static bool HasCommandLineFlag(string name) =>
        HasFlag(Environment.GetCommandLineArgs(), name);

    // The args array is taken as a parameter so the parsing can be tested without the
    // process's real command line, which the test runner controls.
    public static string GetArg(string[] args, string name)
    {
        if (args == null)
        {
            return null;
        }

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "-" + name && args.Length > i + 1)
            {
                return args[i + 1];
            }
        }
        return null;
    }

    public static bool HasFlag(string[] args, string name)
    {
        if (args == null)
        {
            return false;
        }

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "-" + name)
            {
                return true;
            }
        }
        return false;
    }
}
