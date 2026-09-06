using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record RenderStoredFrame(
    int LocalFrame,
    string DocumentKey);

internal sealed class RenderSnapshotStore
{
    private const string DocumentsDirectoryName = "documents";
    private const string AssetsDirectoryName = "assets";
    private readonly string _batchRoot;
    private readonly string _documentsDirectory;
    private readonly string _assetsDirectory;

    public RenderSnapshotStore(string batchRoot, bool create)
    {
        _batchRoot = Path.GetFullPath(batchRoot);
        _documentsDirectory = Path.Combine(
            _batchRoot,
            DocumentsDirectoryName);
        _assetsDirectory = Path.Combine(
            _batchRoot,
            AssetsDirectoryName);
        if (create)
        {
            Directory.CreateDirectory(_documentsDirectory);
            Directory.CreateDirectory(_assetsDirectory);
        }
    }

    public string WriteDocument(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new InvalidOperationException(
                "A frozen render document cannot be empty.");
        }
        var key = Hash(html);
        WriteOnce(DocumentPath(key), html);
        return key;
    }

    public void WriteAsset(string key, string dataUri)
    {
        RequireKey(key);
        if (!dataUri.StartsWith("data:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Frozen render asset '{key}' is not a data URI.");
        }
        if (!Hash(dataUri).Equals(key, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Frozen render asset '{key}' does not match its content hash.");
        }
        WriteOnce(AssetPath(key), dataUri);
    }

    public RenderFrameManifestWriter CreateManifest(
        string appearance)
    {
        return new RenderFrameManifestWriter(
            ManifestPath(appearance));
    }

    public static IEnumerable<RenderStoredFrame> ReadFrames(
        RenderFrameStoreReference reference)
    {
        ValidateReference(reference);
        var manifest = Path.Combine(
            reference.BatchRootPath,
            reference.ManifestFileName);
        var expectedFrame = 0;
        foreach (var line in File.ReadLines(manifest))
        {
            var frame = ParseFrame(line);
            if (frame.LocalFrame != expectedFrame)
            {
                throw new InvalidOperationException(
                    "The frozen render frame manifest is incomplete.");
            }
            expectedFrame++;
            yield return frame;
        }
        if (expectedFrame != reference.TotalFrames)
        {
            throw new InvalidOperationException(
                "The frozen render frame manifest is incomplete.");
        }
    }

    public string ReadDocument(string key)
    {
        RequireKey(key);
        var html = File.ReadAllText(DocumentPath(key));
        if (!Hash(html).Equals(key, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Frozen render document '{key}' does not match its content hash.");
        }
        return html;
    }

    public string ReadAsset(string key)
    {
        RequireKey(key);
        var dataUri = File.ReadAllText(AssetPath(key));
        if (!Hash(dataUri).Equals(key, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Frozen render asset '{key}' does not match its content hash.");
        }
        return dataUri;
    }

    public static void ValidateReference(
        RenderFrameStoreReference reference)
    {
        if (string.IsNullOrWhiteSpace(reference.BatchRootPath)
            || !Path.IsPathFullyQualified(reference.BatchRootPath)
            || reference.ManifestFileName
                != $"{reference.Appearance}.frames"
            || reference.Appearance is not RenderQueueAppearance.Light
                and not RenderQueueAppearance.Dark
            || reference.TotalFrames <= 0)
        {
            throw new InvalidOperationException(
                "The frozen render frame-store reference is incomplete.");
        }
        var root = Path.GetFullPath(reference.BatchRootPath);
        var manifest = Path.GetFullPath(Path.Combine(
            root,
            reference.ManifestFileName));
        if (!Path.GetDirectoryName(manifest)!.Equals(
                root,
                PathComparison())
            || !File.Exists(manifest)
            || !Directory.Exists(Path.Combine(
                root,
                DocumentsDirectoryName))
            || !Directory.Exists(Path.Combine(
                root,
                AssetsDirectoryName)))
        {
            throw new InvalidOperationException(
                "The frozen render frame store is unavailable.");
        }
    }

    public static void RequireContainedBatchRoot(
        string batchRoot,
        string storageRoot)
    {
        var root = Path.GetFullPath(storageRoot);
        var candidate = Path.GetFullPath(batchRoot);
        var relative = Path.GetRelativePath(root, candidate);
        if (relative.Length == 0
            || relative == "."
            || Path.IsPathFullyQualified(relative)
            || relative.StartsWith("..", StringComparison.Ordinal)
            || relative.IndexOfAny(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar])
                >= 0
            || !Guid.TryParse(relative, out _))
        {
            throw new InvalidOperationException(
                "The render snapshot store is outside the local queue root.");
        }
    }

    public static string BatchRoot(
        string storageRoot,
        string batchId)
    {
        if (!Guid.TryParse(batchId, out _))
        {
            throw new InvalidOperationException(
                "A render snapshot store requires a stable batch id.");
        }
        var result = Path.Combine(
            Path.GetFullPath(storageRoot),
            batchId);
        RequireContainedBatchRoot(result, storageRoot);
        return result;
    }

    public static void DeleteBatchRoot(
        string batchRoot,
        string storageRoot)
    {
        RequireContainedBatchRoot(batchRoot, storageRoot);
        if (Directory.Exists(batchRoot))
        {
            Directory.Delete(batchRoot, recursive: true);
        }
    }

    private string ManifestPath(string appearance)
    {
        if (appearance is not RenderQueueAppearance.Light
            and not RenderQueueAppearance.Dark)
        {
            throw new InvalidOperationException(
                $"Unsupported render appearance '{appearance}'.");
        }
        return Path.Combine(_batchRoot, $"{appearance}.frames");
    }

    private string DocumentPath(string key) =>
        Path.Combine(_documentsDirectory, $"{RequireKey(key)}.html");

    private string AssetPath(string key) =>
        Path.Combine(_assetsDirectory, $"{RequireKey(key)}.uri");

    private static RenderStoredFrame ParseFrame(string line)
    {
        var separator = line.IndexOf('|');
        if (separator <= 0
            || !int.TryParse(line[..separator], out var localFrame)
            || localFrame < 0)
        {
            throw new InvalidOperationException(
                "The frozen render frame manifest is malformed.");
        }
        var key = RequireKey(line[(separator + 1)..]);
        return new RenderStoredFrame(localFrame, key);
    }

    private static string RequireKey(string key)
    {
        if (key.Length != 64
            || key.Any((character) =>
                character is not (>= '0' and <= '9')
                    and not (>= 'a' and <= 'f')))
        {
            throw new InvalidOperationException(
                $"Invalid frozen render content hash '{key}'.");
        }
        return key;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static void WriteOnce(string path, string value)
    {
        if (File.Exists(path)) return;
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, value);
            try
            {
                File.Move(temporary, path);
            }
            catch (IOException) when (File.Exists(path))
            {
                // Another appearance froze the same content first.
            }
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static StringComparison PathComparison() =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}

internal sealed class RenderFrameManifestWriter : IDisposable
{
    private readonly string _path;
    private readonly string _temporary;
    private readonly StreamWriter _writer;
    private bool _committed;

    public RenderFrameManifestWriter(string path)
    {
        _path = path;
        _temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        _writer = new StreamWriter(
            new FileStream(
                _temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void Write(int localFrame, string documentKey)
    {
        _writer.Write(localFrame);
        _writer.Write('|');
        _writer.WriteLine(documentKey);
    }

    public void Commit()
    {
        if (_committed) return;
        _writer.Flush();
        _writer.Dispose();
        File.Move(_temporary, _path);
        _committed = true;
    }

    public void Dispose()
    {
        _writer.Dispose();
        if (!_committed && File.Exists(_temporary))
        {
            File.Delete(_temporary);
        }
    }
}
