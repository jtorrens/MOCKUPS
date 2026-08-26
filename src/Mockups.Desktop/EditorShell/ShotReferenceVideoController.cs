using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ShotReferenceVideoController : IDisposable
{
    private readonly Window _owner;
    private readonly IProjectPathResolver _projectPaths;
    private readonly IEditorShellMessageSink _messages;
    private readonly ReferenceVideoHttpSource _mediaSource = new();
    private readonly Func<string, ShotReferenceVideoDocument, Task> _commit;
    private readonly Action<int> _setShotFrame;
    private readonly Action _toggleShotPlayback;
    private readonly NativeWebView _video = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        Background = Brushes.Black,
    };
    private readonly Canvas _markerStrip = new()
    {
        Height = 32,
        Background = new SolidColorBrush(Color.Parse("#222831")),
        ClipToBounds = true,
    };
    private readonly TextBox _markerText = new()
    {
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        MinHeight = 64,
        PlaceholderText = "Marker text",
        IsEnabled = false,
    };
    private readonly Button _deleteMarker = new()
    {
        Content = "Delete marker",
        IsEnabled = false,
    };
    private readonly Button _audioButton = new()
    {
        Content = "Audio muted",
    };
    private readonly Button _setInButton = new()
    {
        Content = "Set In",
    };
    private readonly TextBlock _inStatus = new()
    {
        Text = "In not set · reference playhead active",
        VerticalAlignment = VerticalAlignment.Center,
        Opacity = 0.72,
    };
    private readonly Button _previousFrameButton = new()
    {
        Content = "‹",
        MinWidth = 38,
    };
    private readonly Button _playButton = new()
    {
        Content = "Play",
        MinWidth = 68,
    };
    private readonly Button _nextFrameButton = new()
    {
        Content = "›",
        MinWidth = 38,
    };
    private readonly Slider _frameSlider = new()
    {
        Minimum = 0,
        Maximum = 0,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };
    private readonly TextBlock _frameText = new()
    {
        Text = "0 / 0",
        MinWidth = 92,
        TextAlignment = TextAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly DispatcherTimer _metadataTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(350),
    };
    private SukiWindow? _window;
    private ShotReferenceVideoDocument _document = ShotReferenceVideoDocument.Empty;
    private string _shotId = "";
    private string _loadedSource = "";
    private string _selectedMarkerId = "";
    private int _frameRate = 25;
    private int _shotFrame;
    private int _videoDurationFrames;
    private bool _isPlaying;
    private bool _isMuted = true;
    private bool _reportedMediaError;
    private bool _isUpdatingFrameSlider;
    private bool _disposed;

    public ShotReferenceVideoController(
        Window owner,
        IProjectPathResolver projectPaths,
        IEditorShellMessageSink messages,
        Func<string, ShotReferenceVideoDocument, Task> commit,
        Action<int> setShotFrame,
        Action toggleShotPlayback)
    {
        _owner = owner;
        _projectPaths = projectPaths;
        _messages = messages;
        _commit = commit;
        _setShotFrame = setShotFrame;
        _toggleShotPlayback = toggleShotPlayback;
        _markerStrip.SizeChanged += (_, _) => RebuildMarkers();
        EditorTextBoxBehavior.AttachCommit(
            _markerText,
            ValueKind.StringSingleLine,
            CommitSelectedMarkerText);
        _deleteMarker.Click += async (_, _) => await DeleteSelectedMarkerAsync();
        _audioButton.Click += (_, _) =>
        {
            _isMuted = !_isMuted;
            _audioButton.Content = _isMuted ? "Audio muted" : "Audio on";
            SyncVideo();
        };
        _setInButton.Click += async (_, _) => await SetInAsync();
        _previousFrameButton.Click += async (_, _) => await MoveFrameAsync(-1);
        _playButton.Click += async (_, _) => await ToggleNavigatorPlaybackAsync();
        _nextFrameButton.Click += async (_, _) => await MoveFrameAsync(1);
        _frameSlider.ValueChanged += async (_, _) =>
        {
            if (_isUpdatingFrameSlider) return;
            await SetNavigatorFrameAsync((int)Math.Round(_frameSlider.Value));
        };
        _metadataTimer.Tick += async (_, _) => await RefreshVideoStateAsync();
    }

    public bool IsVisible => _window?.IsVisible == true;

    public void SetContext(
        string shotId,
        int frameRate,
        int shotFrame,
        bool isPlaying,
        ShotReferenceVideoDocument document)
    {
        var documentChanged = !ReferenceEquals(_document, document);
        var sourceOwnerChanged = !_shotId.Equals(shotId, StringComparison.Ordinal);
        var rateChanged = _frameRate != Math.Max(1, frameRate);
        _shotId = shotId;
        _frameRate = Math.Max(1, frameRate);
        _shotFrame = Math.Max(0, shotFrame);
        _isPlaying = isPlaying;
        _document = document;
        if (string.IsNullOrWhiteSpace(shotId) && _window?.IsVisible == true)
        {
            _window.Hide();
            _metadataTimer.Stop();
        }
        if ((documentChanged || sourceOwnerChanged || rateChanged)
            && _window?.IsVisible == true)
        {
            EnsureSourceLoaded();
            RebuildMarkers();
        }
        UpdateInPresentation();
        UpdateNavigatorFromOwner();
        SyncVideo();
    }

    public void Toggle()
    {
        if (_disposed || string.IsNullOrWhiteSpace(_shotId)) return;
        if (_window?.IsVisible == true)
        {
            _window.Hide();
            _metadataTimer.Stop();
            _ = InvokeVideoAsync("window.mockupsReference.pause()");
            return;
        }

        var window = EnsureWindow();
        if (!string.IsNullOrWhiteSpace(_document.SourcePath))
        {
            var localPath = _projectPaths.ResolveProjectPath(
                _document.SourcePath);
            if (!File.Exists(localPath))
            {
                _messages.Error(
                    "Reference video",
                    $"The associated video cannot be loaded because the file does not exist or is unavailable: {localPath}");
            }
        }
        window.Show(_owner);
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || window.IsVisible != true) return;
            EnsureSourceLoaded(force: true);
            _metadataTimer.Start();
            SyncVideo();
        });
    }

    public void Close()
    {
        _metadataTimer.Stop();
        _window?.Close();
        _window = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
        _mediaSource.Dispose();
    }

    private SukiWindow EnsureWindow()
    {
        if (_window is not null) return _window;

        var addMarker = new Button { Content = "Add marker" };
        addMarker.Click += async (_, _) => await AddMarkerAsync();
        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _setInButton, addMarker, _audioButton, _inStatus },
        };
        var navigator = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*,Auto"),
            ColumnSpacing = 8,
            Children =
            {
                _previousFrameButton,
                _playButton,
                _nextFrameButton,
                _frameSlider,
                _frameText,
            },
        };
        Grid.SetColumn(_playButton, 1);
        Grid.SetColumn(_nextFrameButton, 2);
        Grid.SetColumn(_frameSlider, 3);
        Grid.SetColumn(_frameText, 4);
        var markerEditor = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Children = { _markerText },
        };
        Grid.SetColumn(_deleteMarker, 1);
        markerEditor.Children.Add(_deleteMarker);
        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto,Auto,Auto,Auto"),
            RowSpacing = 8,
            Margin = new Thickness(10),
            Children = { _video },
        };
        Grid.SetRow(controls, 1);
        Grid.SetRow(navigator, 2);
        Grid.SetRow(_markerStrip, 3);
        Grid.SetRow(markerEditor, 4);
        layout.Children.Add(controls);
        layout.Children.Add(navigator);
        layout.Children.Add(_markerStrip);
        layout.Children.Add(markerEditor);
        var window = new SukiWindow
        {
            Title = "Shot reference video",
            Width = 720,
            Height = 560,
            MinWidth = 420,
            MinHeight = 360,
            CanResize = true,
            Topmost = false,
            Content = layout,
        };
        window.Closing += (_, args) =>
        {
            if (_disposed) return;
            args.Cancel = true;
            window.Hide();
            _metadataTimer.Stop();
            _ = InvokeVideoAsync("window.mockupsReference.pause()");
        };
        _window = window;
        return window;
    }

    private void EnsureSourceLoaded(bool force = false)
    {
        var localPath = string.IsNullOrWhiteSpace(_document.SourcePath)
            ? ""
            : _projectPaths.ResolveProjectPath(_document.SourcePath);
        string source = "";
        if (File.Exists(localPath))
        {
            try
            {
                source = _mediaSource.Use(
                    ReferenceVideoPlaybackCache.Resolve(localPath));
            }
            catch (Exception exception)
            {
                _messages.Error(
                    "Reference video",
                    $"The associated video could not be prepared for playback: {exception.Message}");
            }
        }
        if (!force && source.Equals(_loadedSource, StringComparison.Ordinal)) return;
        _loadedSource = source;
        _videoDurationFrames = 0;
        _reportedMediaError = false;
        var sourceJson = JsonSerializer.Serialize(source);
        var html = $$$"""
            <!doctype html><html><head><meta charset="utf-8"><style>
            html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#090b0e;color:#cbd3df;font:600 24px system-ui}
            #v{width:100%;height:100%;object-fit:contain;background:#000}#empty{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;background:#111821;color:#aeb8c8}
            </style></head><body><video id="v" playsinline preload="metadata"></video><div id="empty">Sin media</div><script>
            const v=document.getElementById('v'), empty=document.getElementById('empty');
            const source={{{sourceJson}}};
            const available=()=>source&&v.readyState>=1&&Number.isFinite(v.duration)&&v.duration>0;
            const present=(visible)=>{empty.style.display=visible?'none':'flex';v.style.visibility=visible?'visible':'hidden';};
            if(source){v.src=source;v.load();}
            window.mockupsReference={
              setIndependent(muted){v.muted=muted;const valid=available();present(valid);return valid;},
              seekFrame(frame,fps){const valid=available();present(valid);if(!valid)return false;v.pause();try{v.currentTime=Math.max(0,frame)/Math.max(1,fps);}catch(e){}return true;},
              togglePlay(){if(!available())return false;if(v.paused){const p=v.play();if(p&&p.catch)p.catch(()=>{});}else v.pause();return true;},
              setFrame(frame,fps,playing,muted){
                v.muted=muted; const target=Math.max(0,frame)/Math.max(1,fps);
                const valid=available()&&target<v.duration;present(valid);
                if(!valid){v.pause();return false;}
                if(!playing||Math.abs(v.currentTime-target)>.12){try{v.currentTime=target;}catch(e){}}
                if(playing){const p=v.play();if(p&&p.catch)p.catch(()=>{});}else v.pause(); return true;
              },
              state(){return JSON.stringify({duration:Number.isFinite(v.duration)?v.duration:0,current:Number.isFinite(v.currentTime)?v.currentTime:0,ready:v.readyState,paused:v.paused,error:v.error?.message||''});},
              pause(){v.pause();return true;}
            };
            </script></body></html>
            """;
        _video.NavigateToString(
            html,
            _mediaSource.BaseUri);
    }

    private void SyncVideo()
    {
        if (_window?.IsVisible != true) return;
        if (_document.InFrame is not { } inFrame)
        {
            _ = InvokeVideoAsync(
                $"window.mockupsReference.setIndependent({_isMuted.ToString().ToLowerInvariant()})");
            return;
        }
        var videoFrame = inFrame + _shotFrame;
        _ = InvokeVideoAsync(
            $"window.mockupsReference.setFrame({videoFrame},{_frameRate},{_isPlaying.ToString().ToLowerInvariant()},{_isMuted.ToString().ToLowerInvariant()})");
    }

    private async Task RefreshVideoStateAsync()
    {
        var state = await VideoStateAsync();
        if (state is null) return;
        if (!string.IsNullOrWhiteSpace(state.Value.Error)
            && !_reportedMediaError)
        {
            _reportedMediaError = true;
            _messages.Error(
                "Reference video",
                $"The associated video could not be decoded: {state.Value.Error}");
        }
        var durationFrames = Math.Max(0, (int)Math.Floor(state.Value.Duration * _frameRate));
        if (durationFrames != _videoDurationFrames)
        {
            _videoDurationFrames = durationFrames;
            RebuildMarkers();
        }
        UpdateNavigator(
            _document.InFrame is { } inFrame
                ? inFrame + _shotFrame
                : Math.Max(0, (int)Math.Round(
                    state.Value.Current * _frameRate,
                    MidpointRounding.AwayFromZero)),
            state.Value.Paused);
        SyncVideo();
    }

    private async Task SetInAsync()
    {
        var frame = await CurrentVideoFrameAsync();
        if (frame is null || string.IsNullOrWhiteSpace(_shotId)) return;
        await CommitAsync(_document with { InFrame = frame.Value });
    }

    private async Task AddMarkerAsync()
    {
        var frame = await CurrentVideoFrameAsync();
        if (frame is null || string.IsNullOrWhiteSpace(_shotId)) return;
        var marker = new ShotReferenceVideoMarker(
            $"marker_{Guid.NewGuid():N}",
            frame.Value,
            "");
        await CommitAsync(_document with
        {
            Markers = _document.Markers.Append(marker).ToArray(),
        });
        SelectMarker(marker.Id);
    }

    private async Task DeleteSelectedMarkerAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedMarkerId)) return;
        var next = _document.Markers
            .Where((marker) => !marker.Id.Equals(
                _selectedMarkerId,
                StringComparison.Ordinal))
            .ToArray();
        _selectedMarkerId = "";
        await CommitAsync(_document with { Markers = next });
        SelectMarker("");
    }

    private void CommitSelectedMarkerText()
    {
        if (string.IsNullOrWhiteSpace(_selectedMarkerId)) return;
        var text = _markerText.Text ?? "";
        var current = _document.Markers.FirstOrDefault((marker) =>
            marker.Id.Equals(_selectedMarkerId, StringComparison.Ordinal));
        if (current is null || current.Text.Equals(text, StringComparison.Ordinal))
        {
            return;
        }
        _ = CommitAsync(_document with
        {
            Markers = _document.Markers.Select((marker) =>
                marker.Id.Equals(_selectedMarkerId, StringComparison.Ordinal)
                    ? marker with { Text = text }
                    : marker).ToArray(),
        });
    }

    private async Task CommitAsync(ShotReferenceVideoDocument next)
    {
        var shotId = _shotId;
        await _commit(shotId, next);
        if (!shotId.Equals(_shotId, StringComparison.Ordinal)) return;
        _document = next;
        UpdateInPresentation();
        UpdateNavigatorFromOwner();
        RebuildMarkers();
        SyncVideo();
    }

    private void RebuildMarkers()
    {
        _markerStrip.Children.Clear();
        var duration = Math.Max(
            1,
            _videoDurationFrames > 0
                ? _videoDurationFrames
                : _document.Markers.Select((marker) => marker.VideoFrame + 1).DefaultIfEmpty(1).Max());
        foreach (var marker in _document.Markers)
        {
            var button = new Button
            {
                Width = 12,
                Height = 26,
                Padding = new Thickness(0),
                Background = marker.Id.Equals(_selectedMarkerId, StringComparison.Ordinal)
                    ? EditorAnimationVisuals.ActiveTrackBrush
                    : new SolidColorBrush(Color.Parse("#4A90E2")),
                BorderThickness = new Thickness(0),
                Tag = marker.Id,
            };
            ToolTip.SetTip(button, string.IsNullOrWhiteSpace(marker.Text)
                ? $"Frame {marker.VideoFrame}"
                : marker.Text);
            PositionMarker(button, marker.VideoFrame, duration);
            button.Click += (_, _) => SelectMarker(marker.Id);
            var dragging = false;
            button.PointerPressed += (_, args) =>
            {
                if (!PreviewScreenTimelinePointer.IsPrimaryPress(button, args)) return;
                dragging = true;
                args.Pointer.Capture(button);
                args.Handled = true;
            };
            button.PointerMoved += (_, args) =>
            {
                if (!dragging) return;
                var x = args.GetPosition(_markerStrip).X;
                var frame = Math.Clamp(
                    (int)Math.Round(x / Math.Max(1, _markerStrip.Bounds.Width) * duration),
                    0,
                    duration);
                PositionMarker(button, frame, duration);
                button.Tag = $"{marker.Id}|{frame}";
            };
            button.PointerReleased += async (_, args) =>
            {
                if (!dragging) return;
                dragging = false;
                args.Pointer.Capture(null);
                var parts = button.Tag?.ToString()?.Split('|');
                if (parts is not { Length: 2 }
                    || !int.TryParse(parts[1], out var frame)) return;
                await CommitAsync(_document with
                {
                    Markers = _document.Markers.Select((candidate) =>
                        candidate.Id.Equals(marker.Id, StringComparison.Ordinal)
                            ? candidate with { VideoFrame = frame }
                            : candidate).ToArray(),
                });
            };
            _markerStrip.Children.Add(button);
        }
    }

    private void PositionMarker(Control control, int frame, int duration)
    {
        Canvas.SetLeft(
            control,
            Math.Clamp(frame / (double)Math.Max(1, duration), 0, 1)
            * Math.Max(0, _markerStrip.Bounds.Width - control.Width));
        Canvas.SetTop(control, 3);
    }

    private void SelectMarker(string markerId)
    {
        _selectedMarkerId = markerId;
        var marker = _document.Markers.FirstOrDefault((candidate) =>
            candidate.Id.Equals(markerId, StringComparison.Ordinal));
        _markerText.Text = marker?.Text ?? "";
        _markerText.IsEnabled = marker is not null;
        _deleteMarker.IsEnabled = marker is not null;
        RebuildMarkers();
    }

    private async Task<int?> CurrentVideoFrameAsync()
    {
        var state = await VideoStateAsync();
        return state is null
            ? null
            : Math.Max(0, (int)Math.Round(
                state.Value.Current * _frameRate,
                MidpointRounding.AwayFromZero));
    }

    private async Task<(double Duration, double Current, int Ready, bool Paused, string Error)?> VideoStateAsync()
    {
        try
        {
            var result = await _video.InvokeScript(
                "window.mockupsReference ? window.mockupsReference.state() : ''");
            var text = WebViewScriptResult.Text(result);
            if (string.IsNullOrWhiteSpace(text)) return null;
            using var json = JsonDocument.Parse(text);
            return (
                json.RootElement.GetProperty("duration").GetDouble(),
                json.RootElement.GetProperty("current").GetDouble(),
                json.RootElement.GetProperty("ready").GetInt32(),
                json.RootElement.GetProperty("paused").GetBoolean(),
                json.RootElement.GetProperty("error").GetString() ?? "");
        }
        catch
        {
            return null;
        }
    }

    private void UpdateInPresentation()
    {
        if (_document.InFrame is { } inFrame)
        {
            _setInButton.Content = "Change In";
            _inStatus.Text = $"In {inFrame} · synced to Shot";
            return;
        }

        _setInButton.Content = "Set In";
        _inStatus.Text = "In not set · reference playhead active";
    }

    private async Task MoveFrameAsync(int delta)
    {
        var current = _document.InFrame is { } inFrame
            ? inFrame + _shotFrame
            : await CurrentVideoFrameAsync() ?? 0;
        await SetNavigatorFrameAsync(current + delta);
    }

    private async Task SetNavigatorFrameAsync(int videoFrame)
    {
        var frame = Math.Clamp(
            videoFrame,
            0,
            Math.Max(0, _videoDurationFrames - 1));
        if (_document.InFrame is { } inFrame)
        {
            _setShotFrame(frame - inFrame);
            return;
        }

        await InvokeVideoAsync(
            $"window.mockupsReference.seekFrame({frame},{_frameRate})");
        UpdateNavigator(frame, paused: true);
    }

    private async Task ToggleNavigatorPlaybackAsync()
    {
        if (_document.InFrame is not null)
        {
            _toggleShotPlayback();
            return;
        }

        await InvokeVideoAsync("window.mockupsReference.togglePlay()");
        await RefreshVideoStateAsync();
    }

    private void UpdateNavigatorFromOwner()
    {
        var videoFrame = _document.InFrame is { } inFrame
            ? inFrame + _shotFrame
            : (int)Math.Round(_frameSlider.Value);
        UpdateNavigator(videoFrame, paused: !_isPlaying);
    }

    private void UpdateNavigator(int videoFrame, bool paused)
    {
        var maximum = Math.Max(0, _videoDurationFrames - 1);
        var frame = Math.Clamp(videoFrame, 0, maximum);
        _isUpdatingFrameSlider = true;
        _frameSlider.Maximum = maximum;
        _frameSlider.Value = frame;
        _isUpdatingFrameSlider = false;
        _frameText.Text = $"{frame} / {maximum}";
        _previousFrameButton.IsEnabled = frame > 0;
        _nextFrameButton.IsEnabled = frame < maximum;
        _playButton.Content = paused ? "Play" : "Pause";
    }

    private async Task InvokeVideoAsync(string script)
    {
        try
        {
            await _video.InvokeScript(script);
        }
        catch
        {
            // The resident player can be between navigation and DOM readiness.
        }
    }
}
