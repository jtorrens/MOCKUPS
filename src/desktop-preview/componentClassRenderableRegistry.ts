import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { audioComponentToRenderable } from "./audioComponentRenderable.js";
import { resolveAudioComponent } from "./audioComponentResolver.js";
import { avatarComponentToRenderable } from "./avatarComponentRenderable.js";
import { resolveAvatarComponent } from "./avatarComponentResolver.js";
import { bubbleComponentToRenderable } from "./bubbleComponentRenderable.js";
import { resolveBubbleComponent } from "./bubbleComponentResolver.js";
import { buttonComponentToRenderable } from "./buttonComponentRenderable.js";
import { resolveButtonComponent } from "./buttonComponentResolver.js";
import { cursorComponentToRenderable } from "./cursorComponentRenderable.js";
import { resolveCursorComponent } from "./cursorComponentResolver.js";
import { componentStackComponentToRenderable } from "./componentStackComponentRenderable.js";
import { resolveComponentStackComponent } from "./componentStackComponentResolver.js";
import { collectionStackComponentToRenderable } from "./collectionStackComponentRenderable.js";
import { resolveCollectionStackComponent } from "./collectionStackComponentResolver.js";
import { notificationComponentToRenderable } from "./notificationComponentRenderable.js";
import { resolveNotificationComponent } from "./notificationComponentResolver.js";
import { notificationsComponentToRenderable } from "./notificationsComponentRenderable.js";
import { resolveNotificationsComponent } from "./notificationsComponentResolver.js";
import { codeIndicatorComponentToRenderable } from "./codeIndicatorComponentRenderable.js";
import { resolveCodeIndicatorComponent } from "./codeIndicatorComponentResolver.js";
import {
  isDesktopPreviewComponentClass,
  type DesktopPreviewComponentClass,
} from "./desktopPreviewComponents.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { labelComponentToRenderable } from "./labelComponentRenderable.js";
import { resolveLabelComponent } from "./labelComponentResolver.js";
import { keyboardComponentToRenderable } from "./keyboardComponentRenderable.js";
import { resolveKeyboardComponent } from "./keyboardComponentResolver.js";
import { keypadComponentToRenderable } from "./keypadComponentRenderable.js";
import { resolveKeypadComponent } from "./keypadComponentResolver.js";
import { fingerprintComponentToRenderable } from "./fingerprintComponentRenderable.js";
import { resolveFingerprintComponent } from "./fingerprintComponentResolver.js";
import { faceRecognitionComponentToRenderable } from "./faceRecognitionComponentRenderable.js";
import { resolveFaceRecognitionComponent } from "./faceRecognitionComponentResolver.js";
import { drawPasswordComponentToRenderable } from "./drawPasswordComponentRenderable.js";
import { resolveDrawPasswordComponent } from "./drawPasswordComponentResolver.js";
import { passwordComponentToRenderable } from "./passwordComponentRenderable.js";
import { resolvePasswordComponent } from "./passwordComponentResolver.js";
import { iconBarComponentToRenderable } from "./iconBarComponentRenderable.js";
import { resolveIconBarComponent } from "./iconBarComponentResolver.js";
import { iconRowComponentToRenderable } from "./iconRowComponentRenderable.js";
import { resolveIconRowComponent } from "./iconRowComponentResolver.js";
import { navigationBarComponentToRenderable } from "./navigationBarComponentRenderable.js";
import { resolveNavigationBarComponent } from "./navigationBarComponentResolver.js";
import { statusBarComponentToRenderable } from "./statusBarComponentRenderable.js";
import { resolveStatusBarComponent } from "./statusBarComponentResolver.js";
import { surfaceComponentToRenderable } from "./surfaceComponentRenderable.js";
import { resolveSurfaceComponent } from "./surfaceComponentResolver.js";
import { textBoxComponentToRenderable } from "./textBoxComponentRenderable.js";
import { resolveTextBoxComponent } from "./textBoxComponentResolver.js";
import { textInputBarComponentToRenderable } from "./textInputBarComponentRenderable.js";
import { resolveTextInputBarComponent } from "./textInputBarComponentResolver.js";
import { mediaComponentToRenderable } from "./mediaComponentRenderable.js";
import { resolveMediaComponent } from "./mediaComponentResolver.js";
import { applyRuntimeInputForwarding } from "./runtimeInputForwarding.js";
import { badgeComponentToRenderable } from "./badgeComponentRenderable.js";
import { resolveBadgeComponent } from "./badgeComponentResolver.js";

type ComponentRenderableFactory = (payload: DesignPreviewPayload, assignedBox?: RenderableBox) => RenderableNode;

