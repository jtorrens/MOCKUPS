import { useEffect, useMemo, useState, type ReactNode } from "react";
import {
  updateAppRecord,
  type AppFieldDefinition,
  type AppRecord,
  type AppTableDefinition,
} from "../api/client.js";
import { JsonValueEditor } from "./json-editor/JsonValueEditor.js";
import {
  InspectorFieldRow,
  InspectorRestoreButton,
} from "./inspector/InspectorFieldRow.js";
import { EditorHeader } from "../editor-ui/EditorHeader.js";
import { EditorSectionButton as TabButton } from "../editor-ui/EditorSectionButton.js";
import {
  EditorSectionCard,
  EditorSubsectionCard,
} from "../editor-ui/EditorSectionCard.js";
import { EditorSections } from "../editor-ui/EditorSections.js";
import { DeferredTextInput } from "../editor-ui/DeferredTextInput.js";
import { AppEditor } from "../editors/AppEditor.js";
import { GenericRecordEditor } from "../editors/GenericRecordEditor.js";
import { ModuleInstanceEditor } from "../editors/ModuleInstanceEditor.js";
import { ModuleThemeConfigEditor } from "../editors/ModuleThemeConfigEditor.js";
import { ScreenInstanceEditor } from "../editors/ScreenInstanceEditor.js";
import { ThemeEditor } from "../editors/ThemeEditor.js";
import type {
  AppEditorTab,
  ModuleThemeTab,
  ScreenInstanceTab,
  ThemeEditorTab,
} from "../editors/editorTabs.js";
import {
  chatContentGroupHasWarning,
  defaultGroupValue,
} from "../editors/chat/chatContentModel.js";
import { ChatContentGroupEditor } from "../editors/chat/ChatContentGroupEditor.js";
import {
  RecordFieldRenderer,
  titleForRecord,
  type RawJsonFieldOverride,
} from "../editors/RecordFieldRenderer.js";
import {
  DeviceMetricsField,
  FlatJsonObjectEditor,
} from "../editors/FlatJsonFieldEditors.js";
import {
  normalizeGroupValue,
  parsedJsonValue,
  parsedObject,
} from "../editors/recordJsonUtils.js";
import {
  stripAppStatusAndNavigationTokens,
  tokenEditorGroups,
} from "../editors/recordTokenUtils.js";
import {
  renderShotSpecialField,
  shotHasFpsOverride,
} from "../editors/ShotFields.js";
import { renderRenderPresetField } from "../editors/RenderPresetFields.js";
import { ProductionSettingsField } from "../editors/ProductionFields.js";
import { ScreenTransitionFields } from "../editors/ScreenInstanceFields.js";
import { ActorMetadataFields } from "../editors/ActorFields.js";
import {
  AppIconFields,
  AppWallpaperEditor,
} from "../editors/AppMediaFields.js";
import { ModuleBehaviorFields } from "../editors/ModuleBehaviorFields.js";
import {
  normalizedThemeTokenRoot,
  ThemeChromeGroupEditor,
} from "../editors/ThemeFields.js";
import {
  hasModeColorOverrides,
  ModeColorEditor,
} from "./json-editor/ModeColorEditor.js";
import {
  cloneJson,
  defaultJsonValue,
  isJsonObject,
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "./json-editor/jsonEditorUtils.js";
import { friendlyGroupLabel } from "./json-editor/labels.js";
import { buildJsonUiHints } from "./json-editor/uiHints.js";

type SaveState = "saved" | "dirty" | "invalid" | "saving" | "failed";

interface MockupsNativeBridge {
  pickFile?: () => Promise<string[]>;
  pickDirectory?: () => Promise<string[]>;
  mediaDataUrl?: (filePath: string, rootPath: string) => Promise<string>;
}

function mockupsNative() {
  return (window as Window & { mockupsNative?: MockupsNativeBridge }).mockupsNative;
}

function normalizeFilesystemPath(value: string) {
  return value.replace(/\\/g, "/").replace(/\/+$/g, "");
}

function relativePathFromRoot(filePath: string, rootPath: string) {
  const normalizedFile = normalizeFilesystemPath(filePath);
  const normalizedRoot = normalizeFilesystemPath(rootPath);
  if (!normalizedRoot) return normalizedFile;
  if (normalizedFile === normalizedRoot) return "";
  if (normalizedFile.startsWith(`${normalizedRoot}/`)) {
    return normalizedFile.slice(normalizedRoot.length + 1);
  }
  return normalizedFile;
}

function mediaPreviewUrl(filePath: string, rootPath: string) {
  const trimmedPath = filePath.trim();
  if (!trimmedPath) return "";
  if (/^(data:|file:|https?:)/i.test(trimmedPath)) return trimmedPath;
  const isAbsolutePath = trimmedPath.startsWith("/");
  const resolvedPath =
    rootPath && !isAbsolutePath
      ? `${normalizeFilesystemPath(rootPath)}/${trimmedPath}`
      : trimmedPath;
  if (resolvedPath.startsWith("/")) {
    return `file://${encodeURI(resolvedPath)}`;
  }
  return resolvedPath;
}

function cssUrl(value: string) {
  return `url("${value.replace(/"/g, '\\"')}")`;
}

function ActorAvatarPreview({
  filePath,
  mediaRoot,
  scale,
  offsetX,
  offsetY,
  useInitials,
  backgroundColor,
  textColor,
  initials,
}: {
  filePath: string;
  mediaRoot: string;
  scale: number;
  offsetX: number;
  offsetY: number;
  useInitials: boolean;
  backgroundColor: string;
  textColor: string;
  initials: string;
}) {
  const [previewUrl, setPreviewUrl] = useState("");

  useEffect(() => {
    let cancelled = false;
    setPreviewUrl("");
    if (useInitials || !filePath.trim()) return () => undefined;
    const fallbackUrl = mediaPreviewUrl(filePath, mediaRoot);
    const loader = mockupsNative()?.mediaDataUrl;
    if (!loader) {
      setPreviewUrl(fallbackUrl);
      return () => undefined;
    }
    void loader(filePath, mediaRoot)
      .then((nextUrl) => {
        if (!cancelled) setPreviewUrl(nextUrl || fallbackUrl);
      })
      .catch(() => {
        if (!cancelled) setPreviewUrl(fallbackUrl);
      });
    return () => {
      cancelled = true;
    };
  }, [filePath, mediaRoot, useInitials]);

  const shouldShowInitials = useInitials || !previewUrl;
  return (
    <div
      className="actor-avatar-preview"
      style={
        shouldShowInitials
          ? {
              backgroundColor,
              color: textColor,
            }
          : {
              backgroundImage: cssUrl(previewUrl),
              backgroundSize: `${Math.max(0.01, scale) * 100}%`,
              backgroundPosition: `calc(50% + ${offsetX}px) calc(50% + ${offsetY}px)`,
            }
      }
    >
      {shouldShowInitials ? initials : null}
    </div>
  );
}

function MediaCoverPreview({
  filePath,
  mediaRoot,
  fallbackLabel,
}: {
  filePath: string;
  mediaRoot: string;
  fallbackLabel: string;
}) {
  const [previewUrl, setPreviewUrl] = useState("");

  useEffect(() => {
    let cancelled = false;
    setPreviewUrl("");
    if (!filePath.trim()) return () => undefined;
    const fallbackUrl = mediaPreviewUrl(filePath, mediaRoot);
    const loader = mockupsNative()?.mediaDataUrl;
    if (!loader) {
      setPreviewUrl(fallbackUrl);
      return () => undefined;
    }
    void loader(filePath, mediaRoot)
      .then((nextUrl) => {
        if (!cancelled) setPreviewUrl(nextUrl || fallbackUrl);
      })
      .catch(() => {
        if (!cancelled) setPreviewUrl(fallbackUrl);
      });
    return () => {
      cancelled = true;
    };
  }, [filePath, mediaRoot]);

  return (
    <div
      className="media-cover-preview"
      style={
        previewUrl
          ? {
              backgroundImage: cssUrl(previewUrl),
            }
          : undefined
      }
    >
      {!previewUrl ? fallbackLabel : null}
    </div>
  );
}

interface RecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord | undefined;
  records: Record<string, AppRecord[]>;
  inheritedFields?: Record<string, Record<string, unknown>>;
  onRecordsChanged: (records: AppRecord[]) => void;
  onRecordSaved: (record: AppRecord) => void;
}

