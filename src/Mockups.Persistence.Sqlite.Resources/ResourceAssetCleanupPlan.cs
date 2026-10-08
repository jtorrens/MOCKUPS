using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.Data;

internal sealed record ResourceAssetCleanupEntry(string Path, string Kind, string Hash);

internal sealed record ResourceAssetCleanupPlan(
    string Id, string Label, string Root, string Target,
    IReadOnlyList<ResourceAssetCleanupEntry> Entries, string Error)
{
    internal static string ContainedPath(string root, string relative)
    {
        // Stored paths use '/' independently of the OS reading a backup. A
        // foreign OS may inspect a pending task, but never reinterpret its root.
        var absolute = root.StartsWith('/') || (root.Length >= 3 && char.IsAsciiLetter(root[0]) && root[1] == ':' && root[2] == '/');
        if (!absolute || root.Contains('\\') || root.Contains('\0')
            || root.Split('/').Any(part => part is "." or "..")
            || string.IsNullOrWhiteSpace(relative) || relative.Contains('\\') || relative.Contains(':') || relative.Contains('\0')
            || relative.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidOperationException("Asset cleanup must remain strictly inside its declared root.");
        return root.TrimEnd('/') + "/" + relative;
    }

    internal static string StoredPath(string path) => System.IO.Path.DirectorySeparatorChar == '\\' ? path.Replace('\\', '/') : path;

    internal void RequireNativeRoot()
    {
        var windowsRoot = Root.StartsWith("//", StringComparison.Ordinal) || (Root.Length >= 2 && Root[1] == ':');
        if (windowsRoot != OperatingSystem.IsWindows() || !System.IO.Path.IsPathFullyQualified(Root))
            throw new IOException("Cleanup belongs to a different filesystem platform; the original path was retained.");
    }

    internal static void RequireNoLinks(string path)
    {
        for (var current = path; current is not null; current = System.IO.Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException($"Asset cleanup refuses symbolic links: '{current}'.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    internal static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    internal static ResourceAssetCleanupPlan Capture(string label, string root, string relative, bool isDirectory = true)
    {
        if (!System.IO.Path.IsPathFullyQualified(root))
            throw new InvalidOperationException("Asset cleanup requires an explicit absolute resource root.");
        root = StoredPath(System.IO.Path.GetFullPath(root));
        relative = StoredPath(relative);
        var target = ContainedPath(root, relative);
        RequireNoLinks(target);
        var entries = new List<ResourceAssetCleanupEntry>();
        void Visit(string path)
        {
            FileAttributes attributes;
            try { attributes = File.GetAttributes(path); }
            catch (FileNotFoundException) { return; }
            catch (DirectoryNotFoundException) { return; }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Asset cleanup refuses symbolic links: '{path}'.");
            var directory = (attributes & FileAttributes.Directory) != 0;
            entries.Add(new(StoredPath(System.IO.Path.GetRelativePath(root, path)), directory ? "directory" : "file",
                directory ? "" : HashFile(path)));
            if (directory)
                foreach (var child in Directory.EnumerateFileSystemEntries(path)) Visit(child);
        }
        Visit(target);
        if (entries.Count == 0)
            entries.Add(new(StoredPath(System.IO.Path.GetRelativePath(root, target)), isDirectory ? "directory" : "missing-file", ""));
        else if ((entries[0].Kind == "directory") != isDirectory)
            throw new IOException("The cleanup target does not match its declared resource kind.");
        var plan = new ResourceAssetCleanupPlan(Guid.NewGuid().ToString("N"), label, root, StoredPath(System.IO.Path.GetRelativePath(root, target)), entries, "");
        return Read(plan.Id, plan.Label, plan.Root, plan.Target, plan.EntriesJson(), plan.Error);
    }

    internal string EntriesJson() => new JsonArray(Entries.Select(entry => (JsonNode)new JsonObject
        { ["path"] = entry.Path, ["kind"] = entry.Kind, ["hash"] = entry.Hash }).ToArray()).ToJsonString();

    internal static ResourceAssetCleanupPlan Read(string id, string label, string root, string target, string json, string error)
    {
        if (!Guid.TryParseExact(id, "N", out _) || string.IsNullOrWhiteSpace(label))
            throw new InvalidOperationException("Invalid asset cleanup identity.");
        var targetPath = ContainedPath(root, target);
        var array = JsonPath.ParseRequiredArray(json, $"Asset cleanup '{id}' entries");
        var entries = new List<ResourceAssetCleanupEntry>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in array)
        {
            if (node is not JsonObject entry || entry.Count != 3)
                throw new InvalidOperationException("Invalid asset cleanup entry.");
            var path = JsonPath.RequiredString(entry, "path", id);
            var kind = JsonPath.RequiredString(entry, "kind", id);
            var hash = entry["hash"]?.GetValue<string>() ?? throw new InvalidOperationException("Missing cleanup hash.");
            var fullPath = ContainedPath(root, path);
            if (!paths.Add(fullPath) || !Within(targetPath, fullPath)
                || (kind != "file" && kind != "directory" && kind != "missing-file")
                || (kind == "file" ? hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c)) : hash.Length != 0))
                throw new InvalidOperationException("Invalid asset cleanup path, kind or fingerprint.");
            entries.Add(new(path, kind, hash));
        }
        if (entries.Count == 0 || entries[0].Path != target)
            throw new InvalidOperationException("Asset cleanup requires its exact target as its first entry.");
        if (entries.Count > 1 && entries[0].Kind != "directory")
            throw new InvalidOperationException("A file cleanup cannot contain descendant entries.");
        return new(id, label, root, target, entries, error);
    }

    internal static bool Overlaps(string first, string second) =>
        Within(StoredPath(first), StoredPath(second)) || Within(StoredPath(second), StoredPath(first));

    private static bool Within(string parent, string child) =>
        child.Equals(parent, StringComparison.OrdinalIgnoreCase)
        || child.StartsWith(parent.TrimEnd('/') + '/',
            StringComparison.OrdinalIgnoreCase);

    internal void Clean()
    {
        RequireNativeRoot();
        RequireNoLinks(Root);
        if ((File.GetAttributes(Root) & FileAttributes.Directory) == 0)
            throw new IOException("Asset cleanup root is not an available directory.");
        // Validate every surviving entry before deleting any. Missing entries are
        // completed work from an interrupted attempt, never replacement targets.
        foreach (var entry in Entries)
        {
            var path = ContainedPath(Root, entry.Path);
            RequireNoLinks(path);
            FileAttributes attributes;
            try { attributes = File.GetAttributes(path); }
            catch (FileNotFoundException) { continue; }
            catch (DirectoryNotFoundException) { continue; }
            var directory = (attributes & FileAttributes.Directory) != 0;
            if (entry.Kind == "missing-file" || directory != (entry.Kind == "directory") || (!directory && HashFile(path) != entry.Hash))
                throw new IOException($"Asset changed since deletion; retained '{path}'.");
        }
        foreach (var entry in Entries.Where(e => e.Kind == "file"))
        {
            var path = ContainedPath(Root, entry.Path);
            RequireNoLinks(path);
            try
            {
                // Recheck immediately before unlinking as external file editors
                // do not participate in the application's operation coordinator.
                if (HashFile(path) != entry.Hash)
                    throw new IOException($"Asset changed since deletion; retained '{path}'.");
                File.Delete(path);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        foreach (var entry in Entries.Where(e => e.Kind == "directory").OrderByDescending(e => e.Path.Length))
        {
            var path = ContainedPath(Root, entry.Path);
            RequireNoLinks(path);
            if (Directory.Exists(path)) Directory.Delete(path, recursive: false);
        }
    }
}
