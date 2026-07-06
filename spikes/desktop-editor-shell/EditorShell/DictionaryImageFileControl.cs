using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryImageFileControl : Grid, IDictionaryValueControl, IDictionaryPreviewValueControl
{
    private readonly FieldDefinition _definition;
    private readonly Func<string, string?>? _resolveImagePath;
    private readonly Func<string, string>? _getFieldValue;
    private readonly TextBox _textBox;
    private readonly DictionaryPathBrowseButton _browseButton;
    private readonly Border _previewFrame;
    private readonly Image _previewImage;
    private readonly TextBlock _emptyPreviewText;
    private bool _isUpdating;
    private string _value;
    private Bitmap? _bitmap;

    public DictionaryImageFileControl(
        FieldDefinition definition,
        string value,
        Func<string, ValueKind, Task<string?>>? browsePath,
        Func<string, string?>? resolveImagePath,
        Func<string, string>? getFieldValue)
    {
        _definition = definition;
        _value = value;
        _resolveImagePath = resolveImagePath;
        _getFieldValue = getFieldValue;

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

        var pathRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 10,
            Children =
            {
                _textBox,
            },
        };
        _browseButton = new DictionaryPathBrowseButton(definition.ValueKind, value, definition.IsEditable, browsePath);
        _browseButton.ValueCommitted += (_, selectedPath) =>
        {
            SetLocalValue(selectedPath, updateTextBox: true);
            CommitValue();
        };
        SetColumn(_browseButton, 1);
        pathRow.Children.Add(_browseButton);
        Children.Add(pathRow);

        _previewImage = new Image();

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
        _browseButton.SetValue(value);
        _isUpdating = true;
        _textBox.Text = value;
        _isUpdating = false;
        RefreshPreview();
    }

    private void SetLocalValue(string value, bool updateTextBox = false)
    {
        if (_value == value) return;

        _value = value;
        _browseButton.SetValue(value);
        if (updateTextBox)
        {
            _isUpdating = true;
            _textBox.Text = value;
            _isUpdating = false;
        }

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

    public void RefreshPreview()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        _previewImage.Source = null;
        _previewImage.RenderTransform = null;

        var resolvedPath = _resolveImagePath?.Invoke(_value) ?? ProjectPathService.ResolveLocalPath(_value, null);
        if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
        {
            try
            {
                _bitmap = new Bitmap(resolvedPath);
                _previewImage.Source = _bitmap;
                ApplyPreviewMetrics(_bitmap);
                _emptyPreviewText.IsVisible = false;
                return;
            }
            catch
            {
                _previewImage.Source = null;
            }
        }

        _emptyPreviewText.IsVisible = true;
        ApplyEmptyPreviewMetrics();
    }

    private void ApplyPreviewMetrics(Bitmap bitmap)
    {
        var preview = _definition.ImagePreview;
        if (preview?.Mode == ImagePreviewMode.SquareCrop)
        {
            ApplySquareCropPreview(preview);
            return;
        }

        ApplyAspectPreview(bitmap);
    }

    private void ApplyAspectPreview(Bitmap bitmap)
    {
        var pixelSize = bitmap.PixelSize;
        var sourceWidth = Math.Max(1d, pixelSize.Width);
        var sourceHeight = Math.Max(1d, pixelSize.Height);
        var width = Math.Min(360d, Math.Max(120d, sourceWidth));
        var height = Math.Round(width * sourceHeight / sourceWidth);
        if (height > 220)
        {
            height = 220;
            width = Math.Round(height * sourceWidth / sourceHeight);
        }

        _previewFrame.Width = width;
        _previewFrame.Height = height;
        _previewImage.Width = width;
        _previewImage.Height = height;
        _previewImage.Stretch = Stretch.Uniform;
    }

    private void ApplySquareCropPreview(ImagePreviewDefinition preview)
    {
        const double previewSize = 160;
        var baseSize = Math.Max(1, preview.BaseSize);
        var scale = Math.Max(0.01, ParseDouble(FieldValue(preview.ScaleFieldId), 1));
        var offset = SplitPair(FieldValue(preview.OffsetFieldId));
        var offsetX = ParseDouble(offset.First, 0) / baseSize * previewSize;
        var offsetY = ParseDouble(offset.Second, 0) / baseSize * previewSize;

        _previewFrame.Width = previewSize;
        _previewFrame.Height = previewSize;
        _previewImage.Width = previewSize;
        _previewImage.Height = previewSize;
        _previewImage.Stretch = Stretch.UniformToFill;
        _previewImage.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        _previewImage.RenderTransform = new TransformGroup
        {
            Children =
            {
                new ScaleTransform(scale, scale),
                new TranslateTransform(offsetX, offsetY),
            },
        };
    }

    private void ApplyEmptyPreviewMetrics()
    {
        if (_definition.ImagePreview?.Mode == ImagePreviewMode.SquareCrop)
        {
            _previewFrame.Width = 160;
            _previewFrame.Height = 160;
            return;
        }

        _previewFrame.Width = 240;
        _previewFrame.Height = 136;
    }

    private string FieldValue(string? fieldId)
    {
        return string.IsNullOrWhiteSpace(fieldId) ? "" : _getFieldValue?.Invoke(fieldId) ?? "";
    }

    private static double ParseDouble(string value, double fallback)
    {
        return NumericText.Double(value, fallback);
    }

    private static (string First, string Second) SplitPair(string value)
    {
        var parts = value.Split('|', 2);
        return (parts.Length > 0 ? parts[0] : "", parts.Length > 1 ? parts[1] : "");
    }
}
