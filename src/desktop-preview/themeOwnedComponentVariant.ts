import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { requireComponentVariantType } from "./runtimePreviewDocumentContract.js";

export type ThemeOwnedComponentVariantSource = "status_bar" | "navigation_bar";

export interface ThemeOwnedComponentVariantSlot {
  [key: string]: unknown;
  variantReference: string;
  overrides: Record<string, unknown>;
}

export function themeOwnedComponentVariantSlot(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  source: ThemeOwnedComponentVariantSource,
  overrides: Record<string, unknown>,
  owner: string,
): ThemeOwnedComponentVariantSlot {
  const variantReference = (source === "status_bar"
    ? payload.themeStatusBarVariantReference
    : payload.themeNavigationBarVariantReference)?.trim() ?? "";
  if (!variantReference) {
    throw new Error(`${owner} requires the active Theme ${source} Variant reference`);
  }
  const slot = { variantReference, overrides };
  requireComponentVariantType(
    componentBaseConfigs,
    slot,
    source,
    owner,
  );
  return slot;
}
