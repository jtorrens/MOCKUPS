import {
  defineFields,
  type JsonFieldBinding,
} from "../value-system/index.js";

const GROUPS = {
  wallpaper: { id: "wallpaper", label: "Wallpaper" },
  icon: { id: "icon", label: "Icon" },
} as const;

export const APP_FIELDS = defineFields({
  id: {
    id: "app.id",
    kind: "text",
    ui: { label: "ID" },
  },
  productionId: {
    id: "app.productionId",
    kind: "recordReference",
    ui: {
      label: "Production",
      tableId: "productions",
      labelColumn: "name",
    },
  },
  name: {
    id: "app.name",
    kind: "text",
    ui: { label: "Name" },
  },
  bundleKey: {
    id: "app.bundleKey",
    kind: "text",
    ui: { label: "Bundle key" },
  },
  appType: {
    id: "app.appType",
    kind: "text",
    ui: { label: "App type" },
  },
  config: {
    id: "app.config",
    kind: "jsonObject",
    ui: { label: "App settings" },
  },
  metadata: {
    id: "app.metadata",
    kind: "jsonObject",
    ui: { label: "App notes" },
  },
  wallpaperKind: {
    id: "app.wallpaper.kind",
    kind: "enum",
    defaultValue: "solid",
    ui: {
      label: "Kind",
      group: GROUPS.wallpaper,
      options: ["solid", "image"],
    },
  },
  wallpaperOpacity: {
    id: "app.wallpaper.opacity",
    kind: "alpha",
    defaultValue: 1,
    ui: {
      label: "Opacity",
      group: GROUPS.wallpaper,
      step: 0.01,
    },
  },
  wallpaperColorLight: {
    id: "app.wallpaper.color.light",
    kind: "paletteColorToken",
    defaultValue: "gray_100",
    ui: {
      label: "Wallpaper color light",
      group: GROUPS.wallpaper,
      pair: {
        id: "app.wallpaper.color",
        label: "Wallpaper color",
        role: "light",
      },
    },
  },
  wallpaperColorDark: {
    id: "app.wallpaper.color.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_000",
    ui: {
      label: "Wallpaper color dark",
      group: GROUPS.wallpaper,
      pair: {
        id: "app.wallpaper.color",
        label: "Wallpaper color",
        role: "dark",
      },
    },
  },
  wallpaperImageFilePath: {
    id: "app.wallpaper.image.filePath",
    kind: "relativeFilePath",
    ui: {
      label: "Image",
      group: GROUPS.wallpaper,
      fileKind: "file",
      accept: ["image/*"],
    },
  },
  note: {
    id: "app.note",
    kind: "text",
    ui: {
      label: "Note",
      multiline: true,
      rows: 4,
    },
  },
  iconFilePath: {
    id: "app.icon.filePath",
    kind: "relativeFilePath",
    ui: {
      label: "App icon image",
      group: GROUPS.icon,
      fileKind: "file",
      accept: ["image/*"],
    },
  },
  iconScale: {
    id: "app.icon.scale",
    kind: "decimal",
    defaultValue: 1,
    ui: {
      label: "Icon scale",
      group: GROUPS.icon,
      min: 0.01,
      step: 0.01,
    },
  },
  iconOffsetX: {
    id: "app.icon.offsetX",
    kind: "integer",
    defaultValue: 0,
    ui: {
      label: "Icon offset X",
      group: GROUPS.icon,
      step: 1,
      pair: { id: "app.icon.offset", label: "Icon offset", role: "X" },
    },
  },
  iconOffsetY: {
    id: "app.icon.offsetY",
    kind: "integer",
    defaultValue: 0,
    ui: {
      label: "Icon offset Y",
      group: GROUPS.icon,
      step: 1,
      pair: { id: "app.icon.offset", label: "Icon offset", role: "Y" },
    },
  },
  iconBaseSize: {
    id: "app.icon.baseSize",
    kind: "integer",
    defaultValue: 640,
    ui: {
      label: "Icon base size",
      group: GROUPS.icon,
      hidden: true,
      min: 1,
      step: 1,
    },
  },
});

export const APP_COLUMN_BINDINGS = [
  { outputPath: ["id"], field: APP_FIELDS.id },
  { outputPath: ["production_id"], field: APP_FIELDS.productionId },
  { outputPath: ["name"], field: APP_FIELDS.name },
  { outputPath: ["bundle_key"], field: APP_FIELDS.bundleKey },
  { outputPath: ["app_type"], field: APP_FIELDS.appType },
  { outputPath: ["config_json"], field: APP_FIELDS.config },
  { outputPath: ["metadata_json"], field: APP_FIELDS.metadata },
] satisfies readonly JsonFieldBinding[];

export const APP_CONFIG_BINDINGS = [
  { outputPath: ["wallpaper", "kind"], field: APP_FIELDS.wallpaperKind },
  { outputPath: ["wallpaper", "opacity"], field: APP_FIELDS.wallpaperOpacity },
  {
    outputPath: ["modes", "light", "wallpaper", "color"],
    field: APP_FIELDS.wallpaperColorLight,
  },
  {
    outputPath: ["modes", "dark", "wallpaper", "color"],
    field: APP_FIELDS.wallpaperColorDark,
  },
  {
    outputPath: ["wallpaper", "image", "filePath"],
    field: APP_FIELDS.wallpaperImageFilePath,
  },
] satisfies readonly JsonFieldBinding[];

export const APP_METADATA_BINDINGS = [
  { outputPath: ["note"], field: APP_FIELDS.note },
  { outputPath: ["icon", "filePath"], field: APP_FIELDS.iconFilePath },
  { outputPath: ["icon", "scale"], field: APP_FIELDS.iconScale },
  { outputPath: ["icon", "offsetX"], field: APP_FIELDS.iconOffsetX },
  { outputPath: ["icon", "offsetY"], field: APP_FIELDS.iconOffsetY },
  { outputPath: ["icon", "baseSize"], field: APP_FIELDS.iconBaseSize },
] satisfies readonly JsonFieldBinding[];
