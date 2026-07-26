import type { ArchitectureValidationContext } from "./validationContext.js";
import { repositoryFileExists } from "./validationContext.js";

const retiredPaths = [
  "index.html",
  "remotion.config.ts",
  "scripts/checkDesktopPreviewArchitecture.ts",
  "spikes/desktop-editor-shell",
  "src/debug-server",
  "src/debug-ui",
  "src/desktop-preview/systemBarComponentContract.ts",
  "src/desktop-preview/systemBarPreviewResolver.ts",
  "src/desktop-preview/systemBarRenderables.ts",
  "src/desktop-preview/webPreviewBridge.ts",
  "src/domain",
  "src/electron",
  "src/icon-themes/importDevelopmentIconTheme.ts",
  "src/Mockups.Desktop/EditorShell/EditorSimplifiedProjection.cs",
  "src/Mockups.Persistence.Sqlite/SpikeDatabase.ComponentClassDefaults.cs",
  "src/Mockups.Persistence.Sqlite/SpikeDatabase.ComponentClassNormalization.cs",
  "src/persistence",
  "src/remotion",
  "src/visual/adapters/react",
  "src/visual/layout",
  "src/visual/modules",
  "src/visual/renderable/helpers.ts",
  "src/visual/validation",
  "vite.config.ts",
] as const;

export function checkRetiredContracts(
  context: ArchitectureValidationContext,
) {
  for (const retiredPath of retiredPaths) {
    if (repositoryFileExists(context, retiredPath)) {
      context.addViolation(
        retiredPath,
        "retired architecture path must not return",
      );
    }
  }
}
