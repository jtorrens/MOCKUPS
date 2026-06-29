import {
  defineFields,
  type JsonFieldBinding,
} from "../value-system/index.js";

export const ACTOR_FIELDS = defineFields({
  id: {
    id: "actor.id",
    kind: "text",
    ui: {
      label: "ID",
    },
  },
  productionId: {
    id: "actor.productionId",
    kind: "recordReference",
    ui: {
      label: "Production",
      tableId: "productions",
      labelColumn: "name",
    },
  },
  displayName: {
    id: "actor.displayName",
    kind: "text",
    ui: {
      label: "Display name",
    },
  },
  shortName: {
    id: "actor.shortName",
    kind: "text",
    ui: {
      label: "Short name",
    },
  },
  avatarAssetId: {
    id: "actor.avatarAssetId",
    kind: "recordReference",
    ui: {
      label: "Avatar asset",
      tableId: "media_assets",
      labelColumn: "name",
      allowEmpty: true,
    },
  },
  defaultDeviceId: {
    id: "actor.defaultDeviceId",
    kind: "recordReference",
    ui: {
      label: "Default device",
      tableId: "devices",
      labelColumn: "name",
      allowEmpty: true,
    },
  },
  defaultThemeId: {
    id: "actor.defaultThemeId",
    kind: "recordReference",
    ui: {
      label: "Default theme",
      tableId: "themes",
      labelColumn: "name",
      allowEmpty: true,
    },
  },
  metadata: {
    id: "actor.metadata",
    kind: "jsonObject",
    ui: {
      label: "Actor metadata",
    },
  },
  colorLight: {
    id: "actor.color.light",
    kind: "paletteColorToken",
    ui: {
      label: "Actor color light",
      pair: { id: "actor.color", label: "Actor color", role: "light" },
    },
  },
  colorDark: {
    id: "actor.color.dark",
    kind: "paletteColorToken",
    ui: {
      label: "Actor color dark",
      pair: { id: "actor.color", label: "Actor color", role: "dark" },
    },
  },
  avatarTextColorLight: {
    id: "actor.avatarTextColor.light",
    kind: "paletteColorToken",
    ui: {
      label: "Avatar text color light",
      pair: {
        id: "actor.avatarTextColor",
        label: "Avatar text color",
        role: "light",
      },
    },
  },
  avatarTextColorDark: {
    id: "actor.avatarTextColor.dark",
    kind: "paletteColorToken",
    ui: {
      label: "Avatar text color dark",
      pair: {
        id: "actor.avatarTextColor",
        label: "Avatar text color",
        role: "dark",
      },
    },
  },
  avatarUseInitials: {
    id: "actor.avatar.useInitials",
    kind: "boolean",
    defaultValue: false,
    ui: {
      label: "Use initials",
    },
  },
  avatarFilePath: {
    id: "actor.avatar.filePath",
    kind: "relativeFilePath",
    ui: {
      label: "Avatar image",
      fileKind: "file",
      accept: ["image/*"],
    },
  },
  avatarScale: {
    id: "actor.avatar.scale",
    kind: "decimal",
    defaultValue: 1,
    ui: {
      label: "Avatar scale",
      min: 0.01,
      step: 0.01,
    },
  },
  avatarOffsetX: {
    id: "actor.avatar.offsetX",
    kind: "integer",
    defaultValue: 0,
    ui: {
      label: "Avatar offset X",
      step: 1,
      pair: { id: "actor.avatar.offset", label: "Avatar offset", role: "X" },
    },
  },
  avatarOffsetY: {
    id: "actor.avatar.offsetY",
    kind: "integer",
    defaultValue: 0,
    ui: {
      label: "Avatar offset Y",
      step: 1,
      pair: { id: "actor.avatar.offset", label: "Avatar offset", role: "Y" },
    },
  },
  avatarBaseSize: {
    id: "actor.avatar.baseSize",
    kind: "integer",
    defaultValue: 640,
    ui: {
      label: "Avatar base size",
      min: 1,
      step: 1,
      hidden: true,
    },
  },
  avatarInitialsPadding: {
    id: "actor.avatar.initialsPadding",
    kind: "integer",
    defaultValue: 96,
    ui: {
      label: "Initials padding",
      min: 0,
      step: 1,
    },
  },
});

export const ACTOR_COLUMN_BINDINGS = [
  { outputPath: ["id"], field: ACTOR_FIELDS.id },
  { outputPath: ["production_id"], field: ACTOR_FIELDS.productionId },
  { outputPath: ["display_name"], field: ACTOR_FIELDS.displayName },
  { outputPath: ["short_name"], field: ACTOR_FIELDS.shortName },
  { outputPath: ["avatar_asset_id"], field: ACTOR_FIELDS.avatarAssetId },
  { outputPath: ["default_device_id"], field: ACTOR_FIELDS.defaultDeviceId },
  { outputPath: ["default_theme_id"], field: ACTOR_FIELDS.defaultThemeId },
  { outputPath: ["metadata_json"], field: ACTOR_FIELDS.metadata },
] satisfies readonly JsonFieldBinding[];

export const ACTOR_METADATA_BINDINGS = [
  { outputPath: ["modes", "light", "color"], field: ACTOR_FIELDS.colorLight },
  { outputPath: ["modes", "dark", "color"], field: ACTOR_FIELDS.colorDark },
  {
    outputPath: ["modes", "light", "avatarTextColor"],
    field: ACTOR_FIELDS.avatarTextColorLight,
  },
  {
    outputPath: ["modes", "dark", "avatarTextColor"],
    field: ACTOR_FIELDS.avatarTextColorDark,
  },
  {
    outputPath: ["avatar", "useInitials"],
    field: ACTOR_FIELDS.avatarUseInitials,
  },
  { outputPath: ["avatar", "filePath"], field: ACTOR_FIELDS.avatarFilePath },
  { outputPath: ["avatar", "scale"], field: ACTOR_FIELDS.avatarScale },
  { outputPath: ["avatar", "offsetX"], field: ACTOR_FIELDS.avatarOffsetX },
  { outputPath: ["avatar", "offsetY"], field: ACTOR_FIELDS.avatarOffsetY },
  { outputPath: ["avatar", "baseSize"], field: ACTOR_FIELDS.avatarBaseSize },
  {
    outputPath: ["avatar", "initialsPadding"],
    field: ACTOR_FIELDS.avatarInitialsPadding,
  },
] satisfies readonly JsonFieldBinding[];
