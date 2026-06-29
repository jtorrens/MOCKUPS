import {
  defineFields,
  type JsonFieldBinding,
} from "../value-system/index.js";

export const THEME_FIELDS = defineFields({
  id: {
    id: "theme.id",
    kind: "text",
    ui: {
      label: "ID",
    },
  },
  productionId: {
    id: "theme.productionId",
    kind: "recordReference",
    ui: {
      label: "Production",
      tableId: "productions",
      labelColumn: "name",
    },
  },
  name: {
    id: "theme.name",
    kind: "text",
    ui: {
      label: "Name",
    },
  },
  family: {
    id: "theme.family",
    kind: "enum",
    defaultValue: "ios",
    ui: {
      label: "Family",
      options: ["ios", "android"],
    },
  },
  iconThemeId: {
    id: "theme.iconThemeId",
    kind: "recordReference",
    ui: {
      label: "Icon theme",
      tableId: "icon_themes",
      labelColumn: "name",
      allowEmpty: true,
    },
  },
  statusBarId: {
    id: "theme.statusBarId",
    kind: "recordReference",
    ui: {
      label: "Status bar",
      tableId: "status_bars",
      labelColumn: "name",
      allowEmpty: true,
    },
  },
  navigationBarId: {
    id: "theme.navigationBarId",
    kind: "recordReference",
    ui: {
      label: "Navigation bar",
      tableId: "navigation_bars",
      labelColumn: "name",
      allowEmpty: true,
    },
  },
  version: {
    id: "theme.version",
    kind: "text",
    defaultValue: "1.0.0",
    ui: {
      label: "Version",
    },
  },
  tokens: {
    id: "theme.tokens",
    kind: "jsonObject",
    ui: {
      label: "Theme tokens",
    },
  },
  neutralTintHueDeg: {
    id: "theme.neutralTint.hueDeg",
    kind: "integer",
    defaultValue: 0,
    ui: {
      label: "Hue",
      min: 0,
      max: 360,
      step: 1,
    },
  },
  neutralTintSaturation: {
    id: "theme.neutralTint.saturation",
    kind: "alpha",
    defaultValue: 0,
    ui: {
      label: "Saturation",
      min: 0,
      max: 1,
      step: 0.01,
    },
  },
  cursorWidth: {
    id: "theme.cursor.width",
    kind: "integer",
    defaultValue: 2,
    ui: {
      label: "Width",
      min: 1,
      step: 1,
    },
  },
  cursorBlinkFrames: {
    id: "theme.cursor.blinkFrames",
    kind: "integer",
    defaultValue: 15,
    ui: {
      label: "Blink frames",
      min: 1,
      step: 1,
    },
  },
  surfaceReliefAngleDeg: {
    id: "theme.surfaceRelief.default.angleDeg",
    kind: "integer",
    defaultValue: -45,
    ui: {
      label: "Angle",
      step: 1,
    },
  },
  surfaceReliefExtension: {
    id: "theme.surfaceRelief.default.extension",
    kind: "decimal",
    defaultValue: 1,
    ui: {
      label: "Extension",
      step: 0.1,
    },
  },
  surfaceReliefSpread: {
    id: "theme.surfaceRelief.default.spread",
    kind: "decimal",
    defaultValue: 0,
    ui: {
      label: "Spread",
      step: 0.1,
    },
  },
  surfaceReliefUpperIntensity: {
    id: "theme.surfaceRelief.default.upperIntensity",
    kind: "decimal",
    defaultValue: 0.1,
    ui: {
      label: "Upper intensity",
      step: 0.01,
    },
  },
  surfaceReliefLowerIntensity: {
    id: "theme.surfaceRelief.default.lowerIntensity",
    kind: "decimal",
    defaultValue: -0.08,
    ui: {
      label: "Lower intensity",
      step: 0.01,
    },
  },
  typographyFamily: {
    id: "theme.typography.family",
    kind: "fontFamily",
    ui: {
      label: "Font family",
    },
  },
  typographyBodySize: {
    id: "theme.typography.bodySize",
    kind: "decimal",
    ui: {
      label: "Body size",
      min: 1,
      step: 1,
    },
  },
  typographyBodyLineHeight: {
    id: "theme.typography.bodyLineHeight",
    kind: "decimal",
    ui: {
      label: "Body line height",
      min: 1,
      step: 1,
    },
  },
  typographyCaptionSize: {
    id: "theme.typography.captionSize",
    kind: "decimal",
    ui: {
      label: "Caption size",
      min: 1,
      step: 1,
    },
  },
  typographyFontWeight: {
    id: "theme.typography.fontWeight",
    kind: "fontWeight",
    ui: {
      label: "Font weight",
    },
  },
  typographyFontStyle: {
    id: "theme.typography.fontStyle",
    kind: "fontStyle",
    ui: {
      label: "Font style",
    },
  },
});

