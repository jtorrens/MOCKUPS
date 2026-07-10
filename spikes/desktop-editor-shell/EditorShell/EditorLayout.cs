using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorLayout
{
    [JsonPropertyName("cards")]
    public List<EditorLayoutCard> Cards { get; init; } = [];
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
