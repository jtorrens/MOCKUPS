using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    public ModuleSettings GetModuleSettings(string moduleId)
    {
        using var connection = OpenConnection();
        var record = _appModuleRepository.GetModule(
            connection,
            moduleId);
        var config = ParseJsonObject(record.ConfigJson);
        ApplyComponentInputBindingsProjections(
            connection,
            record.ProjectId,
            config,
            ComponentInputBindingsProjectionCatalog.RecordOwners());

        return new ModuleSettings(
            record.ProjectId,
            record.RecordClassId,
            record.SortOrder,
            config.ToJsonString(),
            record.DesignPreviewJson,
            record.MetadataJson);
    }

    public string GetModuleName(string moduleId) =>
        _appModuleRepository.GetModule(moduleId).Name;

    public IReadOnlyDictionary<string, string> GetModuleNames(
        IReadOnlyCollection<string> moduleIds)
    {
        var requested = moduleIds.ToHashSet(StringComparer.Ordinal);
        using var connection = OpenConnection();
        return _appModuleRepository.QueryModules(connection)
            .Where((module) => requested.Contains(module.Id))
            .ToDictionary(
                (module) => module.Id,
                (module) => module.Name,
                StringComparer.Ordinal);
    }

    public void UpdateModuleDesignPreviewJson(
        string moduleId,
        string designPreviewJson) =>
        _appModuleRepository.UpdateModuleDesignPreview(
            moduleId,
            designPreviewJson);

    public AppSettings GetModuleAppSettings(string moduleId)
    {
        var record = _appModuleRepository.GetModuleApp(moduleId);

        return new AppSettings(
            record.ProjectId,
            record.BundleKey,
            record.AppType,
            record.ConfigJson,
            record.MetadataJson);
    }

    public string GetModuleConfigFieldValue(
        string moduleId,
        string fieldId)
    {
        var settings = GetModuleSettings(moduleId);
        return ModuleConfigFieldValue(
            settings.RecordClassId,
            settings.ConfigJson,
            fieldId);
    }

    internal static string ModuleConfigFieldValue(
        string recordClassId,
        string configJson,
        string fieldId)
    {
        var config = ParseJsonObject(configJson);
        if (GeneratedModuleScaffoldConfigRegistry.TryGetField(
                recordClassId,
                fieldId,
                out var generated))
        {
            var node = JsonPath.Get(config, generated.JsonPath)
                ?? throw new InvalidOperationException(
                    $"Module config field '{fieldId}' is missing '{string.Join(".", generated.JsonPath)}'.");
            RuntimeInputValueKindContract.ValidateValue(
                generated.ValueKind,
                node,
                $"Module config field '{fieldId}'");
            return generated.ValueKind switch
            {
                ValueKind.Boolean =>
                    BooleanText.Format(node.GetValue<bool>()),
                ValueKind.Integer
                    or ValueKind.Decimal
                    or ValueKind.HueDegrees
                    or ValueKind.Alpha =>
                        node.ToJsonString(),
                ValueKind.TypographyStyle
                    or ValueKind.TypographySystemStyle =>
                        TypographyStyleValue.Parse(node).ToJsonString(),
                ValueKind.AlignmentPlacement
                    or ValueKind.Motion
                    or ValueKind.MotionTiming
                    or ValueKind.IconTokenList
                    or ValueKind.IconSlots
                    or ValueKind.ComponentInputBindings
                    or ValueKind.ComponentVariantSlot
                    or ValueKind.StructuredCollection
                    or ValueKind.BehaviorTiming =>
                        node.ToJsonString(),
                _ => node.GetValue<string>(),
            };
        }

        return fieldId switch
        {
            "module.appearanceMode" =>
                ModuleAppearanceModeContract.Read(
                    config,
                    "Module Variant config"),
            _ => throw new InvalidOperationException(
                $"Unknown module config field '{fieldId}'."),
        };
    }

    public void UpdateModuleField(
        string moduleId,
        string fieldId,
        string value)
    {
        using var connection = OpenConnection();
        var module = _appModuleRepository.GetModule(
            connection,
            moduleId);
        if (fieldId == "module.appearanceMode"
            || GeneratedModuleScaffoldConfigRegistry.TryGetField(
                module.RecordClassId,
                fieldId,
                out _))
        {
            UpdateModuleConfigField(
                connection,
                moduleId,
                fieldId,
                value);
            return;
        }

        switch (fieldId)
        {
            case "module.sortOrder":
                _appModuleRepository.UpdateModuleSortOrder(
                    connection,
                    moduleId,
                    NumericText.Int32(value, 0));
                return;
            case "module.metadata":
                var metadata = ParseJsonObject(value);
                VariantEnvelopeContract.RequiredArray(
                    metadata,
                    "variants",
                    $"Module '{moduleId}'");
                _appModuleRepository.UpdateModuleMetadata(
                    connection,
                    moduleId,
                    value);
                return;
            case "module.recordClassId":
                return;
            default:
                throw new InvalidOperationException(
                    $"Unknown module field '{fieldId}'.");
        }
    }

    private void UpdateModuleConfigField(
        SqliteConnection connection,
        string moduleId,
        string fieldId,
        string value)
    {
        var module = _appModuleRepository.GetModule(
            connection,
            moduleId);
        var config = ParseJsonObject(module.ConfigJson);

        UpdateModuleConfigFieldValue(
            connection,
            module.ProjectId,
            module.RecordClassId,
            config,
            fieldId,
            value);
        _appModuleRepository.UpdateModuleConfig(
            connection,
            moduleId,
            config.ToJsonString());
    }

    internal void UpdateModuleConfigFieldValue(
        SqliteConnection connection,
        string projectId,
        string recordClassId,
        JsonObject config,
        string fieldId,
        string value)
    {
        if (GeneratedModuleScaffoldConfigRegistry.TryGetField(
                recordClassId,
                fieldId,
                out var generated))
        {
            var next = RuntimeInputValueKindContract.ParseValue(
                generated.ValueKind,
                value,
                $"Module field '{fieldId}' value");
            if (!string.IsNullOrWhiteSpace(
                    generated.ComponentVariantType))
            {
                if (generated.ValueKind == ValueKind.ComponentVariant)
                {
                    next = JsonValue.Create(
                        ValidateComponentVariantReference(
                            connection,
                            projectId,
                            generated.ComponentVariantType,
                            next.GetValue<string>()))!;
                }
                else if (
                    generated.ValueKind
                        == ValueKind.ComponentVariantSlot)
                {
                    var slot = next.AsObject();
                    var owner = $"Module field '{fieldId}'";
                    var reference =
                        ValidateComponentVariantReference(
                            connection,
                            projectId,
                            generated.ComponentVariantType,
                            ComponentVariantSlotDocumentContract
                                .VariantReference(slot, owner));
                    slot["variantReference"] = reference;
                    foreach (var path in generated
                        .SynchronizedVariantReferenceJsonPaths)
                    {
                        JsonPath.Set(
                            config,
                            path,
                            JsonValue.Create(reference)!);
                    }
                }
            }
            JsonPath.Set(config, generated.JsonPath, next);
            ApplyComponentInputBindingsProjections(
                connection,
                projectId,
                config,
                ComponentInputBindingsProjectionCatalog.RecordOwners());
            CurrentModuleConfigContract.Validate(
                recordClassId,
                config,
                "Edited Module config");
            return;
        }

        if (!fieldId.Equals("module.appearanceMode", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unknown module config field '{fieldId}'.");
        }

        SetJsonValue(
            config,
            ["appearanceMode"],
            JsonValue.Create(
                ModuleAppearanceModeContract.Require(
                    value,
                    "Module Variant config"))!);

        ApplyComponentInputBindingsProjections(
            connection,
            projectId,
            config,
            ComponentInputBindingsProjectionCatalog.RecordOwners());
        CurrentModuleConfigContract.Validate(
            recordClassId,
            config,
            "Edited Module config");
    }
}
