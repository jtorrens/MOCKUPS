using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record DeviceCatalogCandidate(
    string Id,
    string Name,
    string Manufacturer,
    string Model,
    string DetailUrl,
    string Source);

internal sealed record DeviceCatalogDetails(
    string Name,
    string Manufacturer,
    string Model,
    string OsFamily,
    int RenderWidth,
    int RenderHeight,
    double ScaleToPixels,
    string Source);

internal sealed record DeviceImportDraft(
    string Name,
    string Manufacturer,
    string Model,
    string OsFamily,
    string MetricsJson);

internal sealed record DeviceImportDialogResult(bool CreateBlank, DeviceImportDraft? Draft);

internal interface IDeviceCatalogProvider
{
    Task<IReadOnlyList<DeviceCatalogCandidate>> SearchAsync(
        string manufacturer,
        string modelQuery,
        CancellationToken cancellationToken);

    Task<DeviceCatalogDetails?> GetDetailsAsync(
        DeviceCatalogCandidate candidate,
        CancellationToken cancellationToken);
}
