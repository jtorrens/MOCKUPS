using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorIcons
{
    public const string Add = "add";
    public const string Delete = "delete";
    public const string Duplicate = "duplicate";
    public const string Expand = "expand";
    public const string Collapse = "collapse";

    public const string Project = "project";
    public const string Apps = "apps";
    public const string App = "app";
    public const string Module = "module";
    public const string Episodes = "episodes";
    public const string Episode = "episode";
    public const string Shot = "shot";
    public const string Screen = "screen";

    public const string Data = "data";
    public const string Actor = "actor";
    public const string Theme = "theme";
    public const string Icon = "icon";
    public const string Status = "status";
    public const string Navigation = "navigation";
    public const string Component = "component";
    public const string Device = "device";
    public const string Media = "media";
    public const string Typography = "typography";
    public const string Render = "render";
    public const string Animation = "animation";
    public const string Color = "color";

    public const string General = "general";
    public const string Style = "style";
    public const string Behavior = "behavior";
    public const string Content = "content";
    public const string Design = "design";
    public const string Layout = "layout";
    public const string Header = "header";
    public const string Messages = "messages";
    public const string Bubble = "bubble";
    public const string Avatar = "avatar";
    public const string Label = "label";
    public const string Image = "image";
    public const string Video = "video";
    public const string Audio = "audio";
    public const string Tail = "tail";
    public const string Keyboard = "keyboard";
    public const string TextInput = "text-input";
    public const string ButtonIcon = "button-icon";
    public const string Relief = "relief";
    public const string Shadow = "shadow";
    public const string Border = "border";

    private static readonly Regex SvgPathRegex = new(
        "<path\\b[^>]*\\bd=\"([^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, string?> SvgPathCache = [];
    private static string? SystemIconsDirectoryCache;

    public static string ForTreeNode(ProjectTreeNodeKind kind)
    {
        return kind switch
        {
            ProjectTreeNodeKind.Project => Project,
            ProjectTreeNodeKind.ProductionDataRoot => Content,
            ProjectTreeNodeKind.SystemDataRoot => Design,
            ProjectTreeNodeKind.AppsRoot => Apps,
            ProjectTreeNodeKind.PaletteRoot => Color,
            ProjectTreeNodeKind.DevicesRoot => Device,
            ProjectTreeNodeKind.ActorsRoot => Actor,
            ProjectTreeNodeKind.ProductionFontsRoot => Typography,
            ProjectTreeNodeKind.App => App,
            ProjectTreeNodeKind.Module => Module,
            ProjectTreeNodeKind.EpisodesRoot => Episodes,
            ProjectTreeNodeKind.Episode => Episode,
            ProjectTreeNodeKind.Shot => Shot,
            ProjectTreeNodeKind.PaletteColor => Color,
            ProjectTreeNodeKind.Device => Device,
            ProjectTreeNodeKind.Actor => Actor,
            ProjectTreeNodeKind.ProductionFont => Typography,
            _ => Component,
        };
    }

    public static Control Create(string name, double size = 20)
    {
        var svgPath = SvgPathData(name);
        var path = svgPath ?? PathData(name);
        if (path is null)
        {
            return new TextBlock
            {
                Text = "•",
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            };
        }

        var icon = new PathIcon
        {
            Data = Geometry.Parse(path),
            Width = size,
            Height = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        return icon;
    }

    private static string? SvgPathData(string name)
    {
        if (SvgPathCache.TryGetValue(name, out var cachedPath))
        {
            return cachedPath;
        }

        var iconPath = SystemIconPath(name);
        if (iconPath is null || !File.Exists(iconPath))
        {
            SvgPathCache[name] = null;
            return null;
        }

        var svg = File.ReadAllText(iconPath);
        var paths = SvgPathRegex
            .Matches(svg)
            .Select((match) => match.Groups[1].Value.Trim())
            .Where((path) => !string.IsNullOrWhiteSpace(path))
            .ToArray();
        var pathData = paths.Length == 0 ? null : string.Join(" ", paths);
        SvgPathCache[name] = pathData;
        return pathData;
    }

    private static string? SystemIconPath(string name)
    {
        var fileName = name switch
        {
            Add => "system_add.svg",
            Delete => "system_delete.svg",
            Duplicate => "system_duplicate.svg",
            Project => "editor_general.svg",
            Apps => "editor_design.svg",
            App => "editor_layout.svg",
            Module => "editor_content.svg",
            Episodes => "editor_messages.svg",
            Episode => "editor_content.svg",
            Shot => "editor_shot.svg",
            General => "editor_general.svg",
            Style => "editor_style.svg",
            Behavior => "editor_behavior.svg",
            Content => "editor_content.svg",
            Design => "editor_design.svg",
            Layout => "editor_layout.svg",
            Header => "editor_header.svg",
            Messages => "editor_messages.svg",
            Bubble => "editor_bubble.svg",
            Avatar => "editor_avatar.svg",
            Label => "editor_label.svg",
            Image => "editor_image.svg",
            Video => "editor_video.svg",
            Audio => "editor_audio.svg",
            Tail => "editor_tail.svg",
            Keyboard => "editor_keyboard.svg",
            TextInput => "editor_text_input.svg",
            ButtonIcon => "editor_button_icon.svg",
            Relief => "editor_relief.svg",
            Shadow => "editor_shadow.svg",
            Border => "editor_border.svg",
            Media => "editor_media.svg",
            Device => "editor_device.svg",
            Typography => "editor_text_input.svg",
            _ => null,
        };

        if (fileName is null)
        {
            return null;
        }

        var directory = SystemIconsDirectory();
        return directory is null ? null : Path.Combine(directory, fileName);
    }

    private static string? SystemIconsDirectory()
    {
        if (SystemIconsDirectoryCache is not null)
        {
            return SystemIconsDirectoryCache;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "assets", "system", "system_icons");
            if (Directory.Exists(candidate))
            {
                SystemIconsDirectoryCache = candidate;
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string? PathData(string name)
    {
        return name switch
        {
            Add => "M6 3H14L19 8V21H6Z M14 3V8H19 M10 14H15 M12.5 11.5V16.5",
            Delete => "M7 7H17V20H7Z M9 4H15V6H20V8H4V6H9Z M9 10H11V18H9Z M13 10H15V18H13Z",
            Duplicate => "M8 8H18V18H8Z M5 5H15V8 M5 5V15H8",
            Expand => "M9 5L16 12L9 19Z",
            Collapse => "M5 15L12 8L19 15Z",

            Project => "M4 9.5L12 4L20 9.5V20C20 20.55 19.55 21 19 21H14V15H10V21H5C4.45 21 4 20.55 4 20V9.5Z",
            Apps => "M5 5H11V11H5Z M13 5H19V11H13Z M5 13H11V19H5Z M13 13H19V19H13Z",
            App => "M9 5H15C17.2 5 19 6.8 19 9V15C19 17.2 17.2 19 15 19H9C6.8 19 5 17.2 5 15V9C5 6.8 6.8 5 9 5Z M9 9H15V15H9Z",
            Module => "M12 3L20 7.5V16.5L12 21L4 16.5V7.5L12 3Z M4 7.5L12 12L20 7.5 M12 12V21",
            Episodes => "M4 5H20V20H4Z M8 3V7 M16 3V7 M4 10H20",
            Episode => "M4 5H20V20H4Z M8 3V7 M16 3V7 M4 10H20",
            Shot => "M7 7H9L10.5 5H13.5L15 7H17C18.65 7 20 8.35 20 10V16C20 17.65 18.65 19 17 19H7C5.35 19 4 17.65 4 16V10C4 8.35 5.35 7 7 7Z M12 9.8A3.2 3.2 0 1 0 12 16.2A3.2 3.2 0 1 0 12 9.8",
            Screen => "M4 5H20V17H4Z M9 21H15 M12 17V21",

            Data => "M5 6C5 4.3 8.1 3 12 3C15.9 3 19 4.3 19 6C19 7.7 15.9 9 12 9C8.1 9 5 7.7 5 6Z M5 6V12C5 13.7 8.1 15 12 15C15.9 15 19 13.7 19 12V6 M5 12V18C5 19.7 8.1 21 12 21C15.9 21 19 19.7 19 18V12",
            Actor => "M12 4A4 4 0 1 0 12 12A4 4 0 1 0 12 4 M4.5 20C5.9 16 8.5 14 12 14C15.5 14 18.1 16 19.5 20",
            Theme => "M12 4A8 8 0 1 0 12 20H13.4C15.15 20 15.95 17.95 14.9 16.7C14.05 15.7 14.8 14 16.2 14H18C18 8.5 15.3 4 12 4Z M8.2 9.2A0.8 0.8 0 1 0 8.2 10.8A0.8 0.8 0 1 0 8.2 9.2 M11 7A0.8 0.8 0 1 0 11 8.6A0.8 0.8 0 1 0 11 7 M14.2 8.2A0.8 0.8 0 1 0 14.2 9.8A0.8 0.8 0 1 0 14.2 8.2",
            Icon => "M7 4H17L20 7V17L17 20H7L4 17V7L7 4Z M9 12H15 M12 9V15",
            Status => "M4 5H20V17H4Z M5 8H19 M7 12H9 M11 12H13 M15 12H17",
            Navigation => "M4 5H20V19H4Z M8 16H16 M8 12L10.5 9.5L13 12 M16 9.5V14.5",
            Component => "M4 4H11V11H4Z M13 4H20V11H13Z M4 13H11V20H4Z M13 16.5H20 M16.5 13V20",
            Device => "M7 3H17V21H7Z M10.5 18H13.5",
            Media => "M4 5H20V19H4Z M7 16L10.2 12.8L12.5 15.1L15.5 12L20 16 M8.5 8.5A1 1 0 1 0 8.5 10.5A1 1 0 1 0 8.5 8.5",
            Typography => "M5 5H19 M12 5V19 M8 19H16",
            Render => "M5 5H19V15H5Z M8 19H16 M12 15V19 M9 8L12 10L15 8",
            Animation => "M4 12H7L9 7L13 17L15 12H20 M4 5H20 M4 19H20",
            Color => "M12 4A8 8 0 1 0 12 20A8 8 0 1 0 12 4 M12 4V20 M4 12H20",

            General => "M12 4V20 M4 12H20 M7 7L17 17 M17 7L7 17",
            Style => "M12 4A8 8 0 0 0 12 20 M12 4A8 8 0 0 1 12 20",
            Behavior => "M12 8A4 4 0 1 0 12 16A4 4 0 1 0 12 8 M12 3V5 M12 19V21 M3 12H5 M19 12H21 M5.6 5.6L7 7 M17 17L18.4 18.4 M18.4 5.6L17 7 M7 17L5.6 18.4",
            Content => "M5 7H19 M5 12H19 M5 17H15",
            Design => "M5 5H19V19H5Z M8 8H16V16H8Z",
            Layout => "M4 5H20V19H4Z M4 10H20 M10 10V19",
            Header => "M4 6H20 M4 10H20",
            Messages => "M5 6H19V15H9L5 19V6Z",
            Bubble => "M5 6H19V16H10L6 20V16H5Z",
            Avatar => "M12 5A4 4 0 1 0 12 13A4 4 0 1 0 12 5 M5 20C6.5 16.5 9 15 12 15C15 15 17.5 16.5 19 20",
            Label => "M5 7H19 M8 7V17 M6 17H10",
            Image => "M4 5H20V19H4Z M7 16L10 13L12 15L15 12L20 16",
            Video => "M5 5H15V19H5Z M15 10L20 7V17L15 14Z",
            Audio => "M4 13H6L8 9V17L6 13 M11 8V16 M14 6V18 M17 9V15 M20 11V13",
            Tail => "M5 5H19V15H12L7 20V15H5Z",
            Keyboard => "M4 7H20V17H4Z M7 10H8 M10 10H11 M13 10H14 M16 10H17 M8 14H16",
            TextInput => "M4 7H20V17H4Z M8 12H16",
            ButtonIcon => "M12 5A7 7 0 1 0 12 19A7 7 0 1 0 12 5 M12 9V15 M9 12H15",
            Relief => "M6 16L16 6 M9 18L18 9",
            Shadow => "M8 8H18V18H8Z M5 5H15",
            Border => "M5 5H19V19H5Z",
            _ => null,
        };
    }
}
