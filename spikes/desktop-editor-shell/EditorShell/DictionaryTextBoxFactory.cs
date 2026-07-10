using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DictionaryTextBoxFactory
{
    public static TextBox Create(FieldDefinition definition)
    {
        var textBox = new TextBox
        {
            IsReadOnly = !definition.IsEditable || definition.ValueKind == ValueKind.StringReadOnly,
            AcceptsReturn = definition.ValueKind == ValueKind.StringMultiline,
            TextWrapping = definition.ValueKind == ValueKind.StringMultiline
                ? TextWrapping.Wrap
                : TextWrapping.NoWrap,
            MinHeight = definition.ValueKind == ValueKind.StringMultiline ? 88 : 36,
            PlaceholderText = definition.ValueKind switch
            {
                ValueKind.DirectoryPath => "Select folder...",
                ValueKind.MediaFilePath => "Select media...",
                _ => null,
            },
            VerticalContentAlignment = definition.ValueKind == ValueKind.StringMultiline
                ? VerticalAlignment.Top
                : VerticalAlignment.Center,
        };
        EditorTextBoxBehavior.Configure(textBox);
        if (definition.ValueKind is ValueKind.ImageFilePath or ValueKind.MediaFilePath)
        {
            textBox.MaxWidth = 420;
            textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        return textBox;
    }

    public static TextBox CreateCompactPair(string value)
    {
        return EditorNumericTextStyle.Apply(EditorTextBoxBehavior.Configure(new TextBox
        {
            Text = value,
            Width = EditorUiDensity.TextAwareWidth(90),
            MinHeight = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
        }));
    }
}
