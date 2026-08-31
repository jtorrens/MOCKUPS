using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Integrations.ProductionOutput;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RecordClassFieldValueService
{
    private readonly IProductionRecordFieldStore _production;
    private readonly IRecordReferenceOverrideStore
        _recordReferenceOverrides;
    private readonly IDesignRecordFieldStore _design;
    private readonly IResourceRecordFieldStore _resources;
    private readonly IModuleInstanceTimelineStore _timeline;
    private readonly ProductionOutputRootStore _productionOutputRoots;
    private readonly ShotManagerDocumentStore _shotManagerDocuments;
    private readonly ProductionOutputPlanResolver _productionOutputPlans;
    private readonly ModuleInstanceTimelineDataSource _timelineDataSource;

    public RecordClassFieldValueService(
        IProductionRecordFieldStore production,
        IRecordReferenceOverrideStore recordReferenceOverrides,
        IDesignRecordFieldStore design,
        IResourceRecordFieldStore resources,
        IModuleInstanceTimelineStore timeline,
        IModuleInstanceThemeTokenQuery moduleInstanceThemes,
        ProductionOutputRootStore? productionOutputRoots = null,
        ShotManagerDocumentStore? shotManagerDocuments = null)
    {
        _production = production;
        _recordReferenceOverrides =
            recordReferenceOverrides;
        _design = design;
        _resources = resources;
        _timeline = timeline;
        _productionOutputRoots =
            productionOutputRoots ?? new ProductionOutputRootStore();
        _shotManagerDocuments =
            shotManagerDocuments ?? new ShotManagerDocumentStore();
        _productionOutputPlans = new ProductionOutputPlanResolver(
            _productionOutputRoots,
            _shotManagerDocuments);
        _timelineDataSource =
            new ModuleInstanceTimelineDataSource(
                timeline,
                moduleInstanceThemes);
    }

    public bool CanHandle(ProjectTreeNodeKind nodeKind, string fieldId)
    {
        return nodeKind switch
        {
            ProjectTreeNodeKind.Project => fieldId.StartsWith("project.", StringComparison.Ordinal),
            ProjectTreeNodeKind.App => fieldId.StartsWith("app.", StringComparison.Ordinal),
            ProjectTreeNodeKind.Module => fieldId.StartsWith("module.", StringComparison.Ordinal),
            ProjectTreeNodeKind.ModuleVariant => fieldId.StartsWith("module.", StringComparison.Ordinal),
            ProjectTreeNodeKind.ModuleInstance => fieldId.StartsWith("moduleInstance.", StringComparison.Ordinal),
            ProjectTreeNodeKind.Episode => fieldId.StartsWith("episode.", StringComparison.Ordinal),
            ProjectTreeNodeKind.Shot => fieldId.StartsWith("shot.", StringComparison.Ordinal),
            ProjectTreeNodeKind.PaletteColor => fieldId.StartsWith("palette.", StringComparison.Ordinal),
            ProjectTreeNodeKind.Device => fieldId.StartsWith("device.", StringComparison.Ordinal),
            ProjectTreeNodeKind.Theme => fieldId.StartsWith("theme.", StringComparison.Ordinal),
            ProjectTreeNodeKind.Actor => fieldId.StartsWith("actor.", StringComparison.Ordinal),
            ProjectTreeNodeKind.ProductionFont => fieldId.StartsWith("font.", StringComparison.Ordinal),
            ProjectTreeNodeKind.IconTheme => fieldId.StartsWith("iconTheme.", StringComparison.Ordinal),
            _ => false,
        };
    }

    public FieldValue CreateFieldValue(ProjectTreeNode node, string fieldId)
    {
        var field = RecordClassFieldCatalog.Get(fieldId);
        var value = node.Kind switch
        {
            ProjectTreeNodeKind.Project => ProjectFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.App => AppFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.Module => ModuleFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.ModuleVariant => ModuleVariantFieldValue(node, field.Id),
            ProjectTreeNodeKind.ModuleInstance => ModuleInstanceFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.Episode => EpisodeFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.Shot => ShotFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.PaletteColor => PaletteColorFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.Device => DeviceFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.Theme => ThemeFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.Actor => ActorFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.ProductionFont => ProductionFontFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.IconTheme => IconThemeFieldValue(node.Id, field.Id),
            _ => throw new InvalidOperationException($"Record class field '{fieldId}' is not supported for '{node.Kind}'."),
        };
        var options = ResolveOptions(node, field);

        if (node.Kind == ProjectTreeNodeKind.Shot && field.Id == "shot.fps")
        {
            var settings = _production.GetShotSettings(node.Id);
            var inheritedValue = settings.ProjectDefaultFps.ToString();
            return ValidateFieldValue(new FieldValue(
                new FieldDefinition(
                    field.Id,
                    field.Label,
                    field.ValueKind,
                    IsEditable: field.IsEditable,
                    DefaultValue: inheritedValue,
                    CommitAsDefault: false,
                    CanInherit: true,
                    InheritedValue: inheritedValue,
                    Options: options,
                    PairLabels: field.PairLabels,
                    ImagePreview: field.ImagePreview,
                    Number: field.Number,
                    RecordReference: field.RecordReference,
                    ComponentInputBindings: field.ComponentInputBindings,
                    StructuredCollection: field.StructuredCollection,
                    RuntimeInputComponentVariantFieldId: field.RuntimeInputComponentVariantFieldId,
                    RuntimeCollectionComponentVariantFieldId: field.RuntimeCollectionComponentVariantFieldId,
                    Unit: field.Unit,
                    MotionTiming: field.MotionTiming),
                settings.FpsOverride?.ToString() ?? inheritedValue,
                IsInherited: settings.FpsOverride is null));
        }

        if (node.Kind == ProjectTreeNodeKind.Shot
            && field.Id is "shot.deviceOverrideId" or "shot.themeOverrideId")
        {
            var settings = _production.GetShotSettings(node.Id);
            var actor = _resources.GetActorSettings(settings.OwnerActorId);
            var isDevice = field.Id == "shot.deviceOverrideId";
            var inheritedValue = isDevice
                ? actor.DefaultDeviceId
                : actor.DefaultThemeId;
            var localValue = isDevice
                ? settings.DeviceOverrideId
                : settings.ThemeOverrideId;
            var resourceOverrideResult = ValidateFieldValue(new FieldValue(
                new FieldDefinition(
                    field.Id,
                    field.Label,
                    field.ValueKind,
                    IsEditable: field.IsEditable,
                    DefaultValue: inheritedValue,
                    CommitAsDefault: false,
                    CanInherit: true,
                    InheritedValue: inheritedValue,
                    Options: options,
                    PairLabels: field.PairLabels,
                    ImagePreview: field.ImagePreview,
                    Number: field.Number,
                    RecordReference: field.RecordReference,
                    ComponentInputBindings: field.ComponentInputBindings,
                    StructuredCollection: field.StructuredCollection,
                    RuntimeInputComponentVariantFieldId: field.RuntimeInputComponentVariantFieldId,
                    RuntimeCollectionComponentVariantFieldId: field.RuntimeCollectionComponentVariantFieldId,
                    Unit: field.Unit,
                    MotionTiming: field.MotionTiming),
                localValue ?? inheritedValue,
                IsInherited: localValue is null));
            return isDevice
                && DeviceSettingsFieldContract.ParseOverrides(
                    settings.DeviceOverridesJson,
                    $"Shot '{node.Id}' Device overrides").Count > 0
                    ? resourceOverrideResult with { IsHighlighted = true }
                    : resourceOverrideResult;
        }

        var isEditable = field.IsEditable
            || (node.Kind == ProjectTreeNodeKind.Shot
                && field.Id == "shot.durationFrames"
                && _production.GetShotSettings(node.Id).DurationPolicy
                    == ShotDurationPolicy.Explicit)
            || (node.Kind == ProjectTreeNodeKind.ModuleInstance
                && field.Id == "moduleInstance.durationFrames"
                && RuntimeDurationContract.Policy(
                    _timeline.GetModuleInstanceEffectiveContractJson(node.Id))
                    == RuntimeDurationPolicy.Explicit)
            || (node.Kind == ProjectTreeNodeKind.ModuleInstance
                && field.Id == "moduleInstance.durationPolicy"
                && RuntimeDurationContract.AllowedPolicies(
                    _timeline.GetModuleInstanceEffectiveContractJson(node.Id))
                    .Count > 1);
        var result = new FieldValue(
            new FieldDefinition(
                field.Id,
                field.Label,
                field.ValueKind,
                IsEditable: isEditable,
                DefaultValue: DefaultValue(node.Kind, field, value),
                CommitAsDefault: CommitAsDefault(node.Kind, field),
                Options: options,
                PairLabels: field.PairLabels,
                ImagePreview: field.ImagePreview,
                Number: field.Number,
                RecordReference: field.RecordReference,
                ComponentInputBindings: field.ComponentInputBindings,
                StructuredCollection: field.StructuredCollection,
                RuntimeInputComponentVariantFieldId: field.RuntimeInputComponentVariantFieldId,
                RuntimeCollectionComponentVariantFieldId: field.RuntimeCollectionComponentVariantFieldId,
                Unit: field.Unit,
                MotionTiming: field.MotionTiming),
            value);
        var lockedResult = node.Kind == ProjectTreeNodeKind.ModuleVariant && node.IsLocked
            ? result with { Definition = result.Definition with { IsEditable = false } }
            : result;
        return ValidateFieldValue(lockedResult);
    }

    public void CommitFieldValue(ProjectTreeNode node, string fieldId, string value)
    {
        if (node.Kind == ProjectTreeNodeKind.ModuleVariant && node.IsLocked) return;
        var current = CreateFieldValue(node, fieldId);
        FieldOptionContract.ValidateValue(
            current.Definition,
            value,
            $"Dictionary field '{fieldId}'");
        if (node.Kind == ProjectTreeNodeKind.PaletteColor && fieldId == "palette.token")
        {
            var renamed = _resources.RenamePaletteColor(node, value);
            node.Name = renamed.Name;
            return;
        }
        switch (node.Kind)
        {
            case ProjectTreeNodeKind.Project when fieldId.StartsWith("project.", StringComparison.Ordinal):
                if (fieldId == "project.productionRoot")
                {
                    _productionOutputRoots.Set(node.Id, value);
                    return;
                }
                if (fieldId == "project.shotManagerJsonPath")
                {
                    var candidate = _shotManagerDocuments.ValidateDocument(value);
                    var association = _production.GetProjectSettings(node.Id)
                        .ShotManagerOutput;
                    if (association.Enabled)
                    {
                        if (!association.ProductionId.Equals(
                                candidate.Production.ProductionId,
                                StringComparison.Ordinal))
                            throw new InvalidOperationException(
                                "Disable Shot Managed before connecting another Production.");
                        _production.RefreshShotManagerProduction(
                            node.Id,
                            candidate.Production);
                    }
                    _shotManagerDocuments.SetValidated(node.Id, candidate);
                    return;
                }
                if (fieldId == "project.shotManagerRoot")
                {
                    _shotManagerDocuments.SetRoot(node.Id, value);
                    return;
                }
                if (fieldId == "project.outputExample")
                {
                    return;
                }
                if (fieldId == "project.shotManagerFolderSuffix")
                {
                    return;
                }
                if (fieldId == "project.shotManaged")
                {
                    var enabled = BooleanText.ParseRequired(
                        value,
                        "Shot Managed");
                    var association = _production.GetProjectSettings(node.Id)
                        .ShotManagerOutput;
                    _shotManagerDocuments.SetRequestedEnabled(
                        node.Id,
                        enabled);
                    if (!enabled || !string.IsNullOrEmpty(
                            association.ProductionId))
                        _production.SetShotManagerProductionEnabled(
                            node.Id,
                            enabled);
                    return;
                }
                if (fieldId == "project.shotManagerWorkstream")
                {
                    _shotManagerDocuments.SetPendingWorkstream(
                        node.Id,
                        value);
                    return;
                }
                if (fieldId == "project.shotManagerFolder")
                {
                    var live = _shotManagerDocuments.Open(node.Id);
                    var location = _shotManagerDocuments.GetLocation(node.Id);
                    var currentAssociation = _production.GetProjectSettings(node.Id)
                        .ShotManagerOutput;
                    var workstream = string.IsNullOrWhiteSpace(
                            location.PendingWorkstreamName)
                        ? currentAssociation.WorkstreamName
                        : location.PendingWorkstreamName;
                    _production.ConnectShotManagerProduction(
                        node.Id,
                        live.Production,
                        workstream,
                        value);
                    _shotManagerDocuments.SetRequestedEnabled(
                        node.Id,
                        true);
                    return;
                }
                _production.UpdateProjectField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.App when fieldId.StartsWith("app.", StringComparison.Ordinal):
                _design.UpdateAppField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.Module when fieldId.StartsWith("module.", StringComparison.Ordinal):
                _design.UpdateModuleField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.ModuleVariant when fieldId.StartsWith("module.", StringComparison.Ordinal):
                _design.UpdateModuleVariantField(node, fieldId, value);
                return;
            case ProjectTreeNodeKind.ModuleInstance when fieldId.StartsWith("moduleInstance.", StringComparison.Ordinal):
                _production.UpdateModuleInstanceField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.Episode when fieldId.StartsWith("episode.", StringComparison.Ordinal):
                if (fieldId == "episode.shotManagerEpisodeId")
                {
                    var projectId = RequiredProjectId(node);
                    var episode = string.IsNullOrEmpty(value)
                        ? null
                        : _shotManagerDocuments.Open(projectId)
                            .Production.Episodes.SingleOrDefault((candidate) =>
                                candidate.Id.Equals(value, StringComparison.Ordinal))
                            ?? throw new InvalidOperationException(
                                $"Shot Manager Episode '{value}' is not available.");
                    _production.AssociateShotManagerEpisode(
                        node.Id,
                        episode);
                    return;
                }
                _production.UpdateEpisodeField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.Shot when fieldId.StartsWith("shot.", StringComparison.Ordinal):
                if (fieldId == "shot.shotManagerShotId")
                {
                    var projectId = RequiredProjectId(node);
                    var shot = string.IsNullOrEmpty(value)
                        ? null
                        : _shotManagerDocuments.Open(projectId)
                            .Production.Shots.SingleOrDefault((candidate) =>
                                candidate.Id.Equals(value, StringComparison.Ordinal))
                            ?? throw new InvalidOperationException(
                                $"Shot Manager Shot '{value}' is not available.");
                    _production.AssociateShotManagerShot(node.Id, shot);
                    return;
                }
                _production.UpdateShotField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.PaletteColor when fieldId.StartsWith("palette.", StringComparison.Ordinal):
                _resources.UpdatePaletteColorField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.Device when fieldId.StartsWith("device.", StringComparison.Ordinal):
                _resources.UpdateDeviceField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.Theme when fieldId.StartsWith("theme.", StringComparison.Ordinal):
                _resources.UpdateThemeField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.Actor when fieldId.StartsWith("actor.", StringComparison.Ordinal):
                _resources.UpdateActorField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.ProductionFont when fieldId.StartsWith("font.", StringComparison.Ordinal):
                _resources.UpdateProductionFontField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.IconTheme when fieldId.StartsWith("iconTheme.", StringComparison.Ordinal):
                return;
            default:
                throw new InvalidOperationException($"Record class field '{fieldId}' is not supported for '{node.Kind}'.");
        }
    }

    public IReadOnlyDictionary<string, FieldValue>
        CreateRecordReferenceOverrideFields(
            EditorEmbeddedContext context,
            IEnumerable<string> fieldIds)
    {
        var definition = RequireRecordReferenceOverride(
            context);
        var overrides = ParseRecordReferenceOverrides(
            context,
            definition);
        var allowedFieldIds = definition.OverrideFieldIds!
            .ToHashSet(StringComparer.Ordinal);
        var fields = new Dictionary<string, FieldValue>(
            StringComparer.Ordinal);
        foreach (var fieldId in fieldIds.Distinct(
                     StringComparer.Ordinal))
        {
            if (!allowedFieldIds.Contains(fieldId))
            {
                continue;
            }
            var inherited = CreateFieldValue(
                context.RecordReferenceOverride!.ReferenceNode,
                fieldId);
            var inheritedValue = inherited.Value;
            var hasOverride = overrides[fieldId]
                is JsonValue;
            var value = hasOverride
                ? overrides[fieldId]!.GetValue<string>()
                : inheritedValue;
            fields[fieldId] = ValidateFieldValue(new FieldValue(
                inherited.Definition with
                {
                    DefaultValue = inheritedValue,
                    CommitAsDefault = false,
                    CanInherit = true,
                    InheritedValue = inheritedValue,
                },
                value,
                IsInherited: !hasOverride));
        }
        return fields;
    }

    public string CurrentRecordReferenceOverrideStoredValue(
        EditorEmbeddedContext context,
        string fieldId)
    {
        var definition = RequireRecordReferenceOverride(
            context);
        RequireOverrideField(definition, fieldId);
        var overrides = ParseRecordReferenceOverrides(
            context,
            definition);
        return overrides[fieldId] is JsonValue value
            ? value.GetValue<string>()
            : "inherited";
    }

    public void CommitRecordReferenceOverrideField(
        EditorEmbeddedContext context,
        string fieldId,
        string value)
    {
        var definition = RequireRecordReferenceOverride(
            context);
        RequireOverrideField(definition, fieldId);
        var current = CreateRecordReferenceOverrideFields(
            context,
            [fieldId])[fieldId];
        FieldOptionContract.ValidateValue(
            current.Definition,
            value,
            $"RecordReference override '{fieldId}'");
        var overrides = ParseRecordReferenceOverrides(
            context,
            definition);
        if (value == current.Definition.InheritedStorageValue)
        {
            overrides.Remove(fieldId);
        }
        else
        {
            overrides[fieldId] = value;
        }
        _recordReferenceOverrides.UpdateOverrideDocument(
            context.OwnerNode,
            definition.OverrideDocumentFieldId,
            overrides.ToJsonString());
    }

    private static RecordReferenceDefinition
        RequireRecordReferenceOverride(
            EditorEmbeddedContext context)
    {
        var reference = context.RecordReferenceOverride
            ?? throw new InvalidOperationException(
                "The editor context is not a RecordReference Overrides context.");
        var definition = RecordClassFieldCatalog.Get(
                reference.ReferenceFieldId)
            .RecordReference
            ?? throw new InvalidOperationException(
                $"Field '{reference.ReferenceFieldId}' is not a RecordReference.");
        if (string.IsNullOrWhiteSpace(
                definition.OverrideRecordClassId)
            || string.IsNullOrWhiteSpace(
                definition.OverrideDocumentFieldId)
            || definition.OverrideFieldIds is null
            || definition.OverrideFieldIds.Count == 0)
        {
            throw new InvalidOperationException(
                $"RecordReference '{reference.ReferenceFieldId}' does not declare a complete Overrides contract.");
        }
        if (!reference.ReferenceNode.RecordClassId.Equals(
                definition.OverrideRecordClassId,
                StringComparison.Ordinal)
            || !reference.OverrideDocumentFieldId.Equals(
                definition.OverrideDocumentFieldId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"RecordReference Overrides context '{reference.ReferenceFieldId}' does not match its declared contract.");
        }
        return definition;
    }

    private JsonObject ParseRecordReferenceOverrides(
        EditorEmbeddedContext context,
        RecordReferenceDefinition definition)
    {
        var owner =
            $"{context.OwnerNode.RecordClassId} '{context.OwnerNode.Id}' RecordReference Overrides";
        var overrides = JsonPath.ParseRequiredObject(
            _recordReferenceOverrides.GetOverrideDocument(
                context.OwnerNode,
                definition.OverrideDocumentFieldId),
            owner);
        var allowed = definition.OverrideFieldIds!
            .ToHashSet(StringComparer.Ordinal);
        foreach (var (fieldId, node) in overrides)
        {
            if (!allowed.Contains(fieldId)
                || node is not JsonValue value
                || !value.TryGetValue<string>(out _))
            {
                throw new InvalidOperationException(
                    $"{owner} contains invalid field '{fieldId}'.");
            }
        }
        return overrides;
    }

    private static void RequireOverrideField(
        RecordReferenceDefinition definition,
        string fieldId)
    {
        if (!definition.OverrideFieldIds!.Contains(
                fieldId,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Field '{fieldId}' is not declared by this RecordReference Overrides contract.");
        }
    }

    private static FieldValue ValidateFieldValue(FieldValue fieldValue)
    {
        FieldOptionContract.ValidateValue(
            fieldValue.Definition,
            fieldValue.Value,
            $"Dictionary field '{fieldValue.Definition.Id}'");
        return fieldValue;
    }

    private string ProjectFieldValue(string projectId, string fieldId)
    {
        var settings = _production.GetProjectSettings(projectId);
        return fieldId switch
        {
            "project.slug" => settings.Slug,
            "project.defaultFps" => settings.DefaultFps.ToString(),
            "project.mediaRoot" => settings.MediaRoot,
            "project.productionRoot" =>
                _productionOutputRoots.Get(projectId) ?? "",
            "project.shotManaged" =>
                BoolToString(
                    settings.ShotManagerOutput.Enabled
                    || _shotManagerDocuments.GetLocation(projectId)
                        .RequestedEnabled),
            "project.shotManagerJsonPath" =>
                _shotManagerDocuments.Get(projectId) ?? "",
            "project.shotManagerRoot" =>
                _shotManagerDocuments.GetRoot(projectId) ?? "",
            "project.shotManagerWorkstream" =>
                string.IsNullOrWhiteSpace(
                        _shotManagerDocuments.GetLocation(projectId)
                            .PendingWorkstreamName)
                    ? settings.ShotManagerOutput.WorkstreamName
                    : _shotManagerDocuments.GetLocation(projectId)
                        .PendingWorkstreamName,
            "project.shotManagerFolder" =>
                settings.ShotManagerOutput.FolderName,
            "project.shotManagerFolderSuffix" =>
                settings.ShotManagerOutput.FolderSuffix,
            "project.productionCode" =>
                settings.ProductionOutput.TechnicalCode,
            "project.productionSeasonCode" =>
                settings.ProductionOutput.SeasonCode,
            "project.episodePrefix" =>
                settings.ProductionOutput.EpisodePrefix,
            "project.shotPrefix" =>
                settings.ProductionOutput.ShotPrefix,
            "project.shotNumberPadding" =>
                settings.ProductionOutput.ShotNumberPadding.ToString(),
            "project.outputVersionPadding" =>
                settings.ProductionOutput.VersionPadding.ToString(),
            "project.outputFramePadding" =>
                settings.ProductionOutput.FramePadding.ToString(),
            "project.outputRelativeDirectoryTemplate" =>
                settings.ProductionOutput.RelativeDirectoryTemplate,
            "project.outputExample" =>
                ProductionOutputContract.Resolve(
                    projectId,
                    "example",
                    1,
                    ProductionOutputContract.CreateEpisodeCode(
                        settings.ProductionOutput.EpisodePrefix,
                        1),
                    ProductionOutputContract.CreateShotCode(
                        settings.ProductionOutput.ShotPrefix,
                        1,
                        settings.ProductionOutput.ShotNumberPadding),
                    settings.ProductionOutput).TechnicalName,
            _ => throw new InvalidOperationException($"Unknown project field '{fieldId}'."),
        };
    }

    private string EpisodeFieldValue(string episodeId, string fieldId)
    {
        var settings = _production.GetEpisodeSettings(episodeId);
        return fieldId switch
        {
            "episode.slug" => settings.Slug,
            "episode.sortOrder" => settings.SortOrder.ToString(),
            "episode.shotManagerEpisodeId" =>
                settings.ShotManagerEpisode.IsAssociated
                    ? settings.ShotManagerEpisode.EpisodeId
                    : "",
            _ => throw new InvalidOperationException($"Unknown episode field '{fieldId}'."),
        };
    }

    private string ModuleFieldValue(string moduleId, string fieldId)
    {
        var settings = _design.GetModuleSettings(moduleId);
        return fieldId switch
        {
            "module.recordClassId" => settings.RecordClassId,
            "module.sortOrder" => settings.SortOrder.ToString(),
            "module.metadata" => settings.MetadataJson,
            "module.appearanceMode" =>
                _design.GetModuleConfigFieldValue(moduleId, fieldId),
            _ when fieldId.StartsWith("module.", StringComparison.Ordinal) =>
                _design.GetModuleConfigFieldValue(moduleId, fieldId),
            _ => throw new InvalidOperationException($"Unknown module field '{fieldId}'."),
        };
    }

    private string ModuleVariantFieldValue(ProjectTreeNode node, string fieldId)
    {
        var settings = _design.GetModuleVariantSettings(node);
        return fieldId switch
        {
            "module.recordClassId" => settings.RecordClassId,
            "module.sortOrder" => settings.SortOrder.ToString(),
            "module.metadata" => settings.MetadataJson,
            "module.appearanceMode" => _design.GetModuleVariantConfigFieldValue(node, fieldId),
            _ when fieldId.StartsWith("module.", StringComparison.Ordinal) =>
                _design.GetModuleVariantConfigFieldValue(node, fieldId),
            _ => throw new InvalidOperationException($"Unknown module variant field '{fieldId}'."),
        };
    }

    private string ModuleInstanceFieldValue(string moduleInstanceId, string fieldId)
    {
        var settings = _timeline.GetModuleInstanceSettings(
            moduleInstanceId);
        return fieldId switch
        {
            "moduleInstance.module" =>
                _timeline.GetModuleInstanceModuleName(moduleInstanceId),
            "moduleInstance.variant" => _production.GetModuleInstanceVariantReference(moduleInstanceId),
            "moduleInstance.sortOrder" => settings.SortOrder.ToString(),
            "moduleInstance.durationPolicy" => settings.DurationPolicy,
            "moduleInstance.durationFrames" =>
                ModuleInstanceTimeline
                    .ScreenRange(
                        _timelineDataSource,
                        moduleInstanceId)
                    .EffectiveDurationFrames
                    .ToString(),
            "moduleInstance.actionDelayFrames" =>
                settings.ActionDelayFrames.ToString(),
            "moduleInstance.transition" =>
                settings.TransitionJson,
            _ => throw new InvalidOperationException($"Unknown module instance field '{fieldId}'."),
        };
    }

    private string ShotFieldValue(string shotId, string fieldId)
    {
        var settings = _production.GetShotSettings(shotId);
        return fieldId switch
        {
            "shot.slug" => settings.Slug,
            "shot.version" => settings.Version.ToString(),
            "shot.sortOrder" => settings.SortOrder.ToString(),
            "shot.durationPolicy" => ShotTimelineDuration.FormatPolicy(settings.DurationPolicy),
            "shot.calculatedDurationFrames" => ModuleInstanceTimeline.ShotDurationFrames(_timelineDataSource, shotId).ToString(),
            "shot.durationFrames" => settings.DurationFrames.ToString(),
            "shot.fps" => settings.Fps.ToString(),
            "shot.ownerActorId" => settings.OwnerActorId,
            "shot.deviceOverrideId" => settings.DeviceOverrideId ?? "",
            "shot.themeOverrideId" => settings.ThemeOverrideId ?? "",
            "shot.referenceVideoPath" => settings.ReferenceVideo.SourcePath,
            "shot.renderName" => ShotRenderName(shotId),
            "shot.shotManagerShotId" =>
                settings.ShotManagerShot.IsAssociated
                    ? settings.ShotManagerShot.ShotId
                    : "",
            "shot.canvas" => settings.CanvasJson,
            "shot.metadata" => settings.MetadataJson,
            _ => throw new InvalidOperationException($"Unknown shot field '{fieldId}'."),
        };
    }

    private string AppFieldValue(string appId, string fieldId)
    {
        var settings = _design.GetAppSettings(appId);
        return fieldId switch
        {
            "app.bundleKey" => settings.BundleKey,
            "app.appType" => settings.AppType,
            "app.config" => settings.ConfigJson,
            "app.metadata" => settings.MetadataJson,
            "app.wallpaper.kind" => _design.GetAppConfigFieldValue(appId, fieldId),
            "app.wallpaper.opacity" => _design.GetAppConfigFieldValue(appId, fieldId),
            "app.wallpaper.color" => _design.GetAppConfigFieldValue(appId, fieldId),
            "app.wallpaper.images.light.filePath" => _design.GetAppConfigFieldValue(appId, fieldId),
            "app.wallpaper.images.dark.filePath" => _design.GetAppConfigFieldValue(appId, fieldId),
            "app.note" => _design.GetAppMetadataFieldValue(appId, fieldId),
            "app.icon.filePath" => _design.GetAppMetadataFieldValue(appId, fieldId),
            "app.icon.scale" => _design.GetAppMetadataFieldValue(appId, fieldId),
            "app.icon.offset" => _design.GetAppMetadataFieldValue(appId, fieldId),
            _ => throw new InvalidOperationException($"Unknown app field '{fieldId}'."),
        };
    }

    private string PaletteColorFieldValue(string colorId, string fieldId)
    {
        var settings = _resources.GetPaletteColorSettings(colorId);
        return fieldId switch
        {
            "palette.token" => settings.Token,
            "palette.valueHex" => settings.ValueHex,
            "palette.isNeutral" => BoolToString(settings.IsNeutral),
            "palette.source" => settings.Source,
            "palette.protected" => BoolToString(settings.IsProtected),
            "palette.hiddenFromPickers" => BoolToString(settings.HiddenFromPickers),
            "palette.note" => settings.Note,
            _ => throw new InvalidOperationException($"Unknown palette field '{fieldId}'."),
        };
    }

    private string DeviceFieldValue(string deviceId, string fieldId)
    {
        var settings = _resources.GetDeviceSettings(deviceId);
        return fieldId switch
        {
            "device.manufacturer" => settings.Manufacturer,
            "device.model" => settings.Model,
            "device.osFamily" => settings.OsFamily,
            _ => _resources.GetDeviceMetricFieldValue(deviceId, fieldId),
        };
    }

    private string ActorFieldValue(string actorId, string fieldId)
    {
        return _resources.GetActorFieldValue(actorId, fieldId);
    }

    private string ThemeFieldValue(string themeId, string fieldId)
    {
        return _resources.GetThemeFieldValue(themeId, fieldId);
    }

    private string ProductionFontFieldValue(string fontId, string fieldId)
    {
        return _resources.GetProductionFontFieldValue(fontId, fieldId);
    }

    private string IconThemeFieldValue(string iconThemeId, string fieldId)
    {
        return _resources.GetIconThemeFieldValue(iconThemeId, fieldId);
    }

    private IReadOnlyList<FieldOption>? ResolveOptions(
        ProjectTreeNode node,
        RecordClassFieldDescriptor field)
    {
        if (node.Kind == ProjectTreeNodeKind.ModuleInstance
            && field.Id == "moduleInstance.durationPolicy")
        {
            return RuntimeDurationContract.AllowedPolicies(
                    _timeline.GetModuleInstanceEffectiveContractJson(node.Id))
                .Select((policy) => new FieldOption(
                    RuntimeDurationContract.FormatPolicy(policy),
                    policy == RuntimeDurationPolicy.Calculated
                        ? "Calculated"
                        : "Free"))
                .ToList();
        }
        if (field.Options is { Count: > 0 }) return field.Options;
        if (field.OptionSource == FieldOptionSource.ModuleVariants)
        {
            return _design.GetModuleVariantOptions(
                _timeline.GetModuleInstanceSettings(node.Id).ModuleId);
        }
        if (field.OptionSource == FieldOptionSource.ShotManagerWorkstreams)
        {
            var shotManagerProjectId = RequiredProjectId(node);
            var current = _production.GetProjectSettings(shotManagerProjectId)
                .ShotManagerOutput.WorkstreamName;
            var pending = _shotManagerDocuments.GetLocation(
                    shotManagerProjectId)
                .PendingWorkstreamName;
            return ShotManagerWorkstreamOptions(
                shotManagerProjectId,
                string.IsNullOrWhiteSpace(pending) ? current : pending);
        }
        if (field.OptionSource == FieldOptionSource.ShotManagerFolders)
        {
            var shotManagerProjectId = RequiredProjectId(node);
            var settings = _production.GetProjectSettings(
                    shotManagerProjectId)
                .ShotManagerOutput;
            var pending = _shotManagerDocuments.GetLocation(
                    shotManagerProjectId)
                .PendingWorkstreamName;
            return ShotManagerFolderOptions(
                shotManagerProjectId,
                string.IsNullOrWhiteSpace(pending)
                    ? settings.WorkstreamName
                    : pending,
                settings.FolderName);
        }

        var requiresProjectOptions = field.ValueKind is ValueKind.ComponentVariant
            or ValueKind.ComponentVariantSlot
            or ValueKind.PaletteColorToken
            or ValueKind.PaletteColorPair
            or ValueKind.PaletteColorAlphaPair
            || field.RecordReference is not null;
        if (!requiresProjectOptions) return null;
        var projectId = RequiredProjectId(node);
        if (field.ValueKind is ValueKind.ComponentVariant or ValueKind.ComponentVariantSlot)
        {
            if (string.IsNullOrWhiteSpace(field.ComponentVariantType))
            {
                throw new InvalidOperationException(
                    $"Component Variant field '{field.Id}' requires a declared Component type or OptionSource.");
            }
            return _design.GetComponentVariantReferenceOptionsByType(
                projectId,
                field.ComponentVariantType);
        }
        if (field.ValueKind is ValueKind.PaletteColorToken
            or ValueKind.PaletteColorPair
            or ValueKind.PaletteColorAlphaPair)
        {
            return _resources.GetPaletteColorOptions(projectId);
        }
        if (field.RecordReference is not null)
        {
            return field.RecordReference.TableId switch
            {
                "actors" => _resources.GetRequiredActorOptions(projectId),
                "devices" => _resources.GetDeviceOptions(projectId),
                "themes" => _resources.GetThemeOptions(projectId),
                "icon_themes" => _resources.GetIconThemeOptions(projectId),
                "production_fonts" => _resources.GetProductionFontOptions(
                    projectId,
                    string.IsNullOrWhiteSpace(field.RecordReference.Filter)
                        ? null
                        : field.RecordReference.Filter),
                "shot_manager_episodes" =>
                    ShotManagerEpisodeOptions(
                        projectId,
                        _production.GetEpisodeSettings(node.Id)
                            .ShotManagerEpisode.IsAssociated
                                ? _production.GetEpisodeSettings(node.Id)
                                    .ShotManagerEpisode.EpisodeId
                                : ""),
                "shot_manager_shots" =>
                    ShotManagerShotOptions(
                        projectId,
                        _production.GetShotSettings(node.Id)),
                _ => throw new InvalidOperationException(
                    $"Record field '{field.Id}' has unsupported option table '{field.RecordReference.TableId}'."),
            };
        }
        return null;
    }

    private static string RequiredProjectId(ProjectTreeNode node)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current.Kind == ProjectTreeNodeKind.Project) return current.Id;
        }
        throw new InvalidOperationException(
            $"Record field owner '{node.Id}' requires an exact Project ancestor.");
    }

    private static string DefaultValue(ProjectTreeNodeKind nodeKind, RecordClassFieldDescriptor field, string currentValue)
    {
        if (nodeKind != ProjectTreeNodeKind.Actor)
        {
            return currentValue;
        }

        return field.Id switch
        {
            "actor.avatar.filePath" => "",
            "actor.avatar.scale" => "1",
            "actor.avatar.offset" => "0|0",
            "actor.avatar.useInitials" => "false",
            "actor.avatar.initialsPadding" => "96",
            _ => currentValue,
        };
    }

    private static bool CommitAsDefault(ProjectTreeNodeKind nodeKind, RecordClassFieldDescriptor field)
    {
        if (nodeKind != ProjectTreeNodeKind.Actor)
        {
            return true;
        }

        return !field.Id.StartsWith("actor.avatar.", StringComparison.Ordinal)
            && field.ValueKind != ValueKind.PaletteColorPair;
    }

    private static string BoolToString(bool value) => BooleanText.Format(value);

    private string ShotRenderName(string shotId)
    {
        try
        {
            return _productionOutputPlans.Resolve(
                _production.GetProductionOutputShotContext(shotId))
                .Plan.TechnicalName;
        }
        catch (Exception exception)
        {
            return $"Unavailable — {exception.Message}";
        }
    }

    private IReadOnlyList<FieldOption> ShotManagerWorkstreamOptions(
        string projectId,
        string current)
    {
        var options = new List<FieldOption>
        {
            new("", "Select Workstream…"),
        };
        try
        {
            options.AddRange(_shotManagerDocuments.Open(projectId)
                .Production.Workstreams
                .OrderBy((workstream) => workstream.Name, StringComparer.Ordinal)
                .Select((workstream) => new FieldOption(
                    workstream.Name,
                    workstream.Name)));
        }
        catch (Exception exception) when (IsUnavailableShotManagerDocument(exception))
        {
        }
        return IncludeMissing(options, current, "Missing Workstream");
    }

    private IReadOnlyList<FieldOption> ShotManagerFolderOptions(
        string projectId,
        string workstreamName,
        string current)
    {
        var options = new List<FieldOption>
        {
            new("", "Select output folder…"),
        };
        try
        {
            var workstream = _shotManagerDocuments.Open(projectId)
                .Production.Workstreams.Single((candidate) =>
                    candidate.Name.Equals(
                        workstreamName,
                        StringComparison.OrdinalIgnoreCase));
            options.AddRange(workstream.Folders
                .OrderBy((folder) => folder.Name, StringComparer.Ordinal)
                .Select((folder) => new FieldOption(
                    folder.Name,
                    string.IsNullOrEmpty(folder.Suffix)
                        ? folder.Name
                        : $"{folder.Name} · {folder.Suffix}")));
        }
        catch (Exception exception) when (IsUnavailableShotManagerDocument(exception))
        {
        }
        return IncludeMissing(options, current, "Missing folder");
    }

    private IReadOnlyList<FieldOption> ShotManagerEpisodeOptions(
        string projectId,
        string current)
    {
        var options = new List<FieldOption>
        {
            new("", "Free · Manual output"),
        };
        try
        {
            options.AddRange(_shotManagerDocuments.Open(projectId)
                .Production.Episodes
                .OrderBy((episode) => episode.Order)
                .ThenBy((episode) => episode.Id, StringComparer.Ordinal)
                .Select((episode) => new FieldOption(
                    episode.Id,
                    episode.Order.ToString("D3"))));
        }
        catch (Exception exception) when (IsUnavailableShotManagerDocument(exception))
        {
        }
        return IncludeMissing(options, current, "Missing Episode");
    }

    private IReadOnlyList<FieldOption> ShotManagerShotOptions(
        string projectId,
        ShotSettings settings)
    {
        var options = new List<FieldOption>
        {
            new("", "Free · Manual output"),
        };
        try
        {
            var episodeId = _production.GetEpisodeSettings(
                    settings.EpisodeId)
                .ShotManagerEpisode;
            options.AddRange(_shotManagerDocuments.Open(projectId)
                .Production.Shots
                .Where((shot) => shot.EpisodeId.Equals(
                    episodeId.IsAssociated ? episodeId.EpisodeId : "",
                    StringComparison.Ordinal))
                .OrderBy((shot) => shot.CanonicalName, StringComparer.Ordinal)
                .Select((shot) => new FieldOption(
                    shot.Id,
                    shot.CanonicalName)));
        }
        catch (Exception exception) when (IsUnavailableShotManagerDocument(exception))
        {
        }
        return IncludeMissing(
            options,
            settings.ShotManagerShot.IsAssociated
                ? settings.ShotManagerShot.ShotId
                : "",
            "Missing Shot");
    }

    private static IReadOnlyList<FieldOption> IncludeMissing(
        List<FieldOption> options,
        string current,
        string label)
    {
        if (!string.IsNullOrWhiteSpace(current)
            && !options.Any((option) => option.Value.Equals(
                current,
                StringComparison.Ordinal)))
        {
            options.Add(new FieldOption(
                current,
                $"{label} · {current}"));
        }
        return options;
    }

    private static bool IsUnavailableShotManagerDocument(
        Exception exception) =>
        exception is InvalidOperationException
            or IOException
            or UnauthorizedAccessException;

    private static string JsonString(string json, string key)
    {
        var owner = JsonPath.ParseRequiredObject(json, "Record field JSON");
        return owner[key]?.GetValue<string>() ?? "";
    }

    private static string JsonBoolString(string json, string key, bool fallback)
    {
        var owner = JsonPath.ParseRequiredObject(json, "Record field JSON");
        return owner[key] is JsonValue value && value.TryGetValue<bool>(out var boolean)
            ? BoolToString(boolean)
            : BoolToString(fallback);
    }

}
