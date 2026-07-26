using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class IconThemeSvgReplaceDialog
{
    private const double DefaultPadding = 1;
    private const double DefaultCornerRadius = 0;
    private const double DefaultStrokeWidth = 0;
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
            Width = 1120,
            Height = 860,
            MinWidth = 980,
            MinHeight = 760,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };
        EditorSukiWindowTheme.ApplyDialogChrome(dialog, _owner);

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
            MinHeight = 150,
            Text = original.SvgText,
            PlaceholderText = "<svg viewBox=\"0 0 24 24\">...</svg>",
        });
        var transformedSvgBox = EditorTextBoxBehavior.Configure(new TextBox
        {
            AcceptsReturn = true,
            IsReadOnly = true,
            TextWrapping = TextWrapping.NoWrap,
            MinHeight = 150,
            PlaceholderText = "Transformed SVG output",
        });
        var modeOptions = new[]
        {
            new FieldOption("positive", "Positive SVG"),
            new FieldOption("negative", "Negative / cutout SVG"),
            new FieldOption("fill", "Fill SVG"),
        };
        var modeBox = new EditorInstantComboBox
        {
            MinWidth = 180,
            ItemsSource = modeOptions,
            SelectedItem = modeOptions[0],
        };
        var loadFileButton = new Button
        {
            Content = "Open SVG file",
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var saveFileButton = new Button
        {
            Content = "Save as SVG file",
            MinWidth = 136,
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var padding = CreateNumber(DefaultPadding, 0, 999, 0.25m);
        var radius = CreateNumber(DefaultCornerRadius, 0, 999, 0.25m);
        var stroke = CreateNumber(DefaultStrokeWidth, 0, 32, 0.05m);
        var scale = CreateNumber(1, 0.05, 8, 0.01m);
        var rotation = CreateNumber(0, -360, 360, 1m);
        var offsetX = CreateNumber(0, -999, 999, 0.25m);
        var offsetY = CreateNumber(0, -999, 999, 0.25m);
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
                        Width = 240,
                        Height = 240,
                        Padding = new Thickness(6),
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
                var currentPadding = Number(padding);
                var originalPreviewGeometry = SvgReplacementService.TryGeometry(originalSvg);
                originalPreview.SetSvg(originalSvg, originalPreviewGeometry, currentPadding);
                originalGeometry.Text = originalPreviewGeometry?.Label ?? "Unknown size";
                transformedSvg = SvgReplacementService.Transform(
                    svgBox.Text ?? "",
                    new SvgReplacementService.TransformOptions(
                        SelectedMode(),
                        currentPadding,
                        Number(radius),
                        Number(stroke),
                        Number(scale),
                        Number(rotation),
                        Number(offsetX),
                        Number(offsetY),
                        original.SvgText));
                var newPreviewGeometry = SvgReplacementService.TryGeometry(svgBox.Text ?? "");
                newPreview.SetSvg(transformedSvg, originalPreviewGeometry, currentPadding);
                newGeometry.Text = newPreviewGeometry?.Label ?? "Unknown size";
                transformedSvgBox.Text = transformedSvg;
                acceptButton.IsEnabled = true;
                saveFileButton.IsEnabled = true;
                SetError("");
            }
            catch (Exception exception)
            {
                transformedSvg = "";
                transformedSvgBox.Text = "";
                newPreview.SetMessage(exception.Message);
                newGeometry.Text = "";
                acceptButton.IsEnabled = false;
                saveFileButton.IsEnabled = false;
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

        Control NumberEditor(string label, NumericUpDown numeric, double sliderMinimum, double sliderMaximum, double sliderStep, int column, int row)
        {
            var slider = new Slider
            {
                Minimum = sliderMinimum,
                Maximum = sliderMaximum,
                TickFrequency = sliderStep,
                IsSnapToTickEnabled = false,
                Value = Math.Clamp(Number(numeric), sliderMinimum, sliderMaximum),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var syncing = false;
            slider.PropertyChanged += (_, change) =>
            {
                if (change.Property != Slider.ValueProperty || syncing) return;

                syncing = true;
                numeric.Value = (decimal)slider.Value;
                syncing = false;
                UpdatePreview();
            };
            numeric.PropertyChanged += (_, change) =>
            {
                if (change.Property != NumericUpDown.ValueProperty || syncing) return;

                syncing = true;
                slider.Value = Math.Clamp(Number(numeric), sliderMinimum, sliderMaximum);
                syncing = false;
            };

            var stack = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,82"),
                        ColumnSpacing = 8,
                        Children =
                        {
                            new TextBlock { Text = label, FontSize = 12, Opacity = 0.78, VerticalAlignment = VerticalAlignment.Center },
                            numeric,
                        },
                    },
                    slider,
                },
            };
            Grid.SetColumn(numeric, 1);
            Grid.SetColumn(stack, column);
            Grid.SetRow(stack, row);
            return stack;
        }

        svgBox.TextChanged += (_, _) => UpdatePreview();
        Hook(padding);
        Hook(radius);
        Hook(stroke);
        Hook(scale);
        Hook(rotation);
        Hook(offsetX);
        Hook(offsetY);

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
        saveFileButton.Click += async (_, _) =>
        {
            try
            {
                var svgText = SvgReplacementService.Validate(transformedSvg);
                var choice = await ShowSaveAsChoice(dialog, token, SelectedMode());
                if (choice is null) return;

                if (choice.SaveToAllIconSets)
                {
                    var confirmed = await ConfirmSaveToAllIconSets(dialog, choice.Token);
                    if (!confirmed) return;

                    _database.WriteIconThemeTokenSvgToAllSets(
                        node.Id,
                        choice.Token,
                        svgText,
                        $"Derived from {token} with {SelectedMode()} transform.");
                    _reloadAndSelect(node);
                    return;
                }

                var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save transformed SVG",
                    SuggestedFileName = $"{choice.Token}.svg",
                    DefaultExtension = "svg",
                    FileTypeChoices =
                    [
                        new FilePickerFileType("SVG")
                        {
                            Patterns = ["*.svg"],
                            AppleUniformTypeIdentifiers = ["public.svg-image"],
                            MimeTypes = ["image/svg+xml"],
                        },
                    ],
                });
                if (file is null) return;

                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(svgText);
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

        var radiusEditor = NumberEditor("Radius", radius, 0, 12, 0.25, 1, 0);
        var controls = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = 14,
            RowSpacing = 10,
            Children =
            {
                LabeledControl("Mode", modeBox),
                radiusEditor,
                NumberEditor("Padding", padding, 0, 12, 0.25, 0, 1),
                NumberEditor("Stroke", stroke, 0, 8, 0.05, 1, 1),
                NumberEditor("Scale", scale, 0.05, 8, 0.01, 2, 1),
                NumberEditor("Rotation", rotation, -180, 180, 1, 0, 2),
                NumberEditor("Offset X", offsetX, -12, 12, 0.25, 1, 2),
                NumberEditor("Offset Y", offsetY, -12, 12, 0.25, 2, 2),
            },
        };
        void UpdateMode()
        {
            radiusEditor.IsVisible = SelectedMode() == "negative";
            UpdatePreview();
        }
        modeBox.SelectionChanged += (_, _) => UpdateMode();
        UpdateMode();

        string SelectedMode()
        {
            return modeBox.SelectedItem?.Value switch
            {
                "negative" => "negative",
                "fill" => "fill",
                _ => "positive",
            };
        }

        var svgHeader = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 10,
            Children =
            {
                new TextBlock { Text = "SVG markup", FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
                loadFileButton,
                saveFileButton,
            },
        };
        Grid.SetColumn(loadFileButton, 1);
        Grid.SetColumn(saveFileButton, 2);
        var transformedSvgHeader = new TextBlock
        {
            Text = "Transformed SVG markup",
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };

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
                            transformedSvgHeader,
                            transformedSvgBox,
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

    private sealed record SaveAsChoice(string Token, bool SaveToAllIconSets);

    private async Task<SaveAsChoice?> ShowSaveAsChoice(Window owner, string token, string mode)
    {
        var suggestedToken = SuggestedToken(token, mode);
        var dialog = new SukiWindow
        {
            Title = "Save transformed SVG",
            Width = 500,
            Height = 250,
            MinWidth = 500,
            MinHeight = 250,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };
        EditorSukiWindowTheme.ApplyDialogChrome(dialog, _owner);

        var tokenBox = EditorTextBoxBehavior.Configure(new TextBox
        {
            Text = suggestedToken,
            PlaceholderText = "icon_token_name",
            MinHeight = 36,
        });
        var error = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#E8A1A8")),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };
        var cancel = new Button { Content = "Cancel", MinWidth = 92 };
        var saveDisk = new Button { Content = "Save to disk", MinWidth = 116 };
        var saveAll = new Button { Content = "Save to all icon sets", MinWidth = 164 };

        string? ValidToken()
        {
            var next = tokenBox.Text?.Trim() ?? "";
            if (!Regex.IsMatch(next, "^[a-z][a-z0-9_]*(?:\\.[a-z0-9_]+)*$"))
            {
                error.Text = "Icon token must be lower_snake_case.";
                error.IsVisible = true;
                return null;
            }

            error.Text = "";
            error.IsVisible = false;
            return next;
        }

        cancel.Click += (_, _) => dialog.Close(null);
        saveDisk.Click += (_, _) =>
        {
            var next = ValidToken();
            if (next is not null) dialog.Close(new SaveAsChoice(next, false));
        };
        saveAll.Click += (_, _) =>
        {
            var next = ValidToken();
            if (next is not null) dialog.Close(new SaveAsChoice(next, true));
        };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children = { cancel, saveDisk, saveAll },
        };
        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 18,
            Children =
            {
                new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Name",
                            FontWeight = FontWeight.SemiBold,
                        },
                        tokenBox,
                        new TextBlock
                        {
                            Text = "Save to disk writes one SVG file. Save to all icon sets creates or overwrites this token in every icon set for the project.",
                            Opacity = 0.75,
                            TextWrapping = TextWrapping.Wrap,
                        },
                        error,
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

        return await dialog.ShowDialog<SaveAsChoice?>(owner);
    }

    private async Task<bool> ConfirmSaveToAllIconSets(Window owner, string token)
    {
        var dialog = new SukiWindow
        {
            Title = "Save to all icon sets",
            Width = 460,
            Height = 230,
            MinWidth = 460,
            MinHeight = 230,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };
        EditorSukiWindowTheme.ApplyDialogChrome(dialog, _owner);

        var cancel = new Button { Content = "Cancel", MinWidth = 92 };
        var save = new Button { Content = "Save to all", MinWidth = 112 };
        cancel.Click += (_, _) => dialog.Close(false);
        save.Click += (_, _) => dialog.Close(true);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children = { cancel, save },
        };
        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 18,
            Children =
            {
                new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"Create or overwrite \"{token}\" in every icon set?",
                            FontSize = 17,
                            FontWeight = FontWeight.Bold,
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new TextBlock
                        {
                            Text = "This writes the transformed SVG to all icon theme folders and refreshes icon usage metadata.",
                            TextWrapping = TextWrapping.Wrap,
                            Opacity = 0.78,
                        },
                    },
                },
                actions,
            },
        };
        Grid.SetRow(actions, 1);
        dialog.Content = new Border
        {
            Padding = new Thickness(22),
            Child = content,
        };
        return await dialog.ShowDialog<bool>(owner);
    }

    private static string SuggestedToken(string token, string mode)
    {
        var suffix = mode switch
        {
            "fill" => "fill",
            "negative" => "negative",
            _ => "outline",
        };
        var clean = IconTokenRules.TokenFromText(token);
        return clean.EndsWith($"_{suffix}", StringComparison.Ordinal) ? clean : $"{clean}_{suffix}";
    }

    private static Control LabeledControl(string label, Control control, int column = 0, int row = 0)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 8,
            Children =
            {
                new TextBlock { Text = label, FontSize = 12, Opacity = 0.78, VerticalAlignment = VerticalAlignment.Center },
                control,
            },
        };
        Grid.SetColumn(control, 1);
        Grid.SetColumn(grid, column);
        Grid.SetRow(grid, row);
        return grid;
    }

    private static NumericUpDown CreateNumber(double value, double minimum, double maximum, decimal increment)
    {
        return EditorNumericUpDownBehavior.Configure(new NumericUpDown
        {
            Minimum = (decimal)minimum,
            Maximum = (decimal)maximum,
            Increment = increment,
            Value = (decimal)value,
            MinHeight = 34,
        });
    }

    private static double Number(NumericUpDown numeric)
    {
        return double.Parse((numeric.Value ?? 0).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }
}
