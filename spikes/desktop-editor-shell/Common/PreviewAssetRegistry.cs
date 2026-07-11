using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.Common;

internal static partial class PreviewAssetRegistry
{
    private const string AssetPrefix = "mockups-asset:";
    private static readonly object Gate = new();
    private static readonly Dictionary<string, string> Assets = new(StringComparer.Ordinal);

    public static string Compact(string html)
    {
        return DataUriRegex().Replace(html, (match) =>
        {
            var uri = match.Groups["uri"].Value;
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri))).ToLowerInvariant();
            lock (Gate)
            {
                Assets.TryAdd(key, uri);
            }
            return $"{AssetPrefix}{key}";
        });
    }

    public static IReadOnlyList<string> Keys(string html)
    {
        return AssetKeyRegex().Matches(html)
            .Select((match) => match.Groups["key"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static bool TryResolve(string key, out string uri)
    {
        lock (Gate)
        {
            return Assets.TryGetValue(key, out uri!);
        }
    }

    public static void Register(string key, string uri)
    {
        lock (Gate)
        {
            if (Assets.TryGetValue(key, out var existing) && !string.Equals(existing, uri, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Preview asset hash collision for '{key}'.");
            }
            Assets[key] = uri;
        }
    }

    public static string Expand(string html)
    {
        return AssetKeyRegex().Replace(html, (match) =>
        {
            var key = match.Groups["key"].Value;
            return TryResolve(key, out var uri) ? uri : match.Value;
        });
    }

    public static string ExpandSource(string source)
    {
        if (!source.StartsWith(AssetPrefix, StringComparison.Ordinal)) return source;
        var key = source[AssetPrefix.Length..];
        return TryResolve(key, out var uri) ? uri : source;
    }

    [GeneratedRegex("(?<uri>data:(?:image|video)/.*?)(?=&quot;|&#39;|[\\s\"'<>]|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DataUriRegex();

    [GeneratedRegex("mockups-asset:(?<key>[a-f0-9]{64})", RegexOptions.IgnoreCase)]
    private static partial Regex AssetKeyRegex();
}