export const THEME_COLUMN_BINDINGS = [
  { outputPath: ["id"], field: THEME_FIELDS.id },
  { outputPath: ["production_id"], field: THEME_FIELDS.productionId },
  { outputPath: ["name"], field: THEME_FIELDS.name },
  { outputPath: ["family"], field: THEME_FIELDS.family },
  { outputPath: ["icon_theme_id"], field: THEME_FIELDS.iconThemeId },
  { outputPath: ["status_bar_id"], field: THEME_FIELDS.statusBarId },
  { outputPath: ["navigation_bar_id"], field: THEME_FIELDS.navigationBarId },
  { outputPath: ["version"], field: THEME_FIELDS.version },
  { outputPath: ["tokens_json"], field: THEME_FIELDS.tokens },
] satisfies readonly JsonFieldBinding[];

export const THEME_TOKEN_BINDINGS = [
  {
    outputPath: ["neutralTint", "hueDeg"],
    field: THEME_FIELDS.neutralTintHueDeg,
  },
  {
    outputPath: ["neutralTint", "saturation"],
    field: THEME_FIELDS.neutralTintSaturation,
  },
  { outputPath: ["cursor", "width"], field: THEME_FIELDS.cursorWidth },
  {
    outputPath: ["cursor", "blinkFrames"],
    field: THEME_FIELDS.cursorBlinkFrames,
  },
  {
    outputPath: ["surfaceRelief", "default", "angleDeg"],
    field: THEME_FIELDS.surfaceReliefAngleDeg,
  },
  {
    outputPath: ["surfaceRelief", "default", "extension"],
    field: THEME_FIELDS.surfaceReliefExtension,
  },
  {
    outputPath: ["surfaceRelief", "default", "spread"],
    field: THEME_FIELDS.surfaceReliefSpread,
  },
  {
    outputPath: ["surfaceRelief", "default", "upperIntensity"],
    field: THEME_FIELDS.surfaceReliefUpperIntensity,
  },
  {
    outputPath: ["surfaceRelief", "default", "lowerIntensity"],
    field: THEME_FIELDS.surfaceReliefLowerIntensity,
  },
  {
    outputPath: ["typography", "family"],
    field: THEME_FIELDS.typographyFamily,
  },
  {
    outputPath: ["typography", "bodySize"],
    field: THEME_FIELDS.typographyBodySize,
  },
  {
    outputPath: ["typography", "bodyLineHeight"],
    field: THEME_FIELDS.typographyBodyLineHeight,
  },
  {
    outputPath: ["typography", "captionSize"],
    field: THEME_FIELDS.typographyCaptionSize,
  },
  {
    outputPath: ["typography", "fontWeight"],
    field: THEME_FIELDS.typographyFontWeight,
  },
  {
    outputPath: ["typography", "fontStyle"],
    field: THEME_FIELDS.typographyFontStyle,
  },
] satisfies readonly JsonFieldBinding[];
