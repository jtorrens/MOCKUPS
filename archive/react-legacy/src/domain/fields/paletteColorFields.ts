import {
  defineFields,
  type JsonFieldBinding,
} from "../value-system/index.js";

export const PALETTE_COLOR_FIELDS = defineFields({
  id: {
    id: "paletteColor.id",
    kind: "text",
    ui: {
      label: "ID",
    },
  },
  productionId: {
    id: "paletteColor.productionId",
    kind: "recordReference",
    ui: {
      label: "Production",
      tableId: "productions",
      labelColumn: "name",
    },
  },
  token: {
    id: "paletteColor.token",
    kind: "text",
    ui: {
      label: "Token",
    },
  },
  valueHex: {
    id: "paletteColor.valueHex",
    kind: "hexColor",
    defaultValue: "#000000",
    ui: {
      label: "RGB / HEX",
    },
  },
  isNeutral: {
    id: "paletteColor.isNeutral",
    kind: "boolean",
    defaultValue: false,
    ui: {
      label: "Neutral",
    },
  },
  metadata: {
    id: "paletteColor.metadata",
    kind: "jsonObject",
    ui: {
      label: "Palette notes",
    },
  },
});

export const PALETTE_COLOR_COLUMN_BINDINGS = [
  { outputPath: ["id"], field: PALETTE_COLOR_FIELDS.id },
  { outputPath: ["production_id"], field: PALETTE_COLOR_FIELDS.productionId },
  { outputPath: ["token"], field: PALETTE_COLOR_FIELDS.token },
  { outputPath: ["value_hex"], field: PALETTE_COLOR_FIELDS.valueHex },
  { outputPath: ["is_neutral"], field: PALETTE_COLOR_FIELDS.isNeutral },
  { outputPath: ["metadata_json"], field: PALETTE_COLOR_FIELDS.metadata },
] satisfies readonly JsonFieldBinding[];
