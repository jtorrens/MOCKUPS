import type { RenderableNode } from "../visual/renderable/types.js";
import { audioComponentToRenderable } from "./audioComponentRenderable.js";
import { resolveAudioComponent } from "./audioComponentResolver.js";
import { resolveAvatarComponent } from "./avatarComponentResolver.js";
import { resolveButtonIconComponent } from "./buttonIconComponentResolver.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { resolveLabelComponent } from "./labelComponentResolver.js";
import {
  avatarComponentToRenderable,
  buttonIconComponentToRenderable,
  labelComponentToRenderable,
} from "./componentClassRenderables.js";

type ComponentRenderableFactory = (payload: DesignPreviewPayload) => RenderableNode;

const ComponentRenderableFactories: Record<string, ComponentRenderableFactory> = {
  label: (payload) => labelComponentToRenderable(payload, resolveLabelComponent(payload)),
  avatar: (payload) => avatarComponentToRenderable(payload, resolveAvatarComponent(payload)),
  audio: (payload) => audioComponentToRenderable(payload, resolveAudioComponent(payload)),
  buttonIcon: (payload) =>
    buttonIconComponentToRenderable(payload, resolveButtonIconComponent(payload)),
};

export function componentClassToRenderable(payload: DesignPreviewPayload): RenderableNode {
  const componentType = payload.componentType ?? "";
  const factory = ComponentRenderableFactories[componentType];
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
