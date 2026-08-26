using Mockups.DesktopEditorShell.Data;
using System;

namespace Mockups.DesktopEditorShell.Integrations.ProductionOutput;

internal sealed record ResolvedProductionOutputPlan(
    ProductionOutputShotPlan Plan,
    string RootPath,
    bool IsShotManaged);

internal sealed class ProductionOutputPlanResolver
{
    private readonly ProductionOutputRootStore _manualRoots;
    private readonly ShotManagerDocumentStore _shotManagerDocuments;

    public ProductionOutputPlanResolver(
        ProductionOutputRootStore manualRoots,
        ShotManagerDocumentStore shotManagerDocuments)
    {
        _manualRoots = manualRoots;
        _shotManagerDocuments = shotManagerDocuments;
    }

    public ResolvedProductionOutputPlan Resolve(
        ProductionOutputShotContext context)
    {
        if (!context.ShotManagerOutput.Enabled
            || !context.ShotManagerShot.IsAssociated)
        {
            return new ResolvedProductionOutputPlan(
                ProductionOutputContract.ResolveManual(context),
                _manualRoots.Get(context.ProjectId) ?? "",
                IsShotManaged: false);
        }

        if (!context.ShotManagerEpisode.IsAssociated)
        {
            throw new InvalidOperationException(
                "The associated MOCKUPS Episode has no Shot Manager Episode.");
        }
        return new ResolvedProductionOutputPlan(
            ShotManagerReadonlyContract.Resolve(context),
            _shotManagerDocuments.GetRoot(context.ProjectId) ?? "",
            IsShotManaged: true);
    }
}
