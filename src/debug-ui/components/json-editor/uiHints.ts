import type { AppFieldDefinition, AppTableDefinition } from "../../api/client.js";
import { ACTOR_METADATA_BINDINGS } from "../../../domain/fields/actorFields.js";
import { DEVICE_METRICS_BINDINGS } from "../../../domain/fields/deviceFields.js";
import {
  PRODUCTION_METADATA_BINDINGS,
  PRODUCTION_SETTINGS_BINDINGS,
} from "../../../domain/fields/productionFields.js";
import { THEME_TOKEN_BINDINGS } from "../../../domain/fields/themeFields.js";
import type { FieldDefinition } from "../../../domain/value-system/index.js";
import { fieldDescriptorHintsForContext } from "../../field-descriptors/registry.js";
import { moduleJsonUiHintsForRecord } from "../../module-editor-hints/registry.js";
import { jsonUiHintsFromFieldBindings } from "./fieldDefinitionHints.js";
import {
  normalizedHintPath,
  type JsonPath,
  type JsonValue,
} from "./jsonEditorUtils.js";

export type JsonWidget =
  | "text"
  | "number"
  | "checkbox"
  | "color"
  | "font"
  | "select"
  | "textarea";

export type JsonUiHint = {
  field?: FieldDefinition;
  dictionaryDerived?: boolean;
  widget?: JsonWidget;
  label?: string;
  canonicalPath?: string;
  storagePath?: JsonPath;
  section?: string;
  area?: string;
  group?: string;
  role?: string;
  property?: string;
  summaryKeys?: string[];
  options?: string[];
  lockFontFamily?: boolean;
  allowMultiple?: boolean;
  allowEmpty?: boolean;
  semanticTokenGroup?: string;
  tableId?: string;
  labelColumn?: string;
  fileKind?: "file" | "directory";
  accept?: string[];
  multiline?: boolean;
  rows?: number;
  numericControl?: "default" | "hueDegrees";
  min?: number;
  max?: number;
  step?: number | "any";
};

export type JsonUiHints = Record<string, JsonUiHint>;

function encodedHintPath(path: JsonPath): string {
  return JSON.stringify(path);
}

const COMMON_HINTS: JsonUiHints = {
  id: { label: "ID" },
  type: { label: "Type" },
  text: { label: "Text" },
  displayName: { label: "Display name" },
  actorId: { label: "Actor" },
  avatarUrl: { label: "Avatar URL" },
  defaultMode: {
    label: "Default mode",
    widget: "select",
    options: ["light", "dark"],
  },
  initialScroll: {
    label: "Initial scroll",
    widget: "select",
    options: ["top", "bottom", "keep_latest_visible"],
  },
  messageGrouping: {
    label: "Message grouping",
    widget: "select",
    options: ["none", "bySender"],
  },
  "statusBar.type": {
    label: "Status bar type",
    widget: "select",
    options: ["ios-default", "android-default"],
  },
  "navigationBar.type": {
    label: "Navigation bar type",
    widget: "select",
    options: ["ios-home-indicator", "android-gesture", "android-3-button"],
  },
  "modes.light.colors.background": { label: "Background", widget: "color" },
  "modes.light.colors.textPrimary": { label: "Primary text", widget: "color" },
  "modes.light.colors.textSecondary": {
    label: "Secondary text",
    widget: "color",
  },
  "modes.light.colors.accent": { label: "Accent", widget: "color" },
  "modes.dark.colors.background": { label: "Background", widget: "color" },
  "modes.dark.colors.textPrimary": { label: "Primary text", widget: "color" },
  "modes.dark.colors.textSecondary": {
    label: "Secondary text",
    widget: "color",
  },
  "modes.dark.colors.accent": { label: "Accent", widget: "color" },
  "colors.background": { label: "Background", widget: "color" },
  "colors.textPrimary": { label: "Primary text", widget: "color" },
  "colors.textSecondary": { label: "Secondary text", widget: "color" },
  "colors.accent": { label: "Accent", widget: "color" },
  "chatBubbles.outgoingBackground": {
    label: "Outgoing bubble background",
    widget: "color",
  },
  "chatBubbles.outgoingText": { label: "Outgoing text", widget: "color" },
  "chatBubbles.incomingBackground": {
    label: "Incoming bubble background",
    widget: "color",
  },
  "chatBubbles.incomingText": { label: "Incoming text", widget: "color" },
  "cursor.color": { label: "Cursor color", widget: "color" },
};

