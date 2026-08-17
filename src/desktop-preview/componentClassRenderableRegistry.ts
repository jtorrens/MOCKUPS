import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  isDesktopPreviewComponentClass,
  type DesktopPreviewComponentClass,
} from "./desktopPreviewComponents.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { generatedComponentScaffoldFactories } from "./generatedComponentScaffoldRegistry.js";

export type ComponentRenderableBoundary = (
  payload: DesignPreviewPayload,
  assignedBox?: RenderableBox,
) => RenderableNode;

export type ComponentRenderableFactory = (
  payload: DesignPreviewPayload,
  assignedBox: RenderableBox | undefined,
  renderChild: ComponentRenderableBoundary,
) => RenderableNode;

export const componentRenderableFactories: Record<
  DesktopPreviewComponentClass,
  ComponentRenderableFactory
> = generatedComponentScaffoldFactories;

export function routeComponentClassToRenderable(
  payload: DesignPreviewPayload,
  renderChild: ComponentRenderableBoundary,
  assignedBox?: RenderableBox,
): RenderableNode {
  const componentType = payload.componentType ?? "";
  const factory = isRoutedComponentClass(componentType)
    ? componentRenderableFactories[componentType]
    : undefined;
  if (!factory) {
    throw new Error(`Unsupported component preview route '${componentType}'.`);
  }
  return factory(payload, assignedBox, renderChild);
}

function isRoutedComponentClass(
  value: string,
): value is keyof typeof componentRenderableFactories {
  return isDesktopPreviewComponentClass(value)
    && Object.hasOwn(componentRenderableFactories, value);
}