function stringifyJson(value: unknown): string {
  return JSON.stringify(value ?? {}, null, 2);
}

function draftValue(record: AppRecord | undefined, field: AppFieldDefinition) {
  if (!record) return "";
  const value = record[field.column];
  return field.kind === "json" ? stringifyJson(value) : String(value ?? "");
}

function parseValue(
  field: AppFieldDefinition,
  raw: string,
): { ok: true; value: unknown } | { ok: false; error: string } {
  if (field.kind === "json") {
    try {
      const parsed: unknown = JSON.parse(raw);
      if (
        parsed === null ||
        Array.isArray(parsed) ||
        typeof parsed !== "object"
      ) {
        return { ok: false, error: "JSON value must be an object." };
      }
      return { ok: true, value: parsed };
    } catch (error) {
      return {
        ok: false,
        error: error instanceof Error ? error.message : String(error),
      };
    }
  }
  if (field.kind === "number") {
    const numberValue = Number(raw);
    if (!Number.isFinite(numberValue)) {
      return { ok: false, error: "Value must be numeric." };
    }
    return { ok: true, value: numberValue };
  }
  return {
    ok: true,
    value: raw === "" && field.nullable ? null : raw,
  };
}

export function RecordEditor({
  table,
  record,
  records,
  inheritedFields = {},
  onRecordsChanged,
  onRecordSaved,
}: RecordEditorProps) {
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [states, setStates] = useState<Record<string, SaveState>>({});
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [screenTab, setScreenTab] = useState<ScreenInstanceTab>("");
  const [contentTab, setContentTab] = useState("participants");
  const [appTab, setAppTab] = useState<AppEditorTab>("");
  const [appTokenGroup, setAppTokenGroup] = useState("");
  const [themeTab, setThemeTab] = useState<ThemeEditorTab>("");
  const [themeTokenGroup, setThemeTokenGroup] = useState("");
  const [moduleThemeTab, setModuleThemeTab] = useState<ModuleThemeTab>("");
  const [moduleDesignGroup, setModuleDesignGroup] = useState("");
  const [genericTab, setGenericTab] = useState<"" | "general">("general");
  const [openContentItems, setOpenContentItems] = useState<Record<string, boolean>>(
    {},
  );

  useEffect(() => {
    const nextDrafts = Object.fromEntries(
      table.fields.map((field) => [field.column, draftValue(record, field)]),
    );
    setDrafts(nextDrafts);
    setStates(
      Object.fromEntries(table.fields.map((field) => [field.column, "saved"])),
    );
    setErrors({});
    setScreenTab("");
    setContentTab("participants");
    setModuleThemeTab("");
    setAppTab("");
    setAppTokenGroup("");
    setThemeTab("");
    setThemeTokenGroup("");
    setModuleDesignGroup("");
    setGenericTab("general");
    setOpenContentItems({});
  }, [record?.id, table.id]);

  function toggleExclusiveContentItem(groupKey: string, openKey: string, isOpen: boolean) {
    const groupPrefix = `${record?.id ?? "record"}:${groupKey}:`;
    setOpenContentItems((current) => {
      const nextEntries = Object.entries(current).filter(
        ([key]) => !key.startsWith(groupPrefix),
      );
      return isOpen
        ? Object.fromEntries(nextEntries)
        : {
            ...Object.fromEntries(nextEntries),
            [openKey]: true,
          };
    });
  }

  const editableFields = useMemo(
    () => table.fields.filter((field) => !field.readonly),
    [table],
  );

  useEffect(() => {
    if (!record) return;
    const timers = editableFields.flatMap((field) => {
      const raw = drafts[field.column];
      if (raw === undefined || raw === draftValue(record, field)) {
        return [];
      }
      const parsed = parseValue(field, raw);
      if (!parsed.ok) {
        setStates((current) => ({
          ...current,
          [field.column]: "invalid",
        }));
        setErrors((current) => ({
          ...current,
          [field.column]: parsed.error,
        }));
        return [];
      }
      const timer = window.setTimeout(() => {
        setStates((current) => ({ ...current, [field.column]: "saving" }));
        setErrors((current) => ({ ...current, [field.column]: "" }));
        void updateAppRecord(table.id, record.id, {
          [field.column]: parsed.value,
        })
          .then((result) => {
            onRecordsChanged(result.records);
            onRecordSaved((result.saved ?? result.record) as AppRecord);
            setStates((current) => ({
              ...current,
              [field.column]: "saved",
            }));
          })
          .catch((error: Error) => {
            setStates((current) => ({
              ...current,
              [field.column]: "failed",
            }));
            setErrors((current) => ({
              ...current,
              [field.column]: error.message,
            }));
          });
      }, 650);
      setStates((current) => ({ ...current, [field.column]: "dirty" }));
      setErrors((current) => ({ ...current, [field.column]: "" }));
      return [timer];
    });
    return () => timers.forEach((timer) => window.clearTimeout(timer));
  }, [drafts, editableFields, onRecordSaved, onRecordsChanged, record, table.id]);

  if (!record) {
    return (
      <section className="record-editor record-editor-empty">
        No records in this table yet.
      </section>
    );
  }

  const fieldsByColumn = new Map(table.fields.map((field) => [field.column, field]));

  function setJsonDraft(column: string, value: JsonValue) {
    setDrafts({
      ...drafts,
      [column]: stringifyJson(value),
    });
  }

  function hasObjectContent(raw: string | undefined) {
    return Object.keys(parsedObject(raw ?? "{}")).length > 0;
  }

  function explicitLocalDiffers(local: unknown, inherited: unknown): boolean {
    if (local && typeof local === "object" && !Array.isArray(local)) {
      return Object.entries(local as Record<string, unknown>).some(
        ([key, value]) =>
          explicitLocalDiffers(
            value,
            inherited && typeof inherited === "object" && !Array.isArray(inherited)
              ? (inherited as Record<string, unknown>)[key]
              : undefined,
          ),
      );
    }
    if (Array.isArray(local)) {
      return JSON.stringify(local) !== JSON.stringify(inherited);
    }
    return JSON.stringify(local) !== JSON.stringify(inherited);
  }

  function explicitLocalOverridesInherited(
    local: unknown,
    inherited: unknown,
  ): boolean {
    if (inherited === undefined) return false;
    if (local && typeof local === "object" && !Array.isArray(local)) {
      if (!inherited || typeof inherited !== "object" || Array.isArray(inherited)) {
        return false;
      }
      return Object.entries(local as Record<string, unknown>).some(
        ([key, value]) =>
          explicitLocalOverridesInherited(
            value,
            (inherited as Record<string, unknown>)[key],
          ),
      );
    }
    if (Array.isArray(local)) {
      return Array.isArray(inherited) &&
        JSON.stringify(local) !== JSON.stringify(inherited);
    }
    return JSON.stringify(local) !== JSON.stringify(inherited);
  }

  function differsFromInherited(column: string) {
    const inherited = inheritedFields[column];
    if (!inherited) return hasObjectContent(drafts[column]);
    return explicitLocalDiffers(parsedObject(drafts[column] ?? "{}"), inherited);
  }

  function rawForJsonGroup(column: string, groupKey: string) {
    const root = parsedObject(drafts[column] ?? "{}");
    return stringifyJson({ [groupKey]: root[groupKey] ?? defaultGroupValue(groupKey) });
  }

  function updateJsonGroup(column: string, groupKey: string, nextRawText: string) {
    const root = parsedObject(drafts[column] ?? "{}");
    const nextGroupRoot = parsedObject(nextRawText);
    setDrafts({
      ...drafts,
      [column]: stringifyJson({
        ...root,
        [groupKey]: nextGroupRoot[groupKey] ?? defaultGroupValue(groupKey),
      }),
    });
  }

  function rawForJsonGroupValue(column: string, groupKey: string) {
    const root = parsedObject(drafts[column] ?? "{}");
    const value = root[groupKey];
    return stringifyJson(normalizeGroupValue(value, defaultGroupValue(groupKey)));
  }

  function updateJsonGroupValue(
    column: string,
    groupKey: string,
    nextRawText: string,
  ) {
    const root = parsedObject(drafts[column] ?? "{}");
    const fallback = defaultGroupValue(groupKey);
    setDrafts({
      ...drafts,
      [column]: stringifyJson({
        ...root,
        [groupKey]: parsedJsonValue(nextRawText, fallback),
      }),
    });
  }

  function productionIdForCurrentRecord() {
    if (!record) return "";
    if (table.id === "productions") return record.id;
    if (typeof record.production_id === "string") return record.production_id;
    if (table.id === "module_instances") {
      const screen = records.screen_instances?.find(
        (item) => item.id === record.screen_instance_id,
      );
      const shot = records.shots?.find((item) => item.id === screen?.shot_id);
      return typeof shot?.production_id === "string" ? shot.production_id : "";
    }
    if (table.id === "screen_instances") {
      const shot = records.shots?.find((item) => item.id === record.shot_id);
      return typeof shot?.production_id === "string" ? shot.production_id : "";
    }
    return "";
  }

  function productionMediaRoot() {
    const production = records.productions?.find(
      (item) => item.id === productionIdForCurrentRecord(),
    );
    const settings = production?.settings_json;
    if (!settings || typeof settings !== "object" || Array.isArray(settings)) {
      return "";
    }
    const mediaRoot = (settings as Record<string, unknown>).mediaRoot;
    return typeof mediaRoot === "string" ? mediaRoot : "";
  }

  function editorValueForThemeTokenGroup(
    themeTokenRoot: Record<string, JsonValue>,
    groupKey: string,
  ): JsonValue {
    const value = themeTokenRoot[groupKey] ?? defaultGroupValue(groupKey);
    if (!isJsonObject(value)) return value;
    if (groupKey === "fonts") {
      const { source: _source, ...visibleValue } = value;
      return visibleValue;
    }
    if (groupKey === "notifications") {
      const {
        background: _background,
        titleColor: _titleColor,
        bodyColor: _bodyColor,
        ...visibleValue
      } = value;
      return visibleValue;
    }
    return value;
  }

  function visibleTokenGroupValue(value: unknown, groupKey: string): JsonValue {
    if (!isJsonObject(value as JsonValue)) return (value ?? defaultGroupValue(groupKey)) as JsonValue;
    const root = value as Record<string, JsonValue>;
    if (groupKey === "notifications") {
      const {
        background: _background,
        titleColor: _titleColor,
        bodyColor: _bodyColor,
        ...visibleValue
      } = root;
      return visibleValue;
    }
    const { source: _source, ...visibleValue } = root;
    return visibleValue;
  }

  function editorValueForTokenGroup(
    tokenRoot: Record<string, unknown>,
    groupKey: string,
  ): JsonValue {
    const value = tokenRoot[groupKey] ?? defaultGroupValue(groupKey);
    return visibleTokenGroupValue(value, groupKey);
  }

  function inheritedValueForTokenGroup(
    tokenRoot: unknown,
    groupKey: string,
  ): Record<string, unknown> | null {
    if (!isJsonObject(tokenRoot as JsonValue)) return null;
    const value = (tokenRoot as Record<string, JsonValue>)[groupKey];
    if (!isJsonObject(value)) return null;
    const visibleValue = visibleTokenGroupValue(value, groupKey);
    return isJsonObject(visibleValue) ? visibleValue : null;
  }

  function mergeTokenGroupWithInternalFields(
    originalValue: unknown,
    nextVisibleValue: JsonValue,
  ): JsonValue {
    const original = isJsonObject(originalValue as JsonValue)
      ? (originalValue as Record<string, JsonValue>)
      : {};
    const nextVisible = isJsonObject(nextVisibleValue)
      ? (nextVisibleValue as Record<string, JsonValue>)
      : {};
    const internalFields: Record<string, JsonValue> = {};
    if (Object.hasOwn(original, "source")) {
      internalFields.source = original.source;
    }
    return {
      ...internalFields,
      ...nextVisible,
    };
  }

  function updateThemeTokenGroupValue(
    themeTokenRoot: Record<string, JsonValue>,
    groupKey: string,
    nextRawText: string,
  ) {
    const fallback = defaultGroupValue(groupKey);
    const parsedValue = parsedJsonValue(nextRawText, fallback);
    const originalValue = themeTokenRoot[groupKey];
    const nextValue =
      groupKey === "fonts" && isJsonObject(parsedValue)
        ? {
            ...(isJsonObject(originalValue) ? originalValue : {}),
            ...parsedValue,
            source:
              isJsonObject(originalValue) && typeof originalValue.source === "string"
                ? originalValue.source
                : "installed_system_font",
          }
        : parsedValue;

    setJsonDraft("tokens_json", {
      ...themeTokenRoot,
      [groupKey]: nextValue,
    });
  }

  function renderContentGroupEditor(
    field: AppFieldDefinition,
    groupKey: string,
    column = "module_data_json",
  ) {
    const root = parsedObject(drafts[column] ?? "{}");
    const groupValue = normalizeGroupValue(root[groupKey], defaultGroupValue(groupKey));
    const hints = buildJsonUiHints(table, field, record);

    function updateGroupValue(nextValue: JsonValue) {
      setDrafts({
        ...drafts,
        [column]: stringifyJson({
          ...root,
          [groupKey]: nextValue,
        }),
      });
    }

    return (
      <ChatContentGroupEditor
        actors={records.actors ?? []}
        actorTitleForRecord={(actor) => titleForRecord(actor, "display_name")}
        canBrowseMedia={Boolean(mockupsNative()?.pickFile)}
        groupKey={groupKey}
        groupValue={groupValue}
        hints={hints}
        normalizeMediaPath={(filePath) =>
          relativePathFromRoot(filePath, productionMediaRoot())
        }
        onBrowseMedia={async () => {
          const [filePath] =
            await (mockupsNative()?.pickFile?.() ?? Promise.resolve([]));
          return filePath
            ? relativePathFromRoot(filePath, productionMediaRoot())
            : undefined;
        }}
        onGroupValueChange={updateGroupValue}
        onToggleItem={toggleExclusiveContentItem}
        openItems={openContentItems}
        recordId={record?.id}
        root={root}
      />
    );
  }

  function renderField(field: AppFieldDefinition, rawOverride?: RawJsonFieldOverride) {
    return (
      <RecordFieldRenderer
        key={field.column}
        table={table}
        field={field}
        record={record}
        records={records}
        drafts={drafts}
        state={states[field.column] ?? "saved"}
        error={errors[field.column]}
        inheritedValue={inheritedFields[field.column]}
        rawOverride={rawOverride}
        onDraftChange={(column, value) =>
          setDrafts({
            ...drafts,
            [column]: value,
          })
        }
      />
    );
  }

  function renderFields(columns: string[]) {
    return columns
      .map((column) => fieldsByColumn.get(column))
      .filter((field): field is AppFieldDefinition => Boolean(field))
      .filter((field) => field.column !== "id")
      .map((field) => renderField(field));
  }

  function renderGenericField(field: AppFieldDefinition) {
    if (table.id === "productions" && field.column === "settings_json") {
      return (
        <ProductionSettingsField
          field={field}
          rawValue={drafts[field.column] ?? "{}"}
          nativeBridge={mockupsNative()}
          onChange={setJsonDraft}
        />
      );
    }
    if (table.id === "render_presets") {
      return renderRenderPresetField({
        field,
        drafts,
        setDrafts,
        setJsonDraft,
        renderField,
      });
    }
    if (
      table.id === "devices" &&
      ["production_id", "manufacturer", "model", "os_family"].includes(field.column)
    ) {
      return null;
    }
    if (table.id === "devices" && field.column === "metrics_json") {
      return renderDeviceMetricsField(field);
    }
    if (table.id === "actors" && field.column === "production_id") {
      return null;
    }
    if (table.id === "actors" && field.column === "avatar_asset_id") {
      return null;
    }
    if (table.id === "actors" && field.column === "metadata_json") {
      return (
        <ActorMetadataFields
          record={record}
          drafts={drafts}
          mediaRoot={productionMediaRoot()}
          nativeBridge={mockupsNative()}
          relativePathFromRoot={relativePathFromRoot}
          AvatarPreview={ActorAvatarPreview}
          setMetadataRaw={(nextRaw) =>
            setDrafts({
              ...drafts,
              metadata_json: nextRaw,
            })
          }
        />
      );
    }
    if (table.id === "shots") {
      const shotField = renderShotSpecialField({
        field,
        records,
        record,
        drafts,
        states,
        errors,
        setDraftValue: (column, value) =>
          setDrafts({
            ...drafts,
            [column]: value,
          }),
        renderField,
      });
      if (shotField !== undefined) return shotField;
    }
    if (
      table.id === "episodes" &&
      (field.column === "production_id" || field.column === "sort_order")
    ) {
      return null;
    }
    if (field.column === "id") return null;
    if (field.kind === "json") {
      const root = parsedObject(drafts[field.column] ?? "{}");
      const visibleKeys = Object.keys(root).filter((key) => key !== "source");
      if (Object.keys(root).length === 0 || visibleKeys.length === 0) {
        if (table.id === "shots" && field.column === "metadata_json") {
          return (
            <div key={field.column} className="flat-json-field-group">
              <InspectorFieldRow
                className="record-editor-field flat-json-row"
                label={<span>Note</span>}
                control={
                  <JsonValueEditor
                    rootValue={{} as JsonValue}
                    path={["note"]}
                    value=""
                    hints={buildJsonUiHints(table, field, record)}
                    onRootChange={(nextRoot) =>
                      setJsonDraft(field.column, nextRoot)
                    }
                    onChange={(nextValue) =>
                      setJsonDraft(
                        field.column,
                        setAtPath({} as JsonValue, ["note"], nextValue),
                      )
                    }
                  />
                }
              />
            </div>
          );
        }
        return null;
      }
      return (
        <div key={field.column} className="flat-json-field-group">
          {renderFlatJsonObjectEditor(field.column, ["source"])}
        </div>
      );
    }
    return renderField(field);
  }

  function renderFlatJsonObjectEditor(
    column: string,
    omitKeys: string[] = [],
  ) {
    const field = fieldsByColumn.get(column);
    if (!field) return null;
    const root = parsedObject(drafts[column] ?? "{}");
    return (
      <FlatJsonObjectEditor
        table={table}
        field={field}
        record={record}
        root={root}
        omitKeys={omitKeys}
        onRootChange={(nextRoot) => setJsonDraft(column, nextRoot)}
      />
    );
  }

  function renderDeviceMetricsField(field: AppFieldDefinition) {
    const root = parsedJsonValue(drafts[field.column] ?? "{}", {}) as JsonValue;
    return (
      <DeviceMetricsField
        key={field.column}
        table={table}
        field={field}
        record={record}
        root={root}
        onRootChange={(nextRoot) => setJsonDraft(field.column, nextRoot)}
      />
    );
  }

  function configGroupHasContent(groupKey: string) {
    const config = parsedObject(drafts.config_json ?? "{}");
    const value = config[groupKey];
    return (
      value !== null &&
      typeof value === "object" &&
      !Array.isArray(value) &&
      Object.keys(value as Record<string, unknown>).length > 0
    );
  }

  function SubgroupAccordion({
    group,
    activeGroup,
    warning,
    onToggle,
    children,
  }: {
    group: string;
    activeGroup: string;
    warning?: boolean;
    onToggle: (group: string) => void;
    children: ReactNode;
  }) {
    const active = activeGroup === group;
    return (
      <EditorSubsectionCard>
        <TabButton
          active={active}
          warning={warning}
          onClick={() => onToggle(active ? "" : group)}
        >
          {friendlyGroupLabel(group)}
        </TabButton>
        {active ? <div className="editor-subsection-body">{children}</div> : null}
      </EditorSubsectionCard>
    );
  }

  if (table.id === "apps") {
    const configField = fieldsByColumn.get("config_json");
    const metadataField = fieldsByColumn.get("metadata_json");
    const appConfigRoot = parsedObject(drafts.config_json ?? "{}");
    const appMetadataRoot = parsedObject(drafts.metadata_json ?? "{}");
    const appTokenRoot = isJsonObject(appConfigRoot.tokens_json as JsonValue)
      ? (appConfigRoot.tokens_json as Record<string, unknown>)
      : appConfigRoot;
    const appEditorTokenRoot = stripAppStatusAndNavigationTokens(appTokenRoot);
    const inheritedAppRoot = stripAppStatusAndNavigationTokens(inheritedFields.config_json);
    const appTokenGroups = tokenEditorGroups(appEditorTokenRoot, inheritedAppRoot);
    const activeAppTokenGroup =
      appTokenGroup && appTokenGroups.includes(appTokenGroup)
        ? appTokenGroup
        : "";

    function updateAppTokenRoot(nextValue: JsonValue) {
      const cleanNextValue = stripAppStatusAndNavigationTokens(nextValue);
      const nextConfig = Object.hasOwn(appConfigRoot, "tokens_json")
        ? { ...appConfigRoot, tokens_json: cleanNextValue }
        : cleanNextValue;
      setJsonDraft("config_json", nextConfig);
    }

    return (
      <AppEditor
        table={table}
        record={record}
        activeTab={appTab}
        tokensFieldExists={Boolean(configField)}
        notesFieldExists={Boolean(metadataField)}
        tokensWarning={explicitLocalOverridesInherited(
          appEditorTokenRoot,
          inheritedAppRoot,
        )}
        colorsWarning={hasModeColorOverrides(
          appEditorTokenRoot as JsonValue,
          inheritedAppRoot as JsonValue | undefined,
          ["wallpaper"],
        )}
        renderGeneral={() => (
          <>
            {renderFields(["id", "name"])}
            <AppIconFields
              record={record}
              drafts={drafts}
              metadataRoot={appMetadataRoot}
              mediaRoot={productionMediaRoot()}
              nativeBridge={mockupsNative()}
              relativePathFromRoot={relativePathFromRoot}
              AvatarPreview={ActorAvatarPreview}
              onMetadataRootChange={(nextRoot) =>
                setJsonDraft("metadata_json", nextRoot)
              }
            />
          </>
        )}
        renderTokens={() =>
          configField
            ? appTokenGroups.map((group) => (
                <SubgroupAccordion
                  key={group}
                  group={group}
                  activeGroup={activeAppTokenGroup}
                  onToggle={setAppTokenGroup}
                >
                  {group === "wallpaper" ? (
                    <AppWallpaperEditor
                      appTokenRoot={appTokenRoot}
                      mediaRoot={productionMediaRoot()}
                      nativeBridge={mockupsNative()}
                      relativePathFromRoot={relativePathFromRoot}
                      MediaCoverPreview={MediaCoverPreview}
                      onTokenRootChange={updateAppTokenRoot}
                    />
                  ) : (
                    <div className="record-editor-field-stack record-editor-single-column theme-token-group-editor">
                      {renderField(configField, {
                        hideLabel: true,
                        rawText: stringifyJson(
                          editorValueForTokenGroup(appEditorTokenRoot, group),
                        ),
                        groupContext: group,
                        inheritedValue: inheritedValueForTokenGroup(
                          inheritedAppRoot,
                          group,
                        ),
                        onRawTextChange: (nextRawText) => {
                          const nextVisibleValue = parsedObject(nextRawText);
                          updateAppTokenRoot({
                            ...appEditorTokenRoot,
                            [group]: mergeTokenGroupWithInternalFields(
                              appEditorTokenRoot[group],
                              nextVisibleValue as JsonValue,
                            ),
                          } as JsonValue);
                        },
                      })}
                    </div>
                  )}
                </SubgroupAccordion>
              ))
            : null
        }
        renderColors={() => (
          <ModeColorEditor
            rootValue={appEditorTokenRoot as JsonValue}
            inheritedRoot={inheritedAppRoot as JsonValue | undefined}
            hiddenGroups={["wallpaper"]}
            onRootChange={updateAppTokenRoot}
          />
        )}
        renderNotes={() =>
          metadataField ? renderFlatJsonObjectEditor("metadata_json") : null
        }
        setActiveTab={setAppTab}
      />
    );
  }

  if (table.id === "themes") {
    const tokensField = fieldsByColumn.get("tokens_json");
    const themeTokenRoot = normalizedThemeTokenRoot({
      root: parsedObject(drafts.tokens_json ?? "{}"),
      family: String(drafts.family ?? record?.family ?? "ios"),
    });
    const themeTokenGroups = tokenEditorGroups(themeTokenRoot);
    const activeThemeTokenGroup =
      themeTokenGroup && themeTokenGroups.includes(themeTokenGroup)
        ? themeTokenGroup
        : "";

    return (
      <ThemeEditor
        table={table}
        record={record}
        activeTab={themeTab}
        tokensFieldExists={Boolean(tokensField)}
        renderGeneral={() => renderFields(["id", "name", "family", "version"])}
        renderTokens={() =>
          tokensField ? (
            <>
              {themeTokenGroups
                .filter(
                  (group) =>
                    group !== "statusBar" && group !== "navigationBar",
                )
                .map((group) => (
                  <SubgroupAccordion
                    key={group}
                    group={group}
                    activeGroup={activeThemeTokenGroup}
                    onToggle={setThemeTokenGroup}
                  >
                    <div className="record-editor-field-stack record-editor-single-column theme-token-group-editor">
                      {renderField(tokensField, {
                        hideLabel: true,
                        rawText: stringifyJson(
                          editorValueForThemeTokenGroup(themeTokenRoot, group),
                        ),
                        groupContext: group,
                        onRawTextChange: (nextRawText) =>
                          updateThemeTokenGroupValue(
                            themeTokenRoot,
                            group,
                            nextRawText,
                          ),
                      })}
                    </div>
                  </SubgroupAccordion>
                ))}
              {(["statusBar", "navigationBar"] as const).map((group) => (
                <SubgroupAccordion
                  key={group}
                  group={group}
                  activeGroup={activeThemeTokenGroup}
                  onToggle={setThemeTokenGroup}
                >
                  <ThemeChromeGroupEditor
                    tokenRoot={themeTokenRoot}
                    groupKey={group}
                    family={String(drafts.family ?? record?.family ?? "ios")}
                    onTokenRootChange={(nextRoot) =>
                      setJsonDraft("tokens_json", nextRoot)
                    }
                  />
                </SubgroupAccordion>
              ))}
            </>
          ) : null
        }
        renderColors={() =>
          tokensField ? (
            <ModeColorEditor
              rootValue={themeTokenRoot as JsonValue}
              onRootChange={(nextValue) => setJsonDraft("tokens_json", nextValue)}
            />
          ) : null
        }
        setActiveTab={setThemeTab}
      />
    );
  }

  if (table.id === "module_instances") {
    const contentField = fieldsByColumn.get("content_json");
    const behaviorField = fieldsByColumn.get("behavior_json");
    const contentGroups = ["participants", "header", "messages"].filter(
      (group) => group in parsedObject(drafts.content_json ?? "{}"),
    );
    const safeContentGroups = contentGroups.length
      ? contentGroups
      : ["participants", "header", "messages"];
    const activeContentTab = safeContentGroups.includes(contentTab)
      ? contentTab
      : "";

    return (
      <ModuleInstanceEditor
        table={table}
        record={record}
        activeTab={screenTab}
        activeContentTab={activeContentTab}
        contentFieldExists={Boolean(contentField)}
        behaviorFieldExists={Boolean(behaviorField)}
        contentGroups={safeContentGroups}
        contentGroupHasWarning={(group) =>
          chatContentGroupHasWarning({
            group,
            contentRoot: parsedObject(drafts.content_json ?? "{}"),
            actors: records.actors ?? [],
          })
        }
        renderContentGroup={(group) =>
          contentField
            ? renderContentGroupEditor(contentField, group, "content_json")
            : null
        }
        renderBehaviorFields={() => (
          <ModuleBehaviorFields
            rawValue={drafts.behavior_json ?? "{}"}
            onRawChange={(nextRaw) =>
              setDrafts({
                ...drafts,
                behavior_json: nextRaw,
              })
            }
          />
        )}
        renderSubgroupAccordion={(group, activeGroup, warning, onToggle, children) => (
          <SubgroupAccordion
            key={group}
            group={group}
            activeGroup={activeGroup}
            warning={warning}
            onToggle={onToggle}
          >
            {children}
          </SubgroupAccordion>
        )}
        setActiveTab={setScreenTab}
        setContentTab={setContentTab}
      />
    );
  }

  if (table.id === "screen_instances") {
    const deviceStateField = fieldsByColumn.get("device_state_json");
    const transformField = fieldsByColumn.get("transform_json");
    return (
      <ScreenInstanceEditor
        table={table}
        record={record}
        activeTab={screenTab}
        transformFieldExists={Boolean(transformField)}
        deviceStateFieldExists={Boolean(deviceStateField)}
        renderGeneralFields={() =>
          renderFields(["app_id", "theme_mode", "start_frame", "end_frame"])
        }
        renderTransformFields={() =>
          transformField
            ? renderField(transformField, {
                hideLabel: true,
                rawText: drafts.transform_json ?? "{}",
                groupContext: "transform",
                onRawTextChange: (nextRawText) =>
                  setDrafts({ ...drafts, transform_json: nextRawText }),
              })
            : null
        }
        renderTransitionFields={() => (
          <ScreenTransitionFields
            records={records}
            record={record}
            drafts={drafts}
            setDraftValue={(column, value) =>
              setDrafts({
                ...drafts,
                [column]: value,
              })
            }
          />
        )}
        renderDeviceStateFields={() =>
          deviceStateField
            ? renderField(deviceStateField, {
                hideLabel: true,
                rawText: drafts.device_state_json ?? "{}",
                groupContext: "deviceState",
                onRawTextChange: (nextRawText) =>
                  setDrafts({ ...drafts, device_state_json: nextRawText }),
              })
            : null
        }
        setActiveTab={setScreenTab}
      />
    );
  }

  if (table.id === "module_theme_configs") {
    const tokensField = fieldsByColumn.get("tokens_json");
    const tokenRoot = parsedObject(drafts.tokens_json ?? "{}");
    const designGroups = Object.keys(tokenRoot).filter(
      (group) => group !== "modes",
    );
    const activeDesignGroup =
      moduleDesignGroup && designGroups.includes(moduleDesignGroup)
        ? moduleDesignGroup
        : "";
    return (
      <ModuleThemeConfigEditor
        table={table}
        record={record}
        activeTab={moduleThemeTab}
        designFieldExists={Boolean(tokensField)}
        colorsFieldExists={Boolean(tokensField)}
        designWarning={differsFromInherited("tokens_json")}
        colorsWarning={hasModeColorOverrides(
          tokenRoot as JsonValue,
          inheritedFields.tokens_json as JsonValue | undefined,
        )}
        renderDesign={() =>
          tokensField
            ? designGroups.map((group) => (
                <SubgroupAccordion
                  key={group}
                  group={group}
                  activeGroup={activeDesignGroup}
                  warning={explicitLocalDiffers(
                    tokenRoot[group],
                    inheritedFields.tokens_json?.[group],
                  )}
                  onToggle={setModuleDesignGroup}
                >
                  <div className="record-editor-field-stack record-editor-single-column">
                    {renderField(tokensField, {
                      rawText: rawForJsonGroupValue("tokens_json", group),
                      hideLabel: true,
                      groupContext: group,
                      inheritedValue:
                        inheritedFields.tokens_json &&
                        typeof inheritedFields.tokens_json === "object"
                          ? (inheritedFields.tokens_json[
                              group
                            ] as Record<string, unknown>)
                          : undefined,
                      onRawTextChange: (nextRawText) =>
                        updateJsonGroupValue("tokens_json", group, nextRawText),
                    })}
                  </div>
                </SubgroupAccordion>
              ))
            : null
        }
        renderColors={() =>
          tokensField ? (
            <ModeColorEditor
              rootValue={tokenRoot as JsonValue}
              inheritedRoot={inheritedFields.tokens_json as JsonValue | undefined}
              onRootChange={(nextValue) => setJsonDraft("tokens_json", nextValue)}
            />
          ) : null
        }
        renderSettings={() => (
          <>
            <div className="record-editor-field-stack record-editor-direct-fields">
              {renderFields([
                "id",
                "production_id",
                "theme_id",
                "app_id",
                "module_id",
                "module_schema_version",
                "name",
              ])}
            </div>
            {fieldsByColumn.get("metadata_json") ? (
              <div className="record-editor-field-stack record-editor-single-column">
                {renderFlatJsonObjectEditor("metadata_json", [
                  "default_tokens_json",
                ])}
              </div>
            ) : null}
          </>
        )}
        setActiveTab={setModuleThemeTab}
      />
    );
  }

  return (
    <GenericRecordEditor
      table={table}
      record={record}
      activeTab={genericTab}
      renderGenericField={renderGenericField}
      setActiveTab={setGenericTab}
      showGeneralWarning={
        table.id === "shots" &&
        shotHasFpsOverride({
          records,
          record,
          drafts,
        })
      }
    />
  );
}
