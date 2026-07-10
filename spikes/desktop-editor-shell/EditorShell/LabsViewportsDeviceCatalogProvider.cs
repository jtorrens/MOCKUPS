using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

/// <summary>
/// Reads the MIT-licensed labs-viewports catalog directly from its public source.
/// Every explicit search fetches the latest catalog; details reuse that search
/// result so selecting a device does not require a second download.
/// </summary>
internal sealed class LabsViewportsDeviceCatalogProvider : IDeviceCatalogProvider
{
    private const string SourceName = "labs-viewports";
    private const string CatalogUrl = "https://raw.githubusercontent.com/bitcomplete/labs-viewports/main/items.json";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(12) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private IReadOnlyList<CatalogItem>? _lastSearchItems;

    public async Task<IReadOnlyList<DeviceCatalogCandidate>> SearchAsync(
        string manufacturer,
        string modelQuery,
        CancellationToken cancellationToken)
    {
        var queryTokens = SearchText.Tokens($"{manufacturer} {modelQuery}");
        if (queryTokens.Length == 0) return [];

        var items = await DownloadCatalogAsync(cancellationToken);
        _lastSearchItems = items;
        return items
            .Where((item) => item.IsPhone)
            .Where((item) => Matches(item, queryTokens))
            .OrderBy((item) => item.Brand)
            .ThenBy((item) => item.Name)
            .Take(40)
            .Select((item) => new DeviceCatalogCandidate(
                item.Id,
                item.Name,
                item.Brand,
                item.Name,
                "",
                SourceName))
            .ToList();
    }

    public async Task<DeviceCatalogDetails?> GetDetailsAsync(
        DeviceCatalogCandidate candidate,
        CancellationToken cancellationToken)
    {
        var items = _lastSearchItems ?? await DownloadCatalogAsync(cancellationToken);
        var item = items.FirstOrDefault((entry) => entry.Id == candidate.Id);
        if (item is null || !item.HasUsableSizes) return null;

        return new DeviceCatalogDetails(
            item.Name,
            item.Brand,
            item.Name,
            OsFamily(item),
            item.ViewportWidth,
            item.ViewportHeight,
            item.ScreenWidth,
            item.ScreenHeight,
            SourceName);
    }

    private static async Task<IReadOnlyList<CatalogItem>> DownloadCatalogAsync(CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(CatalogUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var items = await JsonSerializer.DeserializeAsync<List<CatalogItem>>(stream, JsonOptions, cancellationToken);
        return items ?? [];
    }

    private static bool Matches(CatalogItem item, IReadOnlyList<string> queryTokens)
    {
        var text = SearchText.Normalize($"{item.Brand} {item.Name}");
        return queryTokens.All((token) => text.Contains(token, StringComparison.Ordinal));
    }

    private static string OsFamily(CatalogItem item)
    {
        if (item.Brand.Equals("Apple", StringComparison.OrdinalIgnoreCase)) return "ios";
        return "android";
    }

    private sealed class CatalogItem
    {
        public string Id { get; init; } = "";
        public Labels Labels { get; init; } = new();
        public Properties Properties { get; init; } = new();

        public string Name => Labels.Primary;
        public string Brand => Properties.Brand;
        public bool IsPhone => Properties.DeviceType.Equals("phone", StringComparison.OrdinalIgnoreCase);
        public int ScreenWidth => Properties.Screen.Width;
        public int ScreenHeight => Properties.Screen.Height;
        public int ViewportWidth => Properties.Viewport.Width;
        public int ViewportHeight => Properties.Viewport.Height;
        public bool HasUsableSizes => ScreenWidth > 0 && ScreenHeight > 0 && ViewportWidth > 0 && ViewportHeight > 0;
    }

    private sealed class Labels
    {
        public string Primary { get; init; } = "";
    }

    private sealed class Properties
    {
        public string Brand { get; init; } = "";
        public string DeviceType { get; init; } = "";
        public Size Screen { get; init; } = new();
        public Size Viewport { get; init; } = new();
        public string? OperatingSystem { get; init; }
    }

    private sealed class Size
    {
        public int Width { get; init; }
        public int Height { get; init; }
    }
}
