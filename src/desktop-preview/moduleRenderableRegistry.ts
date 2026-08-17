import type { RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  isDesktopPreviewModuleClass,
  type DesktopPreviewModuleClass,
} from "./desktopPreviewModules.js";
import { generatedModuleScaffoldFactories } from "./generatedModuleScaffoldRegistry.js";

export type ModuleRenderableFactory = (payload: DesignPreviewPayload) => RenderableNode;

export const moduleRenderableFactories: Record<
  DesktopPreviewModuleClass,
  ModuleRenderableFactory
> = generatedModuleScaffoldFactories;

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
