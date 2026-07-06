export type DesktopPreviewComponentCategory = "system" | "atom" | "component";

export interface DesktopPreviewComponentManifestEntry {
  category: DesktopPreviewComponentCategory;
  contract: string;
  resolver: string;
  renderable: string;
  embeds: readonly string[];
}

export const desktopPreviewComponents = {
  label: {
    category: "atom",
    contract: "./labelComponentContract",
    resolver: "./labelComponentResolver",
    renderable: "./labelComponentRenderable",
    embeds: [],
  },
  avatar: {
    category: "component",
    contract: "./avatarComponentContract",
    resolver: "./avatarComponentResolver",
    renderable: "./avatarComponentRenderable",
    embeds: ["label"],
  },
  buttonIcon: {
    category: "atom",
    contract: "./buttonIconComponentContract",
    resolver: "./buttonIconComponentResolver",
    renderable: "./buttonIconComponentRenderable",
    embeds: ["label"],
  },
  audio: {
    category: "component",
    contract: "./audioComponentContract",
    resolver: "./audioComponentResolver",
    renderable: "./audioComponentRenderable",
    embeds: ["avatar", "buttonIcon"],
  },
  textInputBar: {
    category: "system",
    contract: "./textInputBarComponentContract",
    resolver: "./textInputBarComponentResolver",
    renderable: "./textInputBarComponentRenderable",
    embeds: [],
  },
  keyboard: {
    category: "system",
    contract: "./keyboardComponentContract",
    resolver: "./keyboardComponentResolver",
    renderable: "./keyboardComponentRenderable",
    embeds: [],
  },
  video: {
    category: "component",
    contract: "./videoComponentContract",
    resolver: "./videoComponentResolver",
    renderable: "./videoComponentRenderable",
    embeds: [],
  },
  status_bar: {
    category: "system",
    contract: "./statusBarComponentContract",
    resolver: "./statusBarComponentResolver",
    renderable: "./statusBarComponentRenderable",
    embeds: [],
  },
  navigation_bar: {
    category: "system",
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
