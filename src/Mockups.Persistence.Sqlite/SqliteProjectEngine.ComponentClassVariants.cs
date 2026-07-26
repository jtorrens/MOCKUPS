using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    private ProjectTreeNode RenameComponentClass(
        ProjectTreeNode node,
        string name) =>
        _designOwner.RenameComponentClass(node, name);

    private ProjectTreeNode DuplicateComponentVariant(
        ProjectTreeNode node) =>
        _designOwner.DuplicateComponentVariant(node);

    public ProjectTreeNode SaveComponentVariant(
        ProjectTreeNode sourceNode,
        string name) =>
        _designOwner.SaveComponentVariant(sourceNode, name);

    private void DeleteComponentVariant(ProjectTreeNode node)
    {
        if (!VariantReferenceId.TryParse(
                node.Id,
                out _,
                out _))
        {
            throw new InvalidOperationException(
                $"Invalid component variant node id '{node.Id}'.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            _designOwner.RequireComponentVariantDeleteAllowed(
                connection,
                node);
            var usages = GetReferenceUsages(
                connection,
                node.Kind,
                node.Id);
            if (usages.Count > 0)
            {
                throw new InvalidOperationException(
                    $"This component variant is still used and cannot be deleted.\n\n{string.Join(Environment.NewLine, usages.Take(12))}");
            }

            _designOwner.DeleteComponentVariant(connection, node);
        }
    }

    public ProjectTreeNode RenameComponentVariant(
        ProjectTreeNode node,
        string name) =>
        _designOwner.RenameComponentVariant(node, name);

    public ProjectTreeNode ToggleComponentVariantLock(
        ProjectTreeNode node) =>
        _designOwner.ToggleComponentVariantLock(node);

    public void ReplaceComponentVariantConfig(
        ProjectTreeNode node,
        string configJson) =>
        _designOwner.ReplaceComponentVariantConfig(
            node,
            configJson);

    public IReadOnlyList<ComponentVariantReferenceUsage> GetComponentVariantReferenceUsageDetails(ProjectTreeNode node)
    {
        return _referenceUsageService.GetUsages(node.Kind, node.Id)
            .Select((usage) => new ComponentVariantReferenceUsage(
                usage.SourceTypeLabel,
                usage.SourceName,
                usage.FieldLabel,
                usage.SourceNodeId,
                usage.EmbeddedContext is null ? null : ToEmbeddedComponentUsage(usage.EmbeddedContext)))
            .OrderBy((usage) => usage.SourceKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy((usage) => usage.SourceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy((usage) => usage.Detail, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static EmbeddedComponentUsage ToEmbeddedComponentUsage(ReferenceEmbeddedContext context) =>
        new(
            context.ParentComponentClassId,
            context.ParentComponentName,
            context.ParentComponentType,
            context.SlotFieldId,
            context.SlotLabel,
            context.HasOverrides,
            context.SourceNodeId);

}
