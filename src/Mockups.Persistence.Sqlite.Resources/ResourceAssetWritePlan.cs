using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.Data;

internal sealed record ResourceAssetWriteEntry(string Path, string? Before, string After);

// The durable undo image is kept until the authored SQL transaction confirms
// the write. Paths are exact; recovery never rediscovers assets or runs at open.
internal sealed record ResourceAssetWritePlan(string Id, string Label, string Root,
    IReadOnlyList<ResourceAssetWriteEntry> Entries, IReadOnlyList<string> CreatedDirectories,
    bool Committed, string Error)
{
    internal static ResourceAssetWritePlan Capture(string label, string root, IReadOnlyDictionary<string, byte[]> files,
        IReadOnlyList<string>? declaredDirectories = null)
    {
        if (!System.IO.Path.IsPathFullyQualified(root)) throw new InvalidOperationException("Resource writes require an absolute root.");
        root = ResourceAssetCleanupPlan.StoredPath(System.IO.Path.GetFullPath(root));
        var entries = new List<ResourceAssetWriteEntry>();
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void CaptureDirectory(string path)
        {
            ResourceAssetCleanupPlan.RequireNoLinks(path);
            for (var parent = path; !Directory.Exists(parent); parent = System.IO.Path.GetDirectoryName(parent)!)
            {
                if (parent == root) throw new IOException("Resource root must exist before import.");
                if (File.Exists(parent)) throw new IOException($"Resource directory is occupied by a file: '{parent}'.");
                directories.Add(ResourceAssetCleanupPlan.StoredPath(System.IO.Path.GetRelativePath(root, parent)));
            }
        }
        foreach (var directory in declaredDirectories ?? [])
            CaptureDirectory(ResourceAssetCleanupPlan.ContainedPath(root, directory));
        foreach (var (relative, bytes) in files)
        {
            var path = ResourceAssetCleanupPlan.ContainedPath(root, relative);
            ResourceAssetCleanupPlan.RequireNoLinks(path);
            var before = ReadBytes(path);
            entries.Add(new(relative, before is null ? null : Convert.ToBase64String(before), Convert.ToBase64String(bytes)));
            CaptureDirectory(System.IO.Path.GetDirectoryName(path)!);
        }
        var plan = new ResourceAssetWritePlan(Guid.NewGuid().ToString("N"), label, root, entries,
            directories.OrderBy(p => p.Length).ToList(), false, "");
        return Read(plan.Id, label, root, plan.EntriesJson(), plan.DirectoriesJson(), false, "");
    }

    internal string EntriesJson() => new JsonArray(Entries.Select(e => (JsonNode)new JsonObject
        { ["path"] = e.Path, ["before"] = e.Before, ["after"] = e.After }).ToArray()).ToJsonString();
    internal string DirectoriesJson() => new JsonArray(CreatedDirectories.Select(p => (JsonNode)JsonValue.Create(p)!).ToArray()).ToJsonString();

    internal static ResourceAssetWritePlan Read(string id, string label, string root, string entriesJson,
        string directoriesJson, bool committed, string error)
    {
        if (!Guid.TryParseExact(id, "N", out _) || string.IsNullOrWhiteSpace(label))
            throw new InvalidOperationException("Invalid resource write identity.");
        var entries = new List<ResourceAssetWriteEntry>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in JsonPath.ParseRequiredArray(entriesJson, id))
        {
            if (node is not JsonObject entry || entry.Count != 3 || !entry.ContainsKey("before"))
                throw new InvalidOperationException("Invalid resource write entry.");
            var path = JsonPath.RequiredString(entry, "path", id);
            ResourceAssetCleanupPlan.ContainedPath(root, path);
            var before = entry["before"]?.GetValue<string>();
            var after = entry["after"]?.GetValue<string>() ?? throw new InvalidOperationException("Missing resource write contents.");
            if (!paths.Add(path)) throw new InvalidOperationException("Duplicate resource write path.");
            if (before is not null) Convert.FromBase64String(before);
            Convert.FromBase64String(after);
            entries.Add(new(path, before, after));
        }
        var directories = new List<string>();
        foreach (var node in JsonPath.ParseRequiredArray(directoriesJson, id))
        {
            var path = node?.GetValue<string>() ?? throw new InvalidOperationException("Missing resource directory.");
            ResourceAssetCleanupPlan.ContainedPath(root, path);
            if (!paths.Add(path) || entries.Any(e => path.StartsWith(e.Path + '/', StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Invalid resource write directory.");
            directories.Add(path);
        }
        if (entries.Count == 0 && directories.Count == 0) throw new InvalidOperationException("Resource write has no files or directories.");
        for (var i = 0; i < entries.Count; i++)
            if (entries.Where((_, j) => j != i).Any(e => ResourceAssetCleanupPlan.Overlaps(e.Path, entries[i].Path)))
                throw new InvalidOperationException("Resource files cannot contain other resource files.");
        return new(id, label, root, entries, directories, committed, error);
    }

    internal IEnumerable<string> Paths => Entries.Select(e => e.Path).Concat(CreatedDirectories)
        .Select(path => ResourceAssetCleanupPlan.ContainedPath(Root, path));

    private void ValidateRoot()
    {
        new ResourceAssetCleanupPlan(Id, Label, Root, "root-check", [], "").RequireNativeRoot();
        ResourceAssetCleanupPlan.RequireNoLinks(Root);
        if (!Directory.Exists(Root)) throw new IOException("Resource root is unavailable.");
    }

    private static byte[]? ReadBytes(string path)
    {
        ResourceAssetCleanupPlan.RequireNoLinks(path);
        try { return File.ReadAllBytes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static bool Matches(byte[]? bytes, string? expected) =>
        bytes is null ? expected is null : expected is not null && bytes.AsSpan().SequenceEqual(Convert.FromBase64String(expected));

    private string StagePath(ResourceAssetWriteEntry entry) =>
        ResourceAssetCleanupPlan.ContainedPath(Root, entry.Path) + $".mockups-{Id}.tmp";

    private void Swap(ResourceAssetWriteEntry entry, string contents, string? expected)
    {
        var path = ResourceAssetCleanupPlan.ContainedPath(Root, entry.Path);
        var stage = StagePath(entry);
        ResourceAssetCleanupPlan.RequireNoLinks(path);
        using (var stream = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.Write(Convert.FromBase64String(contents));
            stream.Flush(flushToDisk: true);
        }
        ResourceAssetCleanupPlan.RequireNoLinks(path);
        if (!Matches(ReadBytes(path), expected)) throw new IOException($"Resource changed before file swap: '{path}'.");
        File.Move(stage, path, overwrite: true);
    }

    internal void Apply()
    {
        ValidateRoot();
        foreach (var entry in Entries)
        {
            var path = ResourceAssetCleanupPlan.ContainedPath(Root, entry.Path);
            if (!Matches(ReadBytes(path), entry.Before)) throw new IOException($"Resource changed before import: '{path}'.");
            if (File.Exists(StagePath(entry))) throw new IOException("Resource staging path is occupied.");
        }
        foreach (var directory in CreatedDirectories.OrderBy(p => p.Length))
        {
            var path = ResourceAssetCleanupPlan.ContainedPath(Root, directory);
            ResourceAssetCleanupPlan.RequireNoLinks(path);
            Directory.CreateDirectory(path);
        }
        foreach (var entry in Entries)
        {
            var path = ResourceAssetCleanupPlan.ContainedPath(Root, entry.Path);
            if (!Matches(ReadBytes(path), entry.Before)) throw new IOException($"Resource changed during import: '{path}'.");
            Swap(entry, entry.After, entry.Before);
        }
    }

    internal void Recover()
    {
        // A committed job only releases its undo image; it never replays a write.
        if (Committed) return;
        ValidateRoot();
        foreach (var entry in Entries)
        {
            var path = ResourceAssetCleanupPlan.ContainedPath(Root, entry.Path);
            var current = ReadBytes(path);
            if (!Matches(current, entry.Before) && !Matches(current, entry.After))
                throw new IOException($"Resource changed after interrupted write; retained '{path}'.");
            var stage = ReadBytes(StagePath(entry));
            if (stage is not null && !Matches(stage, entry.After) && !Matches(stage, entry.Before))
                throw new IOException($"Resource staging file changed; retained '{StagePath(entry)}'.");
        }
        foreach (var entry in Entries.Reverse())
        {
            var path = ResourceAssetCleanupPlan.ContainedPath(Root, entry.Path);
            var stage = StagePath(entry);
            ResourceAssetCleanupPlan.RequireNoLinks(stage);
            var staged = ReadBytes(stage);
            if (staged is not null)
            {
                if (!Matches(staged, entry.After) && !Matches(staged, entry.Before)) throw new IOException("Resource staging file changed.");
                File.Delete(stage);
            }
            var current = ReadBytes(path);
            if (Matches(current, entry.Before)) continue;
            if (!Matches(current, entry.After)) throw new IOException($"Resource changed during recovery: '{path}'.");
            if (entry.Before is null) File.Delete(path);
            else Swap(entry, entry.Before, entry.After);
        }
        foreach (var directory in CreatedDirectories.OrderByDescending(p => p.Length))
        {
            var path = ResourceAssetCleanupPlan.ContainedPath(Root, directory);
            ResourceAssetCleanupPlan.RequireNoLinks(path);
            if (Directory.Exists(path)) Directory.Delete(path, recursive: false);
        }
    }
}
