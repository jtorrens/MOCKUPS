import {
  defineFields,
  type JsonFieldBinding,
} from "../value-system/index.js";

export const PRODUCTION_FONT_FIELDS = defineFields({
  id: {
    id: "productionFont.id",
    kind: "text",
    ui: {
      label: "ID",
    },
  },
  productionId: {
    id: "productionFont.productionId",
    kind: "recordReference",
    ui: {
      label: "Production",
      tableId: "productions",
      labelColumn: "name",
    },
  },
  family: {
    id: "productionFont.family",
    kind: "text",
    ui: {
      label: "Family",
    },
  },
  category: {
    id: "productionFont.category",
    kind: "enum",
    defaultValue: "normal",
    ui: {
      label: "Category",
      options: ["normal", "emoji"],
    },
  },
  sourcePath: {
    id: "productionFont.sourcePath",
    kind: "filePath",
    ui: {
      label: "Source directory",
      fileKind: "directory",
      allowEmpty: true,
    },
  },
  files: {
    id: "productionFont.files",
    kind: "jsonObject",
    ui: {
      label: "Font files",
    },
  },
  metadata: {
    id: "productionFont.metadata",
    kind: "jsonObject",
    ui: {
      label: "Font notes",
    },
  },
});

export const PRODUCTION_FONT_COLUMN_BINDINGS = [
  { outputPath: ["id"], field: PRODUCTION_FONT_FIELDS.id },
  { outputPath: ["production_id"], field: PRODUCTION_FONT_FIELDS.productionId },
  { outputPath: ["family"], field: PRODUCTION_FONT_FIELDS.family },
  { outputPath: ["category"], field: PRODUCTION_FONT_FIELDS.category },
  { outputPath: ["source_path"], field: PRODUCTION_FONT_FIELDS.sourcePath },
  { outputPath: ["files_json"], field: PRODUCTION_FONT_FIELDS.files },
  { outputPath: ["metadata_json"], field: PRODUCTION_FONT_FIELDS.metadata },
] satisfies readonly JsonFieldBinding[];
