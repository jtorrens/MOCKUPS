using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DeviceImportDialog
{
    private readonly Window _owner;
    private readonly IDeviceCatalogProvider _provider;
    private readonly TextBox _manufacturerBox = new() { PlaceholderText = "Apple, Samsung, Google..." };
    private readonly TextBox _modelBox = new() { PlaceholderText = "iPhone 15, Galaxy S24..." };
    private readonly Button _searchButton = new() { Content = "Search", MinWidth = 92 };
    private readonly Button _importButton = new() { Content = "Create from selected", MinWidth = 150, IsEnabled = false };
    private readonly Button _blankButton = new() { Content = "Blank device", MinWidth = 112 };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinWidth = 92 };
    private readonly ListBox _results = new() { MinHeight = 220 };
    private readonly TextBlock _statusText = new() { Opacity = 0.72, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _previewText = new() { Opacity = 0.78, TextWrapping = TextWrapping.Wrap };
    private List<DeviceCatalogCandidate> _candidates = [];
    private DeviceCatalogDetails? _selectedDetails;
    private CancellationTokenSource? _searchCancellation;

    public DeviceImportDialog(Window owner, IDeviceCatalogProvider provider)
    {
        _owner = owner;
        _provider = provider;
    }

    public async Task<DeviceImportDialogResult?> ShowAsync()
    {
        var dialog = CreateDialog();

        _searchButton.Click += async (_, _) => await Search(dialog);
        _importButton.Click += (_, _) =>
        {
            if (_selectedDetails is null) return;
            dialog.Close(new DeviceImportDialogResult(false, DeviceImportMapper.ToDraft(_selectedDetails)));
        };
        _blankButton.Click += (_, _) => dialog.Close(new DeviceImportDialogResult(true, null));
        _cancelButton.Click += (_, _) => dialog.Close(null);
        _results.SelectionChanged += async (_, _) => await SelectCurrent();

        return await dialog.ShowDialog<DeviceImportDialogResult?>(_owner);
    }

    private SukiWindow CreateDialog()
    {
        var dialog = new SukiWindow
        {
            Title = "Create device",
            Width = 680,
            Height = 560,
            MinWidth = 620,
            MinHeight = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };

        var searchGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,Auto"),
            ColumnSpacing = 10,
            Children =
            {
                _manufacturerBox,
                _modelBox,
                _searchButton,
            },
        };
        Grid.SetColumn(_modelBox, 1);
        Grid.SetColumn(_searchButton, 2);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children =
            {
                _cancelButton,
                _blankButton,
                _importButton,
            },
        };

        var body = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto"),
            RowSpacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = "Search by brand and model, then create a device with basic physical and design dimensions.",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.82,
                },
                searchGrid,
                _results,
                new StackPanel
                {
                    Spacing = 6,
                    Children = { _statusText, _previewText },
                },
                actions,
            },
        };
        Grid.SetRow(searchGrid, 1);
        Grid.SetRow(_results, 2);
        Grid.SetRow((Control)body.Children[3], 3);
        Grid.SetRow(actions, 4);

        dialog.Content = new Border
        {
            Padding = new Thickness(22),
            Child = body,
        };

        return dialog;
    }

    private async Task Search(Window dialog)
    {
        _searchCancellation?.Cancel();
        _searchCancellation = new CancellationTokenSource();
        _selectedDetails = null;
        _importButton.IsEnabled = false;
        _previewText.Text = "";
        _statusText.Text = "Searching...";
        _searchButton.IsEnabled = false;

        try
        {
            _candidates = (await _provider.SearchAsync(
                _manufacturerBox.Text?.Trim() ?? "",
                _modelBox.Text?.Trim() ?? "",
                _searchCancellation.Token)).ToList();
            _results.ItemsSource = _candidates.Select(DisplayCandidate).ToList();
            _statusText.Text = _candidates.Count == 0
                ? "No devices found. You can create a blank device and edit the fields manually."
                : $"{_candidates.Count} device(s) found.";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _candidates = [];
            _results.ItemsSource = Array.Empty<string>();
            _statusText.Text = $"Device catalog is unavailable: {exception.Message}";
        }
        finally
        {
            _searchButton.IsEnabled = true;
        }
    }

    private async Task SelectCurrent()
    {
        var index = _results.SelectedIndex;
        _selectedDetails = null;
        _importButton.IsEnabled = false;
        _previewText.Text = "";
        if (index < 0 || index >= _candidates.Count) return;

        var candidate = _candidates[index];
        _statusText.Text = "Loading device details...";

        try
        {
            _selectedDetails = await _provider.GetDetailsAsync(candidate, CancellationToken.None);
            if (_selectedDetails is null)
            {
                _statusText.Text = "This result did not include a usable screen resolution.";
                return;
            }

            _statusText.Text = "Ready to create.";
            _previewText.Text = $"{_selectedDetails.RenderWidth} x {_selectedDetails.RenderHeight} px · scale {_selectedDetails.ScaleToPixels:0.#} · {_selectedDetails.OsFamily}";
            _importButton.IsEnabled = true;
        }
        catch (Exception exception)
        {
            _statusText.Text = $"Could not load details: {exception.Message}";
        }
    }

    private static string DisplayCandidate(DeviceCatalogCandidate candidate)
    {
        return string.IsNullOrWhiteSpace(candidate.Manufacturer)
            ? candidate.Name
            : $"{candidate.Manufacturer} · {candidate.Name}";
    }
}
