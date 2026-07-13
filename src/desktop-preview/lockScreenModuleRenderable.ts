import type { RenderableNode } from "../visual/renderable/types.js";
import { wallpaperRenderable } from "./wallpaperRenderable.js";
import { componentPresetConfig } from "./componentPreviewDefaults.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { navigationBarComponentToRenderable } from "./navigationBarComponentRenderable.js";
import { resolveNavigationBarComponent } from "./navigationBarComponentResolver.js";
import { parseObject } from "./componentResolverCommon.js";
import type { LockScreenModuleContract } from "./lockScreenModuleContract.js";
import { resolveLockScreenModuleFrame } from "./lockScreenModuleResolver.js";
import { previewScreenBox } from "./componentRenderableCommon.js";
import { statusBarComponentToRenderable } from "./statusBarComponentRenderable.js";
import { resolveStatusBarComponent } from "./statusBarComponentResolver.js";

export function lockScreenModuleToRenderable(payload: DesignPreviewPayload): RenderableNode {
  const contract = resolveLockScreenModuleFrame(payload);
  const screen = previewScreenBox(payload);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const children: RenderableNode[] = [];
  const wallpaper = wallpaperRenderable(payload, screen);
  if (wallpaper) children.push(wallpaper);
  children.push(
    statusBarComponentToRenderable(
      componentPayload(payload, componentBaseConfigs, "status_bar", contract.statusBarVariant),
      resolveStatusBarComponent(
        componentPayload(payload, componentBaseConfigs, "status_bar", contract.statusBarVariant),
      ),
    ),
    navigationBarComponentToRenderable(
      componentPayload(payload, componentBaseConfigs, "navigation_bar", contract.navigationBarVariant),
      resolveNavigationBarComponent(
        componentPayload(payload, componentBaseConfigs, "navigation_bar", contract.navigationBarVariant),
      ),
    ),
  );
  return {
    id: "module.lockScreen",
    type: "group",
    frame: 0,
    box: screen,
    style: { overflow: "hidden" },
    children,
  };
}

function componentPayload(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  componentType: "status_bar" | "navigation_bar",
  variant: string,
): DesignPreviewPayload {
  return {
    ...payload,
    componentType,
    configJson: JSON.stringify(componentPresetConfig(componentBaseConfigs, componentType, variant)),
  };
}
