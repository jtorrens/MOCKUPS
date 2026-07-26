using Mockups.DesktopEditorShell.Integrations.ShotManager;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal static class ShotManagerExternalShotPlanExtensions
{
    public static ShotManagerPortableStructure ToPortableStructure(
        this ShotManagerExternalShotPlan plan)
    {
        var structure = new ShotManagerPortableStructure(
            2,
            plan.Directories.Select((directory) => directory.RelativePath).ToList(),
            plan.ShotOwnedDirectories.Select((directory) =>
                directory.RelativePath).ToList(),
            plan.StructureEntries.Select((entry) =>
                new ShotManagerPortableStructureEntry(
                    entry.EntryId,
                    entry.RelativePath)).ToList(),
            plan.OutputContracts.Select((output) =>
                new ShotManagerPortableOutputContract(
                    output.EntryId,
                    output.RelativeDirectory,
                    output.FileNamePrefix,
                    output.VersionPadding)).ToList());
        structure.Validate("Shot Manager external Shot plan");
        return structure;
    }
}
