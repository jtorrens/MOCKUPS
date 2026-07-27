import type { ArchitectureValidationContext } from "./validationContext.js";
import { repositoryFileExists } from "./validationContext.js";

const retiredPaths = [
  "index.html",
  "remotion.config.ts",
  "scripts/checkDesktopPreviewArchitecture.ts",
  "spikes/desktop-editor-shell",
  "src/debug-server",
  "src/debug-ui",
  "src/domain",
  "src/electron",
  "src/persistence",
  "src/remotion",
  "src/visual/adapters/react",
  "src/visual/layout",
  "src/visual/modules",
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
