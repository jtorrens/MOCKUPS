using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorVariantHistorySnapshot(
    string Id,
    string Label,
    DateTime CreatedAt,
    string ConfigJson);

internal sealed class EditorVariantHistoryService
{
    private const int MaxSnapshotsPerVariant = 10;
    private readonly SpikeDatabase _database;
    private readonly Dictionary<string, string> _activeEntryConfigJsonByVariant = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<EditorVariantHistorySnapshot>> _snapshotsByVariant = new(StringComparer.Ordinal);
    private int _sequence;

    public EditorVariantHistoryService(SpikeDatabase database)
    {
        _database = database;
    }

    public void TrackTransition(ProjectTreeNode? previousNode, ProjectTreeNode nextNode)
    {
        if (previousNode?.Id == nextNode.Id)
        {
            return;
        }

        if (previousNode?.Kind is ProjectTreeNodeKind.ComponentVariant or ProjectTreeNodeKind.ModuleVariant)
        {
            Leave(previousNode, VariantConfig(previousNode));
        }

        if (nextNode.Kind is ProjectTreeNodeKind.ComponentVariant or ProjectTreeNodeKind.ModuleVariant)
        {
            Enter(nextNode, VariantConfig(nextNode));
        }
    }

    private void Enter(ProjectTreeNode node, string configJson)
    {
        if (node.Kind is not ProjectTreeNodeKind.ComponentVariant and not ProjectTreeNodeKind.ModuleVariant)
        {
            return;
        }

        _activeEntryConfigJsonByVariant[node.Id] = Normalize(configJson);
    }

    private void Leave(ProjectTreeNode? node, string configJson)
    {
        if (node?.Kind is not ProjectTreeNodeKind.ComponentVariant and not ProjectTreeNodeKind.ModuleVariant)
        {
            return;
        }

        var nextConfig = Normalize(configJson);
        if (_activeEntryConfigJsonByVariant.TryGetValue(node.Id, out var entryConfig)
            && entryConfig.Equals(nextConfig, StringComparison.Ordinal))
        {
            return;
        }

        AddSnapshot(node.Id, nextConfig);
        _activeEntryConfigJsonByVariant[node.Id] = nextConfig;
    }

    public IReadOnlyList<EditorVariantHistorySnapshot> Snapshots(ProjectTreeNode node)
    {
        return node.Kind is ProjectTreeNodeKind.ComponentVariant or ProjectTreeNodeKind.ModuleVariant
            && _snapshotsByVariant.TryGetValue(node.Id, out var snapshots)
                ? snapshots
                : [];
    }

    private string VariantConfig(ProjectTreeNode node) => node.Kind switch
    {
        ProjectTreeNodeKind.ComponentVariant => _database.GetComponentVariantSettings(node).ConfigJson,
        ProjectTreeNodeKind.ModuleVariant => _database.GetModuleVariantSettings(node).ConfigJson,
        _ => throw new InvalidOperationException($"'{node.Kind}' is not a variant."),
    };

    public EditorVariantHistoryStore ExportState()
    {
        return new EditorVariantHistoryStore
        {
            Sequence = _sequence,
            SnapshotsByVariant = _snapshotsByVariant.ToDictionary(
                (entry) => entry.Key,
                (entry) => entry.Value.Select((snapshot) => new EditorVariantHistorySnapshotState
                {
                    Id = snapshot.Id,
                    Label = snapshot.Label,
                    CreatedAt = snapshot.CreatedAt,
                    ConfigJson = snapshot.ConfigJson,
                }).ToList(),
                StringComparer.Ordinal),
        };
    }

    public void RestoreState(EditorVariantHistoryStore? state)
    {
        _snapshotsByVariant.Clear();
        _sequence = Math.Max(0, state?.Sequence ?? 0);
        if (state?.SnapshotsByVariant is null)
        {
            return;
        }

        foreach (var (nodeId, snapshots) in state.SnapshotsByVariant)
        {
            _snapshotsByVariant[nodeId] = snapshots
                .Where((snapshot) => !string.IsNullOrWhiteSpace(snapshot.Id))
                .Take(10)
                .Select((snapshot) => new EditorVariantHistorySnapshot(
                    snapshot.Id,
                    snapshot.Label,
                    snapshot.CreatedAt,
                    string.IsNullOrWhiteSpace(snapshot.ConfigJson) ? "{}" : snapshot.ConfigJson))
                .ToList();
        }
    }

    private void AddSnapshot(string nodeId, string configJson)
    {
        if (!_snapshotsByVariant.TryGetValue(nodeId, out var snapshots))
        {
            snapshots = [];
            _snapshotsByVariant[nodeId] = snapshots;
        }

        var now = DateTime.Now;
        var baseLabel = now.ToString("HH:mm:ss");
        var duplicateCount = snapshots.Count((snapshot) => snapshot.Label == baseLabel || snapshot.Label.StartsWith($"{baseLabel} · ", StringComparison.Ordinal));
        var label = duplicateCount == 0 ? baseLabel : $"{baseLabel} · {duplicateCount + 1}";
        snapshots.Insert(0, new EditorVariantHistorySnapshot(
            (++_sequence).ToString(),
            label,
            now,
            configJson));

        if (snapshots.Count > MaxSnapshotsPerVariant)
        {
            snapshots.RemoveRange(MaxSnapshotsPerVariant, snapshots.Count - MaxSnapshotsPerVariant);
        }
    }

    private static string Normalize(string configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return "{}";
        }

        return JsonNode.Parse(configJson)?.ToJsonString() ?? "{}";
    }
}
