import type { RenderableNode } from "../visual/renderable/types.js";
import { conversationModuleToRenderable } from "./conversationModuleRenderable.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  isDesktopPreviewModuleClass,
  type DesktopPreviewModuleClass,
} from "./desktopPreviewModules.js";
import { lockScreenModuleToRenderable } from "./lockScreenModuleRenderable.js";

type ModuleRenderableFactory = (payload: DesignPreviewPayload) => RenderableNode;

export const moduleRenderableFactories = {
  "module.core.chat": conversationModuleToRenderable,
  "module.core.lockScreen": lockScreenModuleToRenderable,
} satisfies Record<DesktopPreviewModuleClass, ModuleRenderableFactory>;

export function routeModuleToRenderable(payload: DesignPreviewPayload): RenderableNode {
  const moduleClass = payload.componentType ?? "";
  const factory = isRoutedModuleClass(moduleClass)
    ? moduleRenderableFactories[moduleClass]
    : undefined;
  if (!factory) {
    throw new Error(`Unsupported module preview route '${moduleClass}'.`);
  }
  return factory(payload);
}

function isRoutedModuleClass(
  value: string,
): value is keyof typeof moduleRenderableFactories {
  return isDesktopPreviewModuleClass(value)
    && Object.hasOwn(moduleRenderableFactories, value);
}
