import { useState } from "react";
import {
  SURFACE_STYLE_FIELDS,
  surfaceStyleDefaultValue,
  surfaceStyleGet,
  surfaceStyleNormalize,
  surfaceStyleSet,
  type SurfaceStyleFieldDefinition,
} from "../../domain/value-system/index.js";
import type { FieldDefinition } from "../../domain/value-system/index.js";
import { ToggleInspectorLabel } from "../components/inspector/InspectorFieldRow.js";
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
  localValue?: unknown;
  parentValue?: unknown;
  defaultValue?: unknown;
  disabled?: boolean;
  readOnly?: boolean;
  placeholder?: string;
  selectOptions?: DictionarySelectOptions;
  fileBrowser?: DictionaryFileBrowser;
  mediaRoot?: string;
  paletteCatalog?: PaletteColorCatalog;
  productionFontCatalog?: ProductionFontCatalog;
  iconThemeRecords?: readonly IconThemeLikeRecord[];
  componentOverride?: {
    readonly componentName: string;
    readonly hasOverrides: boolean;
    readonly onEdit: () => void;
  };
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

function objectValue(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function hasNestedValue(value: unknown, path: readonly string[]): boolean {
  let current: unknown = value;
  for (const part of path) {
    if (!objectValue(current) || !(part in objectValue(current))) return false;
    current = objectValue(current)[part];
  }
  return true;
}

function mergeSurfaceStyleLayer(
  base: Record<string, unknown>,
  layer: unknown,
): Record<string, unknown> {
  return SURFACE_STYLE_FIELDS.reduce<Record<string, unknown>>((nextRoot, styleField) => {
    if (!hasNestedValue(layer, styleField.path)) return nextRoot;
    return surfaceStyleSet(
      nextRoot,
      styleField.path,
      surfaceStyleGet(objectValue(layer), styleField.path),
    );
  }, base);
}


function relativePathFromRoot(filePath: string, rootPath: string) {
  if (!rootPath || !filePath.startsWith(rootPath)) return filePath;
  return filePath.slice(rootPath.length).replace(/^[/\\]+/, "");
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

function SurfaceStyleControl({
  field,
  value,
  localValue,
  parentValue,
  defaultValue,
  disabled,
  readOnly,
  onChange,
}: {
  field: FieldDefinition;
  value: unknown;
  localValue?: unknown;
  parentValue?: unknown;
  defaultValue?: unknown;
  disabled: boolean;
  readOnly: boolean;
  onChange: (nextValue: unknown) => void;
}) {
  const [open, setOpen] = useState(false);
  const localRoot = objectValue(localValue);
  const parentRoot = objectValue(parentValue);
  const defaultRoot = surfaceStyleNormalize(
    defaultValue === undefined ? surfaceStyleDefaultValue() : defaultValue,
  );
  const fallbackDisplayRoot = surfaceStyleNormalize(value);
  const root = surfaceStyleNormalize(
    mergeSurfaceStyleLayer(
      mergeSurfaceStyleLayer(defaultRoot, parentRoot),
      localRoot,
    ),
  );
  const isDisabled = disabled || readOnly;

  function valueForField(styleField: SurfaceStyleFieldDefinition) {
    const current = surfaceStyleGet(root, styleField.path);
    const fallback = surfaceStyleGet(fallbackDisplayRoot, styleField.path);
    return current ?? fallback ?? styleField.defaultValue;
  }

  function setStyleField(
    styleField: SurfaceStyleFieldDefinition,
    nextValue: unknown,
  ) {
    onChange(surfaceStyleSet(localRoot, styleField.path, nextValue));
  }

  function fieldTechnicalLabel(styleField: SurfaceStyleFieldDefinition) {
    return `${field.id}.${styleField.path.join(".")}`;
  }

  function renderSubLabel(styleField: SurfaceStyleFieldDefinition) {
    return (
      <ToggleInspectorLabel
        label={<span>{styleField.label}</span>}
        technicalLabel={fieldTechnicalLabel(styleField)}
      />
    );
  }

  function renderStyleField(styleField: SurfaceStyleFieldDefinition) {
    const currentValue = valueForField(styleField);
    if (styleField.kind === "boolean") {
      return (
        <label
          key={styleField.path.join(".")}
          className="surface-style-control__row surface-style-control__row--checkbox"
        >
          {renderSubLabel(styleField)}
          <input
            disabled={isDisabled}
            type="checkbox"
            checked={currentValue === true}
            onChange={(event) =>
              setStyleField(styleField, event.currentTarget.checked)
            }
          />
        </label>
      );
    }
    if (
      styleField.kind === "themeColorToken" ||
      styleField.kind === "themeRadiusToken"
    ) {
      const options = styleField.options ?? [];
      return (
        <label key={styleField.path.join(".")} className="surface-style-control__row">
          {renderSubLabel(styleField)}
          <select
            className="json-value-control dictionary-control"
            disabled={isDisabled}
            value={String(currentValue)}
            onChange={(event) => setStyleField(styleField, event.currentTarget.value)}
          >
            {options.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </label>
      );
    }
    return (
      <label key={styleField.path.join(".")} className="surface-style-control__row">
        {renderSubLabel(styleField)}
        <DeferredNumberInput
          className="json-value-control dictionary-control"
          disabled={isDisabled}
          min={styleField.min}
          step={styleField.step ?? "any"}
          value={
            typeof currentValue === "number" && Number.isFinite(currentValue)
              ? currentValue
              : ""
          }
          onCommit={(nextValue) => setStyleField(styleField, nextValue)}
        />
      </label>
    );
  }

  function renderReliefInput(styleField: SurfaceStyleFieldDefinition) {
    const currentValue = valueForField(styleField);
    return (
      <DeferredNumberInput
        key={`${styleField.path.join(".")}:input`}
        className="json-value-control dictionary-control surface-style-control__relief-input"
        disabled={isDisabled}
        min={styleField.min}
        step={styleField.step ?? "any"}
        value={
          typeof currentValue === "number" && Number.isFinite(currentValue)
            ? currentValue
            : ""
        }
        onCommit={(nextValue) => setStyleField(styleField, nextValue)}
      />
    );
  }

  const baseFields = SURFACE_STYLE_FIELDS.filter((entry) => entry.group !== "relief");
  const reliefFields = SURFACE_STYLE_FIELDS.filter((entry) => entry.group === "relief");
  const shadowEnabled = surfaceStyleGet(root, ["shadowEnabled"]) === true;
  const reliefEnabled = surfaceStyleGet(root, ["surfaceReliefEnabled"]) === true;
  const borderWidth = surfaceStyleGet(root, ["borderWidth"]);
  const hasCustomValue = SURFACE_STYLE_FIELDS.some((entry) =>
    hasNestedValue(localRoot, entry.path),
  );

  return (
    <div
      className={`surface-style-control dictionary-control ${
        hasCustomValue ? "has-override" : ""
      }`}
    >
      <button
        type="button"
        className="surface-style-control__summary"
        disabled={isDisabled}
        onClick={() => setOpen((current) => !current)}
      >
        <span>{field.ui?.label ?? "Style"}</span>
        <span className="surface-style-control__summary-detail">
          {shadowEnabled ? "shadow" : "no shadow"}
          {" · "}
          {reliefEnabled ? "relief" : "no relief"}
          {" · "}
          {typeof borderWidth === "number" ? borderWidth : 0}px border
        </span>
        <span aria-hidden="true" className="surface-style-control__chevron">
          {open ? "⌃" : "⌄"}
        </span>
      </button>
      {open ? (
        <div className="surface-style-control__body">
          {baseFields.map(renderStyleField)}
          <div className="surface-style-control__relief-viewport">
            <div className="surface-style-control__subgrid">
              <span className="surface-style-control__subgrid-heading">Relief</span>
              {reliefFields.map((styleField) => (
                <span
                  key={`${styleField.path.join(".")}:label`}
                  className="surface-style-control__relief-label"
                >
                  {renderSubLabel(styleField)}
                </span>
              ))}
              {reliefFields.map(renderReliefInput)}
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}

export function DictionaryFieldControl({
  field,
  value,
  localValue,
  parentValue,
  defaultValue,
  disabled = false,
  readOnly = false,
  placeholder,
  selectOptions,
  fileBrowser,
  mediaRoot,
  paletteCatalog,
  productionFontCatalog,
  iconThemeRecords,
  componentOverride,
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

  if (control === "surfaceStyle") {
    return (
      <SurfaceStyleControl
        field={field}
        value={value}
        localValue={localValue}
        parentValue={parentValue}
        defaultValue={defaultValue}
        disabled={disabled}
        readOnly={readOnly}
        onChange={onChange}
      />
    );
  }

  if (control === "componentOverride") {
    const hasOverrides = componentOverride?.hasOverrides === true;
    return (
      <button
        type="button"
        className={`component-override-control ${controlClassName} ${
          hasOverrides ? "has-override" : ""
        }`}
        disabled={disabled || readOnly || !componentOverride}
        title={
          hasOverrides
            ? `Edit ${componentOverride?.componentName ?? "component"} overrides`
            : `Add ${componentOverride?.componentName ?? "component"} overrides`
        }
        aria-label={
          hasOverrides
            ? `Edit ${componentOverride?.componentName ?? "component"} overrides`
            : `Add ${componentOverride?.componentName ?? "component"} overrides`
        }
        onClick={componentOverride?.onEdit}
      >
        <span aria-hidden="true" className="component-override-control__icon">
          &#9998;
        </span>
      </button>
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
      if (selectedPath) {
        onChange(
          field.kind === "relativeFilePath" && mediaRoot
            ? relativePathFromRoot(selectedPath, mediaRoot)
            : selectedPath,
        );
      }
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
