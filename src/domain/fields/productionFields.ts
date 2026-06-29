import {
  defineFields,
  type JsonFieldBinding,
} from "../value-system/index.js";

export const PRODUCTION_FIELDS = defineFields({
  id: {
    id: "production.id",
    kind: "text",
    ui: {
      label: "ID",
    },
  },
  name: {
    id: "production.name",
    kind: "text",
    ui: {
      label: "Name",
    },
  },
  slug: {
    id: "production.slug",
    kind: "text",
    ui: {
      label: "Slug",
      allowEmpty: true,
    },
  },
  defaultFps: {
    id: "production.defaultFps",
    kind: "integer",
    defaultValue: 30,
    ui: {
      label: "Default FPS",
      min: 1,
      step: 1,
    },
  },
  settings: {
    id: "production.settings",
    kind: "jsonObject",
    ui: {
      label: "Production settings",
    },
  },
  mediaRoot: {
    id: "production.mediaRoot",
    kind: "filePath",
    ui: {
      label: "Media root",
      fileKind: "directory",
      allowEmpty: true,
    },
  },
  metadata: {
    id: "production.metadata",
    kind: "jsonObject",
    ui: {
      label: "Production notes",
    },
  },
  note: {
    id: "production.note",
    kind: "text",
    ui: {
      label: "Production notes",
      multiline: true,
      rows: 4,
    },
  },
});

export const PRODUCTION_COLUMN_BINDINGS = [
  { outputPath: ["id"], field: PRODUCTION_FIELDS.id },
  { outputPath: ["name"], field: PRODUCTION_FIELDS.name },
  { outputPath: ["slug"], field: PRODUCTION_FIELDS.slug },
  { outputPath: ["default_fps"], field: PRODUCTION_FIELDS.defaultFps },
  { outputPath: ["settings_json"], field: PRODUCTION_FIELDS.settings },
  { outputPath: ["metadata_json"], field: PRODUCTION_FIELDS.metadata },
] satisfies readonly JsonFieldBinding[];

export const PRODUCTION_SETTINGS_BINDINGS = [
  { outputPath: ["mediaRoot"], field: PRODUCTION_FIELDS.mediaRoot },
] satisfies readonly JsonFieldBinding[];

export const PRODUCTION_METADATA_BINDINGS = [
  { outputPath: ["note"], field: PRODUCTION_FIELDS.note },
] satisfies readonly JsonFieldBinding[];
