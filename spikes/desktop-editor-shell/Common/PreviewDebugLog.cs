using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace Mockups.DesktopEditorShell.Common;

internal static class PreviewDebugLog
{
    private const string FileName = "desktop-preview-debug.log";
    private static readonly object Gate = new();
    private static bool _initialized;
    private static string? _filePath;
    private static readonly AsyncLocal<string?> Correlation = new();

    public static IDisposable BeginCorrelation(string correlationId)
    {
        var previous = Correlation.Value;
        Correlation.Value = correlationId;
        return new CorrelationScope(previous);
    }

    public static string FilePath
    {
        get
        {
            lock (Gate)
            {
                EnsureInitialized();
                return _filePath!;
            }
        }
    }

    public static void Write(string eventName, params (string Key, object? Value)[] fields)
    {
        lock (Gate)
        {
            EnsureInitialized();
            var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
            var correlatedFields = string.IsNullOrWhiteSpace(Correlation.Value)
                ? fields
                : new (string Key, object? Value)[] { ("transaction", Correlation.Value) }.Concat(fields).ToArray();
            var suffix = correlatedFields.Length == 0
                ? ""
                : "\t" + string.Join("\t", correlatedFields.Select((field) => $"{field.Key}={FormatValue(field.Value)}"));
            File.AppendAllText(_filePath!, $"{timestamp}\t{eventName}{suffix}{Environment.NewLine}");
        }
    }

    private sealed class CorrelationScope(string? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Correlation.Value = previous;
        }
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        var directory = Path.Combine(FindRepositoryRoot(), "logs");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, FileName);
        File.AppendAllText(
            _filePath,
            $"{Environment.NewLine}--- preview debug session {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} ---{Environment.NewLine}");
        _initialized = true;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "package.json"))
                && Directory.Exists(Path.Combine(directory.FullName, "spikes", "desktop-editor-shell")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "",
            double number => number.ToString("0.###", CultureInfo.InvariantCulture),
            float number => number.ToString("0.###", CultureInfo.InvariantCulture),
            decimal number => number.ToString("0.###", CultureInfo.InvariantCulture),
            TimeSpan duration => duration.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture) + "ms",
            bool flag => flag ? "true" : "false",
            _ => Sanitize(Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""),
        };
    }

    private static string Sanitize(string value)
    {
        return value
            .Replace('\t', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }
}
