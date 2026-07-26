using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed record ShotManagerPortableStructureEntry(
    string EntryId,
    string RelativePath);

internal sealed record ShotManagerPortableOutputContract(
    string EntryId,
    string RelativeDirectory,
    string FileNamePrefix,
    int VersionPadding);

internal sealed record ShotManagerPortableStructure(
    int SchemaVersion,
    IReadOnlyList<string> Directories,
    IReadOnlyList<string> ShotOwnedDirectories,
    IReadOnlyList<ShotManagerPortableStructureEntry> Entries,
    IReadOnlyList<ShotManagerPortableOutputContract> OutputContracts)
{
    private static readonly HashSet<string> RootKeys =
        new(
            [
                "schemaVersion",
                "directories",
                "shotOwnedDirectories",
                "entries",
                "outputContracts",
            ],
            StringComparer.Ordinal);
    private static readonly HashSet<string> EntryKeys =
        new(["entryId", "relativePath"], StringComparer.Ordinal);
    private static readonly HashSet<string> OutputContractKeys =
        new(
            [
                "entryId",
                "relativeDirectory",
                "fileNamePrefix",
                "versionPadding",
            ],
            StringComparer.Ordinal);

    public string ToJson()
    {
        Validate("Shot Manager portable structure");
        return new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["directories"] = new JsonArray(
                Directories.Select((directory) =>
                    JsonValue.Create(directory)).ToArray<JsonNode?>()),
            ["shotOwnedDirectories"] = new JsonArray(
                ShotOwnedDirectories.Select((directory) =>
                    JsonValue.Create(directory)).ToArray<JsonNode?>()),
            ["entries"] = new JsonArray(
                Entries.Select((entry) => (JsonNode)new JsonObject
                {
                    ["entryId"] = entry.EntryId,
                    ["relativePath"] = entry.RelativePath,
                }).ToArray()),
            ["outputContracts"] = new JsonArray(
                OutputContracts.Select((output) => (JsonNode)new JsonObject
                {
                    ["entryId"] = output.EntryId,
                    ["relativeDirectory"] = output.RelativeDirectory,
                    ["fileNamePrefix"] = output.FileNamePrefix,
                    ["versionPadding"] = output.VersionPadding,
                }).ToArray()),
        }.ToJsonString();
    }

    public static ShotManagerPortableStructure Parse(
        string json,
        string context)
    {
        var root = JsonPath.ParseRequiredObject(json, context);
        RequireExactKeys(root, RootKeys, context);
        var schemaVersion = root["schemaVersion"]?.GetValue<int>()
            ?? throw new InvalidOperationException(
                $"{context} requires schemaVersion.");
        var directoriesNode = root["directories"] as JsonArray
            ?? throw new InvalidOperationException(
                $"{context}.directories must be an array.");
        var entriesNode = root["entries"] as JsonArray
            ?? throw new InvalidOperationException(
                $"{context}.entries must be an array.");
        var shotOwnedDirectoriesNode =
            root["shotOwnedDirectories"] as JsonArray
            ?? throw new InvalidOperationException(
                $"{context}.shotOwnedDirectories must be an array.");
        var outputContractsNode = root["outputContracts"] as JsonArray
            ?? throw new InvalidOperationException(
                $"{context}.outputContracts must be an array.");
        var directories = directoriesNode.Select((node) =>
            node?.GetValue<string>()
            ?? throw new InvalidOperationException(
                $"{context}.directories contains a non-string value.")).ToList();
        var shotOwnedDirectories =
            shotOwnedDirectoriesNode.Select((node) =>
                node?.GetValue<string>()
                ?? throw new InvalidOperationException(
                    $"{context}.shotOwnedDirectories contains a non-string value."))
            .ToList();
        var entries = entriesNode.Select((node) =>
        {
            var entry = node as JsonObject
                ?? throw new InvalidOperationException(
                    $"{context}.entries contains a non-object value.");
            RequireExactKeys(entry, EntryKeys, $"{context}.entries");
            return new ShotManagerPortableStructureEntry(
                entry["entryId"]?.GetValue<string>()
                    ?? throw new InvalidOperationException(
                        $"{context}.entries requires entryId."),
                entry["relativePath"]?.GetValue<string>()
                    ?? throw new InvalidOperationException(
                        $"{context}.entries requires relativePath."));
        }).ToList();
        var outputContracts = outputContractsNode.Select((node) =>
        {
            var output = node as JsonObject
                ?? throw new InvalidOperationException(
                    $"{context}.outputContracts contains a non-object value.");
            RequireExactKeys(
                output,
                OutputContractKeys,
                $"{context}.outputContracts");
            return new ShotManagerPortableOutputContract(
                output["entryId"]?.GetValue<string>()
                    ?? throw new InvalidOperationException(
                        $"{context}.outputContracts requires entryId."),
                output["relativeDirectory"]?.GetValue<string>()
                    ?? throw new InvalidOperationException(
                        $"{context}.outputContracts requires relativeDirectory."),
                output["fileNamePrefix"]?.GetValue<string>()
                    ?? throw new InvalidOperationException(
                        $"{context}.outputContracts requires fileNamePrefix."),
                output["versionPadding"]?.GetValue<int>()
                    ?? throw new InvalidOperationException(
                        $"{context}.outputContracts requires versionPadding."));
        }).ToList();
        var structure = new ShotManagerPortableStructure(
            schemaVersion,
            directories,
            shotOwnedDirectories,
            entries,
            outputContracts);
        structure.Validate(context);
        return structure;
    }

    public void Validate(string context)
    {
        if (SchemaVersion != 2)
        {
            throw new InvalidOperationException(
                $"{context} uses unsupported schemaVersion '{SchemaVersion}'.");
        }
        if (Directories.Count == 0
            || Directories.Any((directory) =>
                !IsPortableRelativePath(directory))
            || Directories.Distinct(StringComparer.Ordinal).Count()
                != Directories.Count)
        {
            throw new InvalidOperationException(
                $"{context} requires unique portable directories.");
        }
        if (Entries.Count == 0
            || Entries.Any((entry) =>
                string.IsNullOrWhiteSpace(entry.EntryId)
                || !IsPortableRelativePath(entry.RelativePath))
            || Entries.Select((entry) => entry.EntryId)
                .Distinct(StringComparer.Ordinal).Count() != Entries.Count)
        {
            throw new InvalidOperationException(
                $"{context} requires unique complete structure entries.");
        }
        var directorySet = Directories.ToHashSet(StringComparer.Ordinal);
        if (ShotOwnedDirectories.Count == 0
            || ShotOwnedDirectories.Any((directory) =>
                !directorySet.Contains(directory))
            || ShotOwnedDirectories.Distinct(StringComparer.Ordinal).Count()
                != ShotOwnedDirectories.Count)
        {
            throw new InvalidOperationException(
                $"{context} requires an exact unique Shot-owned directory subset.");
        }
        if (Entries.Any((entry) => !directorySet.Contains(entry.RelativePath)))
        {
            throw new InvalidOperationException(
                $"{context} contains an entry outside its directory snapshot.");
        }
        var entriesById = Entries.ToDictionary(
            (entry) => entry.EntryId,
            StringComparer.Ordinal);
        if (OutputContracts.Select((output) => output.EntryId)
                .Distinct(StringComparer.Ordinal).Count()
                != OutputContracts.Count
            || OutputContracts.Any((output) =>
                !entriesById.TryGetValue(output.EntryId, out var entry)
                || !entry.RelativePath.Equals(
                    output.RelativeDirectory,
                    StringComparison.Ordinal)
                || !IsPortableFileNamePrefix(output.FileNamePrefix)
                || output.VersionPadding is < 1 or > 8))
        {
            throw new InvalidOperationException(
                $"{context} contains invalid or ambiguous output contracts.");
        }
    }

    private static bool IsPortableRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || path.StartsWith("/", StringComparison.Ordinal)
            || path.Contains('\\', StringComparison.Ordinal)
            || path.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }
        return path.Split('/').All((segment) =>
            !string.IsNullOrWhiteSpace(segment)
            && segment is not "." and not "..");
    }

    private static bool IsPortableFileNamePrefix(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.All((character) =>
                character is >= 'A' and <= 'Z'
                    or >= 'a' and <= 'z'
                    or >= '0' and <= '9'
                    or '_'
                    or '-'
                    or '.');
    }

    private static void RequireExactKeys(
        JsonObject value,
        IReadOnlySet<string> expected,
        string context)
    {
        if (value.Count != expected.Count
            || value.Any((property) => !expected.Contains(property.Key)))
        {
            throw new InvalidOperationException(
                $"{context} contains missing or unknown current fields.");
        }
    }
}
