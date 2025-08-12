using System;
using UnityEngine;

public static class ArgumentReader
{
  public static string GetCommandLineArg(string name)
  {
      var args = Environment.GetCommandLineArgs();
      Debug.Log($"Received {args.Length} arguments.");
      for (var i = 0; i < args.Length; i++)
      {
          if (args[i].StartsWith("-"))
          {
              Debug.Log($"    {args[i]}");
          }
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
}