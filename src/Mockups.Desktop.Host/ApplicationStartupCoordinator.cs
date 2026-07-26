using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell;

public abstract record StartupResult
{
    private StartupResult() { }

    public sealed record Success(EditorApplicationSession Session)
        : StartupResult;

    public sealed record DatabaseMissing(string Path)
        : StartupResult;

    public sealed record DatabaseInvalid(string Path, string Reason)
        : StartupResult;

    public sealed record PreviewBundleMissing(string Path)
        : StartupResult;

    public sealed record PreviewBundleInvalid(string Path, string Reason)
        : StartupResult;

    public sealed record RecoveryRequired(string Reason)
        : StartupResult;

    public sealed record Canceled : StartupResult;
}

public sealed class EditorApplicationSession
{
    private DesktopApplicationServices? _services;
    private IReadOnlyList<ProjectTreeNode>? _initialTreeRoots;

    internal EditorApplicationSession(
        DesktopApplicationServices services,
        IReadOnlyList<ProjectTreeNode> initialTreeRoots)
    {
        _services = services;
        _initialTreeRoots = initialTreeRoots.ToArray();
    }

    public MainWindow CreateWindow()
    {
        var services = Interlocked.Exchange(
                ref _services,
                null)
            ?? throw new InvalidOperationException(
                "The prepared editor session already owns a window.");
        var initialTreeRoots = Interlocked.Exchange(
                ref _initialTreeRoots,
                null)
            ?? throw new InvalidOperationException(
                "The prepared editor session has no initial tree snapshot.");
        return new MainWindow(
            services,
            initialTreeRoots);
    }
}

public sealed class ApplicationStartupCoordinator
{
    private readonly string _previewBundleDirectory;

    public ApplicationStartupCoordinator(
        string previewBundleDirectory)
    {
        _previewBundleDirectory = Path.GetFullPath(
            previewBundleDirectory);
    }

    public async Task<StartupResult> StartAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(
                () => Start(databasePath, cancellationToken),
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return new StartupResult.Canceled();
        }
    }

    public StartupResult Start(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manifestPath = Path.Combine(
            _previewBundleDirectory,
            "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return new StartupResult.PreviewBundleMissing(
                manifestPath);
        }
        try
        {
            DesktopPreviewBundle.RequireCurrent(
                _previewBundleDirectory);
        }
        catch (FileNotFoundException exception)
        {
            return new StartupResult.PreviewBundleMissing(
                exception.FileName ?? manifestPath);
        }
        catch (Exception exception)
            when (exception is InvalidDataException
                or UnauthorizedAccessException
                or IOException)
        {
            return new StartupResult.PreviewBundleInvalid(
                manifestPath,
                exception.Message);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var currentDatabasePath = Path.GetFullPath(databasePath);
        if (!File.Exists(currentDatabasePath))
        {
            return new StartupResult.DatabaseMissing(
                currentDatabasePath);
        }
        try
        {
            var project = SqlitePersistence.OpenCurrent(
                currentDatabasePath);
            cancellationToken.ThrowIfCancellationRequested();
            var ports = new DesktopApplicationDataPorts(
                project.ProjectPaths,
                project.Navigation,
                project.CoreFields,
                project.RecordFields,
                project.ComponentFields,
                project.VariantHistory,
                project.Preview,
                project.Dictionary,
                project.Children,
                project.NodeCommands,
                project.ProductionNavigation,
                project.Presentation,
                project.ModuleInstances,
                project.IconThemes,
                project.ThemeTokens,
                project.Components,
                project.RuntimeInputOwners,
                project.RuntimeInputInstances,
                project.Animation,
                project.ReferenceUsage,
                project.Layouts,
                project.ActorPreview);
            var initialTreeRoots =
                ports.Navigation.LoadProjectTree();
            cancellationToken.ThrowIfCancellationRequested();
            var services = DesktopApplicationServices.Create(
                ports);
            return new StartupResult.Success(
                new EditorApplicationSession(
                    services,
                    initialTreeRoots));
        }
        catch (FileNotFoundException)
        {
            return new StartupResult.DatabaseMissing(
                currentDatabasePath);
        }
        catch (CurrentDatabaseException exception)
        {
            return new StartupResult.DatabaseInvalid(
                currentDatabasePath,
                exception.Message);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new StartupResult.RecoveryRequired(
                exception.Message);
        }
    }
}

internal static class StartupResultMessage
{
    public static string For(StartupResult result) =>
        result switch
        {
            StartupResult.Success =>
                "Startup completed successfully.",
            StartupResult.DatabaseMissing missing =>
                $"The current database is missing: {missing.Path}",
            StartupResult.DatabaseInvalid invalid =>
                $"The current database is invalid: {invalid.Reason}",
            StartupResult.PreviewBundleMissing missing =>
                $"The Desktop Preview bundle is missing: {missing.Path}",
            StartupResult.PreviewBundleInvalid invalid =>
                $"The Desktop Preview bundle is invalid: {invalid.Reason}",
            StartupResult.RecoveryRequired recovery =>
                $"The editor requires recovery: {recovery.Reason}",
            StartupResult.Canceled =>
                "Startup was canceled.",
            _ => "Startup failed.",
        };
}
