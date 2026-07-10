import {
  defineFields,
  type JsonFieldBinding,
} from "../value-system/index.js";

export const RENDER_PRESET_FIELDS = defineFields({
  id: {
    id: "renderPreset.id",
    kind: "text",
    ui: {
      label: "ID",
    },
  },
  productionId: {
    id: "renderPreset.productionId",
    kind: "recordReference",
    ui: {
      label: "Production",
      tableId: "productions",
      labelColumn: "name",
    },
  },
  name: {
    id: "renderPreset.name",
    kind: "text",
    ui: {
      label: "Name",
    },
  },
  width: {
    id: "renderPreset.width",
    kind: "integer",
    ui: {
      label: "Width",
      min: 1,
      step: 1,
    },
  },
  height: {
    id: "renderPreset.height",
    kind: "integer",
    ui: {
      label: "Height",
      min: 1,
      step: 1,
    },
  },
  fps: {
    id: "renderPreset.fps",
    kind: "integer",
    ui: {
      label: "FPS",
      min: 1,
      step: 1,
    },
  },
  format: {
    id: "renderPreset.format",
    kind: "enum",
    defaultValue: "mov",
    ui: {
      label: "Format",
      options: ["mov", "image"],
    },
  },
  codec: {
    id: "renderPreset.codec",
    kind: "jsonObject",
    ui: {
      label: "Codec settings",
    },
  },
  codecValue: {
    id: "renderPreset.codec.value",
    kind: "enum",
    defaultValue: "prores_422_hq",
    ui: {
      label: "Codec",
      options: [
        "prores_422_proxy",
        "prores_422_lt",
        "prores_422",
        "prores_422_hq",
        "prores_4444",
        "prores_4444_xq",
        "h264_low",
        "h264_medium",
        "h264_high",
        "png",
        "exr",
      ],
    },
  },
  color: {
    id: "renderPreset.color",
    kind: "jsonObject",
    ui: {
      label: "Color settings",
    },
  },
  quality: {
    id: "renderPreset.quality",
    kind: "jsonObject",
    ui: {
      label: "Quality settings",
    },
  },
  export: {
    id: "renderPreset.export",
    kind: "jsonObject",
    ui: {
      label: "Export settings",
    },
  },
  ffmpegArgs: {
    id: "renderPreset.export.ffmpegArgs",
    kind: "text",
    ui: {
      label: "FFmpeg args",
      multiline: true,
      rows: 2,
    },
  },
});

export const RENDER_PRESET_COLUMN_BINDINGS = [
  { outputPath: ["id"], field: RENDER_PRESET_FIELDS.id },
  { outputPath: ["production_id"], field: RENDER_PRESET_FIELDS.productionId },
  { outputPath: ["name"], field: RENDER_PRESET_FIELDS.name },
  { outputPath: ["width"], field: RENDER_PRESET_FIELDS.width },
  { outputPath: ["height"], field: RENDER_PRESET_FIELDS.height },
  { outputPath: ["fps"], field: RENDER_PRESET_FIELDS.fps },
  { outputPath: ["format"], field: RENDER_PRESET_FIELDS.format },
  { outputPath: ["codec_json"], field: RENDER_PRESET_FIELDS.codec },
  { outputPath: ["color_json"], field: RENDER_PRESET_FIELDS.color },
  { outputPath: ["quality_json"], field: RENDER_PRESET_FIELDS.quality },
  { outputPath: ["export_json"], field: RENDER_PRESET_FIELDS.export },
] satisfies readonly JsonFieldBinding[];
