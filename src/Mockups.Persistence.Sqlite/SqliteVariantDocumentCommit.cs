using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;

namespace Mockups.DesktopEditorShell.Data;

internal static class SqliteVariantDocumentCommit
{
    internal static void Commit(
        SqliteProjectContext context,
        SqliteConnection connection,
        IReadOnlyList<VariantDocumentChange> changes,
        SqliteDesignOwner design,
        SqliteProductionOwner production) =>
        Execute(context, connection, changes, design, production, true, null, null);

    internal static VariantFieldChangePreparation Execute(
        SqliteProjectContext context, SqliteConnection connection,
        IReadOnlyList<VariantDocumentChange> changes, SqliteDesignOwner design,
        SqliteProductionOwner production, bool commit, string? expectedRevision,
        IReadOnlyDictionary<string, RecordCreationDraft>? screenInputs)
    {
        lock (context.WriteGate)
        {
            using var transaction = connection.BeginTransaction();
            var owners = new HashSet<(ProjectTreeNodeKind, string)>();
            var changed = new HashSet<ReferenceTarget>();
            var removed = new HashSet<ReferenceTarget>();
            foreach (var change in changes)
            {
                if (!owners.Add((change.Kind, change.OwnerId)))
                    throw new InvalidOperationException($"Duplicate Variant document owner '{change.OwnerId}'.");
                var previous = Configs(change, Metadata(connection, change, design));
                var next = Configs(change, change.MetadataJson);
                foreach (var id in previous.Keys.Union(next.Keys))
                {
                    if (previous.TryGetValue(id, out var before) && next.TryGetValue(id, out var after)
                        && JsonNode.DeepEquals(before, after)) continue;
                    var target = new ReferenceTarget(change.Kind, id);
                    changed.Add(target);
                    if (!next.ContainsKey(id)) removed.Add(target);
                }
            }

            var usages = new ReferenceUsageService(context);
            var affected = Dependents(changed, usages.BuildIndex(connection),
                target => design.VariantTargets(connection, target));
            var previousContracts = affected.Where(target => target.Kind == ProjectTreeNodeKind.ModuleInstance)
                .ToDictionary(target => target.Id, target =>
                {
                    var screen = production.ModuleInstanceRepository.Get(connection, target.Id);
                    return production.ResolveModuleInstanceContract(connection, screen.ModuleId, screen.MetadataJson);
                }, StringComparer.Ordinal);

            var baseline = string.Join("\n", changes.Select(change => Metadata(connection, change, design)))
                + production.VariantRuntimeBaseline(connection, previousContracts.Keys)
                + System.Text.Json.JsonSerializer.Serialize(previousContracts)
                + System.Text.Json.JsonSerializer.Serialize(changes);

            foreach (var change in changes)
                design.StoreVariantDocument(connection, transaction, change);

            // Read the candidate graph on this same connection. Its exact edges
            // also reject newly introduced cycles before recursive preparation.
            var ordered = Dependents(changed, usages.BuildIndex(connection),
                target => design.VariantTargets(connection, target));
            foreach (var target in ordered.Where(target => !removed.Contains(target)
                         && target.Kind is ProjectTreeNodeKind.ComponentVariant or ProjectTreeNodeKind.ModuleVariant))
                design.PrepareVariantDependencies(connection, transaction, target.Kind, target.Id);

            var requests = production.ReconcileVariantRuntimePayloads(connection, transaction, previousContracts, commit: false);
            var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                baseline + System.Text.Json.JsonSerializer.Serialize(requests))));
            if (expectedRevision is not null && revision != expectedRevision)
                throw new InvalidOperationException("The Variant or its Screens changed while completing Runtime Inputs. Reopen the change to prepare current values.");
            if (commit)
            {
                production.ReconcileVariantRuntimePayloads(connection, transaction, previousContracts, true, screenInputs);
                transaction.Commit();
            }
            else transaction.Rollback();
            return new VariantFieldChangePreparation(revision, requests);
        }
    }

    private static string Metadata(SqliteConnection connection, VariantDocumentChange change, SqliteDesignOwner design) =>
        change.Kind switch
        {
            ProjectTreeNodeKind.ComponentVariant => design.ComponentClassRepository.Get(connection, change.OwnerId).MetadataJson,
            ProjectTreeNodeKind.ModuleVariant => design.AppModuleRepository.GetModule(connection, change.OwnerId).MetadataJson,
            _ => throw new InvalidOperationException($"Invalid Variant document kind '{change.Kind}'."),
        };

    private static Dictionary<string, JsonObject> Configs(VariantDocumentChange change, string metadata) =>
        VariantEnvelopeContract.Read(JsonPath.ParseRequiredObject(metadata, $"Variant owner '{change.OwnerId}'"),
                "variants", $"Variant owner '{change.OwnerId}'")
            .ToDictionary(variant => VariantReferenceId.Format(change.OwnerId, variant.Id), variant => variant.Config,
                StringComparer.Ordinal);

    private static IReadOnlyList<ReferenceTarget> Dependents(
        IEnumerable<ReferenceTarget> roots,
        IReadOnlyDictionary<ReferenceTarget, IReadOnlyList<ReferenceUsageRecord>> index,
        Func<ReferenceTarget, IReadOnlyList<ReferenceTarget>> variants)
    {
        var result = new List<ReferenceTarget>();
        var active = new HashSet<ReferenceTarget>();
        var visited = new HashSet<ReferenceTarget>();
        foreach (var root in roots) Visit(root);
        result.Reverse();
        return result;

        void Visit(ReferenceTarget target)
        {
            if (active.Contains(target))
                throw new InvalidOperationException($"Cyclic Variant dependency at '{target.Id}'.");
            if (!visited.Add(target)) return;
            active.Add(target);
            if (index.TryGetValue(target, out var usages))
                foreach (var usage in usages)
                    Visit(new ReferenceTarget(usage.SourceKind, usage.SourceNodeId));
            foreach (var variant in variants(target)) Visit(variant);
            active.Remove(target);
            result.Add(target);
        }
    }
}
