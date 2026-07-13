using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ModuleInstanceAnimationEditor
{
    private static readonly IBrush OtherKeyframeBrush = new SolidColorBrush(Color.Parse("#7B8493"));
    private static readonly IBrush ActiveTrackBrush = new SolidColorBrush(Color.Parse("#F0B429"));
    private static readonly IBrush CurrentKeyframeBrush = new SolidColorBrush(Color.Parse("#2F80ED"));
    private readonly SpikeDatabase _database;
    private readonly EditorDictionaryFieldServices _dictionaryServices;
    private readonly Action _onChanged;
    private readonly Action<ProjectTreeNode> _reloadAndSelect;
    private readonly Func<string, int> _screenFrame;
    private readonly Action<string, int> _setScreenFrame;

    public ModuleInstanceAnimationEditor(
        SpikeDatabase database,
        EditorDictionaryFieldServices dictionaryServices,
        Action onChanged,
        Action<ProjectTreeNode> reloadAndSelect,
        Func<string, int> screenFrame,
        Action<string, int> setScreenFrame)
    {
        _database = database;
        _dictionaryServices = dictionaryServices;
        _onChanged = onChanged;
        _reloadAndSelect = reloadAndSelect;
        _screenFrame = screenFrame;
        _setScreenFrame = setScreenFrame;
    }

    public InstantEditorCard Create(ProjectTreeNode node)
    {
        var instance = _database.GetModuleInstanceSettings(node.Id);
        var module = _database.GetModuleSettings(instance.ModuleId);
        var preview = DesignPreviewTestValues.Parse(_database.GetModuleInstanceRuntimePreviewJson(node.Id));
        var config = DesignPreviewTestValues.Parse(module.ConfigJson);
        var targets = ReadTargets(node, preview, config);
        var document = new ModuleInstanceAnimationDocument(instance.AnimationJson);
        var resolvedTargets = targets
            .Select((target) => new ResolvedAnimationTarget(target, document.Track(target.FieldId, target.TargetId)))
            .ToList();
        resolvedTargets.AddRange(document.Tracks
            .Where((track) => targets.All((target) => target.FieldId != track.FieldId || target.TargetId != track.TargetId))
            .Select((track) => new ResolvedAnimationTarget(null, track)));
        var activeTrackCount = resolvedTargets.Count((target) => target.Track is not null);
        var content = new StackPanel { Spacing = 12 };
        if (resolvedTargets.Count == 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = "Activa el rombo de un Runtime Value para crear su track de animación.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.72,
            });
        }
        else
        {
            content.Children.Add(CreateTimelineEditor(node, document, resolvedTargets));
        }
        return new InstantEditorCard(
            EditorCardHeader.Create(
                "Animation",
                EditorUiText.Count(activeTrackCount, "animated property"),
                EditorIcons.CreateSemantic("Animation", EditorIcons.Animation, 18)),
            new Border { Padding = new Thickness(10), Child = content },
            isExpanded: true)
        { HorizontalAlignment = HorizontalAlignment.Stretch };
    }

    private Control CreateTimelineEditor(
        ProjectTreeNode node,
        ModuleInstanceAnimationDocument document,
        IReadOnlyList<ResolvedAnimationTarget> targets)
    {
        var duration = Math.Max(1, ModuleInstanceTimeline.DurationFrames(_database, node.Id));
        var currentFrame = Math.Clamp(_screenFrame(node.Id), 0, duration - 1);
        var selected = targets.FirstOrDefault((target) => target.Track is not null) ?? targets.First();
        var root = new StackPanel { Spacing = 12 };
        var frameText = new TextBlock { MinWidth = 76, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = duration - 1,
            Value = currentFrame,
            TickFrequency = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var timelineHost = new ContentControl();
        var trackList = new StackPanel { Spacing = 4 };
        var detailHost = new ContentControl();
        var changingFrame = false;

        void SetFrame(int frame)
        {
            currentFrame = Math.Clamp(frame, 0, duration - 1);
            changingFrame = true;
            slider.Value = currentFrame;
            changingFrame = false;
            _setScreenFrame(node.Id, currentFrame);
            RefreshVisuals();
        }

        void SaveAndReload()
        {
            _database.UpdateModuleInstanceAnimationJson(node.Id, document.ToJson());
            _onChanged();
            _reloadAndSelect(node);
        }

        void RefreshVisuals()
        {
            frameText.Text = $"{currentFrame}/{duration - 1}";
            timelineHost.Content = CreateMiniTimeline(targets, selected, currentFrame, duration, SetFrame);
            trackList.Children.Clear();
            foreach (var target in targets)
            {
                var selectedTarget = ReferenceEquals(target, selected);
                var active = target.Track is not null;
                var button = new Button
                {
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = selectedTarget ? EditorSukiWindowTheme.AccentBrush(0x18) : Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    Foreground = selectedTarget && active ? ActiveTrackBrush : null,
                    Content = $"{(active ? "◆" : "◇")}  {target.Label}  ·  {target.Track?.Keyframes.Count ?? 0} keyframes",
                };
                button.Click += (_, _) =>
                {
                    selected = target;
                    RefreshVisuals();
                };
                trackList.Children.Add(button);
            }
            detailHost.Content = CreateTrackDetail(node, document, selected, currentFrame, SetFrame, SaveAndReload);
        }

        var transport = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*,Auto,Auto,Auto"),
            ColumnSpacing = 6,
        };
        AddTransportButton(transport, 0, "|‹", "First Screen frame", () => SetFrame(0));
        AddTransportButton(transport, 1, "‹", "Previous frame", () => SetFrame(currentFrame - 1));
        AddTransportButton(transport, 2, "◆", "Current frame", () => SetFrame(currentFrame));
        Grid.SetColumn(frameText, 3);
        transport.Children.Add(frameText);
        AddTransportButton(transport, 4, "›", "Next frame", () => SetFrame(currentFrame + 1));
        AddTransportButton(transport, 5, "›|", "Last Screen frame", () => SetFrame(duration - 1));
        root.Children.Add(transport);
        slider.ValueChanged += (_, args) =>
        {
            if (!changingFrame) SetFrame((int)Math.Round(args.NewValue, MidpointRounding.AwayFromZero));
        };
        root.Children.Add(slider);
        root.Children.Add(timelineHost);
        root.Children.Add(trackList);
        root.Children.Add(detailHost);
        RefreshVisuals();
        return root;
    }

    private Control CreateTrackDetail(
        ProjectTreeNode node,
        ModuleInstanceAnimationDocument document,
        ResolvedAnimationTarget selected,
        int screenFrame,
        Action<int> setFrame,
        Action saveAndReload)
    {
        if (selected.Target is null)
            return new TextBlock { Text = "El target de este track ya no existe.", Foreground = ActiveTrackBrush };
        var target = selected.Target;
        if (selected.Track is null)
        {
            var activate = new Button
            {
                Content = $"◇  Activate animation for {target.Label}",
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            activate.Click += (_, _) =>
            {
                document.AddTrack(
                    target.FieldId,
                    target.TargetId,
                    ValueNode(target.Input.ValueKind, target.BaseValue),
                    target.Input.Animation!.Interpolations.First());
                saveAndReload();
            };
            return activate;
        }
        var localFrame = screenFrame - target.ScreenFrameOrigin;
        var enabledKeyframes = selected.Track.Keyframes.Where((keyframe) => keyframe.Enabled).ToList();
        var exact = enabledKeyframes.FirstOrDefault((keyframe) => keyframe.Frame == localFrame);
        var previous = enabledKeyframes.LastOrDefault((keyframe) => keyframe.Frame < localFrame);
        var next = enabledKeyframes.FirstOrDefault((keyframe) => keyframe.Frame > localFrame);
        var panel = new StackPanel { Spacing = 9 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*,Auto"), ColumnSpacing = 6 };
        AddTransportButton(header, 0, "‹", "Previous keyframe", () => { if (previous is not null) setFrame(target.ScreenFrameOrigin + previous.Frame); }, previous is not null);
        var keyframeButton = new Button
        {
            Content = exact is null ? "◇" : "◆",
            Width = 34,
            Height = 30,
            Padding = new Thickness(0),
            Foreground = exact is not null ? CurrentKeyframeBrush : ActiveTrackBrush,
        };
        EditorAccessibility.Describe(keyframeButton, exact is null ? "Create keyframe at current frame" : "Update current keyframe");
        Grid.SetColumn(keyframeButton, 1);
        header.Children.Add(keyframeButton);
        AddTransportButton(header, 2, "›", "Next keyframe", () => { if (next is not null) setFrame(target.ScreenFrameOrigin + next.Frame); }, next is not null);
        var label = new TextBlock
        {
            Text = selected.Label,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 3);
        header.Children.Add(label);
        var count = new TextBlock
        {
            Text = EditorUiText.Count(enabledKeyframes.Count, "keyframe"),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.72,
        };
        Grid.SetColumn(count, 4);
        header.Children.Add(count);
        panel.Children.Add(header);

        var effective = exact ?? previous;
        var displayedValue = effective?.Value is null ? target.BaseValue : DisplayValue(effective.Value, target.Input.ValueKind);
        var valueControl = new DictionaryFieldControl(
            new FieldValue(RuntimeInputFieldDefinitionFactory.Create(_database, node, target.Input), displayedValue),
            _dictionaryServices.ForNode(node, (_) => ""));
        var selectedInterpolation = exact?.Interpolation
            ?? target.Input.Animation!.Interpolations.First();
        void SaveValue(string value, string interpolation)
        {
            document.UpsertKeyframe(
                target.FieldId,
                target.TargetId,
                localFrame,
                ValueNode(target.Input.ValueKind, value),
                interpolation);
            saveAndReload();
        }
        keyframeButton.Click += (_, _) => SaveValue(valueControl.Value, selectedInterpolation);
        keyframeButton.IsEnabled = localFrame >= 0;
        valueControl.ValueCommitted += (_, value) => SaveValue(value, selectedInterpolation);
        panel.Children.Add(valueControl);

        var interpolation = new EditorInstantComboBox
        {
            ItemsSource = target.Input.Animation!.Interpolations
                .Select((value) => new FieldOption(value, InterpolationLabel(value)))
                .ToList(),
            MinWidth = 160,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        interpolation.SelectedItem = ((IEnumerable<FieldOption>)interpolation.ItemsSource!)
            .First((option) => option.Value == selectedInterpolation);
        interpolation.SelectionChanged += (_, _) =>
        {
            if (interpolation.SelectedItem is not FieldOption option) return;
            selectedInterpolation = option.Value;
            if (exact is not null) SaveValue(valueControl.Value, selectedInterpolation);
        };
        panel.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = "Interpolation", FontWeight = FontWeight.SemiBold },
                interpolation,
            },
        });
        if (exact is not null)
        {
            var delete = new Button
            {
                Content = "Delete keyframe",
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            delete.Click += (_, _) =>
            {
                document.RemoveKeyframe(target.FieldId, target.TargetId, localFrame);
                saveAndReload();
            };
            panel.Children.Add(delete);
        }
        return panel;
    }

    private static Control CreateMiniTimeline(
        IReadOnlyList<ResolvedAnimationTarget> targets,
        ResolvedAnimationTarget active,
        int currentFrame,
        int duration,
        Action<int> setFrame)
    {
        const double width = 520;
        var canvas = new Canvas { Width = width, Height = 30, HorizontalAlignment = HorizontalAlignment.Left };
        var line = new Border
        {
            Width = width,
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(Color.Parse("#4B5563")),
        };
        Canvas.SetTop(line, 13);
        canvas.Children.Add(line);
        foreach (var target in targets.Where((candidate) => candidate.Track is not null))
        {
            foreach (var keyframe in target.Track!.Keyframes.Where((candidate) => candidate.Enabled))
            {
                var screenKeyframe = target.Target!.ScreenFrameOrigin + keyframe.Frame;
                var isActive = ReferenceEquals(target, active);
                var isCurrent = isActive && screenKeyframe == currentFrame;
                var marker = new Button
                {
                    Content = "◆",
                    FontSize = 13,
                    Width = 24,
                    Height = 24,
                    Padding = new Thickness(0),
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    Foreground = isCurrent ? CurrentKeyframeBrush : isActive ? ActiveTrackBrush : OtherKeyframeBrush,
                };
                marker.Click += (_, _) => setFrame(screenKeyframe);
                Canvas.SetLeft(marker, Math.Max(0, Math.Min(width - 24, screenKeyframe / (double)Math.Max(1, duration - 1) * (width - 24))));
                Canvas.SetTop(marker, 3);
                canvas.Children.Add(marker);
            }
        }
        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = canvas,
        };
    }

    private IReadOnlyList<AnimationTarget> ReadTargets(ProjectTreeNode node, JsonObject preview, JsonObject config)
    {
        var result = new List<AnimationTarget>();
        foreach (var input in ComponentPreviewInputSession.ReadRuntimeInputs(preview, config).Where((input) => input.Animation is not null))
            result.Add(new AnimationTarget(input.Id, "", input.Label, input, DesignPreviewTestValues.Value(preview, input), 0));
        foreach (var collection in ComponentPreviewInputSession.ReadRuntimeCollections(preview, config))
        {
            var items = DesignPreviewTestValues.CollectionItems(preview, collection);
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var targetId = item["id"]?.GetValue<string>() ?? "";
                foreach (var input in collection.Fields.Where((input) => input.Animation is not null))
                    result.Add(new AnimationTarget(
                        input.Id,
                        targetId,
                        $"{collection.ItemLabel} {index + 1} · {input.Label}",
                        input,
                        DesignPreviewTestValues.CollectionValue(item, input),
                        RuntimeAnimationFrameOrigin.ScreenFrame(preview, preview, input.Id, targetId)));
            }
        }
        return result;
    }

    private static JsonNode ValueNode(ValueKind kind, string value) => kind switch
    {
        ValueKind.Boolean => JsonValue.Create(bool.TryParse(value, out var boolean) && boolean)!,
        ValueKind.Integer => JsonValue.Create(int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) ? integer : 0)!,
        ValueKind.Decimal => JsonValue.Create(decimal.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0)!,
        _ => JsonValue.Create(value)!,
    };

    private static string DisplayValue(JsonNode value, ValueKind kind)
    {
        if (kind == ValueKind.Boolean && value is JsonValue boolean && boolean.TryGetValue<bool>(out var flag)) return flag ? "true" : "false";
        if (value is JsonValue scalar && scalar.TryGetValue<string>(out var text)) return text;
        return value.ToJsonString().Trim('"');
    }

    private static string InterpolationLabel(string interpolation) => interpolation switch
    {
        "writeOn" => "Write-on",
        "easeInOut" => "Ease in/out",
        "linear" => "Linear",
        _ => "Hold",
    };

    private static void AddTransportButton(
        Grid host,
        int column,
        string content,
        string accessibleName,
        Action action,
        bool enabled = true)
    {
        var button = new Button
        {
            Content = content,
            Width = 34,
            Height = 30,
            Padding = new Thickness(0),
            IsEnabled = enabled,
        };
        EditorAccessibility.Describe(button, accessibleName);
        button.Click += (_, _) => action();
        Grid.SetColumn(button, column);
        host.Children.Add(button);
    }

    private sealed record AnimationTarget(
        string FieldId,
        string TargetId,
        string Label,
        ComponentInputDefinition Input,
        string BaseValue,
        int ScreenFrameOrigin);

    private sealed record ResolvedAnimationTarget(
        AnimationTarget? Target,
        AnimationTrackView? Track)
    {
        public string Label => Target?.Label ?? $"Missing target · {Track!.FieldId}";
    }
}
