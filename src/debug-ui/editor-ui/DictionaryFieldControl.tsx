import type { FieldDefinition } from "../../domain/value-system/index.js";
import { ColorValueEditor } from "../components/json-editor/ColorValueEditor.js";
import type { PaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import type { ProductionFontCatalog } from "../components/json-editor/productionFonts.js";
import {
  cssUrl,
  MediaCoverPreview,
  useMediaPreviewUrl,
} from "../editors/MediaPreviews.js";
import { DeferredNumberInput } from "./DeferredNumberInput.js";
import { DeferredTextInput } from "./DeferredTextInput.js";
import {
  IconTokenPicker,
} from "./IconTokenPicker.js";
import type {
  IconThemeLikeRecord,
} from "./IconGlyphPreview.js";
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
  productionFontCatalog?: ProductionFontCatalog;
  iconThemeRecords?: readonly IconThemeLikeRecord[];
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

function fontFamilyCategoryForField(field: FieldDefinition): "normal" | "emoji" {
  const text = `${field.id} ${field.ui?.label ?? ""}`.toLowerCase();
  return text.includes("emoji") ? "emoji" : "normal";
}

function fontWeightValue(value: unknown) {
  if (typeof value === "number" && Number.isFinite(value)) return String(value);
  if (typeof value === "string" && value.trim()) return value.trim();
  return "400";
}

function fontWeightOptions(value: unknown) {
  const weights = ["100", "200", "300", "400", "500", "600", "700", "800", "900"];
  const current = fontWeightValue(value);
  return weights.includes(current) ? weights : [current, ...weights];
}

function fontStyleValue(value: unknown) {
  return value === "italic" ? "italic" : "normal";
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

function normalizeHueDeg(value: number) {
  return ((value % 360) + 360) % 360;
}

function jsonTextValue(value: unknown) {
  if (typeof value === "string") return value;
  if (value === undefined || value === null) return "";
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return "";
  }
}

function parseJsonTextValue(value: string) {
  try {
    return JSON.parse(value) as unknown;
  } catch {
    return undefined;
  }
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
  productionFontCatalog,
  iconThemeRecords,
  imagePreview,
  onChange,
}: DictionaryFieldControlProps) {
  const spec = fieldControlSpecForField(field);
  const control = spec.controlDefinition.control;
  const metadata = spec.metadata;
  const controlClassName = classNameForField(field);
  const nativeControlClassName = `json-value-control ${controlClassName}`;
  const nativeTextareaClassName = `json-value-textarea ${controlClassName}`;

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

  if (control === "hexColor") {
    return (
      <div className={`${controlClassName} palette-color-value-control palette-color-free-control`}>
        <ColorValueEditor
          value={stringValue(value)}
          label={field.ui?.label ?? field.id}
          onChange={onChange}
        />
      </div>
    );
  }

  if (control === "typography") {
    if (field.kind === "fontFamily") {
      const category = fontFamilyCategoryForField(field);
      const families =
        category === "emoji"
          ? productionFontCatalog?.emojiFamilies ?? []
          : productionFontCatalog?.families ?? [];
      const currentValue = stringValue(value);
      const options = families.includes(currentValue)
        ? families
        : currentValue
          ? [currentValue, ...families]
          : families;
      return (
        <select
          className={nativeControlClassName}
          disabled={disabled || readOnly || !options.length}
          value={currentValue || options[0] || ""}
          onChange={(event) => onChange(event.currentTarget.value)}
        >
          {options.length ? (
            options.map((family) => (
              <option key={family} value={family}>
                {family}
              </option>
            ))
          ) : (
            <option value="">
              {category === "emoji" ? "No emoji fonts" : "No production fonts"}
            </option>
          )}
        </select>
      );
    }

    if (field.kind === "fontWeight") {
      const options = fontWeightOptions(value);
      return (
        <select
          className={nativeControlClassName}
          disabled={disabled || readOnly}
          value={fontWeightValue(value)}
          onChange={(event) => onChange(Number(event.currentTarget.value))}
        >
          {options.map((weight) => (
            <option key={weight} value={weight}>
              {weight}
            </option>
          ))}
        </select>
      );
    }

    if (field.kind === "fontStyle") {
      return (
        <select
          className={nativeControlClassName}
          disabled={disabled || readOnly}
          value={fontStyleValue(value)}
          onChange={(event) => onChange(event.currentTarget.value)}
        >
          <option value="normal">Normal</option>
          <option value="italic">Italic</option>
        </select>
      );
    }
  }

  if (control === "iconToken") {
    return (
      <IconTokenPicker
        allowMultiple={field.ui?.allowMultiple === true}
        disabled={disabled || readOnly}
        iconThemeRecords={iconThemeRecords}
        mediaRoot={mediaRoot}
        nativeBridge={fileBrowser}
        value={value}
        onChange={onChange}
      />
    );
  }

  if (
    control === "select" ||
    control === "recordSelect" ||
    control === "themeColorToken"
  ) {
    const options = selectOptions?.options ?? [];
    return (
      <select
        className={nativeControlClassName}
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
    if (metadata.numericControl === "hueDegrees") {
      const hue = normalizeHueDeg(numberOrDefault(value, 0));
      const isDisabled = disabled || readOnly;
      return (
        <div className={`json-hue-slider-control ${controlClassName}`}>
          <input
            aria-label={`${field.ui?.label ?? field.id} hue`}
            className="json-hue-slider"
            disabled={isDisabled}
            max={metadata.max ?? 360}
            min={metadata.min ?? 0}
            step={metadata.step ?? 1}
            type="range"
            value={hue}
            style={{ accentColor: `hsl(${hue} 80% 52%)` }}
            onChange={(event) =>
              onChange(normalizeHueDeg(Number(event.currentTarget.value)))
            }
          />
          <DeferredNumberInput
            ariaLabel={`${field.ui?.label ?? field.id} degrees`}
            className={`${nativeControlClassName} json-hue-slider-value`}
            disabled={isDisabled}
            max={metadata.max ?? 360}
            min={metadata.min ?? 0}
            step={metadata.step ?? 1}
            value={hue}
            onCommit={(nextValue) => onChange(normalizeHueDeg(nextValue))}
          />
          <span className="json-hue-slider-unit">°</span>
        </div>
      );
    }
    return (
      <DeferredNumberInput
        className={nativeControlClassName}
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
          className={nativeControlClassName}
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

  if (control === "jsonObject" || control === "jsonArray") {
    return (
      <DeferredTextInput
        className={nativeTextareaClassName}
        disabled={disabled || readOnly}
        multiline
        placeholder={placeholder}
        rows={field.ui?.rows ?? 4}
        value={jsonTextValue(value)}
        onCommit={(nextValue) => {
          const parsed = parseJsonTextValue(String(nextValue));
          if (
            (control === "jsonObject" &&
              parsed !== null &&
              typeof parsed === "object" &&
              !Array.isArray(parsed)) ||
            (control === "jsonArray" && Array.isArray(parsed))
          ) {
            onChange(parsed);
          }
        }}
      />
    );
  }

  return (
    <DeferredTextInput
      className={
        field.ui?.multiline === true
          ? nativeTextareaClassName
          : nativeControlClassName
      }
      disabled={disabled || readOnly}
      multiline={field.ui?.multiline === true}
      placeholder={placeholder}
      rows={field.ui?.rows}
      value={stringValue(value)}
      onCommit={onChange}
    />
  );
}
