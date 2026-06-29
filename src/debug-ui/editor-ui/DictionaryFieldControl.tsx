import type { FieldDefinition } from "../../domain/value-system/index.js";
import { ColorValueEditor } from "../components/json-editor/ColorValueEditor.js";
import type { PaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import {
  cssUrl,
  MediaCoverPreview,
  useMediaPreviewUrl,
} from "../editors/MediaPreviews.js";
import { DeferredNumberInput } from "./DeferredNumberInput.js";
import { DeferredTextInput } from "./DeferredTextInput.js";
import {
  controlDefinitionForField,
  fieldControlSpecForField,
} from "./ValueKindControlRegistry.js";

export const DICTIONARY_FIELD_CLASS = "dictionary-field";
export const DICTIONARY_CONTROL_CLASS = "dictionary-control";

export interface DictionarySelectOption {
  readonly value: string;
  readonly label: string;
}

export interface DictionarySelectOptions {
  readonly allowEmpty?: boolean;
  readonly emptyLabel?: string;
  readonly options: readonly DictionarySelectOption[];
}

export interface DictionaryFileBrowser {
  readonly pickFile?: () => Promise<string[]>;
  readonly pickDirectory?: () => Promise<string[]>;
  readonly mediaDataUrl?: (filePath: string, rootPath: string) => Promise<string>;
}

export interface DictionaryFieldControlProps {
  field: FieldDefinition;
  value: unknown;
  disabled?: boolean;
  readOnly?: boolean;
  placeholder?: string;
  selectOptions?: DictionarySelectOptions;
  fileBrowser?: DictionaryFileBrowser;
  mediaRoot?: string;
  paletteCatalog?: PaletteColorCatalog;
  imagePreview?: {
    readonly baseSize?: number;
    readonly scale?: number;
    readonly offsetX?: number;
    readonly offsetY?: number;
  };
  validation?: {
    readonly valid: boolean;
    readonly message?: string;
  };
  onChange: (nextValue: unknown) => void;
}

function classNameForField(field: FieldDefinition) {
  return `${DICTIONARY_CONTROL_CLASS} ${DICTIONARY_CONTROL_CLASS}--${controlDefinitionForField(field).control}`;
}

function stringValue(value: unknown) {
  return value === undefined || value === null ? "" : String(value);
}

function booleanValue(value: unknown) {
  return value === true || value === "true" || value === "1";
}

function acceptsImagePreview(field: FieldDefinition) {
  const accept = field.ui?.accept ?? [];
  return accept.some((entry) => entry === "image/*" || entry.startsWith("image/"));
}

function numberOrDefault(value: unknown, fallback: number) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function DictionaryImagePreview({
  filePath,
  mediaRoot,
  preview,
}: {
  filePath: string;
  mediaRoot: string;
  preview?: DictionaryFieldControlProps["imagePreview"];
}) {
  const previewUrl = useMediaPreviewUrl({ filePath, mediaRoot });
  const baseSize = Math.max(1, numberOrDefault(preview?.baseSize, 640));
  const previewSize = 160;
  const scale = Math.max(0.01, numberOrDefault(preview?.scale, 1));
  const offsetX = (numberOrDefault(preview?.offsetX, 0) / baseSize) * previewSize;
  const offsetY = (numberOrDefault(preview?.offsetY, 0) / baseSize) * previewSize;

  return (
    <div
      className="actor-avatar-preview"
      style={
        previewUrl
          ? {
              backgroundImage: cssUrl(previewUrl),
              backgroundSize: `${scale * 100}%`,
              backgroundPosition: `calc(50% + ${offsetX}px) calc(50% + ${offsetY}px)`,
            }
          : undefined
      }
    >
      {!previewUrl ? "Image preview" : null}
    </div>
  );
}

export function DictionaryFieldControl({
  field,
  value,
  disabled = false,
  readOnly = false,
  placeholder,
  selectOptions,
  fileBrowser,
  mediaRoot,
  paletteCatalog,
  imagePreview,
  onChange,
}: DictionaryFieldControlProps) {
  const spec = fieldControlSpecForField(field);
  const control = spec.controlDefinition.control;
  const metadata = spec.metadata;
  const controlClassName = classNameForField(field);

  if (control === "checkbox") {
    return (
      <label className={`json-checkbox ${controlClassName}`}>
        <input
          disabled={disabled || readOnly}
          type="checkbox"
          checked={booleanValue(value)}
          onChange={(event) => onChange(event.currentTarget.checked)}
        />
        {booleanValue(value) ? "true" : "false"}
      </label>
    );
  }

  if (control === "paletteColorToken") {
    return (
      <div className={controlClassName}>
        <ColorValueEditor
          value={stringValue(value)}
          label={field.ui?.label ?? field.id}
          paletteCatalog={paletteCatalog}
          onChange={onChange}
        />
      </div>
    );
  }

  if (
    control === "select" ||
    control === "recordSelect" ||
    control === "themeColorToken" ||
    control === "iconToken"
  ) {
    const options = selectOptions?.options ?? [];
    return (
      <select
        className={controlClassName}
        disabled={disabled || readOnly}
        value={stringValue(value)}
        onChange={(event) => onChange(event.currentTarget.value)}
      >
        {selectOptions?.allowEmpty ? (
          <option value="">{selectOptions.emptyLabel ?? "Inherited/default"}</option>
        ) : null}
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    );
  }

  if (control === "number" || control === "alpha") {
    return (
      <DeferredNumberInput
        className={controlClassName}
        disabled={disabled || readOnly}
        max={metadata.max}
        min={metadata.min}
        placeholder={placeholder}
        step={metadata.step ?? "any"}
        value={typeof value === "number" && Number.isFinite(value) ? value : ""}
        onEmptyCommit={() => onChange("")}
        onCommit={onChange}
      />
    );
  }

  if (control === "filePath" || control === "relativeFilePath") {
    const pick =
      metadata.fileKind === "directory"
        ? fileBrowser?.pickDirectory
        : fileBrowser?.pickFile;
    const isDisabled = disabled || readOnly;

    async function choosePath() {
      const [selectedPath] = await (pick?.() ?? Promise.resolve([]));
      if (selectedPath) onChange(selectedPath);
    }

    return (
      <div className={`media-file-control ${controlClassName}`}>
        <DeferredTextInput
          className={controlClassName}
          disabled={isDisabled}
          placeholder={placeholder}
          value={stringValue(value)}
          onCommit={onChange}
        />
        <button
          type="button"
          className="record-editor-compact-button"
          disabled={isDisabled || !pick}
          onClick={() => {
            void choosePath();
          }}
        >
          Browse…
        </button>
        {mediaRoot && acceptsImagePreview(field) && stringValue(value).trim() ? (
          imagePreview ? (
            <DictionaryImagePreview
              filePath={stringValue(value)}
              mediaRoot={mediaRoot}
              preview={imagePreview}
            />
          ) : (
            <MediaCoverPreview
              filePath={stringValue(value)}
              mediaRoot={mediaRoot}
              fallbackLabel="Image preview"
            />
          )
        ) : null}
      </div>
    );
  }

  return (
    <DeferredTextInput
      className={controlClassName}
      disabled={disabled || readOnly}
      multiline={field.ui?.multiline === true}
      placeholder={placeholder}
      rows={field.ui?.rows}
      value={stringValue(value)}
      onCommit={onChange}
    />
  );
}
