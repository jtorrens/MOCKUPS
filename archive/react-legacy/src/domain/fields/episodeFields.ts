import {
  defineFields,
  type JsonFieldBinding,
} from "../value-system/index.js";

export const EPISODE_FIELDS = defineFields({
  id: {
    id: "episode.id",
    kind: "text",
    ui: {
      label: "ID",
    },
  },
  productionId: {
    id: "episode.productionId",
    kind: "recordReference",
    ui: {
      label: "Production",
      tableId: "productions",
      labelColumn: "name",
    },
  },
  name: {
    id: "episode.name",
    kind: "text",
    ui: {
      label: "Name",
    },
  },
  slug: {
    id: "episode.slug",
    kind: "text",
    ui: {
      label: "Slug",
      allowEmpty: true,
    },
  },
  sortOrder: {
    id: "episode.sortOrder",
    kind: "integer",
    ui: {
      label: "Sort order",
      min: 0,
      step: 1,
    },
  },
  metadata: {
    id: "episode.metadata",
    kind: "jsonObject",
    ui: {
      label: "Episode notes",
    },
  },
  note: {
    id: "episode.note",
    kind: "text",
    ui: {
      label: "Episode notes",
      multiline: true,
      rows: 4,
    },
  },
});

export const EPISODE_COLUMN_BINDINGS = [
  { outputPath: ["id"], field: EPISODE_FIELDS.id },
  { outputPath: ["production_id"], field: EPISODE_FIELDS.productionId },
  { outputPath: ["name"], field: EPISODE_FIELDS.name },
  { outputPath: ["slug"], field: EPISODE_FIELDS.slug },
  { outputPath: ["sort_order"], field: EPISODE_FIELDS.sortOrder },
  { outputPath: ["metadata_json"], field: EPISODE_FIELDS.metadata },
] satisfies readonly JsonFieldBinding[];

export const EPISODE_METADATA_BINDINGS = [
  { outputPath: ["note"], field: EPISODE_FIELDS.note },
] satisfies readonly JsonFieldBinding[];
