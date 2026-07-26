using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell;

internal static class DesktopPreviewBundle
{
    private const int CurrentSchemaVersion = 1;
    private static readonly string[] RequiredArtifacts =
    [
        "renderDesignPreviewHtml.cjs",
        "renderDesignPreviewHtmlServer.cjs",
        "renderPreviewRasterServer.cjs",
    ];

    public static void RequireCurrent(string bundleDirectory)
    {
        var manifestPath = Path.Combine(bundleDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                "Desktop Preview bundle manifest is missing.",
                manifestPath);
        }

        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath)) as JsonObject
            ?? throw new InvalidDataException(
                "Desktop Preview bundle manifest must be a JSON object.");
        var schemaVersion = manifest["schemaVersion"]?.GetValue<int>()
            ?? throw new InvalidDataException(
                "Desktop Preview bundle manifest requires schemaVersion.");
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Desktop Preview bundle schema {schemaVersion} is not supported.");
        }

        var commit = RequiredString(manifest, "commit");
        if ((commit.Length != 40 && commit.Length != 64) || !IsLowerHex(commit))
        {
            throw new InvalidDataException(
                "Desktop Preview bundle manifest contains an invalid commit.");
        }
        var builtAt = RequiredString(manifest, "builtAt");
        if (!DateTimeOffset.TryParse(
                builtAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _))
        {
            throw new InvalidDataException(
                "Desktop Preview bundle manifest contains an invalid builtAt.");
        }

        var expectedBundleHash = RequiredHash(manifest, "bundleHash");
        var artifactDocument = manifest["artifacts"] as JsonObject
            ?? throw new InvalidDataException(
                "Desktop Preview bundle manifest requires an artifacts object.");
        var expectedArtifacts = artifactDocument
            .ToDictionary(
                (entry) => entry.Key,
                (entry) => entry.Value?.GetValue<string>()
                    ?? throw new InvalidDataException(
                        $"Desktop Preview artifact '{entry.Key}' requires a hash."),
                StringComparer.Ordinal);
        foreach (var requiredArtifact in RequiredArtifacts)
        {
            if (!expectedArtifacts.ContainsKey(requiredArtifact))
            {
                throw new InvalidDataException(
                    $"Desktop Preview bundle manifest omits '{requiredArtifact}'.");
            }
        }

        var actualArtifacts = Directory
            .EnumerateFiles(bundleDirectory)
            .Select(Path.GetFileName)
            .Where((name) => name is not null && name != "manifest.json")
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToList();
        if (!actualArtifacts.SequenceEqual(
                expectedArtifacts.Keys.Order(StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Desktop Preview bundle artifacts do not match the manifest.");
        }

        var bundleSource = new StringBuilder();
        foreach (var artifactName in actualArtifacts)
        {
            if (Path.GetFileName(artifactName) != artifactName)
            {
                throw new InvalidDataException(
                    "Desktop Preview artifact names must be local file names.");
            }
            var expectedHash = expectedArtifacts[artifactName];
            if (expectedHash.Length != 64 || !IsLowerHex(expectedHash))
            {
                throw new InvalidDataException(
                    $"Desktop Preview artifact '{artifactName}' has an invalid hash.");
            }
            var actualHash = FileHash(Path.Combine(bundleDirectory, artifactName));
            if (!actualHash.Equals(expectedHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Desktop Preview artifact '{artifactName}' does not match its manifest hash.");
            }
            bundleSource.Append(artifactName).Append(':').Append(actualHash).Append('\n');
        }

        var actualBundleHash = Convert
            .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bundleSource.ToString())))
            .ToLowerInvariant();
        if (!actualBundleHash.Equals(expectedBundleHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Desktop Preview bundle hash does not match its manifest.");
        }
    }

    private static string RequiredString(JsonObject source, string propertyName)
    {
        var value = source[propertyName]?.GetValue<string>();
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidDataException(
                $"Desktop Preview bundle manifest requires {propertyName}.");
    }

    private static string RequiredHash(JsonObject source, string propertyName)
    {
        var value = RequiredString(source, propertyName);
        return value.Length == 64 && IsLowerHex(value)
            ? value
            : throw new InvalidDataException(
                $"Desktop Preview bundle manifest contains an invalid {propertyName}.");
    }

    private static bool IsLowerHex(string value) =>
        value.All((character) =>
            character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    private static string FileHash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
            .ToLowerInvariant();
}
