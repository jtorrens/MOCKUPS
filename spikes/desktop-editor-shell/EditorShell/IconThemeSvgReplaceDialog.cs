using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class IconThemeSvgReplaceDialog
{
    private const double DefaultPadding = 1;
    private const double DefaultCornerRadius = 3;
    private const double DefaultStrokeWidth = 1.5;
    private readonly Window _owner;
    private readonly SpikeDatabase _database;
    private readonly Func<Task<string?>> _browseSvgFile;
    private readonly Action<ProjectTreeNode> _reloadAndSelect;

    public IconThemeSvgReplaceDialog(
        Window owner,
        SpikeDatabase database,
        Func<Task<string?>> browseSvgFile,
        Action<ProjectTreeNode> reloadAndSelect)
    {
        _owner = owner;
        _database = database;
        _browseSvgFile = browseSvgFile;
        _reloadAndSelect = reloadAndSelect;
    }

    public async Task Show(ProjectTreeNode node, string token)
    {
        var original = _database.ReadIconThemeTokenSvg(node.Id, token);
        var dialog = new SukiWindow
        {
            Title = $"Replace SVG for \"{token}\"",
            Width = 920,
            Height = 760,
            MinWidth = 840,
            MinHeight = 660,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };

        var originalPreview = new SvgMarkupPreview();
        var newPreview = new SvgMarkupPreview();
        var originalGeometry = new TextBlock { Opacity = 0.72, HorizontalAlignment = HorizontalAlignment.Center };
        var newGeometry = new TextBlock { Opacity = 0.72, HorizontalAlignment = HorizontalAlignment.Center };
        var errorText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#E8A1A8")),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };
        var svgBox = EditorTextBoxBehavior.Configure(new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            MinHeight = 190,
            Text = original.SvgText,
            PlaceholderText = "<svg viewBox=\"0 0 24 24\">...</svg>",
        });
        var modeBox = new ComboBox
        {
            MinWidth = 180,
            ItemsSource = new[] { "Positive SVG", "Negative / cutout SVG" },
            SelectedIndex = 0,
        };
        var loadFileButton = new Button
        {
            Content = "Open SVG file",
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var padding = CreateNumber(DefaultPadding, 0, 12, 0.25m);
        var radius = CreateNumber(DefaultCornerRadius, 0, 12, 0.25m);
        var stroke = CreateNumber(DefaultStrokeWidth, 0, 8, 0.05m);
        var scale = CreateNumber(1, 0.05, 8, 0.01m);
        var rotation = CreateNumber(0, -180, 180, 1m);
        var acceptButton = new Button { Content = "Accept", MinWidth = 96, IsEnabled = false };
        var cancelButton = new Button { Content = "Cancel", MinWidth = 92 };
        string transformedSvg = "";

        Control PreviewBox(Control preview, TextBlock geometry)
        {
            return new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new Border
                    {
                        Width = 220,
                        Height = 180,
                        Padding = new Thickness(18),
                        BorderThickness = new Thickness(1),
                        BorderBrush = new SolidColorBrush(Color.Parse("#44546A")),
                        CornerRadius = new CornerRadius(8),
                        Child = preview,
                    },
                    geometry,
                },
            };
        }

        void SetError(string message)
        {
            errorText.Text = message;
            errorText.IsVisible = !string.IsNullOrWhiteSpace(message);
        }

        void UpdatePreview()
        {
            try
            {
                var originalSvg = SvgReplacementService.Validate(original.SvgText);
                originalPreview.SetSvg(originalSvg);
                originalGeometry.Text = SvgReplacementService.TryGeometry(originalSvg)?.Label ?? "Unknown size";
                transformedSvg = SvgReplacementService.Transform(
                    svgBox.Text ?? "",
                    new SvgReplacementService.TransformOptions(
                        modeBox.SelectedIndex == 1 ? "negative" : "positive",
                        Number(padding),
                        Number(radius),
                        Number(stroke),
                        Number(scale),
                        Number(rotation),
                        original.SvgText));
                newPreview.SetSvg(transformedSvg);
                newGeometry.Text = SvgReplacementService.TryGeometry(svgBox.Text ?? "")?.Label ?? "Unknown size";
                acceptButton.IsEnabled = true;
                SetError("");
            }
            catch (Exception exception)
            {
                transformedSvg = "";
                newPreview.SetMessage(exception.Message);
                newGeometry.Text = "";
                acceptButton.IsEnabled = false;
                SetError(exception.Message);
            }
        }

        void Hook(NumericUpDown numeric)
        {
            numeric.PropertyChanged += (_, change) =>
            {
                if (change.Property == NumericUpDown.ValueProperty) UpdatePreview();
            };
        }

        svgBox.TextChanged += (_, _) => UpdatePreview();
        modeBox.SelectionChanged += (_, _) => UpdatePreview();
        Hook(padding);
        Hook(radius);
        Hook(stroke);
        Hook(scale);
        Hook(rotation);

        cancelButton.Click += (_, _) => dialog.Close();
        loadFileButton.Click += async (_, _) =>
        {
            try
            {
                var path = await _browseSvgFile();
                if (string.IsNullOrWhiteSpace(path)) return;

                svgBox.Text = File.ReadAllText(path);
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
            }
        };
        acceptButton.Click += (_, _) =>
        {
            try
            {
                _database.ReplaceIconThemeTokenSvg(node.Id, token, transformedSvg);
                _reloadAndSelect(node);
                dialog.Close();
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
            }
        };

        var previewLabels = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            Children =
            {
                new TextBlock { Text = $"Original ({original.File})", FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = "New transformed SVG", FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
            },
        };
        Grid.SetColumn(previewLabels.Children[1], 1);

        var previews = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 14,
            Children =
            {
                PreviewBox(originalPreview, originalGeometry),
                PreviewBox(newPreview, newGeometry),
            },
        };
        Grid.SetColumn(previews.Children[1], 1);

        var controls = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*"),
            ColumnSpacing = 8,
            Children =
            {
                LabeledControl("Mode", modeBox),
                LabeledControl("Padding", padding, 1),
                LabeledControl("Radius", radius, 2),
                LabeledControl("Stroke", stroke, 3),
                LabeledControl("Scale", scale, 4),
                LabeledControl("Rotation", rotation, 5),
            },
        };

        var svgHeader = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new TextBlock { Text = "SVG markup", FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
                loadFileButton,
            },
        };
        Grid.SetColumn(loadFileButton, 1);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children = { cancelButton, acceptButton },
        };

        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 14,
            Children =
            {
                new ScrollViewer
                {
                    Content = new StackPanel
                    {
                        Spacing = 14,
                        Children =
                        {
                            previewLabels,
                            previews,
                            controls,
                            svgHeader,
                            svgBox,
                            errorText,
                        },
                    },
                },
                actions,
            },
        };
        Grid.SetRow(actions, 1);

        dialog.Content = new Border
        {
            Padding = new Thickness(18),
            Child = content,
        };

        UpdatePreview();
        await dialog.ShowDialog(_owner);
    }

    private static Control LabeledControl(string label, Control control, int column = 0)
    {
        var stack = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = label, FontSize = 12, Opacity = 0.78 },
                control,
            },
        };
        Grid.SetColumn(stack, column);
        return stack;
    }

    private static NumericUpDown CreateNumber(double value, double minimum, double maximum, decimal increment)
    {
        return new NumericUpDown
        {
            Minimum = (decimal)minimum,
            Maximum = (decimal)maximum,
            Increment = increment,
            Value = (decimal)value,
            MinHeight = 34,
        };
    }

    private static double Number(NumericUpDown numeric)
    {
        return double.Parse((numeric.Value ?? 0).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }
}
