import type { RenderableNode } from "../visual/renderable/types.js";
import { wallpaperRenderable } from "./wallpaperRenderable.js";
import { componentVariantConfig, embeddedComponentConfig } from "./componentPreviewDefaults.js";
import { componentClassToRenderable } from "./componentRenderableBoundary.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { navigationBarComponentToRenderable } from "./navigationBarComponentRenderable.js";
import { resolveNavigationBarComponent } from "./navigationBarComponentResolver.js";
import { parseObject } from "./componentResolverCommon.js";
import type { SystemCompositionComponentSlot } from "./systemCompositionModuleContract.js";
import { resolveSystemCompositionModuleFrame } from "./systemCompositionModuleResolver.js";
import { previewPayloadInBox, previewScreenBox } from "./componentRenderableCommon.js";
import { statusBarComponentToRenderable } from "./statusBarComponentRenderable.js";
import { resolveStatusBarComponent } from "./statusBarComponentResolver.js";
import { resolveInternalComponentStackLayout } from "./componentStackComponentResolver.js";
import { componentStackLayoutToRenderable } from "./componentStackComponentRenderable.js";

export function systemCompositionModuleToRenderable(payload: DesignPreviewPayload): RenderableNode {
  const contract = resolveSystemCompositionModuleFrame(payload);
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
  const stackPayload = previewPayloadInBox(payload, contentBox);
  children.push(componentStackLayoutToRenderable(
    stackPayload,
    resolveInternalComponentStackLayout(
      stackPayload,
      contract.stackInputs,
      "module.system.composition.contentStack",
    ),
    componentClassToRenderable,
    contentBox,
  ));
  if (status) children.push(status);
  if (navigation) children.push(navigation);
  return {
    id: "module.systemComposition",
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
  slot: SystemCompositionComponentSlot,
): DesignPreviewPayload {
  return {
    ...componentPayload(payload, componentBaseConfigs, componentType, slot.variantReference),
    configJson: JSON.stringify(embeddedComponentConfig(
      componentBaseConfigs,
      { ...slot },
      componentType,
      `module.system.composition.${componentType}`,
    )),
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
    configJson: JSON.stringify(componentVariantConfig(componentBaseConfigs, componentType, variant)),
  };
}
