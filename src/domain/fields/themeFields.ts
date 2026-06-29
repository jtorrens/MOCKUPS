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
