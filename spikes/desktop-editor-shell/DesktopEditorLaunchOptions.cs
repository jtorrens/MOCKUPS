using System;
using System.Collections.Generic;
using System.IO;

namespace Mockups.DesktopEditorShell;

internal static class DesktopEditorLaunchOptions
{
    public static string? DatabasePath { get; private set; }

    public static string[] Configure(string[] args)
    {
        var avaloniaArgs = new List<string>();

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--db", StringComparison.Ordinal))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("--db requires a database path.");
                }

                DatabasePath = FullPath(args[++index]);
                continue;
            }

            if (arg.StartsWith("--db=", StringComparison.Ordinal))
            {
                DatabasePath = FullPath(arg["--db=".Length..]);
                continue;
            }

            avaloniaArgs.Add(arg);
        }

        var environmentDatabase = Environment.GetEnvironmentVariable("MOCKUPS_DB");
        if (DatabasePath is null && !string.IsNullOrWhiteSpace(environmentDatabase))
        {
            DatabasePath = FullPath(environmentDatabase);
        }

        return [.. avaloniaArgs];
    }

    private static string FullPath(string path)
    {
        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
    }
}
