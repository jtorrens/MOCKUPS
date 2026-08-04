using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

public sealed record ShotReferenceVideoMarker(
    string Id,
    int VideoFrame,
    string Text);

public sealed record ShotReferenceVideoDocument(
    string SourcePath,
    int InFrame,
    IReadOnlyList<ShotReferenceVideoMarker> Markers)
{
    public static ShotReferenceVideoDocument Empty { get; } =
        new("", 0, []);

    public static ShotReferenceVideoDocument ParseRequired(
        string json,
        string owner)
    {
        var root = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidOperationException(
                $"{owner} must be a current JSON object.");
        RequireExactProperties(
            root,
            ["sourcePath", "inFrame", "markers"],
            owner);
        var sourcePath = RequiredString(root, "sourcePath", owner);
        RequirePortableRelativePath(sourcePath, owner);
        var inFrame = RequiredNonNegativeInteger(root, "inFrame", owner);
        var markersNode = root["markers"] as JsonArray
            ?? throw new InvalidOperationException(
                $"{owner}.markers must be a current JSON array.");
        var markers = new List<ShotReferenceVideoMarker>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < markersNode.Count; index++)
        {
            var markerOwner = $"{owner}.markers[{index}]";
            var marker = markersNode[index] as JsonObject
                ?? throw new InvalidOperationException(
                    $"{markerOwner} must be a current JSON object.");
            RequireExactProperties(
                marker,
                ["id", "videoFrame", "text"],
                markerOwner);
            var id = RequiredString(marker, "id", markerOwner);
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
            {
                throw new InvalidOperationException(
                    $"{markerOwner}.id must be stable, non-blank and unique.");
            }
            markers.Add(new ShotReferenceVideoMarker(
                id,
                RequiredNonNegativeInteger(
                    marker,
                    "videoFrame",
                    markerOwner),
                RequiredString(marker, "text", markerOwner)));
        }

        return new ShotReferenceVideoDocument(
            sourcePath,
            inFrame,
            markers);
    }

    public string ToJson()
    {
        var markers = new JsonArray();
        foreach (var marker in Markers)
        {
            markers.Add(new JsonObject
            {
                ["id"] = marker.Id,
                ["videoFrame"] = marker.VideoFrame,
                ["text"] = marker.Text,
            });
        }
        return new JsonObject
        {
            ["sourcePath"] = SourcePath,
            ["inFrame"] = InFrame,
            ["markers"] = markers,
        }.ToJsonString();
    }

    public IReadOnlyList<int> ShotMarkerFrames(int shotDurationFrames) =>
        Markers
            .Select((marker) => marker.VideoFrame - InFrame)
            .Where((frame) => frame >= 0 && frame < shotDurationFrames)
            .Distinct()
            .Order()
            .ToArray();

    private static void RequirePortableRelativePath(
        string path,
        string owner)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (Path.IsPathFullyQualified(path)
            || path.Replace('\\', '/').Split('/').Any(
                (segment) => segment == ".."))
        {
            throw new InvalidOperationException(
                $"{owner}.sourcePath must be relative to the Project root.");
        }
        if (Path.GetExtension(path).ToLowerInvariant()
            is not (".mp4" or ".mov" or ".m4v" or ".webm"))
        {
            throw new InvalidOperationException(
                $"{owner}.sourcePath must reference a supported video file.");
        }
    }

    private static string RequiredString(
        JsonObject owner,
        string property,
        string context) =>
        owner[property] is JsonValue value
        && value.TryGetValue<string>(out var text)
            ? text
            : throw new InvalidOperationException(
                $"{context}.{property} must be an explicit JSON string.");

    private static int RequiredNonNegativeInteger(
        JsonObject owner,
        string property,
        string context)
    {
        if (owner[property] is not JsonValue value
            || !value.TryGetValue<int>(out var number)
            || number < 0)
        {
            throw new InvalidOperationException(
                $"{context}.{property} must be a non-negative integer.");
        }
        return number;
    }

    private static void RequireExactProperties(
        JsonObject owner,
        IReadOnlyCollection<string> expected,
        string context)
    {
        var expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
        var actual = owner.Select((property) => property.Key).ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expectedSet))
        {
            throw new InvalidOperationException(
                $"{context} must contain exactly: {string.Join(", ", expected)}.");
        }
    }
}
