import type { ReactNode } from "react";
import {
  type AppFieldDefinition,
  type AppRecord,
} from "../api/client.js";
import type { JsonValue } from "../components/json-editor/jsonEditorUtils.js";
import {
  DictionaryFieldControl,
  DICTIONARY_FIELD_CLASS,
} from "../editor-ui/DictionaryFieldControl.js";
import { EditorFieldRow } from "../editor-ui/fields/EditorFieldRow.js";
import type { EditorFieldDescriptor } from "../editor-ui/fields/EditorFieldDescriptor.js";
import { toDictionaryFieldControlProps } from "../editor-ui/fields/EditorFieldDescriptor.js";
import { RENDER_PRESET_FIELDS } from "../../domain/fields/renderPresetFields.js";
import { parsedObject } from "./recordJsonUtils.js";

const movCodecOptions = [
  { value: "prores_422_proxy", label: "ProRes 422 Proxy" },
  { value: "prores_422_lt", label: "ProRes 422 LT" },
  { value: "prores_422", label: "ProRes 422" },
  { value: "prores_422_hq", label: "ProRes 422 HQ" },
  { value: "prores_4444", label: "ProRes 4444 (alpha)" },
  { value: "prores_4444_xq", label: "ProRes 4444 XQ (alpha)" },
  { value: "h264_low", label: "H.264 Low" },
  { value: "h264_medium", label: "H.264 Medium" },
  { value: "h264_high", label: "H.264 High" },
];

const imageCodecOptions = [
  { value: "png", label: "PNG" },
  { value: "exr", label: "EXR" },
];

function stringifyJson(value: unknown): string {
  return JSON.stringify(value, null, 2);
}

function ffmpegArgsForRenderPreset(format: string, codec: string) {
  if (format === "image") {
    if (codec === "exr") return "-compression zip -pix_fmt rgba64le";
    return "-compression_level 6 -pix_fmt rgba";
  }
  if (codec === "prores_422_proxy") return "-c:v prores_ks -profile:v 0 -pix_fmt yuv422p10le";
  if (codec === "prores_422_lt") return "-c:v prores_ks -profile:v 1 -pix_fmt yuv422p10le";
  if (codec === "prores_422") return "-c:v prores_ks -profile:v 2 -pix_fmt yuv422p10le";
  if (codec === "prores_422_hq") return "-c:v prores_ks -profile:v 3 -pix_fmt yuv422p10le";
  if (codec === "prores_4444" || codec === "prores_4444_alpha") {
    return "-c:v prores_ks -profile:v 4 -pix_fmt yuva444p10le";
  }
  if (codec === "prores_4444_xq") return "-c:v prores_ks -profile:v 5 -pix_fmt yuva444p10le";
  if (codec === "h264_low") return "-c:v libx264 -preset medium -crf 28 -pix_fmt yuv420p";
  if (codec === "h264_medium") return "-c:v libx264 -preset medium -crf 23 -pix_fmt yuv420p";
  if (codec === "h264_high") return "-c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p";
  return "";
}

function renderPresetPayload(format: string, codec: string) {
  const isImage = format === "image";
  const hasAlpha =
    codec === "prores_4444" ||
    codec === "prores_4444_xq" ||
    codec === "prores_4444_alpha" ||
    codec === "png" ||
    codec === "exr";
  return {
    codec_json: { codec },
    color_json: {
      colorSpace: codec === "exr" ? "linear" : isImage ? "srgb" : "rec709",
      alpha: hasAlpha,
    },
    quality_json: { profile: codec },
    export_json: {
      extension: isImage ? codec : "mov",
      sequence: isImage,
      ffmpegArgs: ffmpegArgsForRenderPreset(format, codec),
    },
  };
}

function hiddenRenderPresetField(column: string) {
  return [
    "id",
    "production_id",
    "width",
    "height",
    "fps",
    "color_json",
    "quality_json",
  ].includes(column);
}

interface RenderPresetFieldContext {
  field: AppFieldDefinition;
  drafts: Record<string, string>;
  setDrafts: (nextDrafts: Record<string, string>) => void;
  setJsonDraft: (column: string, value: JsonValue) => void;
  renderField: (field: AppFieldDefinition) => ReactNode;
}

