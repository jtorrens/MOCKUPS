// Generated from scaffolding/components/*.json. Do not edit manually.
import type { ComponentRenderableFactory } from "./componentClassRenderableRegistry.js";
import { audioComponentToRenderable } from "./audioComponentRenderable.js";
import { resolveAudioComponent } from "./audioComponentResolver.js";
import { avatarComponentToRenderable } from "./avatarComponentRenderable.js";
import { resolveAvatarComponent } from "./avatarComponentResolver.js";
import { badgeComponentToRenderable } from "./badgeComponentRenderable.js";
import { resolveBadgeComponent } from "./badgeComponentResolver.js";
import { bubbleComponentToRenderable } from "./bubbleComponentRenderable.js";
import { resolveBubbleComponent } from "./bubbleComponentResolver.js";
import { buttonComponentToRenderable } from "./buttonComponentRenderable.js";
import { resolveButtonComponent } from "./buttonComponentResolver.js";
import { codeIndicatorComponentToRenderable } from "./codeIndicatorComponentRenderable.js";
import { resolveCodeIndicatorComponent } from "./codeIndicatorComponentResolver.js";
import { collectionStackComponentToRenderable } from "./collectionStackComponentRenderable.js";
import { resolveCollectionStackComponent } from "./collectionStackComponentResolver.js";
import { componentStackComponentToRenderable } from "./componentStackComponentRenderable.js";
import { resolveComponentStackComponent } from "./componentStackComponentResolver.js";
import { cursorComponentToRenderable } from "./cursorComponentRenderable.js";
import { resolveCursorComponent } from "./cursorComponentResolver.js";
import { drawPasswordComponentToRenderable } from "./drawPasswordComponentRenderable.js";
import { resolveDrawPasswordComponent } from "./drawPasswordComponentResolver.js";
import { faceRecognitionComponentToRenderable } from "./faceRecognitionComponentRenderable.js";
import { resolveFaceRecognitionComponent } from "./faceRecognitionComponentResolver.js";
import { fingerprintComponentToRenderable } from "./fingerprintComponentRenderable.js";
import { resolveFingerprintComponent } from "./fingerprintComponentResolver.js";
import { galleryComponentToRenderable } from "./galleryComponentRenderable.js";
import { resolveGalleryComponent } from "./galleryComponentResolver.js";
import { iconBarComponentToRenderable } from "./iconBarComponentRenderable.js";
import { resolveIconBarComponent } from "./iconBarComponentResolver.js";
import { iconRowComponentToRenderable } from "./iconRowComponentRenderable.js";
import { resolveIconRowComponent } from "./iconRowComponentResolver.js";
import { incomingCallNotificationComponentToRenderable } from "./incomingCallNotificationComponentRenderable.js";
import { resolveIncomingCallNotificationComponent } from "./incomingCallNotificationComponentResolver.js";
import { keyboardComponentToRenderable } from "./keyboardComponentRenderable.js";
import { resolveKeyboardComponent } from "./keyboardComponentResolver.js";
import { keypadComponentToRenderable } from "./keypadComponentRenderable.js";
import { resolveKeypadComponent } from "./keypadComponentResolver.js";
import { labelComponentToRenderable } from "./labelComponentRenderable.js";
import { resolveLabelComponent } from "./labelComponentResolver.js";
import { listComponentToRenderable } from "./listComponentRenderable.js";
import { resolveListComponent } from "./listComponentResolver.js";
import { listItemComponentToRenderable } from "./listItemComponentRenderable.js";
import { resolveListItemComponent } from "./listItemComponentResolver.js";
import { mediaComponentToRenderable } from "./mediaComponentRenderable.js";
import { resolveMediaComponent } from "./mediaComponentResolver.js";
import { navigationBarComponentToRenderable } from "./navigationBarComponentRenderable.js";
import { resolveNavigationBarComponent } from "./navigationBarComponentResolver.js";
import { notificationComponentToRenderable } from "./notificationComponentRenderable.js";
import { resolveNotificationComponent } from "./notificationComponentResolver.js";
import { notificationsComponentToRenderable } from "./notificationsComponentRenderable.js";
import { resolveNotificationsComponent } from "./notificationsComponentResolver.js";
import { passwordComponentToRenderable } from "./passwordComponentRenderable.js";
import { resolvePasswordComponent } from "./passwordComponentResolver.js";
import { statusBarComponentToRenderable } from "./statusBarComponentRenderable.js";
import { resolveStatusBarComponent } from "./statusBarComponentResolver.js";
import { surfaceComponentToRenderable } from "./surfaceComponentRenderable.js";
import { resolveSurfaceComponent } from "./surfaceComponentResolver.js";
import { textBoxComponentToRenderable } from "./textBoxComponentRenderable.js";
import { resolveTextBoxComponent } from "./textBoxComponentResolver.js";
import { textInputBarComponentToRenderable } from "./textInputBarComponentRenderable.js";
import { resolveTextInputBarComponent } from "./textInputBarComponentResolver.js";

