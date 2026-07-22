using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ActorAvatarPreviewFactory
{
    private readonly ActorPreviewDataSource _dataSource;

    public ActorAvatarPreviewFactory(ActorPreviewDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public Control Create(
        string actorId,
        bool isDark,
        IReadOnlyDictionary<string, string>? draftValues = null)
    {
        var source = _dataSource.LoadPreview(actorId);
        var imagePath = PreviewField("actor.avatar.filePath", source.AvatarFilePath, draftValues);
        var useInitials = StringToBool(PreviewField("actor.avatar.useInitials", source.AvatarUseInitials, draftValues));
        var initialsPadding = ParseDouble(PreviewField("actor.avatar.initialsPadding", source.AvatarInitialsPadding, draftValues), 96);
        var scale = ParseDouble(PreviewField("actor.avatar.scale", source.AvatarScale, draftValues), 1);
        var offset = SplitPair(PreviewField("actor.avatar.offset", source.AvatarOffset, draftValues));
        var offsetX = ParseDouble(offset.First, 0) / 4;
        var offsetY = ParseDouble(offset.Second, 0) / 4;
        var colorPair = SplitPair(PreviewField("actor.color.modes", source.ColorModes, draftValues));
        var textColorPair = SplitPair(PreviewField("actor.avatarTextColor.modes", source.AvatarTextColorModes, draftValues));
        var paletteOptions = _dataSource.PaletteColorOptions(source.ProjectId);
        var background = PaletteBrush(paletteOptions, colorPair.First, "#808080");
        var foreground = PaletteBrush(paletteOptions, textColorPair.First, "#1A1A1A");

        var viewport = new Border
        {
            Width = 160,
            Height = 160,
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse(isDark ? "#8FA0B8" : "#667085")),
            Background = background,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var fullPath = ProjectPathService.ResolveLocalPath(imagePath, source.ProjectMediaRoot);
        if (!useInitials && !string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
        {
            try
            {
                viewport.Child = new Image
                {
                    Source = new Bitmap(fullPath),
                    Width = 160,
                    Height = 160,
                    Stretch = Stretch.UniformToFill,
                    RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                    RenderTransform = new TransformGroup
                    {
                        Children =
                        {
                            new ScaleTransform(scale, scale),
                            new TranslateTransform(offsetX, offsetY),
                        },
                    },
                };
                return Wrap(viewport);
            }
            catch (Exception)
            {
                // Unsupported image formats fall back to initials in the spike shell.
            }
        }

        var previewPadding = Math.Clamp(initialsPadding / 4, 0, 72);
        var initialsFontSize = Math.Max(12, (160 - previewPadding * 2) * 0.46);
        viewport.Child = new TextBlock
        {
            Text = ActorIdentityText.Initials(source.ShortName, source.DisplayName),
            Foreground = foreground,
            FontSize = initialsFontSize,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        return Wrap(viewport);
    }

    public string? RelativeActorMediaPath(string actorId, string path)
    {
        var source = _dataSource.LoadPreview(actorId);
        return ProjectPathService.RelativePathIfInsideMediaRoot(path, source.ProjectMediaRoot);
    }

    private static string PreviewField(
        string fieldId,
        string storedValue,
        IReadOnlyDictionary<string, string>? draftValues)
    {
        return draftValues is not null && draftValues.TryGetValue(fieldId, out var value)
            ? value
            : storedValue;
    }

    private static Control Wrap(Control viewport)
    {
        return new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Avatar preview · 640×640 crop",
                    FontSize = 12,
                    Opacity = 0.72,
                },
                viewport,
            },
        };
    }

    private static IBrush PaletteBrush(IReadOnlyList<FieldOption> paletteOptions, string token, string fallback)
    {
        var hex = paletteOptions.FirstOrDefault((option) => option.Value == token)?.ColorHex;
        return SafeColorBrush(hex, fallback);
    }

    private static IBrush SafeColorBrush(string? hex, string fallback)
    {
        return ColorValue.SafeBrush(hex, fallback);
    }

    private static double ParseDouble(string value, double fallback)
    {
        return NumericText.Double(value, fallback);
    }

    private static (string First, string Second) SplitPair(string value)
    {
        var parts = value.Split('|', 2);
        return (parts.ElementAtOrDefault(0) ?? "", parts.ElementAtOrDefault(1) ?? "");
    }

    private static bool StringToBool(string value)
    {
        return BooleanText.Parse(value);
    }
}
