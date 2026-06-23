import type { JsonUiHints } from "../components/json-editor/uiHints.js";

export type ModuleEditorHintField =
  | "module_data_json"
  | "module_config_json"
  | "module_tokens_override_json"
  | "content_json"
  | "behavior_json"
  | "tokens_json"
  | "metadata_json";

export interface ModuleEditorHintContract {
  moduleId: string;
  schemaVersion: number;
  fields: Partial<Record<ModuleEditorHintField, JsonUiHints>>;
}
