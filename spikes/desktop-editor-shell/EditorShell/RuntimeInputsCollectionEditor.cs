using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RuntimeInputsCollectionEditor
{
    private readonly SpikeDatabase _database;
    private readonly EditorDictionaryFieldServices _dictionaryServices;
    private readonly Action _onChanged;
    private readonly Action<string> _triggerAction;

    public RuntimeInputsCollectionEditor(
        SpikeDatabase database,
        EditorDictionaryFieldServices dictionaryServices,
        Action onChanged,
        Action<string> triggerAction)
    {
        _database = database;
        _dictionaryServices = dictionaryServices;
        _onChanged = onChanged;
        _triggerAction = triggerAction;
    }

    public InstantEditorCard Create(ProjectTreeNode node)
    {
        var owner = ResolveOwner(node);
        var preview = DesignPreviewTestValues.Parse(owner.DesignPreviewJson);
        var config = DesignPreviewTestValues.Parse(owner.ConfigJson);
        var inputs = ComponentInputsPanel.ReadRuntimeInputs(preview, config);
        var actions = ComponentPreviewActions.Read(preview);
        var tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Runtime API", Content = CreateApiTab(inputs) },
                new TabItem { Header = "Test Values", Content = CreateTestValuesTab(owner, preview, inputs, actions) },
            },
        };

        return new InstantEditorCard(
            EditorCardHeader.Create("Runtime Inputs", $"{inputs.Count} public input(s)", EditorIcons.Create(EditorIcons.Design, 18)),
            new Border { Padding = new Thickness(10), Child = tabs },
            isExpanded: false)
        { HorizontalAlignment = HorizontalAlignment.Stretch };
    }

    private Control CreateApiTab(IReadOnlyList<ComponentInputDefinition> inputs)
    {
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
        if (inputs.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "This definition exposes no runtime inputs.", Opacity = 0.68 });
            return panel;
        }

        foreach (var input in ComponentInputGrouping.OwnInputs(inputs))
        {
            panel.Children.Add(CreateApiInputRow(input));
        }

        var groups = ComponentInputGrouping.EmbeddedGroups(inputs);
        foreach (var groupId in ComponentInputGrouping.TopLevelGroupIds(groups))
        {
            panel.Children.Add(CreateApiGroupCard(groupId, groups));
        }

        return panel;
    }

    private Control CreateTestValuesTab(
        RuntimeInputOwner owner,
        JsonObject preview,
        IReadOnlyList<ComponentInputDefinition> inputs,
        IReadOnlyList<ComponentPreviewActionDefinition> actions)
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        if (actions.Count > 0)
        {
            var header = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
            };
            header.Children.Add(new TextBlock
            {
                Text = "Test Values",
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            });
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            foreach (var action in actions)
            {
                var button = new Button
                {
                    MinWidth = 92,
                    Content = CreateActionContent(action),
                };
                ToolTip.SetTip(button, action.Label);
                button.Click += (_, args) =>
                {
                    args.Handled = true;
                    _triggerAction(action.Id);
                };
                buttons.Children.Add(button);
            }
            Grid.SetColumn(buttons, 1);
            header.Children.Add(buttons);
            panel.Children.Add(header);
        }
        if (inputs.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "No test values are required.", Opacity = 0.68 });
            return panel;
        }

        foreach (var input in ComponentInputGrouping.OwnInputs(inputs))
        {
            panel.Children.Add(CreateTestValueControl(owner, preview, input));
        }

        var groups = ComponentInputGrouping.EmbeddedGroups(inputs);
        foreach (var groupId in ComponentInputGrouping.TopLevelGroupIds(groups))
        {
            panel.Children.Add(CreateTestValueGroupCard(owner, preview, groupId, groups));
        }

        return panel;
    }

    private Control CreateApiInputRow(ComponentInputDefinition input)
    {
        return new Border
        {
            Padding = new Thickness(8, 6),
            Child = new StackPanel
            {
                Spacing = 1,
                Children =
                {
                    new TextBlock { Text = input.Id, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                    new TextBlock { Text = $"Runtime · {input.ValueKind} · {input.JsonKey}", FontSize = 11, Opacity = 0.7 },
                },
            },
        };
    }

    private Control CreateApiGroupCard(
        string groupId,
        IReadOnlyDictionary<string, List<ComponentInputDefinition>> groups)
    {
        var groupInputs = groups[groupId];
        var content = new StackPanel { Spacing = 6 };
        foreach (var input in groupInputs)
        {
            content.Children.Add(CreateApiInputRow(input));
        }
        foreach (var childId in ComponentInputGrouping.ChildGroupIds(groupId, groups))
        {
            content.Children.Add(CreateApiGroupCard(childId, groups));
        }
        return CreateEmbeddedCard(ComponentInputGrouping.GroupLabel(groupInputs), content);
    }

    private Control CreateTestValueControl(
        RuntimeInputOwner owner,
        JsonObject preview,
        ComponentInputDefinition input)
    {
        var value = DesignPreviewTestValues.Value(preview, input);
        var control = new DictionaryFieldControl(
            new FieldValue(CreateDefinition(owner.Node, input), value),
            _dictionaryServices.ForNode(owner.Node, (_) => ""));
        control.ValueCommitted += (_, next) =>
        {
            DesignPreviewTestValues.SetValue(preview, input, next);
            owner.Save(preview.ToJsonString());
            _onChanged();
        };
        return control;
    }

    private Control CreateTestValueGroupCard(
        RuntimeInputOwner owner,
        JsonObject preview,
        string groupId,
        IReadOnlyDictionary<string, List<ComponentInputDefinition>> groups)
    {
        var groupInputs = groups[groupId];
        var content = new StackPanel { Spacing = 8 };
        foreach (var input in groupInputs)
        {
            content.Children.Add(CreateTestValueControl(owner, preview, input));
        }
        foreach (var childId in ComponentInputGrouping.ChildGroupIds(groupId, groups))
        {
            content.Children.Add(CreateTestValueGroupCard(owner, preview, childId, groups));
        }
        return CreateEmbeddedCard(ComponentInputGrouping.GroupLabel(groupInputs), content);
    }

    private static Control CreateEmbeddedCard(string label, Control content)
    {
        return new InstantEditorCard(
            EditorCardHeader.Create(label, "Embedded component inputs", EditorIcons.Create(EditorIcons.Component, 16)),
            new Border { Padding = new Thickness(10), Child = content },
            isExpanded: false)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
    }

    private static Control CreateActionContent(ComponentPreviewActionDefinition action)
    {
        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
        };
        content.Children.Add(EditorIcons.Create(EditorIcons.Play, 12));
        var label = new TextBlock
        {
            Text = action.Label,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 1);
        content.Children.Add(label);
        return content;
    }

    private FieldDefinition CreateDefinition(ProjectTreeNode node, ComponentInputDefinition input)
    {
        var projectId = ProjectAncestor(node).Id;
        var options = input.ValueKind switch
        {
            ValueKind.RecordReference when input.TableId == "actors" => _database.GetActorOptions(projectId),
            ValueKind.ComponentPreset when !string.IsNullOrWhiteSpace(input.ComponentType) =>
                _database.GetComponentPresetReferenceOptionsByType(projectId, input.ComponentType),
            ValueKind.PaletteColorToken => _database.GetPaletteColorOptions(projectId),
            _ => input.Options,
        };
        return new FieldDefinition(
            input.Id,
            input.Label,
            input.ValueKind,
            DefaultValue: input.DefaultValue,
            Options: options,
            PairLabels: input.ValueKind == ValueKind.IntegerPair ? input.PairLabels : null,
            Number: input.ValueKind is ValueKind.Decimal or ValueKind.Integer or ValueKind.Alpha
                ? new NumberDefinition(input.Minimum, input.Maximum, input.Increment, input.ValueKind == ValueKind.Integer ? 0 : 2)
                : null,
            RecordReference: input.ValueKind == ValueKind.RecordReference
                ? new RecordReferenceDefinition(input.TableId)
                : null);
    }

    private RuntimeInputOwner ResolveOwner(ProjectTreeNode node)
    {
        if (node.Kind == ProjectTreeNodeKind.Module)
        {
            var settings = _database.GetModuleSettings(node.Id);
            return new RuntimeInputOwner(node, settings.ConfigJson, settings.DesignPreviewJson,
                (json) => _database.UpdateModuleDesignPreviewJson(node.Id, json));
        }

        if (node.Kind == ProjectTreeNodeKind.ComponentPreset && node.Parent is not null)
        {
            var settings = _database.GetComponentPresetSettings(node);
            return new RuntimeInputOwner(node, settings.ConfigJson, settings.DesignPreviewJson,
                (json) => _database.UpdateComponentClassDesignPreviewJson(node.Parent.Id, json));
        }

        throw new InvalidOperationException($"Runtime inputs are not supported by '{node.Kind}'.");
    }

    private static ProjectTreeNode ProjectAncestor(ProjectTreeNode node)
    {
        var current = node;
        while (current.Kind != ProjectTreeNodeKind.Project)
        {
            current = current.Parent ?? throw new InvalidOperationException($"{node.Kind} has no project ancestor.");
        }

        return current;
    }

    private sealed record RuntimeInputOwner(
        ProjectTreeNode Node,
        string ConfigJson,
        string DesignPreviewJson,
        Action<string> Save);
}
