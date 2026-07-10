import {
  defineFields,
  type JsonFieldBinding,
} from "../value-system/index.js";

export const SHOT_FIELDS = defineFields({
  id: {
    id: "shot.id",
    kind: "text",
    ui: {
      label: "ID",
    },
  },
  productionId: {
    id: "shot.productionId",
    kind: "recordReference",
    ui: {
      label: "Production",
      tableId: "productions",
      labelColumn: "name",
    },
  },
  episodeId: {
    id: "shot.episodeId",
    kind: "recordReference",
    ui: {
      label: "Episode",
      tableId: "episodes",
      labelColumn: "name",
    },
  },
  ownerActorId: {
    id: "shot.ownerActorId",
    kind: "recordReference",
    ui: {
      label: "Owner actor",
      tableId: "actors",
      labelColumn: "display_name",
      allowEmpty: true,
    },
  },
  name: {
    id: "shot.name",
    kind: "text",
    ui: {
      label: "Name",
    },
  },
  slug: {
    id: "shot.slug",
    kind: "text",
    ui: {
      label: "Slug",
      allowEmpty: true,
    },
  },
  version: {
    id: "shot.version",
    kind: "integer",
    defaultValue: 1,
    ui: {
      label: "Version",
      min: 0,
      step: 1,
    },
  },
  sortOrder: {
    id: "shot.sortOrder",
    kind: "integer",
    ui: {
      label: "Sort order",
      min: 0,
      step: 1,
    },
  },
  durationFrames: {
    id: "shot.durationFrames",
    kind: "integer",
    defaultValue: 1,
    ui: {
      label: "Duration frames",
      min: 1,
      step: 1,
    },
  },
  fps: {
    id: "shot.fps",
    kind: "integer",
    defaultValue: 30,
    ui: {
      label: "FPS",
      min: 1,
      step: 1,
    },
  },
  renderPresetId: {
    id: "shot.renderPresetId",
    kind: "recordReference",
    ui: {
      label: "Render preset",
      tableId: "render_presets",
      labelColumn: "name",
      allowEmpty: true,
    },
  },
  canvas: {
    id: "shot.canvas",
    kind: "jsonObject",
    ui: {
      label: "Canvas setup",
    },
  },
  metadata: {
    id: "shot.metadata",
    kind: "jsonObject",
    ui: {
      label: "Shot notes",
    },
  },
  note: {
    id: "shot.note",
    kind: "text",
    ui: {
      label: "Shot notes",
      multiline: true,
      rows: 4,
    },
  },
  renderName: {
    id: "shot.renderName",
    kind: "text",
    ui: {
      label: "Render name",
    },
  },
  ownerDevice: {
    id: "shot.ownerDevice",
    kind: "text",
    ui: {
      label: "Device",
    },
  },
});

export const SHOT_COLUMN_BINDINGS = [
  { outputPath: ["id"], field: SHOT_FIELDS.id },
  { outputPath: ["production_id"], field: SHOT_FIELDS.productionId },
  { outputPath: ["episode_id"], field: SHOT_FIELDS.episodeId },
  { outputPath: ["owner_actor_id"], field: SHOT_FIELDS.ownerActorId },
  { outputPath: ["name"], field: SHOT_FIELDS.name },
  { outputPath: ["slug"], field: SHOT_FIELDS.slug },
  { outputPath: ["version"], field: SHOT_FIELDS.version },
  { outputPath: ["sort_order"], field: SHOT_FIELDS.sortOrder },
  { outputPath: ["duration_frames"], field: SHOT_FIELDS.durationFrames },
  { outputPath: ["fps"], field: SHOT_FIELDS.fps },
  { outputPath: ["render_preset_id"], field: SHOT_FIELDS.renderPresetId },
  { outputPath: ["canvas_json"], field: SHOT_FIELDS.canvas },
  { outputPath: ["metadata_json"], field: SHOT_FIELDS.metadata },
] satisfies readonly JsonFieldBinding[];

export const SHOT_METADATA_BINDINGS = [
  { outputPath: ["note"], field: SHOT_FIELDS.note },
] satisfies readonly JsonFieldBinding[];
