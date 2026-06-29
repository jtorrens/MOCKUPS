import type { FieldDefinition } from "../../../domain/value-system/index.js";
import type {
  DictionaryFileBrowser,
  DictionaryFieldControlProps,
  DictionarySelectOptions,
} from "../DictionaryFieldControl.js";
import type { PaletteColorCatalog } from "../../components/json-editor/paletteColors.js";

export type EditorFieldState =
  | "local"
  | "inherited"
  | "default"
  | "invalid";

export type EditorFieldSourceKind =
  | "record-column"
  | "json-binding"
  | "component-override"
  | "module-instance-content"
  | "custom"
  | "legacy";

export type EditorValidationSeverity = "error" | "warning";

export interface EditorValidation {
  readonly valid: boolean;
  readonly message?: string;
  readonly severity?: EditorValidationSeverity;
}

export interface EditorFieldActions {
  readonly write: (value: unknown) => void;
  readonly restore?: () => void;
  readonly setInherited?: () => void;
}

export interface EditorFieldSource {
  readonly kind: EditorFieldSourceKind;
  readonly path?: readonly string[];
  readonly recordId?: string;
  readonly recordType?: string;
}

export interface EditorFieldDescriptor {
  readonly kind: "field";
  readonly field: FieldDefinition;
  readonly localValue?: unknown;
  readonly parentValue?: unknown;
  readonly defaultValue?: unknown;
  readonly resolvedValue?: unknown;
  readonly displayValue: unknown;
  readonly state: EditorFieldState;
  readonly readonly: boolean;
  readonly canInherit: boolean;
  readonly canRestore: boolean;
  readonly validation?: EditorValidation;
  readonly source: EditorFieldSource;
  readonly actions: EditorFieldActions;
  readonly selectOptions?: DictionarySelectOptions;
  readonly placeholder?: string;
}

export type EditorFieldGroupControl =
  | "typography"
  | "color-alpha"
  | "xy"
  | "size"
  | "file-preview"
  | "icon-token-picker"
  | "component-override";

export interface EditorFieldGroupDescriptor {
  readonly kind: "group";
  readonly id: string;
  readonly label: string;
  readonly description?: string;
  readonly control: EditorFieldGroupControl;
  readonly fields: Readonly<Record<string, EditorFieldDescriptor>>;
  readonly validation?: EditorValidation;
}

export function editorFieldLabel(descriptor: EditorFieldDescriptor): string {
  return descriptor.field.ui?.label ?? descriptor.field.id;
}

export function toDictionaryFieldControlProps(
  descriptor: EditorFieldDescriptor,
  options: {
    readonly fileBrowser?: DictionaryFileBrowser;
    readonly mediaRoot?: string;
    readonly paletteCatalog?: PaletteColorCatalog;
    readonly imagePreview?: DictionaryFieldControlProps["imagePreview"];
  } = {},
): DictionaryFieldControlProps {
  return {
    field: descriptor.field,
    value: descriptor.displayValue,
    readOnly: descriptor.readonly,
    placeholder: descriptor.placeholder,
    selectOptions: descriptor.selectOptions,
    fileBrowser: options.fileBrowser,
    mediaRoot: options.mediaRoot,
    paletteCatalog: options.paletteCatalog,
    imagePreview: options.imagePreview,
    validation: descriptor.validation
      ? {
          valid: descriptor.validation.valid,
          message: descriptor.validation.message,
        }
      : undefined,
    onChange: descriptor.actions.write,
  };
}
