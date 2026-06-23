import type { JsonWidget } from "../components/json-editor/uiHints.js";
import type { JsonPath } from "../components/json-editor/jsonEditorUtils.js";

export type FieldDescriptorArea =
  | "general"
  | "tokens"
  | "design"
  | "colors"
  | "content"
  | "behavior"
  | "timeline"
  | "transform"
  | "notes"
  | "settings";

export interface FieldDescriptor {
  canonicalPath: string;
  storagePath: JsonPath;
  label: string;
  section: string;
  area: FieldDescriptorArea;
  group?: string;
  role?: string;
  property?: string;
  widget?: JsonWidget;
  options?: string[];
  min?: number;
  max?: number;
  step?: number;
  summaryKeys?: string[];
}

export interface FieldDescriptorContext {
  tableId: string;
  fieldColumn: string;
  record?: Record<string, unknown>;
}