function RenderPresetCodecField({
  drafts,
  setDrafts,
}: Pick<RenderPresetFieldContext, "drafts" | "setDrafts">) {
  const format = drafts.format === "image" ? "image" : "mov";
  const codecRoot = parsedObject(drafts.codec_json ?? "{}");
  const options = format === "image" ? imageCodecOptions : movCodecOptions;
  const current =
    typeof codecRoot.codec === "string" &&
    options.some((option) => option.value === codecRoot.codec)
      ? codecRoot.codec
      : options[0].value;

  function updateCodec(nextCodec: string, nextFormat = format) {
    const payload = renderPresetPayload(nextFormat, nextCodec);
    setDrafts({
      ...drafts,
      format: nextFormat,
      codec_json: stringifyJson(payload.codec_json),
      color_json: stringifyJson(payload.color_json),
      quality_json: stringifyJson(payload.quality_json),
      export_json: stringifyJson(payload.export_json),
    });
  }

  const descriptor = createCustomDescriptor({
    field: {
      ...RENDER_PRESET_FIELDS.codecValue,
      ui: {
        ...RENDER_PRESET_FIELDS.codecValue.ui,
        label: format === "image" ? "Image type" : "Codec",
      },
    },
    value: current,
    selectOptions: {
      options,
    },
    onWrite: (nextValue) => updateCodec(String(nextValue)),
  });
  return renderCustomDictionaryField("render_preset_codec", descriptor);
}

function createCustomDescriptor({
  field,
  value,
  selectOptions,
  onWrite,
}: Pick<EditorFieldDescriptor, "field" | "selectOptions"> & {
  readonly value: unknown;
  readonly onWrite: (nextValue: unknown) => void;
}): EditorFieldDescriptor {
  return {
    kind: "field",
    field,
    displayValue: value,
    resolvedValue: value,
    localValue: value,
    state: "local",
    readonly: false,
    canInherit: false,
    canRestore: false,
    source: {
      kind: "custom",
      path: [field.id],
      recordType: "render_presets",
    },
    actions: {
      write: onWrite,
    },
    selectOptions,
  };
}

function renderCustomDictionaryField(key: string, descriptor: EditorFieldDescriptor) {
  return (
    <EditorFieldRow
      key={key}
      className={`record-editor-field ${DICTIONARY_FIELD_CLASS}`}
      descriptor={descriptor}
    >
      <DictionaryFieldControl {...toDictionaryFieldControlProps(descriptor)} />
    </EditorFieldRow>
  );
}

export function renderRenderPresetField({
  field,
  drafts,
  setDrafts,
  setJsonDraft,
  renderField,
}: RenderPresetFieldContext) {
  if (hiddenRenderPresetField(field.column)) {
    return null;
  }
  if (field.column === "codec_json") {
    return <RenderPresetCodecField drafts={drafts} setDrafts={setDrafts} />;
  }
  if (field.column === "format") {
    const format = drafts.format === "image" ? "image" : "mov";
    const descriptor = createCustomDescriptor({
      field: RENDER_PRESET_FIELDS.format,
      value: format,
      selectOptions: {
        options: [
          { value: "mov", label: "MOV" },
          { value: "image", label: "Image" },
        ],
      },
      onWrite: (nextValue) => {
        const nextFormat = String(nextValue);
        const nextCodec = nextFormat === "image" ? "png" : "prores_422_hq";
        const payload = renderPresetPayload(nextFormat, nextCodec);
        setDrafts({
          ...drafts,
          format: nextFormat,
          codec_json: stringifyJson(payload.codec_json),
          color_json: stringifyJson(payload.color_json),
          quality_json: stringifyJson(payload.quality_json),
          export_json: stringifyJson(payload.export_json),
        });
      },
    });
    return renderCustomDictionaryField(field.column, descriptor);
  }
  if (field.column === "export_json") {
    const exportRoot = parsedObject(drafts.export_json ?? "{}");
    const ffmpegArgs =
      typeof exportRoot.ffmpegArgs === "string"
        ? exportRoot.ffmpegArgs
        : "";
    const descriptor = createCustomDescriptor({
      field: RENDER_PRESET_FIELDS.ffmpegArgs,
      value: ffmpegArgs,
      onWrite: (nextValue) =>
        setJsonDraft("export_json", {
          ...exportRoot,
          ffmpegArgs: String(nextValue),
        }),
    });
    return renderCustomDictionaryField(field.column, descriptor);
  }
  return renderField(field);
}

export function renderPresetSeedPayload(format: string, codec: string) {
  return renderPresetPayload(format, codec);
}
