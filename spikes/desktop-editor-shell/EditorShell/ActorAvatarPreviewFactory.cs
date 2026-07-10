using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ActorAvatarPreviewFactory
{
    private readonly SpikeDatabase _database;

    public ActorAvatarPreviewFactory(SpikeDatabase database)
    {
        _database = database;
    }

    public Control Create(
        string actorId,
        bool isDark,
        IReadOnlyDictionary<string, string>? draftValues = null)
    {
        var settings = _database.GetActorSettings(actorId);
        var imagePath = PreviewField(actorId, "actor.avatar.filePath", draftValues);
        var useInitials = StringToBool(PreviewField(actorId, "actor.avatar.useInitials", draftValues));
        var initialsPadding = ParseDouble(PreviewField(actorId, "actor.avatar.initialsPadding", draftValues), 96);
        var scale = ParseDouble(PreviewField(actorId, "actor.avatar.scale", draftValues), 1);
        var offset = SplitPair(PreviewField(actorId, "actor.avatar.offset", draftValues));
        var offsetX = ParseDouble(offset.First, 0) / 4;
        var offsetY = ParseDouble(offset.Second, 0) / 4;
        var colorPair = SplitPair(PreviewField(actorId, "actor.color.modes", draftValues));
        var textColorPair = SplitPair(PreviewField(actorId, "actor.avatarTextColor.modes", draftValues));
        var paletteOptions = _database.GetPaletteColorOptions(settings.ProjectId);
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

        var mediaRoot = _database.GetProjectSettings(settings.ProjectId).MediaRoot;
        var fullPath = ProjectPathService.ResolveLocalPath(imagePath, mediaRoot);
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
            Text = Initials(settings.ShortName, settings.DisplayName),
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
        var settings = _database.GetActorSettings(actorId);
        var mediaRoot = _database.GetProjectSettings(settings.ProjectId).MediaRoot;
        return ProjectPathService.RelativePathIfInsideMediaRoot(path, mediaRoot);
    }

    private string PreviewField(
        string actorId,
        string fieldId,
        IReadOnlyDictionary<string, string>? draftValues)
    {
        return draftValues is not null && draftValues.TryGetValue(fieldId, out var value)
            ? value
            : _database.GetActorFieldValue(actorId, fieldId);
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

    private static string Initials(string shortName, string displayName)
    {
        var source = string.IsNullOrWhiteSpace(shortName) ? displayName : shortName;
        var parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Concat(parts.Take(2).Select((part) => part[0])).ToUpperInvariant();
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
