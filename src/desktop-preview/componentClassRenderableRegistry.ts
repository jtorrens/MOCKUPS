import type { RenderableNode } from "../visual/renderable/types.js";
import { audioComponentToRenderable } from "./audioComponentRenderable.js";
import { resolveAudioComponent } from "./audioComponentResolver.js";
import { avatarComponentToRenderable } from "./avatarComponentRenderable.js";
import { resolveAvatarComponent } from "./avatarComponentResolver.js";
import { buttonIconComponentToRenderable } from "./buttonIconComponentRenderable.js";
import { resolveButtonIconComponent } from "./buttonIconComponentResolver.js";
import {
  isDesktopPreviewComponentClass,
  type DesktopPreviewComponentClass,
} from "./desktopPreviewComponents.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { labelComponentToRenderable } from "./labelComponentRenderable.js";
import { resolveLabelComponent } from "./labelComponentResolver.js";

type ComponentRenderableFactory = (payload: DesignPreviewPayload) => RenderableNode;

export const componentRenderableFactoryKeys = [
  "label",
  "avatar",
  "audio_message",
  "button_icon",
] as const satisfies readonly DesktopPreviewComponentClass[];

const ComponentRenderableFactories: Record<
  (typeof componentRenderableFactoryKeys)[number],
  ComponentRenderableFactory
> = {
  label: (payload) => labelComponentToRenderable(payload, resolveLabelComponent(payload)),
  avatar: (payload) => avatarComponentToRenderable(payload, resolveAvatarComponent(payload)),
  audio_message: (payload) => audioComponentToRenderable(payload, resolveAudioComponent(payload)),
  button_icon: (payload) =>
    buttonIconComponentToRenderable(payload, resolveButtonIconComponent(payload)),
};

export function componentClassToRenderable(payload: DesignPreviewPayload): RenderableNode {
  const componentType = payload.componentType ?? "";
  const factory = isRoutedComponentClass(componentType)
    ? ComponentRenderableFactories[componentType]
    : undefined;
  if (factory) {
    return factory(payload);
  }

  const box = {
    x: payload.device.screenX + payload.device.screenWidth * 0.16,
    y: payload.device.screenY + payload.device.screenHeight * 0.42,
    width: payload.device.screenWidth * 0.68,
    height: 88,
  };
  return {
    id: "component.preview.unsupported",
    type: "component_preview_unsupported",
    frame: 0,
    box,
    text: `Unsupported component preview: ${componentType}`,
    style: {
      backgroundColor: "#ff00ff",
      borderRadius: 6,
      color: "#ffffff",
      fontSize: 14,
      fontWeight: 700,
      lineHeight: box.height,
      textAlign: "center",
    },
    metadata: {
      route: "component-preview.unsupported",
      componentType,
    },
  };
}

function isRoutedComponentClass(
  value: string,
): value is (typeof componentRenderableFactoryKeys)[number] {
  return isDesktopPreviewComponentClass(value)
    && Object.hasOwn(ComponentRenderableFactories, value);
}
