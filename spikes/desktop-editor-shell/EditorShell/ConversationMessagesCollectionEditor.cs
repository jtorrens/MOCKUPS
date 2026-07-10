using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ConversationMessagesCollectionEditor
{
    private readonly SpikeDatabase _database;
    private readonly Action _onChanged;

    public ConversationMessagesCollectionEditor(SpikeDatabase database, Action onChanged)
    {
        _database = database;
        _onChanged = onChanged;
    }

    public InstantEditorCard Create(ProjectTreeNode node)
    {
        var messages = _database.GetConversationMessages(node.Id);
        var body = new StackPanel { Spacing = 10 };
        for (var index = 0; index < messages.Count; index++)
        {
            body.Children.Add(CreateMessage(node, index, messages[index]));
        }

        var add = new Button { Content = "Add message", HorizontalAlignment = HorizontalAlignment.Left };
        add.Click += (_, _) =>
        {
            _database.AddConversationMessage(node.Id);
            _onChanged();
        };
        body.Children.Add(add);

        return new InstantEditorCard(
            EditorCardHeader.Create("Messages", $"{messages.Count} ordered message(s)", EditorIcons.Create(EditorIcons.Bubble, 18)),
            new Border { Padding = new Thickness(10), Child = body },
            isExpanded: true)
        { HorizontalAlignment = HorizontalAlignment.Stretch };
    }

    private Control CreateMessage(ProjectTreeNode node, int index, SpikeDatabase.ConversationMessage message)
    {
        var panel = new StackPanel { Spacing = 8 };
        var delete = new Button { Content = EditorIcons.Create(EditorIcons.Delete, 16), HorizontalAlignment = HorizontalAlignment.Right };
        delete.Click += (_, _) => { _database.DeleteConversationMessage(node.Id, message.Id); _onChanged(); };
        panel.Children.Add(new DockPanel
        {
            Children =
            {
                delete,
                new TextBlock { Text = $"Message {index + 1}", FontWeight = Avalonia.Media.FontWeight.SemiBold },
            },
        });
        panel.Children.Add(Field("Direction", ValueKind.OptionToken, message.Direction, [new("incoming", "Incoming"), new("outgoing", "Outgoing"), new("system", "System")], (value) => message = message with { Direction = value }));
        panel.Children.Add(Field("Text", ValueKind.StringMultiline, message.Text, null, (value) => message = message with { Text = value }));
        panel.Children.Add(Field("Delay frames", ValueKind.Integer, message.DelayAfterPreviousFrames.ToString(), null, (value) => message = message with { DelayAfterPreviousFrames = NumericText.Int32(value, 0) }));
        panel.Children.Add(Field("Write-on frames", ValueKind.Integer, message.WriteOnDurationFrames.ToString(), null, (value) => message = message with { WriteOnDurationFrames = NumericText.Int32(value, 0) }));
        return new Border { Padding = new Thickness(10), BorderThickness = new Thickness(1), Child = panel };

        DictionaryFieldControl Field(string label, ValueKind kind, string value, FieldOption[]? options, Action<string> update)
        {
            var control = new DictionaryFieldControl(new FieldValue(new FieldDefinition($"conversation.messages.{message.Id}.{label}", label, kind, DefaultValue: value, Options: options), value), new DictionaryFieldServices());
            control.ValueCommitted += (_, next) => { update(next); _database.UpdateConversationMessage(node.Id, message.Id, message); _onChanged(); };
            return control;
        }
    }
}
