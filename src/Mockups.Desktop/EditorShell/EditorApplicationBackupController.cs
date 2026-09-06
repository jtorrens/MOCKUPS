using Avalonia.Controls;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal enum ApplicationBackupOutcome
{
    Published,
    Unchanged,
}

internal sealed record ApplicationBackupResult(
    ApplicationBackupOutcome Outcome,
    string? PackageName = null);

internal interface IApplicationBackupLifecycle
{
    Task<ApplicationBackupResult> PublishManualAsync(
        EditorOperationCoordinator operations);

    Task<ApplicationBackupResult> PublishCleanExitAsync(
        EditorOperationCoordinator operations);
}

internal sealed class NoApplicationBackupLifecycle
    : IApplicationBackupLifecycle
{
    public Task<ApplicationBackupResult> PublishManualAsync(
        EditorOperationCoordinator operations) =>
        Task.FromResult(
            new ApplicationBackupResult(
                ApplicationBackupOutcome.Unchanged));

    public Task<ApplicationBackupResult> PublishCleanExitAsync(
        EditorOperationCoordinator operations) =>
        Task.FromResult(
            new ApplicationBackupResult(
                ApplicationBackupOutcome.Unchanged));
}

internal sealed class EditorApplicationBackupController
{
    private readonly Window _owner;
    private readonly IApplicationBackupLifecycle _lifecycle;
    private readonly EditorOperationCoordinator _operations;
    private readonly Func<bool> _isDark;
    private bool _operationActive;

    public EditorApplicationBackupController(
        Window owner,
        IApplicationBackupLifecycle lifecycle,
        EditorOperationCoordinator operations,
        Func<bool> isDark)
    {
        _owner = owner;
        _lifecycle = lifecycle;
        _operations = operations;
        _isDark = isDark;
    }

    public bool OperationActive => _operationActive;

    public async Task PublishManualAsync()
    {
        if (_operationActive)
        {
            return;
        }
        _operationActive = true;
        try
        {
            var result = await _lifecycle
                .PublishManualAsync(_operations);
            await new EditorDialogService(
                    _owner,
                    _isDark())
                .ShowInfo(
                    "Backup entregado",
                    result.PackageName is null
                        ? "No hay un productor de backups configurado para esta sesión."
                        : $"MOCKUPS entregó {result.PackageName} a Backup Hub. El cifrado, historial y sincronización se procesan allí.");
        }
        catch (Exception exception)
        {
            await new EditorDialogService(
                    _owner,
                    _isDark())
                .ShowInfo(
                    "No se pudo crear el backup",
                    exception.Message);
        }
        finally
        {
            _operationActive = false;
        }
    }

    public async Task<bool> PrepareCloseAsync()
    {
        if (_operationActive)
        {
            return false;
        }
        _operationActive = true;
        try
        {
            _ = await _lifecycle
                .PublishCleanExitAsync(_operations);
            return true;
        }
        catch (Exception exception)
        {
            return await new EditorDialogService(
                    _owner,
                    _isDark())
                .ConfirmAction(
                    "Backup de cierre fallido",
                    "No se pudo proteger la última versión de la base de datos.",
                    $"{exception.Message}\n\n¿Cerrar MOCKUPS sin crear el backup?",
                    "Cerrar sin backup",
                    width: 520,
                    height: 280);
        }
        finally
        {
            _operationActive = false;
        }
    }
}
