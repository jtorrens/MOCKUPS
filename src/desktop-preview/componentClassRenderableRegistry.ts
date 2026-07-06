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
import { keyboardComponentToRenderable } from "./keyboardComponentRenderable.js";
import { resolveKeyboardComponent } from "./keyboardComponentResolver.js";
import { navigationBarComponentToRenderable } from "./navigationBarComponentRenderable.js";
import { resolveNavigationBarComponent } from "./navigationBarComponentResolver.js";
import { statusBarComponentToRenderable } from "./statusBarComponentRenderable.js";
import { resolveStatusBarComponent } from "./statusBarComponentResolver.js";
import { surfaceComponentToRenderable } from "./surfaceComponentRenderable.js";
import { resolveSurfaceComponent } from "./surfaceComponentResolver.js";
import { textInputBarComponentToRenderable } from "./textInputBarComponentRenderable.js";
import { resolveTextInputBarComponent } from "./textInputBarComponentResolver.js";
import { videoComponentToRenderable } from "./videoComponentRenderable.js";
import { resolveVideoComponent } from "./videoComponentResolver.js";

type ComponentRenderableFactory = (payload: DesignPreviewPayload) => RenderableNode;

export const componentRenderableFactories = {
  label: (payload) => labelComponentToRenderable(payload, resolveLabelComponent(payload)),
  surface: (payload) =>
    surfaceComponentToRenderable(payload, resolveSurfaceComponent(payload)),
  avatar: (payload) => avatarComponentToRenderable(payload, resolveAvatarComponent(payload)),
  audio: (payload) => audioComponentToRenderable(payload, resolveAudioComponent(payload)),
  buttonIcon: (payload) =>
    buttonIconComponentToRenderable(payload, resolveButtonIconComponent(payload)),
  textInputBar: (payload) =>
    textInputBarComponentToRenderable(payload, resolveTextInputBarComponent(payload)),
  keyboard: (payload) =>
    keyboardComponentToRenderable(payload, resolveKeyboardComponent(payload)),
  video: (payload) => videoComponentToRenderable(payload, resolveVideoComponent(payload)),
  status_bar: (payload) =>
    statusBarComponentToRenderable(payload, resolveStatusBarComponent(payload)),
  navigation_bar: (payload) =>
    navigationBarComponentToRenderable(payload, resolveNavigationBarComponent(payload)),
} satisfies Record<DesktopPreviewComponentClass, ComponentRenderableFactory>;

export function componentClassToRenderable(payload: DesignPreviewPayload): RenderableNode {
  const componentType = payload.componentType ?? "";
  const factory = isRoutedComponentClass(componentType)
    ? componentRenderableFactories[componentType]
    : undefined;
  if (factory) {
    return factory(payload);
  }

  const box = {
    x: payload.previewFrame.screenX + payload.previewFrame.screenWidth * 0.16,
    y: payload.previewFrame.screenY + payload.previewFrame.screenHeight * 0.42,
    width: payload.previewFrame.screenWidth * 0.68,
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
  };
}

function isRoutedComponentClass(
  value: string,
): value is keyof typeof componentRenderableFactories {
  return isDesktopPreviewComponentClass(value)
    && Object.hasOwn(componentRenderableFactories, value);
}