export function buildJsonUiHints(
  table: AppTableDefinition,
  field: AppFieldDefinition,
  record?: Record<string, unknown>,
): JsonUiHints {
  const hints: JsonUiHints = {
    ...COMMON_HINTS,
    ...fieldDescriptorHintsForContext({
      tableId: table.id,
      fieldColumn: field.column,
      record,
    }),
    ...(table.id === "themes" && field.column === "tokens_json"
      ? jsonUiHintsFromFieldBindings(THEME_TOKEN_BINDINGS)
      : {}),
    ...(table.id === "productions" && field.column === "settings_json"
      ? jsonUiHintsFromFieldBindings(PRODUCTION_SETTINGS_BINDINGS)
      : {}),
    ...(table.id === "productions" && field.column === "metadata_json"
      ? jsonUiHintsFromFieldBindings(PRODUCTION_METADATA_BINDINGS)
      : {}),
    ...(table.id === "actors" && field.column === "metadata_json"
      ? jsonUiHintsFromFieldBindings(ACTOR_METADATA_BINDINGS)
      : {}),
    ...(table.id === "devices" && field.column === "metrics_json"
      ? jsonUiHintsFromFieldBindings(DEVICE_METRICS_BINDINGS)
      : {}),
    ...moduleJsonUiHintsForRecord(table.id, field.column, record),
  };
  if (table.id === "screen_instances" && field.column === "transform_json") {
    hints.x = { label: "X position", widget: "number", step: 1 };
    hints.y = { label: "Y position", widget: "number", step: 1 };
    hints.scale = { label: "Scale", widget: "number", step: 0.01 };
    hints.rotation_degrees = {
      label: "Rotation",
      widget: "number",
      step: 1,
    };
  }
  if (table.id === "device_states" && field.column === "state_json") {
    hints.orientation = {
      label: "Orientation",
      widget: "select",
      options: ["portrait", "landscape"],
    };
    hints.locked = { label: "Locked", widget: "checkbox" };
    hints.wifiEnabled = { label: "Wi-Fi enabled", widget: "checkbox" };
    hints.batteryCharging = {
      label: "Battery charging",
      widget: "checkbox",
    };
  }
  return hints;
}

export function hintForPath(
  hints: JsonUiHints,
  path: JsonPath,
  value: JsonValue,
  groupContext?: string,
): JsonUiHint {
  const normalized = normalizedHintPath(path);
  const encoded = encodedHintPath(path);
  const contextNormalized = groupContext
    ? `${groupContext}.${normalized}`
    : normalized;
  const exact =
    hints[encoded] ??
    hints[contextNormalized] ??
    hints[normalized] ??
    hints[String(path[path.length - 1] ?? "")];
  if (exact) return exact;

  const key = String(path[path.length - 1] ?? "");
  const parent = String(path[path.length - 2] ?? "");
  if (/fontFamily$/i.test(key) || (key === "family" && /font|fonts/i.test(parent))) {
    return { widget: "font" };
  }
  if (key === "style" && /font|type/i.test(parent)) {
    return { widget: "select" };
  }
  if (
    /fontWeight$/i.test(key) ||
    /Weight$/i.test(key) ||
    (key === "weight" && /font/i.test(parent))
  ) {
    return { widget: "select" };
  }
  if (
    typeof value === "string" &&
    /color|background|text|accent|foreground/i.test(key)
  ) {
    return { widget: "color" };
  }

  return {};
}
