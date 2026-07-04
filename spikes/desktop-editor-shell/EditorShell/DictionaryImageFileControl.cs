using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.IO;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryImageFileControl : Grid, IDictionaryValueControl
{
    private readonly FieldDefinition _definition;
    private readonly Func<string, string?>? _resolveImagePath;
    private readonly TextBox _textBox;
    private readonly Border _previewFrame;
    private readonly Image _previewImage;
    private readonly TextBlock _emptyPreviewText;
    private bool _isUpdating;
    private string _value;
    private Bitmap? _bitmap;

    public DictionaryImageFileControl(
        FieldDefinition definition,
        string value,
        Func<string, string?>? resolveImagePath)
    {
        _definition = definition;
        _value = value;
        _resolveImagePath = resolveImagePath;

        RowDefinitions = new RowDefinitions("Auto,Auto");
        RowSpacing = 8;

        _textBox = DictionaryTextBoxFactory.Create(definition);
        _textBox.Text = value;
        _textBox.TextChanged += (_, _) =>
        {
            if (_isUpdating) return;

            SetLocalValue(_textBox.Text ?? "");
        };
        AttachDeferredCommit(_textBox);
        Children.Add(_textBox);

        _previewImage = new Image
        {
            Stretch = Stretch.UniformToFill,
        };

        _emptyPreviewText = new TextBlock
        {
            Text = "Image preview",
            Opacity = 0.72,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _previewFrame = new Border
        {
            Width = 240,
            Height = 136,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#5A6472")),
            Background = new SolidColorBrush(Color.Parse("#20242B")),
            ClipToBounds = true,
            Child = PreviewContent(),
            HorizontalAlignment = HorizontalAlignment.Left,
            IsVisible = true,
        };
        SetRow(_previewFrame, 1);
        Children.Add(_previewFrame);

        RefreshPreview();
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        if (_value == value) return;

        _value = value;
        _isUpdating = true;
        _textBox.Text = value;
        _isUpdating = false;
        RefreshPreview();
    }

    private void SetLocalValue(string value)
    {
        if (_value == value) return;

        _value = value;
        RefreshPreview();
        ValueChanged?.Invoke(this, _value);
    }

    private void AttachDeferredCommit(TextBox textBox)
    {
        textBox.LostFocus += (_, _) => CommitValue();
        textBox.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter) return;

            CommitValue();
            args.Handled = true;
        };
    }

    private void CommitValue()
    {
        ValueCommitted?.Invoke(this, _value);
    }

    private Control PreviewContent()
    {
        return new Grid
        {
            Children =
            {
                _previewImage,
                _emptyPreviewText,
            },
        };
    }

    private void RefreshPreview()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        _previewImage.Source = null;

        var resolvedPath = _resolveImagePath?.Invoke(_value) ?? MediaPathService.ResolveLocalPath(_value, null);
        if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
        {
            try
            {
                _bitmap = new Bitmap(resolvedPath);
                _previewImage.Source = _bitmap;
                _emptyPreviewText.IsVisible = false;
                return;
            }
            catch
            {
                _previewImage.Source = null;
            }
        }

        _emptyPreviewText.IsVisible = true;
    }
}
