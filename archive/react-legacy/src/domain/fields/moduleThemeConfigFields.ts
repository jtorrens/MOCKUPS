import {
  defineFields,
  type JsonFieldBinding,
} from "../value-system/index.js";

export const MODULE_THEME_CONFIG_FIELDS = defineFields({
  id: {
    id: "moduleThemeConfig.id",
    kind: "text",
    ui: { label: "ID" },
  },
  productionId: {
    id: "moduleThemeConfig.productionId",
    kind: "recordReference",
    ui: {
      label: "Production",
      tableId: "productions",
      labelColumn: "name",
    },
  },
  themeId: {
    id: "moduleThemeConfig.themeId",
    kind: "recordReference",
    ui: {
      label: "Theme",
      tableId: "themes",
      labelColumn: "name",
    },
  },
  appId: {
    id: "moduleThemeConfig.appId",
    kind: "recordReference",
    ui: {
      label: "App",
      tableId: "apps",
      labelColumn: "name",
    },
  },
  moduleId: {
    id: "moduleThemeConfig.moduleId",
    kind: "text",
    ui: { label: "Module ID" },
  },
  moduleSchemaVersion: {
    id: "moduleThemeConfig.moduleSchemaVersion",
    kind: "integer",
    defaultValue: 1,
    ui: {
      label: "Schema version",
      min: 1,
      step: 1,
    },
  },
  name: {
    id: "moduleThemeConfig.name",
    kind: "text",
    ui: { label: "Name" },
  },
  tokens: {
    id: "moduleThemeConfig.tokens",
    kind: "jsonObject",
    ui: { label: "Module design tokens" },
  },
  metadata: {
    id: "moduleThemeConfig.metadata",
    kind: "jsonObject",
    ui: { label: "Module theme notes" },
  },
});

export const MODULE_THEME_CONFIG_COLUMN_BINDINGS = [
  { outputPath: ["id"], field: MODULE_THEME_CONFIG_FIELDS.id },
  {
    outputPath: ["production_id"],
    field: MODULE_THEME_CONFIG_FIELDS.productionId,
  },
  { outputPath: ["theme_id"], field: MODULE_THEME_CONFIG_FIELDS.themeId },
  { outputPath: ["app_id"], field: MODULE_THEME_CONFIG_FIELDS.appId },
  { outputPath: ["module_id"], field: MODULE_THEME_CONFIG_FIELDS.moduleId },
  {
    outputPath: ["module_schema_version"],
    field: MODULE_THEME_CONFIG_FIELDS.moduleSchemaVersion,
  },
  { outputPath: ["name"], field: MODULE_THEME_CONFIG_FIELDS.name },
  { outputPath: ["tokens_json"], field: MODULE_THEME_CONFIG_FIELDS.tokens },
  { outputPath: ["metadata_json"], field: MODULE_THEME_CONFIG_FIELDS.metadata },
] satisfies readonly JsonFieldBinding[];
