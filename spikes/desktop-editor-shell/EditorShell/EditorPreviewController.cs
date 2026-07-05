using Avalonia.Controls;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorPreviewController
{
    private readonly SpikeDatabase _database;
    private readonly ComboBox _deviceComboBox;
    private readonly ComboBox _themeComboBox;
    private readonly ComboBox _modeComboBox;
    private readonly Func<bool> _isDark;
    private readonly Func<ProjectTreeNode?> _selectedNode;
    private readonly RuntimeWebPreviewPane _runtimePreviewPane = new();
    private readonly DesignWebPreviewPane _designPreviewPane = new();
    private readonly VisualIrDesignPreviewPane _visualIrPreviewPane = new();
    private string? _projectId;
    private string? _selectedThemeId;
    private string _selectedMode = "light";
    private bool _isRefreshingOptions;

    public EditorPreviewController(
        SpikeDatabase database,
        ComboBox deviceComboBox,
        ComboBox themeComboBox,
        ComboBox modeComboBox,
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
        _isDark = isDark;
        _selectedNode = selectedNode;

        runtimePreviewHost.Content = _runtimePreviewPane;
        designPreviewHost.Content = _designPreviewPane;
        visualIrPreviewHost.Content = _visualIrPreviewPane;
    }

    public string? SelectedDeviceId { get; private set; }

    public void Initialize(IReadOnlyList<ProjectTreeNode> treeRoots)
    {
        EditorComboBoxBehavior.Configure(_deviceComboBox);
        EditorComboBoxBehavior.Configure(_themeComboBox);
        EditorComboBoxBehavior.Configure(_modeComboBox);

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
            _selectedMode = ((FieldOption)_modeComboBox.SelectedItem).Value;
        }
        finally
        {
            _isRefreshingOptions = false;
        }

        Refresh();
    }

    public void OnDeviceChanged()
    {
        if (_deviceComboBox.SelectedItem is not FieldOption option) return;

        SelectedDeviceId = option.Value;
        if (!_isRefreshingOptions)
        {
            Refresh();
        }
    }

    public void OnThemeChanged()
    {
        if (_themeComboBox.SelectedItem is not FieldOption option) return;

        _selectedThemeId = option.Value;
        if (!_isRefreshingOptions)
        {
            Refresh();
        }
    }

    public void OnModeChanged()
    {
        if (_modeComboBox.SelectedItem is not FieldOption option) return;

        _selectedMode = option.Value;
        if (!_isRefreshingOptions)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        EnsureSelectedOptionsExist();
        if (string.IsNullOrWhiteSpace(SelectedDeviceId)) return;

        var metrics = _database.GetDevicePreviewMetrics(SelectedDeviceId);
        var themeName = _themeComboBox.SelectedItem is FieldOption option ? option.Label : "No theme";
        _runtimePreviewPane.Update(metrics, _isDark(), themeName, _selectedMode);
        var designPayload = DesignPreviewPayloadFactory.Create(_database, _selectedNode(), _selectedThemeId);
        _designPreviewPane.Update(
            metrics,
            _isDark(),
            themeName,
            _selectedMode,
            designPayload);
        _visualIrPreviewPane.Update(
            metrics,
            _isDark(),
            themeName,
            _selectedMode,
            designPayload);
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
