import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { NavigationBarDesignContract } from "./navigationBarComponentContract.js";
import { resolveNavigationBar } from "./systemBarPreviewResolver.js";

export function resolveNavigationBarComponent(
  payload: DesignPreviewPayload,
): NavigationBarDesignContract {
  return resolveNavigationBar(payload);
}
