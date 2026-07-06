import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { iconUriForToken } from "./previewAssetResolver.js";

export function iconTokenStyle(
  payload: DesignPreviewPayload,
  token: string,
  color: string,
) {
  const iconUri = iconUriForToken(payload, token);
  return {
    color,
    ...(iconUri
      ? {
          maskImage: `url("${iconUri.replace(/"/g, '\\"')}")`,
          WebkitMaskImage: `url("${iconUri.replace(/"/g, '\\"')}")`,
        }
      : {}),
  };
}

