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
  button_icon: {
    category: "atom",
    contract: "./buttonIconComponentContract",
    resolver: "./buttonIconComponentResolver",
    renderable: "./buttonIconComponentRenderable",
    embeds: ["label"],
  },
  audio_message: {
    category: "component",
    contract: "./audioComponentContract",
    resolver: "./audioComponentResolver",
    renderable: "./audioComponentRenderable",
    embeds: ["avatar", "button_icon"],
  },
  statusBar: {
    category: "system",
    contract: "./systemBarPreviewResolver",
    resolver: "./systemBarPreviewResolver",
    renderable: "./systemBarRenderables",
    embeds: [],
  },
  navigationBar: {
    category: "system",
    contract: "./systemBarPreviewResolver",
    resolver: "./systemBarPreviewResolver",
    renderable: "./systemBarRenderables",
    embeds: [],
  },
} as const satisfies Record<string, DesktopPreviewComponentManifestEntry>;

export type DesktopPreviewComponentClass = keyof typeof desktopPreviewComponents;

export function isDesktopPreviewComponentClass(
  value: string,
): value is DesktopPreviewComponentClass {
  return Object.hasOwn(desktopPreviewComponents, value);
}

