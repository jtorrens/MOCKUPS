using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Common;

public static class SystemPreviewFixtureCatalog
{
    public const string MediaScheme = "system-preview://";
    public const string PrimaryActorId = "system_preview_actor_primary";
    public const string SecondaryActorId = "system_preview_actor_secondary";

    private static readonly ActorFixture[] Actors =
    [
        new(
            PrimaryActorId,
            "Sample One",
            "One",
            "SO",
            "avatars/sample-one.svg",
            "#3657D6",
            "#FFFFFF"),
        new(
            SecondaryActorId,
            "Sample Two",
            "Two",
            "ST",
            "avatars/sample-two.svg",
            "#C84C73",
            "#FFFFFF"),
    ];

    private static readonly MediaFixture[] Media =
    [
        new(
            "system-preview://media/test-image.svg",
            "Synthetic image",
            "image"),
        new(
            "system-preview://media/test-video.mp4",
            "Synthetic video",
            "video"),
        new(
            "system-preview://media/test-audio.wav",
            "Synthetic audio",
            "audio"),
    ];

    private static readonly MediaFixture[] MediaDirectories =
    [
        new(
            "system-preview://media/gallery",
            "Synthetic gallery",
            "directory"),
    ];

    public static string Root => Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData),
        "MOCKUPS",
        "system-preview-fixtures");

    public static bool IsActor(string id) =>
        Actors.Any((actor) => actor.Id.Equals(id, StringComparison.Ordinal));

    public static bool IsMediaReference(string reference) =>
        Media.Concat(MediaDirectories).Any((fixture) =>
            fixture.Reference.Equals(reference, StringComparison.Ordinal));

    public static IReadOnlyList<FieldOption> ActorOptions(
        bool includeNone)
    {
        var options = Actors
            .Select((actor) => new FieldOption(actor.Id, actor.Label))
            .ToList();
        if (includeNone)
        {
            options.Insert(0, new FieldOption("", "None"));
        }
        return options;
    }

    public static IReadOnlyList<FieldOption> MediaOptions() =>
        Media.Select((fixture) => new FieldOption(
                fixture.Reference,
                fixture.Label,
                GroupValue: fixture.Kind,
                GroupLabel: char.ToUpperInvariant(fixture.Kind[0])
                    + fixture.Kind[1..]))
            .ToList();

    public static IReadOnlyList<FieldOption> MediaDirectoryOptions() =>
        MediaDirectories.Select((fixture) => new FieldOption(
                fixture.Reference,
                fixture.Label))
            .ToList();

    public static JsonObject ActorPreview(string actorId)
    {
        var actor = Actors.SingleOrDefault((candidate) =>
                candidate.Id.Equals(actorId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Missing System Preview Actor fixture '{actorId}'.");
        var avatarPath = ResolveRelative(actor.AvatarRelativePath);
        if (!File.Exists(avatarPath))
        {
            throw new InvalidOperationException(
                $"Missing System Preview avatar fixture '{actor.AvatarRelativePath}'.");
        }
        var imageUri = $"data:image/svg+xml;base64,{Convert.ToBase64String(File.ReadAllBytes(avatarPath))}";
        return new JsonObject
        {
            ["id"] = actor.Id,
            ["displayName"] = actor.Label,
            ["shortName"] = actor.ShortName,
            ["initials"] = actor.Initials,
            ["avatar"] = new JsonObject
            {
                ["imageUri"] = imageUri,
                ["backgroundColor"] = actor.BackgroundColor,
                ["textColor"] = actor.TextColor,
                ["scale"] = 1,
                ["offsetX"] = 0,
                ["offsetY"] = 0,
                ["baseSize"] = 640,
            },
        };
    }

    public static string ResolveMediaReference(string reference)
    {
        if (!reference.StartsWith(MediaScheme, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"System Preview media reference '{reference}' must use the '{MediaScheme}' scheme.");
        }
        if (!Media.Any((fixture) =>
                fixture.Reference.Equals(reference, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Unknown System Preview media fixture '{reference}'.");
        }
        return ResolveRelative(reference[MediaScheme.Length..]);
    }

    public static void ValidateInstalled()
    {
        foreach (var actor in Actors)
        {
            RequireFile(ResolveRelative(actor.AvatarRelativePath));
        }
        foreach (var media in Media)
        {
            RequireFile(ResolveMediaReference(media.Reference));
        }
        foreach (var directory in MediaDirectories)
        {
            var path = ResolveRelative(directory.Reference[MediaScheme.Length..]);
            if (!Directory.Exists(path))
            {
                throw new InvalidOperationException(
                    $"Missing installed System Preview fixture directory '{path}'.");
            }
        }
    }

    private static string ResolveRelative(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathFullyQualified(relativePath))
        {
            throw new InvalidOperationException(
                "System Preview fixture paths must be non-empty relative paths.");
        }
        var root = Path.GetFullPath(Root);
        var resolved = Path.GetFullPath(Path.Combine(root, relativePath));
        var relative = Path.GetRelativePath(root, resolved);
        if (relative.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathFullyQualified(relative))
        {
            throw new InvalidOperationException(
                $"System Preview fixture path '{relativePath}' escapes its root.");
        }
        return resolved;
    }

    private static void RequireFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Missing installed System Preview fixture '{path}'.");
        }
    }

    private sealed record ActorFixture(
        string Id,
        string Label,
        string ShortName,
        string Initials,
        string AvatarRelativePath,
        string BackgroundColor,
        string TextColor);

    private sealed record MediaFixture(
        string Reference,
        string Label,
        string Kind);
}
