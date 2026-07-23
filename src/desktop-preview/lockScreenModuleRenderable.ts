import type { RenderableNode } from "../visual/renderable/types.js";
import { wallpaperRenderable } from "./wallpaperRenderable.js";
import { componentVariantConfig, embeddedComponentConfig } from "./componentPreviewDefaults.js";
import { componentClassToRenderable } from "./componentRenderableBoundary.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { navigationBarComponentToRenderable } from "./navigationBarComponentRenderable.js";
import { resolveNavigationBarComponent } from "./navigationBarComponentResolver.js";
import { parseObject } from "./componentResolverCommon.js";
import type { LockScreenComponentSlot } from "./lockScreenModuleContract.js";
import { resolveLockScreenModuleFrame } from "./lockScreenModuleResolver.js";
import { previewPayloadInBox, previewScreenBox } from "./componentRenderableCommon.js";
import { statusBarComponentToRenderable } from "./statusBarComponentRenderable.js";
import { resolveStatusBarComponent } from "./statusBarComponentResolver.js";

export function lockScreenModuleToRenderable(payload: DesignPreviewPayload): RenderableNode {
  const contract = resolveLockScreenModuleFrame(payload);
  const screen = previewScreenBox(payload);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const children: RenderableNode[] = [];
  const wallpaper = wallpaperRenderable(payload, screen);
  if (wallpaper) children.push(wallpaper);
  const status = contract.showStatusBar
    ? statusBarComponentToRenderable(
      componentSlotPayload(payload, componentBaseConfigs, "status_bar", contract.statusBarSlot),
      resolveStatusBarComponent(
        componentSlotPayload(payload, componentBaseConfigs, "status_bar", contract.statusBarSlot),
      ),
    )
    : undefined;
  const navigation = contract.showNavigationBar
    ? navigationBarComponentToRenderable(
      componentSlotPayload(payload, componentBaseConfigs, "navigation_bar", contract.navigationBarSlot),
      resolveNavigationBarComponent(
        componentSlotPayload(payload, componentBaseConfigs, "navigation_bar", contract.navigationBarSlot),
      ),
    )
    : undefined;
  const contentTop = screen.y + (status?.box?.height ?? 0);
  const contentBottom = navigation?.box?.y ?? screen.y + screen.height;
  const contentBox = {
    x: screen.x,
    y: contentTop,
    width: screen.width,
    height: Math.max(0, contentBottom - contentTop),
  };
  const stackPayload = previewPayloadInBox(
    {
      ...componentPayload(payload, componentBaseConfigs, "componentStack", contract.stackSlot.variantReference),
      configJson: JSON.stringify(embeddedComponentConfig(
        componentBaseConfigs,
        { ...contract.stackSlot },
        "componentStack",
        "module.lockScreen.stackSlot",
      )),
      designPreviewJson: JSON.stringify(contract.stackInputs),
    },
    contentBox,
  );
  children.push(componentClassToRenderable(stackPayload));
  if (status) children.push(status);
  if (navigation) children.push(navigation);
  return {
    id: "module.lockScreen",
    type: "group",
    frame: 0,
    box: screen,
    style: { overflow: "hidden" },
    children,
  };
}

function componentSlotPayload(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  componentType: "status_bar" | "navigation_bar",
  slot: LockScreenComponentSlot,
): DesignPreviewPayload {
  return {
    ...componentPayload(payload, componentBaseConfigs, componentType, slot.variantReference),
    configJson: JSON.stringify(embeddedComponentConfig(
      componentBaseConfigs,
      { ...slot },
      componentType,
      `module.lockScreen.${componentType}`,
    )),
  };
}

function componentPayload(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  componentType: "status_bar" | "navigation_bar" | "componentStack",
  variant: string,
): DesignPreviewPayload {
  return {
    ...payload,
    componentType,
    configJson: JSON.stringify(componentVariantConfig(componentBaseConfigs, componentType, variant)),
  };
}
