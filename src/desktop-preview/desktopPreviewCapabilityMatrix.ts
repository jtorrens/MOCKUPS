import type { DesktopPreviewComponentClass } from "./desktopPreviewComponents.js";
import type { DesktopPreviewModuleClass } from "./desktopPreviewModules.js";

export type DesktopPreviewFrameCapability =
  | "authenticationProgress"
  | "calculatedText"
  | "controlsFade"
  | "cursorBlink"
  | "displayModeMotion"
  | "distributionMotion"
  | "fullScreenMotion"
  | "pressedState"
  | "keyboardEntranceMotion"
  | "keyboardPressedPopup"
  | "mediaPlayback"
  | "ownerTimeline"
  | "passwordEntry"
  | "presenceMotion"
  | "stateMotion"
  | "writeOn";

export interface DesktopPreviewOwnerCapabilities {
  readonly runtimeActions: readonly string[];
  readonly collectionActions: readonly string[];
  readonly frameCapabilities: readonly DesktopPreviewFrameCapability[];
}

const capabilities = (
  runtimeActions: readonly string[] = [],
  collectionActions: readonly string[] = [],
  frameCapabilities: readonly DesktopPreviewFrameCapability[] = [],
): DesktopPreviewOwnerCapabilities => ({
  runtimeActions,
  collectionActions,
  frameCapabilities,
});

export const desktopPreviewComponentCapabilities = {
  label: capabilities([], [], ["calculatedText"]),
  surface: capabilities(),
  cursor: capabilities([], [], ["cursorBlink"]),
  textBox: capabilities([], [], ["cursorBlink"]),
  iconRow: capabilities(),
  iconBar: capabilities(),
  componentStack: capabilities([], [], ["stateMotion"]),
  collectionStack: capabilities(
    ["changeDistribution"],
    ["items.togglePresent"],
    ["distributionMotion", "presenceMotion"],
  ),
  badge: capabilities(),
  notification: capabilities(["changeDisplayMode"], [], ["displayModeMotion"]),
  notifications: capabilities(
    ["changeDistribution"],
    ["items.togglePresent", "items.changeDisplayMode"],
    ["distributionMotion", "presenceMotion", "displayModeMotion"],
  ),
  listItem: capabilities(),
  list: capabilities([], ["items.togglePresent"], ["presenceMotion"]),
  codeIndicator: capabilities(),
  avatar: capabilities(),
  button: capabilities(["push"], [], ["pressedState"]),
  audio: capabilities(["play"], [], ["mediaPlayback"]),
  textInputBar: capabilities([], [], ["cursorBlink"]),
  keyboard: capabilities(
    ["in"],
    [],
    ["keyboardEntranceMotion", "keyboardPressedPopup"],
  ),
  keypad: capabilities(["pushKey"], [], ["pressedState"]),
  fingerprint: capabilities([], [], ["authenticationProgress"]),
  faceRecognition: capabilities([], [], ["authenticationProgress"]),
  drawPassword: capabilities([], [], ["authenticationProgress"]),
  password: capabilities(["enterPassword"], [], ["passwordEntry"]),
  media: capabilities(
    ["play", "fullScreen"],
    [],
    ["mediaPlayback", "controlsFade", "fullScreenMotion"],
  ),
  bubble: capabilities(
    ["writeOn", "play", "fullScreen"],
    [],
    ["writeOn", "mediaPlayback", "fullScreenMotion"],
  ),
  status_bar: capabilities(),
  navigation_bar: capabilities(),
  incomingCallNotification: capabilities(
    ["togglePresent"],
    [],
    ["presenceMotion"],
  ),
} as const satisfies Record<DesktopPreviewComponentClass, DesktopPreviewOwnerCapabilities>;

export const desktopPreviewModuleCapabilities = {
  "module.core.chat": capabilities(
    ["playConversation"],
    ["messages.playVideo", "messages.playAudio", "messages.fullScreen"],
    ["ownerTimeline", "writeOn", "mediaPlayback", "fullScreenMotion", "stateMotion"],
  ),
  "module.core.lockScreen": capabilities([], [], ["ownerTimeline", "stateMotion"]),
  "module.core.chatList": capabilities(
    [],
    ["items.togglePresent"],
    ["ownerTimeline", "presenceMotion"],
  ),
  "module.core.socialPost": capabilities(
    ["writeOn", "play", "fullScreen"],
    [],
    ["ownerTimeline", "writeOn", "mediaPlayback", "fullScreenMotion", "stateMotion"],
  ),
} as const satisfies Record<DesktopPreviewModuleClass, DesktopPreviewOwnerCapabilities>;
