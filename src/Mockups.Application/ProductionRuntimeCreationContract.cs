using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

public static class ProductionRuntimeCreationContract
{
    public static RecordCreationDefinition Prepare(
        string definitionId,
        JsonObject content,
        JsonObject runtime,
        JsonObject config,
        IReadOnlyList<FieldOption> actorOptions)
    {
        var requirements = Requirements(content, runtime, config, actorOptions);
        return new RecordCreationDefinition(
            definitionId,
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ModuleInstance),
            "Complete Screen Runtime Inputs",
            "Choose the Production values required by this Screen before it is added to the Shot.",
            "Add Screen",
            requirements.Select((requirement) => requirement.Field).ToList(),
            RequiresConfirmation: requirements.Count > 0);
    }

    public static JsonObject Complete(
        string definitionId,
        JsonObject content,
        JsonObject runtime,
        JsonObject config,
        IReadOnlyList<FieldOption> actorOptions,
        RecordCreationDraft draft)
    {
        var definition = Prepare(definitionId, content, runtime, config, actorOptions);
        if (!draft.DefinitionId.Equals(definition.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Screen Runtime creation draft '{draft.DefinitionId}' does not match '{definition.Id}'.");
        }
        var error = definition.ValidationError(draft.Values);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }

        var requirements = Requirements(content, runtime, config, actorOptions);
        var expected = requirements
            .Select((requirement) => requirement.Field.Definition.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (draft.Values.Keys.Any((key) => !expected.Contains(key)))
        {
            throw new InvalidOperationException(
                "Screen Runtime creation draft contains undeclared values.");
        }

        var completed = content.DeepClone().AsObject();
        foreach (var requirement in requirements)
        {
            SetValue(
                completed,
                requirement.Path,
                JsonValue.Create(draft.Values[requirement.Field.Definition.Id]));
        }
        return completed;
    }

    private static IReadOnlyList<Requirement> Requirements(
        JsonObject content,
        JsonObject runtime,
        JsonObject config,
        IReadOnlyList<FieldOption> actorOptions)
    {
        var requirements = new List<Requirement>();
        VisitContract(
            runtime,
            config,
            [],
            actorOptions,
            requirements);
        VisitNestedContracts(
            runtime,
            [],
            actorOptions,
            requirements);

        return requirements
            .Where((requirement) => ValueAt(content, requirement.Path) is not null)
            .GroupBy((requirement) => requirement.Field.Definition.Id, StringComparer.Ordinal)
            .Select((group) => group.First())
            .ToList();
    }

    private static void VisitContract(
        JsonObject runtime,
        JsonObject config,
        IReadOnlyList<PathPart> ownerPath,
        IReadOnlyList<FieldOption> actorOptions,
        List<Requirement> requirements)
    {
        foreach (var input in RuntimeInputDefinitionReader.ReadInputs(
                     runtime,
                     config,
                     includeHidden: true))
        {
            if (input.Source != ComponentInputSource.Runtime
                || !CollectionFieldAvailability.IsEnabled(runtime, input))
            {
                continue;
            }
            AddInput(
                input,
                runtime[input.JsonKey],
                [.. ownerPath, PathPart.Property(input.JsonKey)],
                input.Label,
                actorOptions,
                requirements);
            VisitStructuredValue(
                runtime[input.JsonKey],
                input,
                [.. ownerPath, PathPart.Property(input.JsonKey)],
                input.Label,
                actorOptions,
                requirements);
        }

        foreach (var collection in RuntimeInputDefinitionReader.ReadCollections(
                     runtime,
                     config,
                     includeHidden: true))
        {
            if (runtime[collection.JsonKey] is not JsonArray items) continue;
            for (var index = 0; index < items.Count; index++)
            {
                if (items[index] is not JsonObject item) continue;
                foreach (var field in collection.Fields.Where((field) =>
                             field.Source == ComponentInputSource.Runtime
                             && CollectionFieldAvailability.IsEnabled(item, field)))
                {
                    PathPart[] fieldPath =
                    [
                        .. ownerPath,
                        PathPart.Property(collection.StorageJsonKey),
                        PathPart.Index(index),
                        PathPart.Property(field.JsonKey),
                    ];
                    AddInput(
                        field,
                        item[field.JsonKey],
                        fieldPath,
                        $"{collection.Label} {index + 1} · {field.Label}",
                        actorOptions,
                        requirements);
                    VisitStructuredValue(
                        item[field.JsonKey],
                        field,
                        fieldPath,
                        $"{collection.Label} {index + 1} · {field.Label}",
                        actorOptions,
                        requirements);
                }
            }
        }
    }

    private static void AddInput(
        ComponentInputDefinition input,
        JsonNode? value,
        IReadOnlyList<PathPart> path,
        string label,
        IReadOnlyList<FieldOption> actorOptions,
        List<Requirement> requirements)
    {
        if ((IsProductionActor(input) || IsProductionMedia(input))
            && IsSystemPreviewValue(value, input))
        {
            var options = IsProductionActor(input) ? actorOptions : input.Options;
            var definition = new FieldDefinition(
                FieldId(path),
                label,
                input.ValueKind,
                DefaultValue: "",
                Options: options,
                RecordReference: IsProductionActor(input)
                    ? new RecordReferenceDefinition("actors", AllowEmpty: false)
                    : null,
                HelpText: input.HelpText);
            requirements.Add(new Requirement(
                path,
                input,
                new FieldValue(definition, "")));
        }

    }

    private static void VisitStructuredValue(
        JsonNode? value,
        ComponentInputDefinition input,
        IReadOnlyList<PathPart> path,
        string label,
        IReadOnlyList<FieldOption> actorOptions,
        List<Requirement> requirements)
    {
        if (input.ValueKind != ValueKind.StructuredCollection
            || input.StructuredCollection is null
            || value is not JsonArray items)
        {
            return;
        }
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index] is not JsonObject item) continue;
            foreach (var field in input.StructuredCollection.Fields.Where((field) =>
                         field.Source == ComponentInputSource.Runtime
                         && CollectionFieldAvailability.IsEnabled(item, field)))
            {
                PathPart[] fieldPath =
                [
                    .. path,
                    PathPart.Index(index),
                    PathPart.Property(field.JsonKey),
                ];
                var fieldLabel = $"{label} {index + 1} · {field.Label}";
                AddInput(field, item[field.JsonKey], fieldPath, fieldLabel, actorOptions, requirements);
                VisitStructuredValue(
                    item[field.JsonKey],
                    field,
                    fieldPath,
                    fieldLabel,
                    actorOptions,
                    requirements);
            }
        }
    }

    private static void VisitNestedContracts(
        JsonNode? node,
        IReadOnlyList<PathPart> path,
        IReadOnlyList<FieldOption> actorOptions,
        List<Requirement> requirements)
    {
        if (node is JsonArray array)
        {
            for (var index = 0; index < array.Count; index++)
            {
                VisitNestedContracts(
                    array[index],
                    [.. path, PathPart.Index(index)],
                    actorOptions,
                    requirements);
            }
            return;
        }
        if (node is not JsonObject value) return;

        if (value["inputs"] is JsonArray || value["collections"] is JsonArray)
        {
            VisitContract(value, new JsonObject(), path, actorOptions, requirements);
        }
        foreach (var (key, child) in value)
        {
            VisitNestedContracts(
                child,
                [.. path, PathPart.Property(key)],
                actorOptions,
                requirements);
        }
    }

    private static bool IsProductionActor(ComponentInputDefinition input) =>
        input.ValueKind == ValueKind.RecordReference
        && input.TableId.Equals("actors", StringComparison.Ordinal);

    private static bool IsProductionMedia(ComponentInputDefinition input) =>
        input.ValueKind is ValueKind.ImageFilePath
            or ValueKind.MediaFilePath
            or ValueKind.MediaDirectoryPath;

    private static bool IsSystemPreviewValue(
        JsonNode? value,
        ComponentInputDefinition input) =>
        value is JsonValue scalar
        && scalar.TryGetValue<string>(out var text)
        && (IsProductionActor(input)
            ? SystemPreviewFixtureCatalog.IsActor(text)
            : IsProductionMedia(input)
              && text.StartsWith(SystemPreviewFixtureCatalog.MediaScheme, StringComparison.Ordinal));

    private static JsonNode? ValueAt(
        JsonObject root,
        IReadOnlyList<PathPart> path)
    {
        JsonNode? current = root;
        foreach (var part in path)
        {
            current = part.Name is not null
                ? current?[part.Name]
                : current is JsonArray array && part.ArrayIndex is { } index
                    && index >= 0 && index < array.Count
                    ? array[index]
                    : null;
        }
        return current;
    }

    private static void SetValue(
        JsonObject root,
        IReadOnlyList<PathPart> path,
        JsonNode? value)
    {
        JsonNode current = root;
        for (var index = 0; index < path.Count - 1; index++)
        {
            var part = path[index];
            current = part.Name is not null
                ? current[part.Name]
                    ?? throw new InvalidOperationException($"Screen Runtime path '{FieldId(path)}' is missing '{part.Name}'.")
                : current is JsonArray array && part.ArrayIndex is { } arrayIndex
                    ? array[arrayIndex]
                        ?? throw new InvalidOperationException($"Screen Runtime path '{FieldId(path)}' is missing item {arrayIndex}.")
                    : throw new InvalidOperationException($"Screen Runtime path '{FieldId(path)}' is invalid.");
        }
        var leaf = path[^1];
        if (leaf.Name is not null && current is JsonObject owner)
        {
            owner[leaf.Name] = value;
            return;
        }
        if (leaf.ArrayIndex is { } leafIndex && current is JsonArray ownerArray)
        {
            ownerArray[leafIndex] = value;
            return;
        }
        throw new InvalidOperationException($"Screen Runtime path '{FieldId(path)}' has an invalid owner.");
    }

    private static string FieldId(IReadOnlyList<PathPart> path) =>
        "screen.runtime/" + string.Join(
            "/",
            path.Select((part) => part.Name is not null
                ? part.Name.Replace("~", "~0", StringComparison.Ordinal)
                    .Replace("/", "~1", StringComparison.Ordinal)
                : part.ArrayIndex!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private sealed record Requirement(
        IReadOnlyList<PathPart> Path,
        ComponentInputDefinition Input,
        FieldValue Field);

    private sealed record PathPart(string? Name, int? ArrayIndex)
    {
        public static PathPart Property(string name) => new(name, null);
        public static PathPart Index(int index) => new(null, index);
    }
}
