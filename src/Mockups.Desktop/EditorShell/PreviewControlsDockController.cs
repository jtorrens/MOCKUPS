using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Mockups.DesktopEditorShell.Common;
using SukiUI.Controls;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class PreviewControlsDockController : IDisposable
{
    private const int UtilityRowIndex = 0;
    private const int SplitterRowIndex = 1;
    private const double DefaultUtilityHeight = 240;
    private const double SplitterHeight = 10;
    private readonly Window _owner;
    private readonly Panel _headerDockHost;
    private readonly Control _headerSurface;
    private readonly Panel _utilityDockHost;
    private readonly Control _utilitySurface;
    private readonly Grid _previewGrid;
    private readonly GridSplitter _splitter;
    private readonly Button _toggleButton;
    private readonly Func<bool> _isDark;
    private readonly ContentControl _floatingHost = new()
    {
        HorizontalContentAlignment =
            Avalonia.Layout.HorizontalAlignment.Stretch,
        VerticalContentAlignment =
            Avalonia.Layout.VerticalAlignment.Stretch,
    };
    private readonly Border _floatingHeaderHost = new();
    private readonly Border _floatingUtilityHost = new();
    private SukiWindow? _floatingWindow;
    private PixelPoint? _floatingPosition;
    private double _floatingWidth = 720;
    private double _floatingHeight = 330;
    private double _dockedUtilityHeight = DefaultUtilityHeight;
    private bool _isDisposing;
    private int _transferRevision;

    public PreviewControlsDockController(
        Window owner,
        Panel headerDockHost,
        Control headerSurface,
        Panel utilityDockHost,
        Control utilitySurface,
        Grid previewGrid,
        GridSplitter splitter,
        Button toggleButton,
        Func<bool> isDark)
    {
        _owner = owner;
        _headerDockHost = headerDockHost;
        _headerSurface = headerSurface;
        _utilityDockHost = utilityDockHost;
        _utilitySurface = utilitySurface;
        _previewGrid = previewGrid;
        _splitter = splitter;
        _toggleButton = toggleButton;
        _isDark = isDark;
        _toggleButton.Background = Brushes.Transparent;
        _toggleButton.BorderBrush = Brushes.Transparent;
        _toggleButton.BorderThickness = new Thickness(0);
        _toggleButton.Click += (_, _) => Toggle();
        var floatingLayout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
        };
        floatingLayout.Children.Add(_floatingHeaderHost);
        Grid.SetRow(_floatingUtilityHost, 1);
        floatingLayout.Children.Add(_floatingUtilityHost);
        _floatingHost.Content = floatingLayout;
        RefreshTogglePresentation();
    }

    public bool IsDetached { get; private set; }
    internal Window? FloatingWindow => _floatingWindow;

    public void Toggle()
    {
        if (IsDetached)
        {
            Redock();
        }
        else
        {
            Detach();
        }
    }

    public void RefreshTheme()
    {
        if (_floatingWindow is not null)
        {
            EditorSukiWindowTheme.ApplyNeutralBackground(
                _floatingWindow,
                _isDark());
        }
    }

    public void ApplyTextScale(double scale)
    {
        if (IsDetached && _floatingWindow is not null)
        {
            EditorUiTextScale.Apply(_floatingWindow, scale);
        }
    }

    public void Dispose()
    {
        if (_isDisposing)
        {
            return;
        }

        _isDisposing = true;
        _transferRevision++;
        if (IsDetached)
        {
            _floatingHeaderHost.Child = null;
            _floatingUtilityHost.Child = null;
            IsDetached = false;
        }
        if (_floatingWindow is not null)
        {
            _floatingWindow.Close();
            _floatingWindow = null;
        }
    }

    private void Detach()
    {
        if (IsDetached)
        {
            return;
        }

        var utilityHeight =
            _previewGrid.RowDefinitions[UtilityRowIndex].ActualHeight;
        if (utilityHeight > 0)
        {
            _dockedUtilityHeight = utilityHeight;
        }
        if (_utilitySurface.Bounds.Width > 0)
        {
            _floatingWidth = Math.Max(
                640,
                _utilitySurface.Bounds.Width);
        }
        var combinedHeight =
            _headerSurface.Bounds.Height
            + _utilitySurface.Bounds.Height;
        if (combinedHeight > 0)
        {
            _floatingHeight = Math.Max(300, combinedHeight);
        }

        var window = EnsureFloatingWindow();
        _headerDockHost.Children.Remove(_headerSurface);
        _utilityDockHost.Children.Remove(_utilitySurface);
        CollapseDockRows();
        IsDetached = true;
        RefreshTogglePresentation();

        window.Width = _floatingWidth;
        window.Height = _floatingHeight;
        window.Position = _floatingPosition
            ?? new PixelPoint(
                _owner.Position.X + 48,
                _owner.Position.Y + 72);
        window.Show(_owner);
        window.Activate();
        var revision = ++_transferRevision;
        Dispatcher.UIThread.Post(
            () =>
            {
                if (!_isDisposing
                    && IsDetached
                    && revision == _transferRevision)
                {
                    _floatingHeaderHost.Child = _headerSurface;
                    _floatingUtilityHost.Child = _utilitySurface;
                }
            },
            DispatcherPriority.Loaded);
    }

    private void Redock()
    {
        if (!IsDetached)
        {
            return;
        }

        CaptureFloatingGeometry();
        _floatingHeaderHost.Child = null;
        _floatingUtilityHost.Child = null;
        RestoreDockRows();
        IsDetached = false;
        RefreshTogglePresentation();
        _floatingWindow?.Hide();
        var revision = ++_transferRevision;
        Dispatcher.UIThread.Post(
            () =>
            {
                if (!_isDisposing
                    && !IsDetached
                    && revision == _transferRevision)
                {
                    _headerDockHost.Children.Insert(
                        0,
                        _headerSurface);
                    DockPanel.SetDock(
                        _headerSurface,
                        Dock.Top);
                    _utilityDockHost.Children.Insert(
                        0,
                        _utilitySurface);
                    Grid.SetRow(
                        _utilitySurface,
                        UtilityRowIndex);
                }
            },
            DispatcherPriority.Loaded);
    }

    private SukiWindow EnsureFloatingWindow()
    {
        if (_floatingWindow is not null)
        {
            return _floatingWindow;
        }

        var window = new SukiWindow
        {
            Title = "Preview controls",
            MinWidth = 560,
            MinHeight = 280,
            Width = _floatingWidth,
            Height = _floatingHeight,
            CanResize = true,
            CanFullScreen = false,
            CanPin = false,
            ShowInTaskbar = false,
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Content = _floatingHost,
        };
        EditorSukiWindowTheme.ApplyDialogChrome(window, _owner);
        window.Closing += (_, args) =>
        {
            if (_isDisposing)
            {
                return;
            }

            args.Cancel = true;
            Redock();
        };
        _floatingWindow = window;
        return window;
    }

    private void CaptureFloatingGeometry()
    {
        if (_floatingWindow is null)
        {
            return;
        }

        _floatingPosition = _floatingWindow.Position;
        if (_floatingWindow.Bounds.Width > 0)
        {
            _floatingWidth = _floatingWindow.Bounds.Width;
        }
        if (_floatingWindow.Bounds.Height > 0)
        {
            _floatingHeight = _floatingWindow.Bounds.Height;
        }
    }

    private void CollapseDockRows()
    {
        _previewGrid.RowDefinitions[UtilityRowIndex].Height =
            new GridLength(0);
        _previewGrid.RowDefinitions[SplitterRowIndex].Height =
            new GridLength(0);
        _splitter.IsVisible = false;
    }

    private void RestoreDockRows()
    {
        _previewGrid.RowDefinitions[UtilityRowIndex].Height =
            new GridLength(_dockedUtilityHeight);
        _previewGrid.RowDefinitions[SplitterRowIndex].Height =
            new GridLength(SplitterHeight);
        _splitter.IsVisible = true;
    }

    private void RefreshTogglePresentation()
    {
        _toggleButton.Content = EditorIcons.Create(
            IsDetached ? EditorIcons.Back : EditorIcons.Open,
            15);
        EditorAccessibility.Describe(
            _toggleButton,
            IsDetached
                ? "Dock Preview controls"
                : "Detach Preview controls");
        ToolTip.SetTip(
            _toggleButton,
            IsDetached
                ? "Dock Preview controls"
                : "Detach Preview controls");
    }
}
