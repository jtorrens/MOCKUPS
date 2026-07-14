using Microsoft.Data.Sqlite;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static void EnsureKeypadComponentClasses(SqliteConnection connection)
    {
        var seed = ComponentSeedRows.Single((candidate) => candidate.ComponentType == "keypad");
        foreach (var projectId in QueryProjectRows(connection).Select((project) => project.Id))
        {
            if (ScalarLong(
                    connection,
                    "SELECT COUNT(*) FROM component_classes WHERE project_id = $projectId AND component_type = 'keypad'",
                    ("$projectId", projectId)) > 0)
            {
                continue;
            }
            var config = ParseJsonObject(seed.ConfigJson);
            var keypad = config["keypad"] as JsonObject
                ?? throw new System.InvalidOperationException("Missing seeded Keypad config.");
            foreach (var slotKey in new[] { "normalKeySlot", "activeKeySlot", "disabledKeySlot" })
            {
                var slot = keypad[slotKey] as JsonObject
                    ?? throw new System.InvalidOperationException($"Missing seeded Keypad slot {slotKey}.");
                slot["presetId"] = SeededComponentPresetReference(projectId, "label");
            }
            var metadata = ParseJsonObject(seed.MetadataJson);
            SetDefaultComponentPresetConfig(metadata, config);
            Execute(
                connection,
                """
                INSERT INTO component_classes (id, project_id, component_type, record_class_id, name, notes, config_json, design_preview_json, metadata_json)
                VALUES ($id, $projectId, $componentType, $recordClassId, $name, $notes, $configJson, $designPreviewJson, $metadataJson)
                """,
                ("$id", $"component_{projectId}_{seed.ComponentType}"),
                ("$projectId", projectId),
                ("$componentType", seed.ComponentType),
                ("$recordClassId", seed.RecordClassId),
                ("$name", seed.Name),
                ("$notes", ComponentTypeLabel(seed.ComponentType)),
                ("$configJson", config.ToJsonString()),
                ("$designPreviewJson", seed.DesignPreviewJson),
                ("$metadataJson", metadata.ToJsonString()));
        }

        Execute(
            connection,
            "INSERT OR REPLACE INTO editor_layouts (record_class_id, layout_json) VALUES ('component.keypad', $layoutJson)",
            ("$layoutJson", KeypadEditorLayoutJson()));
    }

    private static string KeypadEditorLayoutJson() =>
        """
        {
          "cards": [
            {
              "id": "general",
              "label": "General",
              "subtitle": "Identity",
              "icon": "general",
              "order": 10,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "identity",
                  "label": "Identity",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "component.type", "order": 20, "visible": true },
                    { "id": "core.notes", "order": 30, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "keypad",
              "label": "Keypad",
              "subtitle": "Grid layout, keys and visual states",
              "icon": "keypad",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groupLayout": "verticalCards",
              "groups": [
                {
                  "id": "layout",
                  "label": "Layout",
                  "icon": "layout",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "component.keypad.sizingMode", "order": 10, "visible": true },
                    { "id": "component.keypad.columns", "order": 20, "visible": true },
                    { "id": "component.keypad.keySize", "order": 30, "visible": true },
                    { "id": "component.keypad.padding", "order": 40, "visible": true },
                    { "id": "component.keypad.columnGapToken", "order": 50, "visible": true },
                    { "id": "component.keypad.rowGapToken", "order": 60, "visible": true }
                  ]
                },
                {
                  "id": "keys",
                  "label": "Keys",
                  "icon": "keypad",
                  "order": 20,
                  "visible": true,
                  "fields": [
                    { "id": "component.keypad.keys", "order": 10, "visible": true }
                  ]
                },
                {
                  "id": "states",
                  "label": "States",
                  "icon": "variants",
                  "order": 30,
                  "visible": true,
                  "fields": [
                    { "id": "component.keypad.normalKey.editor", "order": 10, "visible": true },
                    { "id": "component.keypad.activeKey.editor", "order": 20, "visible": true },
                    { "id": "component.keypad.disabledKey.editor", "order": 30, "visible": true }
                  ]
                }
              ]
            }
          ]
        }
        """;
}
