using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Mockups.DesktopEditorShell;

/// <summary>Portable, deterministic snapshots of the complete editable icon library.</summary>
internal static class IconLibrarySnapshot
{
    private static readonly DateTimeOffset EntryTime = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Create(string root, string destination)
    {
        BackupHubContract.RequireRegularDirectory(root, "icon library");
        BackupHubContract.RequireRegularDirectory(Path.Combine(root, "icon-themes"), "icon collections");
        using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var path in Entries(root).Order(StringComparer.Ordinal))
                {
                    var directory = Directory.Exists(path);
                    var name = Path.GetRelativePath(root, path).Replace('\\', '/') + (directory ? "/" : "");
                    ValidateName(name);
                    var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
                    entry.LastWriteTime = EntryTime;
                    // Do not preserve host-specific permissions or timestamps in the fingerprint.
                    entry.ExternalAttributes = 0;
                    if (directory) continue;
                    using var input = File.OpenRead(path);
                    using var target = entry.Open();
                    input.CopyTo(target);
                }
            }
            output.Flush(flushToDisk: true);
        }
    }

    public static string Fingerprint(string root)
    {
        var path = Path.Combine(Path.GetTempPath(), $"mockups-icons-{Guid.NewGuid():N}.zip");
        try { Create(root, path); return BackupHubContract.HashFile(path); }
        finally { File.Delete(path); }
    }

    public static void Validate(string archive)
    {
        BackupHubContract.RequireRegularFile(archive, "icon library snapshot");
        using var zip = ZipFile.OpenRead(archive);
        _ = CheckedEntries(zip);
        foreach (var entry in zip.Entries.Where(entry => !entry.FullName.EndsWith('/')))
        {
            using var stream = entry.Open();
            stream.CopyTo(Stream.Null);
        }
    }

    public static void Extract(string archive, string destination, bool durable = true)
    {
        if (Path.Exists(destination)) throw new IOException("Icon library extraction requires a new directory.");
        using var zip = ZipFile.OpenRead(archive);
        var entries = CheckedEntries(zip);
        Directory.CreateDirectory(destination);
        foreach (var entry in entries)
        {
            var target = Path.Combine(destination, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            if (entry.FullName.EndsWith('/')) Directory.CreateDirectory(target);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var input = entry.Open();
                using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                input.CopyTo(output);
                if (durable) output.Flush(flushToDisk: true);
            }
        }
        if (!durable) return;
        foreach (var directory in Directory.EnumerateDirectories(destination, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
            BackupHubContract.FlushDirectory(directory);
        BackupHubContract.FlushDirectory(destination);
    }

    private static ZipArchiveEntry[] CheckedEntries(ZipArchive zip)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            ValidateName(entry.FullName);
            if (!names.Add(entry.FullName.TrimEnd('/'))
                || ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000
                || (entry.FullName.EndsWith('/') && entry.Length != 0))
                throw new InvalidDataException("Icon snapshot contains duplicate, symbolic or malformed entries.");
        }
        if (!zip.Entries.Any(entry => entry.FullName == "icon-themes/"))
            throw new InvalidDataException("Icon snapshot must declare its icon-themes directory.");
        // Directories precede children. A conflicting file parent fails CreateNew/extraction.
        return zip.Entries.OrderBy(entry => entry.FullName, StringComparer.Ordinal).ToArray();
    }

    private static void ValidateName(string name)
    {
        var parts = name.TrimEnd('/').Split('/');
        if (!name.StartsWith("icon-themes/", StringComparison.Ordinal)
            || name.Contains('\\') || name.IndexOfAny([':', '\0', '<', '>', '"', '|', '?', '*']) >= 0
            || parts.Any(part => part.Length == 0 || part is "." or ".." || part.EndsWith(' ') || part.EndsWith('.')))
            throw new InvalidDataException($"Invalid icon snapshot path '{name}'.");
    }

    private static IEnumerable<string> Entries(string directory)
    {
        BackupHubContract.RequireRegularDirectory(directory, "icon library directory");
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            if (Directory.Exists(path))
            {
                BackupHubContract.RequireRegularDirectory(path, "icon library directory");
                yield return path;
                foreach (var child in Entries(path)) yield return child;
            }
            else
            {
                BackupHubContract.RequireRegularFile(path, "icon library file");
                yield return path;
            }
        }
    }
}
