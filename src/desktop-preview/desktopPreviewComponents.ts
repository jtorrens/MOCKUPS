export type DesktopPreviewComponentCategory = "system" | "atom" | "component";
export type DesktopPreviewComponentMigrationStatus = "structural" | "functional";

export interface DesktopPreviewComponentManifestEntry {
  category: DesktopPreviewComponentCategory;
  migrationStatus: DesktopPreviewComponentMigrationStatus;
  contract: string;
  resolver: string;
  renderable: string;
  embeds: readonly string[];
}

export const desktopPreviewComponents = {
  label: {
    category: "atom",
    migrationStatus: "functional",
    contract: "./labelComponentContract",
    resolver: "./labelComponentResolver",
    renderable: "./labelComponentRenderable",
    embeds: ["surface"],
  },
  surface: {
    category: "atom",
    migrationStatus: "structural",
    contract: "./surfaceComponentContract",
    resolver: "./surfaceComponentResolver",
    renderable: "./surfaceComponentRenderable",
    embeds: [],
  },
  cursor: {
    category: "atom",
    migrationStatus: "structural",
    contract: "./cursorComponentContract",
    resolver: "./cursorComponentResolver",
    renderable: "./cursorComponentRenderable",
    embeds: [],
  },
  textBox: {
    category: "atom",
    migrationStatus: "structural",
    contract: "./textBoxComponentContract",
    resolver: "./textBoxComponentResolver",
    renderable: "./textBoxComponentRenderable",
    embeds: ["surface", "cursor", "iconRow"],
  },
  iconRow: {
    category: "atom",
    migrationStatus: "structural",
    contract: "./iconRowComponentContract",
    resolver: "./iconRowComponentResolver",
    renderable: "./iconRowComponentRenderable",
    embeds: ["button"],
  },
  iconBar: {
    category: "atom",
    migrationStatus: "structural",
    contract: "./iconBarComponentContract",
    resolver: "./iconBarComponentResolver",
    renderable: "./iconBarComponentRenderable",
    embeds: ["iconRow"],
  },
  componentStack: {
    category: "atom",
    migrationStatus: "structural",
    contract: "./componentStackComponentContract",
    resolver: "./componentStackComponentResolver",
    renderable: "./componentStackComponentRenderable",
    embeds: ["label", "surface", "cursor", "textBox", "iconRow", "iconBar", "collectionStack", "badge", "codeIndicator", "avatar", "button", "audio", "notification", "notifications", "textInputBar", "keyboard", "keypad", "fingerprint", "faceRecognition", "drawPassword", "password", "media", "bubble", "status_bar", "navigation_bar"],
  },
  collectionStack: {
    category: "atom",
    migrationStatus: "structural",
    contract: "./collectionStackComponentContract",
    resolver: "./collectionStackComponentResolver",
    renderable: "./collectionStackComponentRenderable",
    embeds: ["label", "surface", "cursor", "textBox", "iconRow", "iconBar", "componentStack", "badge", "codeIndicator", "avatar", "button", "audio", "notification", "notifications", "textInputBar", "keyboard", "keypad", "fingerprint", "faceRecognition", "drawPassword", "password", "media", "bubble", "status_bar", "navigation_bar"],
  },
  badge: {
    category: "atom",
    migrationStatus: "functional",
    contract: "./badgeComponentContract",
    resolver: "./badgeComponentResolver",
    renderable: "./badgeComponentRenderable",
    embeds: [],
  },
  notification: {
    category: "component",
    migrationStatus: "structural",
    contract: "./notificationComponentContract",
    resolver: "./notificationComponentResolver",
    renderable: "./notificationComponentRenderable",
    embeds: ["surface", "avatar", "label"],
  },
  notifications: {
    category: "component",
    migrationStatus: "structural",
    contract: "./notificationsComponentContract",
    resolver: "./notificationsComponentResolver",
    renderable: "./notificationsComponentRenderable",
    embeds: ["collectionStack", "badge"],
  },
  codeIndicator: {
    category: "atom",
    migrationStatus: "functional",
    contract: "./codeIndicatorComponentContract",
    resolver: "./codeIndicatorComponentResolver",
    renderable: "./codeIndicatorComponentRenderable",
    embeds: ["surface"],
  },
  avatar: {
    category: "component",
    migrationStatus: "functional",
    contract: "./avatarComponentContract",
    resolver: "./avatarComponentResolver",
    renderable: "./avatarComponentRenderable",
    embeds: ["label", "badge"],
  },
  button: {
    category: "atom",
    migrationStatus: "functional",
    contract: "./buttonComponentContract",
    resolver: "./buttonComponentResolver",
    renderable: "./buttonComponentRenderable",
    embeds: ["surface", "label", "badge"],
  },
  audio: {
    category: "component",
    migrationStatus: "functional",
    contract: "./audioComponentContract",
    resolver: "./audioComponentResolver",
    renderable: "./audioComponentRenderable",
    embeds: ["surface", "avatar", "button", "label"],
  },
  textInputBar: {
    category: "system",
    migrationStatus: "structural",
    contract: "./textInputBarComponentContract",
    resolver: "./textInputBarComponentResolver",
    renderable: "./textInputBarComponentRenderable",
    embeds: ["surface", "textBox", "iconBar"],
  },
  keyboard: {
    category: "system",
    migrationStatus: "structural",
    contract: "./keyboardComponentContract",
    resolver: "./keyboardComponentResolver",
    renderable: "./keyboardComponentRenderable",
    embeds: ["iconBar"],
  },
  keypad: {
    category: "system",
    migrationStatus: "functional",
    contract: "./keypadComponentContract",
    resolver: "./keypadComponentResolver",
    renderable: "./keypadComponentRenderable",
    embeds: ["label"],
  },
  fingerprint: {
    category: "system",
    migrationStatus: "functional",
    contract: "./fingerprintComponentContract",
    resolver: "./fingerprintComponentResolver",
    renderable: "./fingerprintComponentRenderable",
    embeds: [],
  },
  faceRecognition: {
    category: "system",
    migrationStatus: "functional",
    contract: "./faceRecognitionComponentContract",
    resolver: "./faceRecognitionComponentResolver",
    renderable: "./faceRecognitionComponentRenderable",
    embeds: [],
  },
  drawPassword: {
    category: "system",
    migrationStatus: "functional",
    contract: "./drawPasswordComponentContract",
    resolver: "./drawPasswordComponentResolver",
    renderable: "./drawPasswordComponentRenderable",
    embeds: [],
  },
  password: {
    category: "system",
    migrationStatus: "functional",
    contract: "./passwordComponentContract",
    resolver: "./passwordComponentResolver",
    renderable: "./passwordComponentRenderable",
    embeds: ["label", "codeIndicator", "keypad", "fingerprint", "faceRecognition", "drawPassword", "iconBar"],
  },
  media: {
    category: "component",
    migrationStatus: "structural",
    contract: "./mediaComponentContract",
    resolver: "./mediaComponentResolver",
    renderable: "./mediaComponentRenderable",
    embeds: ["surface", "iconBar", "label"],
  },
  bubble: {
    category: "component",
    migrationStatus: "structural",
    contract: "./bubbleComponentContract",
    resolver: "./bubbleComponentResolver",
    renderable: "./bubbleComponentRenderable",
    embeds: ["surface", "textBox", "media", "audio", "label", "avatar"],
  },
  status_bar: {
    category: "system",
    migrationStatus: "functional",
    contract: "./statusBarComponentContract",
    resolver: "./statusBarComponentResolver",
    renderable: "./statusBarComponentRenderable",
    embeds: [],
  },
  navigation_bar: {
    category: "system",
    migrationStatus: "functional",
    contract: "./navigationBarComponentContract",
    resolver: "./navigationBarComponentResolver",
    renderable: "./navigationBarComponentRenderable",
    embeds: [],
  },
} as const satisfies Record<string, DesktopPreviewComponentManifestEntry>;

export type DesktopPreviewComponentClass = keyof typeof desktopPreviewComponents;

export function isDesktopPreviewComponentClass(
  value: string,
): value is DesktopPreviewComponentClass {
  return Object.hasOwn(desktopPreviewComponents, value);
}
