import type { ArchitectureValidationContext } from "./validationContext.js";
import { repositoryFileExists } from "./validationContext.js";

export const retiredPaths = [
  "archive/react-legacy",
  "assets/icons/components/Render Presets.svg",
  "assets/system/system_icons/components/Render Presets.svg",
  "docs/WINDOWS_PC_TEST_HANDOFF.md",
  "docs/pc-mac/2026-07-30_windows_design_preview_patch_timeout.md",
  "docs/pc-mac/2026-07-30_windows_unicode_preview_transport.md",
  "index.html",
  "remotion.config.ts",
  "scripts/checkDesktopPreviewArchitecture.ts",
  "scripts/icon-themes/add-editor-material-icons-prompt-weight.cjs",
  "scripts/icon-themes/download-lucide-theme.cjs",
  "scripts/icon-themes/download-material-symbols-theme.cjs",
  "scripts/migratePaletteColorReferencesToIds.mjs",
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

export function isRetiredRepositoryPath(relativePath: string) {
  return retiredPaths.some((retiredPath) =>
    relativePath === retiredPath || relativePath.startsWith(`${retiredPath}/`));
}

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

  const packageScripts = (JSON.parse(context.readText("package.json")) as {
    scripts?: Record<string, string>;
  }).scripts ?? {};
  for (const [name, command] of Object.entries(packageScripts)) {
    if (name.startsWith("legacy:") || command.includes("archive/react-legacy")) {
      context.addViolation(
        "package.json",
        `script '${name}' must not expose the retired React implementation`,
      );
    }
  }
}
