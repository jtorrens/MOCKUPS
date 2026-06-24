import type { FieldDescriptor } from "./types.js";

export const appConfigDescriptors: FieldDescriptor[] = [
  {
    canonicalPath: "app.tokens.wallpaper.kind",
    storagePath: ["tokens_json", "wallpaper", "kind"],
    label: "Kind",
    section: "Tokens",
    area: "tokens",
    group: "Wallpaper",
    property: "kind",
    widget: "select",
    options: ["solid", "image"],
  },
  {
    canonicalPath: "app.tokens.wallpaper.opacity",
    storagePath: ["tokens_json", "wallpaper", "opacity"],
    label: "Opacity",
    section: "Tokens",
    area: "tokens",
    group: "Wallpaper",
    property: "opacity",
    widget: "number",
  },
  {
    canonicalPath: "app.tokens.wallpaper.color.light",
    storagePath: ["tokens_json", "modes", "light", "wallpaper", "color"],
    label: "Wallpaper color",
    section: "Tokens",
    area: "tokens",
    group: "Wallpaper",
    property: "color",
    widget: "color",
  },
  {
    canonicalPath: "app.tokens.wallpaper.color.dark",
    storagePath: ["tokens_json", "modes", "dark", "wallpaper", "color"],
    label: "Wallpaper color",
    section: "Tokens",
    area: "tokens",
    group: "Wallpaper",
    property: "color",
    widget: "color",
  },
  {
    canonicalPath: "app.tokens.wallpaper.image.filePath",
    storagePath: ["tokens_json", "wallpaper", "image", "filePath"],
    label: "Image",
    section: "Tokens",
    area: "tokens",
    group: "Wallpaper",
    property: "filePath",
  },
];

export const appMetadataDescriptors: FieldDescriptor[] = [
  {
    canonicalPath: "app.notes.note",
    storagePath: ["note"],
    label: "Note",
    section: "Notes",
    area: "notes",
    property: "note",
    widget: "textarea",
  },
];
