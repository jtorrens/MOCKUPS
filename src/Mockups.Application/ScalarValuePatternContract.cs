using System;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.EditorShell;

public static class ScalarValuePatternContract
{
    public static void Validate(
        string pattern,
        string message,
        JsonNode value,
        string owner)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return;
        }
        if (value is not JsonValue scalar
            || !scalar.TryGetValue<string>(out var text))
        {
            throw new InvalidOperationException(
                $"{owner} uses a value pattern but is not a string.");
        }

        bool matches;
        try
        {
            matches = Regex.IsMatch(
                text,
                pattern,
                RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100));
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"{owner} has an invalid value pattern.",
                exception);
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw new InvalidOperationException(
                $"{owner} value pattern exceeded its validation limit.",
                exception);
        }
        if (matches)
        {
            return;
        }

        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(message)
                ? $"{owner} does not match its declared value pattern."
                : $"{owner} {message}");
    }
}
