import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { StatusBarDesignContract } from "./statusBarComponentContract.js";
import { resolveStatusBar } from "./systemBarPreviewResolver.js";

export function resolveStatusBarComponent(
  payload: DesignPreviewPayload,
): StatusBarDesignContract {
  return resolveStatusBar(payload);
}
