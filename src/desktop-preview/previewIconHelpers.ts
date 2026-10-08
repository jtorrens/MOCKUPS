import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { iconUriForToken } from "./previewAssetResolver.js";

export function iconTokenStyle(
  payload: DesignPreviewPayload,
  token: string,
  color: string,
) {
  const iconUri = iconUriForToken(payload, token);
  if (!iconUri) {
    console.warn(`[MOCKUPS resource] Icono ausente: ${token}`);
  }
  const maskUri = iconUri || "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 1 1'%3E%3Cpath d='M0 0H1V1H0Z'/%3E%3C/svg%3E";
  return {
    color: iconUri ? color : "#ff0000",
    maskImage: `url("${maskUri.replace(/"/g, '\\"')}")`,
    WebkitMaskImage: `url("${maskUri.replace(/"/g, '\\"')}")`,
  };
}
