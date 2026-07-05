using Avalonia.Controls;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorPreviewController
{
    private readonly SpikeDatabase _database;
    private readonly EditorInstantComboBox _deviceComboBox;
    private readonly EditorInstantComboBox _themeComboBox;
    private readonly EditorInstantComboBox _modeComboBox;
    private readonly EditorInstantComboBox _scaleComboBox;
    private readonly ToggleSwitch _marksToggle;
    private readonly IEditorShellMessageSink _messages;
    private readonly Func<bool> _isDark;
    private readonly Func<ProjectTreeNode?> _selectedNode;
    private readonly RuntimeWebPreviewPane _runtimePreviewPane = new();
    private readonly DesignWebPreviewPane _designPreviewPane = new();
    private readonly VisualIrDesignPreviewPane _visualIrPreviewPane = new();
    private string? _projectId;
    private string? _selectedThemeId;
    private ProjectTreeNode? _lastDesignPreviewNode;
    private string _selectedMode = "light";
    private string _selectedScale = "fit";
    private bool _showDesignMarks = true;
    private bool _isRefreshingOptions;

    public EditorPreviewController(
        SpikeDatabase database,
        EditorInstantComboBox deviceComboBox,
        EditorInstantComboBox themeComboBox,
        EditorInstantComboBox modeComboBox,
        EditorInstantComboBox scaleComboBox,
        ToggleSwitch marksToggle,
        IEditorShellMessageSink messages,
        ContentControl runtimePreviewHost,
        ContentControl designPreviewHost,
        ContentControl visualIrPreviewHost,
        Func<bool> isDark,
        Func<ProjectTreeNode?> selectedNode)
    {
        _database = database;
        _deviceComboBox = deviceComboBox;
        _themeComboBox = themeComboBox;
        _modeComboBox = modeComboBox;
        _scaleComboBox = scaleComboBox;
        _marksToggle = marksToggle;
        _messages = messages;
        _isDark = isDark;
        _selectedNode = selectedNode;

        runtimePreviewHost.Content = _runtimePreviewPane;
        designPreviewHost.Content = _designPreviewPane;
        visualIrPreviewHost.Content = _visualIrPreviewPane;
    }

    public string? SelectedDeviceId { get; private set; }

    public void Initialize(IReadOnlyList<ProjectTreeNode> treeRoots)
    {
        RefreshOptions(treeRoots);
    }

    public void RefreshOptions(IReadOnlyList<ProjectTreeNode> treeRoots)
    {
        var project = treeRoots.FirstOrDefault((node) => node.Kind == ProjectTreeNodeKind.Project);
        if (project is null) return;

        _projectId = project.Id;
        _isRefreshingOptions = true;
        try
        {
            var deviceOptions = _database.GetDeviceOptions(project.Id);
            _deviceComboBox.ItemsSource = deviceOptions;
            var selectedDevice = !string.IsNullOrWhiteSpace(SelectedDeviceId)
                ? deviceOptions.FirstOrDefault((option) => option.Value == SelectedDeviceId)
                : null;
            selectedDevice ??= deviceOptions.FirstOrDefault();
            _deviceComboBox.SelectedItem = selectedDevice;
            SelectedDeviceId = selectedDevice?.Value;

            var themeOptions = _database.GetThemeOptions(project.Id);
            _themeComboBox.ItemsSource = themeOptions;
            var selectedTheme = !string.IsNullOrWhiteSpace(_selectedThemeId)
                ? themeOptions.FirstOrDefault((option) => option.Value == _selectedThemeId)
                : null;
            selectedTheme ??= themeOptions.FirstOrDefault();
            _themeComboBox.SelectedItem = selectedTheme;
            _selectedThemeId = selectedTheme?.Value;

            var modeOptions = new[]
            {
                new FieldOption("light", "Light"),
                new FieldOption("dark", "Dark"),
            };
            _modeComboBox.ItemsSource = modeOptions;
            _modeComboBox.SelectedItem = modeOptions.FirstOrDefault((option) => option.Value == _selectedMode) ?? modeOptions[0];
            _selectedMode = _modeComboBox.SelectedItem?.Value ?? "light";

            var scaleOptions = new[]
            {
                new FieldOption("fit", "Fit"),
                new FieldOption("actual", "1:1"),
                new FieldOption("2x", "2:1"),
                new FieldOption("3x", "3:1"),
                new FieldOption("4x", "4:1"),
            };
            _scaleComboBox.ItemsSource = scaleOptions;
            _scaleComboBox.SelectedItem = scaleOptions.FirstOrDefault((option) => option.Value == _selectedScale) ?? scaleOptions[0];
            _selectedScale = _scaleComboBox.SelectedItem?.Value ?? "fit";
            _marksToggle.IsChecked = _showDesignMarks;
        }
        finally
        {
            _isRefreshingOptions = false;
        }

        Refresh();
    }

    public void OnDeviceChanged()
    {
        if (_deviceComboBox.SelectedItem is not { } option) return;

        SelectedDeviceId = option.Value;
        if (!_isRefreshingOptions)
        {
            Refresh();
        }
    }

    public void OnThemeChanged()
    {
        if (_themeComboBox.SelectedItem is not { } option) return;

        _selectedThemeId = option.Value;
        if (!_isRefreshingOptions)
        {
            Refresh();
        }
    }

    public void OnModeChanged()
    {
        if (_modeComboBox.SelectedItem is not { } option) return;

        _selectedMode = option.Value;
        if (!_isRefreshingOptions)
        {
            Refresh();
        }
    }

    public void OnScaleChanged()
    {
        if (_scaleComboBox.SelectedItem is not { } option) return;

        _selectedScale = option.Value;
        if (!_isRefreshingOptions)
        {
            Refresh();
        }
    }

    public void OnMarksChanged()
    {
        _showDesignMarks = _marksToggle.IsChecked == true;
        if (!_isRefreshingOptions)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        try
        {
            EnsureSelectedOptionsExist();
            if (string.IsNullOrWhiteSpace(SelectedDeviceId))
            {
                _messages.Warning("Preview", "No device selected.");
                return;
            }

            var metrics = _database.GetDevicePreviewMetrics(SelectedDeviceId);
            var themeName = _themeComboBox.SelectedItem?.Label ?? "No theme";
            _runtimePreviewPane.Update(metrics, _isDark(), themeName, _selectedMode, _selectedScale);
            var designPayload = DesignPreviewPayloadForSelection();
            _designPreviewPane.Update(
                metrics,
                _isDark(),
                themeName,
                _selectedMode,
                _selectedScale,
                designPayload);
            var irMessage = _visualIrPreviewPane.Update(
                metrics,
                _isDark(),
                themeName,
                _selectedMode,
                _selectedScale,
                _showDesignMarks,
                designPayload);
            if (string.IsNullOrWhiteSpace(irMessage))
            {
                _messages.Clear();
            }
            else
            {
                _messages.Error("IR", irMessage);
            }
        }
        catch (Exception exception)
        {
            _messages.Error("Preview", exception);
        }
    }

    private DesignPreviewPayload? DesignPreviewPayloadForSelection()
    {
        var selectedNode = _selectedNode();
        var selectedPayload = DesignPreviewPayloadFactory.Create(_database, selectedNode, _selectedThemeId);
        if (selectedPayload is not null)
        {
            _lastDesignPreviewNode = selectedNode;
            return selectedPayload;
        }

        return _lastDesignPreviewNode is null
            ? null
            : DesignPreviewPayloadFactory.Create(_database, _lastDesignPreviewNode, _selectedThemeId);
    }

    private void EnsureSelectedOptionsExist()
    {
        if (string.IsNullOrWhiteSpace(_projectId) || _isRefreshingOptions)
        {
            return;
        }

        var deviceOptions = _database.GetDeviceOptions(_projectId);
        var selectedDevice = !string.IsNullOrWhiteSpace(SelectedDeviceId)
            ? deviceOptions.FirstOrDefault((option) => option.Value == SelectedDeviceId)
            : null;
        selectedDevice ??= deviceOptions.FirstOrDefault();

        var themeOptions = _database.GetThemeOptions(_projectId);
        var selectedTheme = !string.IsNullOrWhiteSpace(_selectedThemeId)
            ? themeOptions.FirstOrDefault((option) => option.Value == _selectedThemeId)
            : null;
        selectedTheme ??= themeOptions.FirstOrDefault();

        _isRefreshingOptions = true;
        try
        {
            _deviceComboBox.ItemsSource = deviceOptions;
            _deviceComboBox.SelectedItem = selectedDevice;
            SelectedDeviceId = selectedDevice?.Value;

            _themeComboBox.ItemsSource = themeOptions;
            _themeComboBox.SelectedItem = selectedTheme;
            _selectedThemeId = selectedTheme?.Value;
        }
        finally
        {
            _isRefreshingOptions = false;
        }
    }
}
