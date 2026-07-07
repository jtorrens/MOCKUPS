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
    embeds: ["surface", "cursor"],
  },
  iconRow: {
    category: "atom",
    migrationStatus: "structural",
    contract: "./iconRowComponentContract",
    resolver: "./iconRowComponentResolver",
    renderable: "./iconRowComponentRenderable",
    embeds: ["buttonIcon"],
  },
  avatar: {
    category: "component",
    migrationStatus: "functional",
    contract: "./avatarComponentContract",
    resolver: "./avatarComponentResolver",
    renderable: "./avatarComponentRenderable",
    embeds: ["label"],
  },
  buttonIcon: {
    category: "atom",
    migrationStatus: "functional",
    contract: "./buttonIconComponentContract",
    resolver: "./buttonIconComponentResolver",
    renderable: "./buttonIconComponentRenderable",
    embeds: ["surface", "label"],
  },
  audio: {
    category: "component",
    migrationStatus: "functional",
    contract: "./audioComponentContract",
    resolver: "./audioComponentResolver",
    renderable: "./audioComponentRenderable",
    embeds: ["surface", "avatar", "buttonIcon"],
  },
  textInputBar: {
    category: "system",
    migrationStatus: "structural",
    contract: "./textInputBarComponentContract",
    resolver: "./textInputBarComponentResolver",
    renderable: "./textInputBarComponentRenderable",
    embeds: ["surface", "textBox", "iconRow"],
  },
  keyboard: {
    category: "system",
    migrationStatus: "structural",
    contract: "./keyboardComponentContract",
    resolver: "./keyboardComponentResolver",
    renderable: "./keyboardComponentRenderable",
    embeds: [],
  },
  video: {
    category: "component",
    migrationStatus: "structural",
    contract: "./videoComponentContract",
    resolver: "./videoComponentResolver",
    renderable: "./videoComponentRenderable",
    embeds: ["surface"],
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
