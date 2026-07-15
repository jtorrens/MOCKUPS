using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorLayout
{
    [JsonPropertyName("cards")]
    public List<EditorLayoutCard> Cards { get; init; } = [];

    [JsonPropertyName("simplified")]
    public EditorSimplifiedLayout? Simplified { get; set; }
}

internal sealed class EditorSimplifiedLayout
{
    [JsonPropertyName("groups")]
    public List<EditorSimplifiedGroup> Groups { get; init; } = [];

    [JsonPropertyName("capturedSlots")]
    public List<EditorSimplifiedCapture> CapturedSlots { get; init; } = [];

    public IEnumerable<EditorSimplifiedEntry> Entries =>
        Groups.SelectMany((group) => group.AllEntries());
}

internal sealed class EditorSimplifiedCapture
{
    [JsonPropertyName("slotFieldId")]
    public string SlotFieldId { get; init; } = "";

    [JsonPropertyName("recordClassId")]
    public string RecordClassId { get; init; } = "";
}

internal sealed class EditorSimplifiedGroup
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("label")]
    public string Label { get; init; } = "";

    [JsonPropertyName("icon")]
    public string Icon { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("entries")]
    public List<EditorSimplifiedEntry> Entries { get; init; } = [];

    [JsonPropertyName("groups")]
    public List<EditorSimplifiedGroup> Groups { get; init; } = [];

    public IEnumerable<EditorSimplifiedEntry> AllEntries() =>
        Entries.Concat(Groups.SelectMany((group) => group.AllEntries()));
}

internal sealed class EditorSimplifiedEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "field";

    [JsonPropertyName("fieldId")]
    public string FieldId { get; init; } = "";

    [JsonPropertyName("slotFieldIds")]
    public List<string> SlotFieldIds { get; init; } = [];

    [JsonPropertyName("collectionFieldId")]
    public string CollectionFieldId { get; init; } = "";

    [JsonPropertyName("itemId")]
    public string ItemId { get; init; } = "";

    [JsonPropertyName("itemFieldId")]
    public string ItemFieldId { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("captured")]
    public bool Captured { get; init; }
}

internal sealed class EditorLayoutCard
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("label")]
    public string Label { get; init; } = "";

    [JsonPropertyName("subtitle")]
    public string Subtitle { get; init; } = "";

    [JsonPropertyName("icon")]
    public string Icon { get; init; } = "•";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("visible")]
    public bool Visible { get; init; } = true;

    [JsonPropertyName("defaultOpen")]
    public bool DefaultOpen { get; init; }

    [JsonPropertyName("groupLayout")]
    public string GroupLayout { get; init; } = "stacked";

    [JsonPropertyName("groups")]
    public List<EditorLayoutGroup> Groups { get; init; } = [];

    public IEnumerable<EditorLayoutGroup> VisibleGroups =>
        Groups.Where((group) => group.Visible).OrderBy((group) => group.Order).ThenBy((group) => group.Label);
}

internal sealed class EditorLayoutGroup
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("label")]
    public string Label { get; init; } = "";

    [JsonPropertyName("icon")]
    public string Icon { get; init; } = "";

    [JsonPropertyName("presentation")]
    public string Presentation { get; init; } = "";

    [JsonPropertyName("pairLayout")]
    public string PairLayout { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("visible")]
    public bool Visible { get; init; } = true;

    [JsonPropertyName("fields")]
    public List<EditorLayoutField> Fields { get; init; } = [];

    [JsonPropertyName("collapsible")]
    public bool Collapsible { get; init; }

    [JsonPropertyName("exclusive")]
    public bool Exclusive { get; init; }

    [JsonPropertyName("defaultOpen")]
    public bool DefaultOpen { get; init; }

    public IEnumerable<EditorLayoutField> VisibleFields =>
        Fields.Where((layoutField) => layoutField.Visible)
            .OrderBy((layoutField) => layoutField.Order)
            .ThenBy((layoutField) => layoutField.Id);
}

internal sealed class EditorLayoutField
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("visible")]
    public bool Visible { get; init; } = true;
}
