namespace Mockups.DesktopEditorShell.EditorShell;

public sealed record ComponentInputBindingsProjectionDefinition(
    string Id,
    IReadOnlyList<string> InputsPath,
    IReadOnlyList<string> SlotPath,
    IReadOnlySet<string> CalculatedInputIds);

public static class ComponentInputBindingsProjectionCatalog
{
    public static IReadOnlyList<ComponentInputBindingsProjectionDefinition>
        ComponentOwners()
    {
        return ComponentClassFieldCatalog.All()
            .Select((descriptor) => Create(
                descriptor.Id,
                descriptor.ValueKind,
                descriptor.JsonPath,
                descriptor.RuntimeInputComponentVariantFieldId,
                descriptor.ComponentInputBindings))
            .Where((definition) => definition is not null)
            .Cast<ComponentInputBindingsProjectionDefinition>()
            .ToList();
    }

    public static IReadOnlyList<ComponentInputBindingsProjectionDefinition>
        RecordOwners()
    {
        return RecordClassFieldCatalog.All
            .Select((descriptor) => Create(
                descriptor.Id,
                descriptor.ValueKind,
                descriptor.ConfigJsonPath ?? [],
                descriptor.RuntimeInputComponentVariantFieldId,
                descriptor.ComponentInputBindings))
            .Where((definition) => definition is not null)
            .Cast<ComponentInputBindingsProjectionDefinition>()
            .ToList();
    }

    private static ComponentInputBindingsProjectionDefinition? Create(
        string id,
        ValueKind valueKind,
        IReadOnlyList<string> inputsPath,
        string variantFieldId,
        IReadOnlyList<ComponentInputBindingDefinition>? bindings)
    {
        if (valueKind != ValueKind.ComponentInputBindings
            || inputsPath.Count == 0
            || string.IsNullOrWhiteSpace(variantFieldId))
        {
            return null;
        }
        return new ComponentInputBindingsProjectionDefinition(
            id,
            inputsPath,
            EmbeddedComponentSlotCatalog.Get(variantFieldId).SlotPath,
            (bindings ?? [])
                .Where((binding) =>
                    binding.Source == ComponentInputBindingSource.Calculated)
                .Select((binding) => binding.Id)
                .ToHashSet(StringComparer.Ordinal));
    }
}
