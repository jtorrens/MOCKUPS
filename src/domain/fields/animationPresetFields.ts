import {
  defineFields,
  type JsonFieldBinding,
} from "../value-system/index.js";

export const ANIMATION_PRESET_FIELDS = defineFields({
  id: {
    id: "animationPreset.id",
    kind: "text",
    ui: {
      label: "ID",
    },
  },
  productionId: {
    id: "animationPreset.productionId",
    kind: "recordReference",
    ui: {
      label: "Production",
      tableId: "productions",
      labelColumn: "name",
    },
  },
  name: {
    id: "animationPreset.name",
    kind: "text",
    ui: {
      label: "Name",
    },
  },
  animationType: {
    id: "animationPreset.animationType",
    kind: "text",
    ui: {
      label: "Animation type",
    },
  },
  version: {
    id: "animationPreset.version",
    kind: "text",
    defaultValue: "1",
    ui: {
      label: "Version",
    },
  },
  parameters: {
    id: "animationPreset.parameters",
    kind: "jsonObject",
    ui: {
      label: "Animation parameters",
    },
  },
});

export const ANIMATION_PRESET_COLUMN_BINDINGS = [
  { outputPath: ["id"], field: ANIMATION_PRESET_FIELDS.id },
  { outputPath: ["production_id"], field: ANIMATION_PRESET_FIELDS.productionId },
  { outputPath: ["name"], field: ANIMATION_PRESET_FIELDS.name },
  { outputPath: ["animation_type"], field: ANIMATION_PRESET_FIELDS.animationType },
  { outputPath: ["version"], field: ANIMATION_PRESET_FIELDS.version },
  { outputPath: ["parameters_json"], field: ANIMATION_PRESET_FIELDS.parameters },
] satisfies readonly JsonFieldBinding[];
