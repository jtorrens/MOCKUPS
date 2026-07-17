import manifest from "./desktopPreviewManifest.json" with { type: "json" };

export interface DesktopPreviewModuleManifestEntry {
  label: string;
  resolver: string;
  renderable: string;
  embeds: readonly string[];
}

type DesktopPreviewModuleManifest = {
  [K in keyof typeof manifest.modules]: DesktopPreviewModuleManifestEntry;
};

export const desktopPreviewModules = manifest.modules as DesktopPreviewModuleManifest;

export type DesktopPreviewModuleClass = keyof typeof desktopPreviewModules;

export function isDesktopPreviewModuleClass(
  value: string,
): value is DesktopPreviewModuleClass {
  return Object.hasOwn(desktopPreviewModules, value);
}