export const componentRenderableFactories = {
  label: (payload) => labelComponentToRenderable(payload, resolveLabelComponent(payload)),
  surface: (payload) =>
    surfaceComponentToRenderable(payload, resolveSurfaceComponent(payload)),
  cursor: (payload) =>
    cursorComponentToRenderable(payload, resolveCursorComponent(payload)),
  textBox: (payload) =>
    textBoxComponentToRenderable(payload, resolveTextBoxComponent(payload)),
  iconRow: (payload) =>
    iconRowComponentToRenderable(payload, resolveIconRowComponent(payload)),
  iconBar: (payload) =>
    iconBarComponentToRenderable(payload, resolveIconBarComponent(payload)),
  componentStack: (payload) =>
    componentStackComponentToRenderable(payload, resolveComponentStackComponent(payload), componentClassToRenderable),
  collectionStack: (payload) =>
    collectionStackComponentToRenderable(payload, resolveCollectionStackComponent(payload), componentClassToRenderable),
  badge: (payload) => badgeComponentToRenderable(payload, resolveBadgeComponent(payload)),
  notification: (payload, assignedBox) =>
    notificationComponentToRenderable(payload, resolveNotificationComponent(payload), assignedBox),
  notifications: (payload) =>
    notificationsComponentToRenderable(payload, resolveNotificationsComponent(payload), componentClassToRenderable),
  codeIndicator: (payload) =>
    codeIndicatorComponentToRenderable(payload, resolveCodeIndicatorComponent(payload)),
  avatar: (payload) => avatarComponentToRenderable(payload, resolveAvatarComponent(payload)),
  audio: (payload) => audioComponentToRenderable(payload, resolveAudioComponent(payload)),
  button: (payload) => buttonComponentToRenderable(payload, resolveButtonComponent(payload)),
  textInputBar: (payload) =>
    textInputBarComponentToRenderable(payload, resolveTextInputBarComponent(payload)),
  keyboard: (payload) =>
    keyboardComponentToRenderable(payload, resolveKeyboardComponent(payload)),
  keypad: (payload) =>
    keypadComponentToRenderable(payload, resolveKeypadComponent(payload)),
  fingerprint: (payload) =>
    fingerprintComponentToRenderable(payload, resolveFingerprintComponent(payload)),
  faceRecognition: (payload) =>
    faceRecognitionComponentToRenderable(payload, resolveFaceRecognitionComponent(payload)),
  drawPassword: (payload) =>
    drawPasswordComponentToRenderable(payload, resolveDrawPasswordComponent(payload)),
  password: (payload) =>
    passwordComponentToRenderable(payload, resolvePasswordComponent(payload)),
  media: (payload) => mediaComponentToRenderable(payload, resolveMediaComponent(payload)),
  bubble: (payload) => bubbleComponentToRenderable(payload, resolveBubbleComponent(payload)),
  status_bar: (payload) =>
    statusBarComponentToRenderable(payload, resolveStatusBarComponent(payload)),
  navigation_bar: (payload) =>
    navigationBarComponentToRenderable(payload, resolveNavigationBarComponent(payload)),
} satisfies Record<DesktopPreviewComponentClass, ComponentRenderableFactory>;

export function componentClassToRenderable(payload: DesignPreviewPayload, assignedBox?: RenderableBox): RenderableNode {
  payload = applyRuntimeInputForwarding(payload);
  const componentType = payload.componentType ?? "";
  const factory = isRoutedComponentClass(componentType)
    ? componentRenderableFactories[componentType]
    : undefined;
  if (factory) {
    return factory(payload, assignedBox);
  }

  const box = {
    x: payload.previewFrame.screenX + payload.previewFrame.screenWidth * 0.16,
    y: payload.previewFrame.screenY + payload.previewFrame.screenHeight * 0.42,
    width: payload.previewFrame.screenWidth * 0.68,
    height: 88,
  };
  return {
    id: "component.preview.unsupported",
    type: "surface",
    frame: 0,
    box,
    text: `Unsupported component preview: ${componentType}`,
    style: {
      alignItems: "center",
      backgroundColor: "#ff00ff",
      borderRadius: 6,
      color: "#ffffff",
      display: "flex",
      fontSize: 14,
      fontWeight: 700,
      justifyContent: "center",
      lineHeight: box.height,
      overflow: "hidden",
      paddingLeft: 12,
      paddingRight: 12,
      textAlign: "center",
      whiteSpace: "nowrap",
    },
  };
}

function isRoutedComponentClass(
  value: string,
): value is keyof typeof componentRenderableFactories {
  return isDesktopPreviewComponentClass(value)
    && Object.hasOwn(componentRenderableFactories, value);
}