export const generatedComponentScaffoldFactories = {
  audio: (payload) =>
    audioComponentToRenderable(payload, resolveAudioComponent(payload)),
  avatar: (payload) =>
    avatarComponentToRenderable(payload, resolveAvatarComponent(payload)),
  badge: (payload) =>
    badgeComponentToRenderable(payload, resolveBadgeComponent(payload)),
  bubble: (payload) =>
    bubbleComponentToRenderable(payload, resolveBubbleComponent(payload)),
  button: (payload) =>
    buttonComponentToRenderable(payload, resolveButtonComponent(payload)),
  codeIndicator: (payload) =>
    codeIndicatorComponentToRenderable(payload, resolveCodeIndicatorComponent(payload)),
  collectionStack: (payload, _assignedBox, renderChild) =>
    collectionStackComponentToRenderable(payload, resolveCollectionStackComponent(payload), renderChild),
  componentStack: (payload, _assignedBox, renderChild) =>
    componentStackComponentToRenderable(payload, resolveComponentStackComponent(payload), renderChild),
  cursor: (payload) =>
    cursorComponentToRenderable(payload, resolveCursorComponent(payload)),
  drawPassword: (payload) =>
    drawPasswordComponentToRenderable(payload, resolveDrawPasswordComponent(payload)),
  faceRecognition: (payload) =>
    faceRecognitionComponentToRenderable(payload, resolveFaceRecognitionComponent(payload)),
  fingerprint: (payload) =>
    fingerprintComponentToRenderable(payload, resolveFingerprintComponent(payload)),
  gallery: (payload) =>
    galleryComponentToRenderable(payload, resolveGalleryComponent(payload)),
  iconBar: (payload) =>
    iconBarComponentToRenderable(payload, resolveIconBarComponent(payload)),
  iconRow: (payload) =>
    iconRowComponentToRenderable(payload, resolveIconRowComponent(payload)),
  incomingCallNotification: (payload, assignedBox) =>
    incomingCallNotificationComponentToRenderable(payload, resolveIncomingCallNotificationComponent(payload), assignedBox),
  keyboard: (payload) =>
    keyboardComponentToRenderable(payload, resolveKeyboardComponent(payload)),
  keypad: (payload) =>
    keypadComponentToRenderable(payload, resolveKeypadComponent(payload)),
  label: (payload) =>
    labelComponentToRenderable(payload, resolveLabelComponent(payload)),
  list: (payload, assignedBox, renderChild) =>
    listComponentToRenderable(payload, resolveListComponent(payload), assignedBox, renderChild),
  listItem: (payload, assignedBox) =>
    listItemComponentToRenderable(payload, resolveListItemComponent(payload), assignedBox),
  media: (payload, assignedBox) =>
    mediaComponentToRenderable(payload, resolveMediaComponent(payload), assignedBox),
  navigation_bar: (payload) =>
    navigationBarComponentToRenderable(payload, resolveNavigationBarComponent(payload)),
  notification: (payload, assignedBox) =>
    notificationComponentToRenderable(payload, resolveNotificationComponent(payload), assignedBox),
  notifications: (payload, _assignedBox, renderChild) =>
    notificationsComponentToRenderable(payload, resolveNotificationsComponent(payload), renderChild),
  password: (payload, assignedBox) =>
    passwordComponentToRenderable(payload, resolvePasswordComponent(payload), assignedBox),
  status_bar: (payload) =>
    statusBarComponentToRenderable(payload, resolveStatusBarComponent(payload)),
  surface: (payload) =>
    surfaceComponentToRenderable(payload, resolveSurfaceComponent(payload)),
  textBox: (payload) =>
    textBoxComponentToRenderable(payload, resolveTextBoxComponent(payload)),
  textInputBar: (payload) =>
    textInputBarComponentToRenderable(payload, resolveTextInputBarComponent(payload)),
} satisfies Record<string, ComponentRenderableFactory>;
