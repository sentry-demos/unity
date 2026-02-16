using System;

public static class ArgumentReader
{
  public static string GetCommandLineArg(string name)
  {
      var args = Environment.GetCommandLineArgs();
      for (var i = 0; i < args.Length; i++)
      {
          if (args[i] == "-" + name && args.Length > i + 1)
          {
              return args[i + 1];
          }
      }
      return null;
  }

  public static bool HasCommandLineFlag(string name)
  {
      var args = Environment.GetCommandLineArgs();
      for (var i = 0; i < args.Length; i++)
      {
          if (args[i] == "-" + name)
              return true;
      }
      return false;
  }
}