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
    private readonly Action<ProjectTreeNode> _reloadAndSelect;

    public ConversationMessagesCollectionEditor(
        SpikeDatabase database,
        Action onChanged,
        Action<ProjectTreeNode> reloadAndSelect)
    {
        _database = database;
        _onChanged = onChanged;
        _reloadAndSelect = reloadAndSelect;
    }

    public InstantEditorCard Create(ProjectTreeNode node)
    {
        var messages = _database.GetConversationMessages(node.Id);
        var body = new StackPanel { Spacing = 10 };
        var messageCards = new System.Collections.Generic.List<InstantEditorCard>();
        for (var index = 0; index < messages.Count; index++)
        {
            var messageCard = CreateMessage(node, index, messages[index]);
            messageCards.Add(messageCard);
            body.Children.Add(messageCard);
        }
        EditorGroupBlock.WireExclusiveCards(messageCards);

        var add = new Button { Content = "Add message", HorizontalAlignment = HorizontalAlignment.Left };
        add.Click += (_, _) =>
        {
            _database.AddConversationMessage(node.Id);
            _onChanged();
            _reloadAndSelect(node);
        };
        body.Children.Add(add);

        return new InstantEditorCard(
            EditorCardHeader.Create("Messages", $"{messages.Count} ordered message(s)", EditorIcons.Create(EditorIcons.Bubble, 18)),
            new Border { Padding = new Thickness(10), Child = body },
            isExpanded: true)
        { HorizontalAlignment = HorizontalAlignment.Stretch };
    }

    private InstantEditorCard CreateMessage(ProjectTreeNode node, int index, SpikeDatabase.ConversationMessage message)
    {
        var panel = new StackPanel { Spacing = 8 };
        var delete = new Button
        {
            Content = EditorIcons.Create(EditorIcons.Delete, 16),
            Width = 30,
            Height = 28,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        ToolTip.SetTip(delete, "Delete message");
        delete.Click += (_, _) => { _database.DeleteConversationMessage(node.Id, message.Id); _onChanged(); _reloadAndSelect(node); };
        panel.Children.Add(Field("Direction", ValueKind.OptionToken, message.Direction, [new("incoming", "Incoming"), new("outgoing", "Outgoing"), new("system", "System")], (value) => message = message with { Direction = value }));
        panel.Children.Add(Field("Text", ValueKind.StringMultiline, message.Text, null, (value) => message = message with { Text = value }));
        panel.Children.Add(Field("Delay frames", ValueKind.Integer, message.DelayAfterPreviousFrames.ToString(), null, (value) => message = message with { DelayAfterPreviousFrames = NumericText.Int32(value, 0) }));
        panel.Children.Add(Field("Write-on frames", ValueKind.Integer, message.WriteOnDurationFrames.ToString(), null, (value) => message = message with { WriteOnDurationFrames = NumericText.Int32(value, 0) }));
        panel.Children.Add(Field("Bubble reveal", ValueKind.OptionToken, message.BubbleRevealMode, [new("duringWriteOn", "During write-on"), new("afterWriteOn", "After write-on")], (value) => message = message with { BubbleRevealMode = value }));
        return new InstantEditorCard(
            EditorCardHeader.Create($"Message {index + 1}", $"{message.Direction} · {MessageSummary(message.Text)}", EditorIcons.Create(EditorIcons.Bubble, 16)),
            new Border { Padding = EditorUiDensity.CardThickness(10), Child = panel },
            isExpanded: false,
            headerTrailing: delete)
        { HorizontalAlignment = HorizontalAlignment.Stretch };

        DictionaryFieldControl Field(string label, ValueKind kind, string value, FieldOption[]? options, Action<string> update)
        {
            var control = new DictionaryFieldControl(new FieldValue(new FieldDefinition($"conversation.messages.{message.Id}.{label}", label, kind, DefaultValue: value, Options: options), value), new DictionaryFieldServices());
            control.ValueCommitted += (_, next) => { update(next); _database.UpdateConversationMessage(node.Id, message.Id, message); _onChanged(); _reloadAndSelect(node); };
            return control;
        }
    }

    private static string MessageSummary(string text) => string.IsNullOrWhiteSpace(text)
        ? "Empty message"
        : text.Replace('\n', ' ').Trim();
}
