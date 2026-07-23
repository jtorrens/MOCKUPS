import manifest from "./desktopPreviewManifest.json" with { type: "json" };

export type DesktopPreviewComponentCategory = "system" | "atom" | "component";

export interface DesktopPreviewComponentManifestEntry {
  category: DesktopPreviewComponentCategory;
  contract: string;
  resolver: string;
  renderable: string;
  embeds: readonly string[];
}

type DesktopPreviewComponentManifest = {
  [K in keyof typeof manifest.components]: DesktopPreviewComponentManifestEntry;
};

export const desktopPreviewComponents = manifest.components as DesktopPreviewComponentManifest;

export type DesktopPreviewComponentClass = keyof typeof desktopPreviewComponents;

export function isDesktopPreviewComponentClass(
  value: string,
): value is DesktopPreviewComponentClass {
  return Object.hasOwn(desktopPreviewComponents, value);
}
