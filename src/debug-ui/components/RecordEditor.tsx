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
import { defaultGroupValue } from "../editors/chat/chatContentModel.js";
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
  hasModeColorOverrides,
  ModeColorEditor,
} from "./json-editor/ModeColorEditor.js";
import { ColorValueEditor } from "./json-editor/ColorValueEditor.js";
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

  function parsedObject(raw: string): Record<string, unknown> {
    try {
      const value = JSON.parse(raw) as unknown;
      if (typeof value === "string") {
        return parsedObject(value);
      }
      return value && typeof value === "object" && !Array.isArray(value)
        ? (value as Record<string, unknown>)
        : {};
    } catch {
      return {};
    }
  }

  function parsedJsonValue(raw: string, fallback: JsonValue): JsonValue {
    try {
      const value = JSON.parse(raw) as unknown;
      if (typeof value === "string" && looksLikeJson(value)) {
        return parsedJsonValue(value, fallback);
      }
      if (
        value === null ||
        typeof value === "string" ||
        typeof value === "number" ||
        typeof value === "boolean" ||
        Array.isArray(value) ||
        typeof value === "object"
      ) {
        return value as JsonValue;
      }
      return fallback;
    } catch {
      return fallback;
    }
  }

  function looksLikeJson(value: string) {
    const trimmed = value.trim();
    return trimmed.startsWith("{") || trimmed.startsWith("[");
  }

  function normalizeGroupValue(value: unknown, fallback: JsonValue): JsonValue {
    if (typeof value === "string" && looksLikeJson(value)) {
      return parsedJsonValue(value, fallback);
    }
    return (value as JsonValue | undefined) ?? fallback;
  }

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

  function tokenEditorGroups(
    root: Record<string, unknown>,
    inheritedRoot?: unknown,
  ) {
    const inherited = isJsonObject(inheritedRoot as JsonValue)
      ? (inheritedRoot as Record<string, unknown>)
      : {};
    return Array.from(
      new Set([...Object.keys(root), ...Object.keys(inherited)]),
    ).filter((group) => {
      const value = root[group] ?? inherited[group];
      return (
        group !== "modes" &&
        group !== "colors" &&
        value !== null &&
        typeof value === "object" &&
        !Array.isArray(value)
      );
    });
  }

  function stripAppStatusAndNavigationTokens(value: unknown): Record<string, JsonValue> {
    const source = isJsonObject(value as JsonValue)
      ? cloneJson(value as JsonValue)
      : ({} as JsonValue);
    const root = isJsonObject(source) ? source : {};
    delete root.statusBar;
    delete root.navigationBar;
    delete root.shadows;
    if (isJsonObject(root.notifications)) {
      delete root.notifications.background;
      delete root.notifications.titleColor;
      delete root.notifications.bodyColor;
    }
    const modes = isJsonObject(root.modes) ? root.modes : {};
    for (const mode of ["light", "dark"] as const) {
      const modeRoot = isJsonObject(modes[mode]) ? modes[mode] : undefined;
      if (!modeRoot) continue;
      delete modeRoot.statusBar;
      delete modeRoot.navigationBar;
      const colors = isJsonObject(modeRoot.colors) ? modeRoot.colors : undefined;
      if (colors) {
        delete colors.navigationBackground;
      }
    }
    return root;
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

  function projectDefaultFps() {
    if (table.id !== "shots") return undefined;
    const production = records.productions?.find(
      (item) => item.id === record?.production_id,
    );
    const fps = Number(production?.default_fps);
    return Number.isFinite(fps) && fps > 0 ? fps : undefined;
  }

  function shotHasFpsOverride() {
    const inheritedFps = projectDefaultFps();
    const currentFps = Number(drafts.fps ?? record?.fps);
    return (
      inheritedFps !== undefined &&
      Number.isFinite(currentFps) &&
      currentFps !== inheritedFps
    );
  }

  function shotCalculatedDurationFrames() {
    if (table.id !== "shots") return undefined;
    const screens = records.screen_instances?.filter(
      (item) => item.shot_id === record?.id,
    );
    if (!screens?.length) return undefined;
    const lastFrame = Math.max(
      ...screens.map((item) => Number(item.end_frame ?? 0)),
    );
    return Number.isFinite(lastFrame) && lastFrame > 0 ? lastFrame : undefined;
  }

  function shotOwnerDeviceName() {
    if (table.id !== "shots") return undefined;
    const owner = records.actors?.find(
      (item) => item.id === (drafts.owner_actor_id || record?.owner_actor_id),
    );
    const device = records.devices?.find(
      (item) => item.id === owner?.default_device_id,
    );
    return device ? titleForRecord(device, "name") : "No default device";
  }

  function renderShotName() {
    const production = records.productions?.find(
      (item) => item.id === record?.production_id,
    );
    const episode = records.episodes?.find(
      (item) => item.id === record?.episode_id,
    );
    const productionSlug = String(production?.slug ?? production?.name ?? "production");
    const episodeSlug = String(episode?.slug ?? episode?.name ?? "episode");
    const shotSlug = String(drafts.slug ?? record?.slug ?? record?.name ?? "shot");
    const version = Number(drafts.version ?? record?.version ?? 1);
    const versionSlug = String(Number.isFinite(version) ? version : 1).padStart(2, "0");
    return `${productionSlug}_${episodeSlug}_${shotSlug}_v${versionSlug}`;
  }

  function renderShotFpsField(field: AppFieldDefinition) {
    const inheritedFps = projectDefaultFps();
    const currentFps = Number(drafts[field.column] ?? record?.[field.column]);
    const hasOverride =
      inheritedFps !== undefined &&
      Number.isFinite(currentFps) &&
      currentFps !== inheritedFps;
    const state = states[field.column] ?? "saved";
    const error = errors[field.column];
    return (
      <InspectorFieldRow
        key={field.column}
        className={`record-editor-field record-editor-field-${field.kind} state-${state} ${
          hasOverride ? "json-override" : ""
        }`}
        state={
          state === "invalid" || state === "failed"
            ? "invalid"
            : hasOverride
              ? "override"
              : "default"
        }
        label={<span>{field.label}</span>}
        meta={
          inheritedFps !== undefined ? (
            <code>{`Project default: ${inheritedFps}`}</code>
          ) : null
        }
        control={
          <>
            <input
              data-testid={`field-${field.column}`}
              type="number"
              value={drafts[field.column] ?? ""}
              onChange={(event) =>
                setDrafts({
                  ...drafts,
                  [field.column]: event.target.value,
                })
              }
            />
            {error ? <strong>{error}</strong> : null}
          </>
        }
        restore={
          hasOverride && inheritedFps !== undefined ? (
            <InspectorRestoreButton
              label="Restore project FPS"
              onClick={() =>
                setDrafts({
                  ...drafts,
                  [field.column]: String(inheritedFps),
                })
              }
            />
          ) : null
        }
      />
    );
  }

  function renderShotDurationField(field: AppFieldDefinition) {
    const calculatedDuration = shotCalculatedDurationFrames();
    return (
      <InspectorFieldRow
        key={field.column}
        className="record-editor-field record-editor-field-number is-readonly"
        label={<span>{field.label}</span>}
        meta={
          calculatedDuration !== undefined ? (
            <code>Calculated from screens</code>
          ) : null
        }
        control={
          <input
            disabled
            value={String(calculatedDuration ?? record?.duration_frames ?? "")}
          />
        }
      />
    );
  }

  function renderShotOwnerDeviceField() {
    return (
      <InspectorFieldRow
        key="owner_device"
        className="record-editor-field record-editor-field-string is-readonly"
        label={<span>Device</span>}
        control={<input disabled value={shotOwnerDeviceName() ?? ""} />}
      />
    );
  }

  function nextScreenInstance() {
    if (table.id !== "screen_instances") return undefined;
    const startFrame = Number(drafts.start_frame ?? record?.start_frame ?? 0);
    return records.screen_instances
      ?.filter(
        (item) =>
          item.shot_id === record?.shot_id &&
          item.id !== record?.id &&
          Number(item.start_frame) >= startFrame,
      )
      .sort((left, right) => {
        const frameDelta = Number(left.start_frame) - Number(right.start_frame);
        return frameDelta || String(left.id).localeCompare(String(right.id));
      })[0];
  }

  function transitionOverlapFrames() {
    if (table.id !== "screen_instances") return 0;
    const next = nextScreenInstance();
    if (!next) return 0;
    const currentEnd = Number(drafts.end_frame ?? record?.end_frame ?? 0);
    const nextStart = Number(next.start_frame ?? 0);
    const overlap = currentEnd - nextStart;
    return Number.isFinite(overlap) && overlap > 0 ? overlap : 0;
  }

  function renderScreenTransitionField() {
    const root = parsedObject(drafts.transition_out_json ?? "{}");
    const type =
      root.type === "dissolve" || root.type === "overlay"
        ? String(root.type)
        : "overlay";
    const overlap = transitionOverlapFrames();
    return (
      <>
        <InspectorFieldRow
          key="transition_type"
          className="record-editor-field record-editor-field-string"
          label={<span>Transition</span>}
          control={
            <select
              value={type}
              onChange={(event) =>
                setDrafts({
                  ...drafts,
                  transition_out_json: stringifyJson({
                    ...root,
                    type: event.target.value,
                    duration_frames: overlap,
                  }),
                })
              }
            >
              <option value="overlay">Overlay</option>
              <option value="dissolve">Dissolve</option>
            </select>
          }
        />
        <InspectorFieldRow
          key="transition_duration"
          className="record-editor-field record-editor-field-number is-readonly"
          label={<span>Duration frames</span>}
          meta={<code>Calculated from screen overlap</code>}
          control={<input disabled value={String(overlap)} />}
        />
      </>
    );
  }

  function actorInitials() {
    const displayName = String(drafts.display_name ?? record?.display_name ?? "");
    const shortName = String(drafts.short_name ?? record?.short_name ?? "");
    const source = shortName || displayName;
    const words = source
      .split(/\s+/)
      .map((word) => word.trim())
      .filter(Boolean);
    if (words.length === 0) return "?";
    return words
      .slice(0, 2)
      .map((word) => word[0]?.toUpperCase() ?? "")
      .join("");
  }

  function actorColor() {
    const root = parsedObject(drafts.metadata_json ?? "{}");
    return typeof root.color === "string" && /^#[0-9a-f]{6}$/i.test(root.color)
      ? root.color
      : "#64748b";
  }

  function setActorColor(nextColor: string) {
    const root = parsedObject(drafts.metadata_json ?? "{}");
    setDrafts({
      ...drafts,
      metadata_json: stringifyJson({
        ...root,
        color: nextColor,
      }),
    });
  }

  function actorAvatar() {
    const root = parsedObject(drafts.metadata_json ?? "{}");
    const avatar = root.avatar;
    return avatar && typeof avatar === "object" && !Array.isArray(avatar)
      ? (avatar as Record<string, unknown>)
      : {};
  }

  function setActorAvatarPatch(patch: Record<string, unknown>) {
    const root = parsedObject(drafts.metadata_json ?? "{}");
    const avatar = actorAvatar();
    setDrafts({
      ...drafts,
      metadata_json: stringifyJson({
        ...root,
        avatar: {
          baseSize: 640,
          scale: 1,
          offsetX: 0,
          offsetY: 0,
          ...avatar,
          ...patch,
        },
      }),
    });
  }

  function renderActorColorField() {
    const color = actorColor();
    return (
      <InspectorFieldRow
        key="actor_color"
        className="record-editor-field record-editor-field-string actor-color-field"
        label={<span>Color</span>}
        control={
          <div className="actor-color-control">
            <span
              className="actor-color-preview"
              style={{ backgroundColor: color }}
              aria-hidden="true"
            >
              {actorInitials()}
            </span>
            <input
              aria-label="Actor color"
              type="color"
              value={color}
              onChange={(event) => setActorColor(event.target.value)}
            />
            <input
              aria-label="Actor color hex"
              value={color}
              onChange={(event) => setActorColor(event.target.value)}
            />
          </div>
        }
      />
    );
  }

  function renderActorAvatarFields() {
    const avatar = actorAvatar();
    const filePath = typeof avatar.filePath === "string" ? avatar.filePath : "";
    const scale = typeof avatar.scale === "number" ? avatar.scale : 1;
    const offsetX = typeof avatar.offsetX === "number" ? avatar.offsetX : 0;
    const offsetY = typeof avatar.offsetY === "number" ? avatar.offsetY : 0;
    const useInitials = avatar.useInitials === true;
    const textColor =
      typeof avatar.textColor === "string" && /^#[0-9a-f]{6}$/i.test(avatar.textColor)
        ? avatar.textColor
        : "#ffffff";
    const mediaRoot = productionMediaRoot();

    async function chooseAvatarFile() {
      const [selectedPath] = await (mockupsNative()?.pickFile?.() ?? Promise.resolve([]));
      if (selectedPath) {
        setActorAvatarPatch({
          filePath: relativePathFromRoot(selectedPath, mediaRoot),
        });
      }
    }

    return (
      <>
        <InspectorFieldRow
          key="actor_avatar_use_initials"
          className="record-editor-field record-editor-field-boolean"
          label={<span>Use initials</span>}
          control={
            <input
              type="checkbox"
              checked={useInitials}
              onChange={(event) =>
                setActorAvatarPatch({ useInitials: event.target.checked })
              }
            />
          }
        />
        <InspectorFieldRow
          key="actor_avatar_file"
          className="record-editor-field record-editor-field-string"
          label={<span>Avatar image</span>}
          control={
            <div className="media-file-control actor-avatar-file-control">
              <DeferredTextInput
                value={filePath}
                onCommit={(nextValue) => setActorAvatarPatch({ filePath: nextValue })}
              />
              <button
                type="button"
                className="record-editor-compact-button"
                disabled={!mockupsNative()?.pickFile}
                onClick={() => {
                  void chooseAvatarFile();
                }}
              >
                Browse…
              </button>
            </div>
          }
        />
        <InspectorFieldRow
          key="actor_avatar_text_color"
          className="record-editor-field record-editor-field-string actor-color-field"
          label={<span>Avatar text color</span>}
          control={
            <div className="actor-color-control">
              <span
                className="actor-color-preview"
                style={{ backgroundColor: textColor, color: actorColor() }}
                aria-hidden="true"
              >
                Aa
              </span>
              <input
                aria-label="Avatar text color"
                type="color"
                value={textColor}
                onChange={(event) =>
                  setActorAvatarPatch({ textColor: event.target.value })
                }
              />
              <input
                aria-label="Avatar text color hex"
                value={textColor}
                onChange={(event) =>
                  setActorAvatarPatch({ textColor: event.target.value })
                }
              />
            </div>
          }
        />
        <div className="actor-avatar-frame" aria-label="Avatar crop frame">
          <ActorAvatarPreview
            filePath={filePath}
            mediaRoot={mediaRoot}
            scale={scale}
            offsetX={offsetX}
            offsetY={offsetY}
            useInitials={useInitials}
            backgroundColor={actorColor()}
            textColor={textColor}
            initials={actorInitials()}
          />
          <small>Base avatar frame: 640×640</small>
        </div>
        <InspectorFieldRow
          key="actor_avatar_scale"
          className="record-editor-field record-editor-field-number"
          label={<span>Avatar scale</span>}
          control={
            <input
              type="number"
              step={0.01}
              min={0.01}
              value={String(scale)}
              onChange={(event) =>
                setActorAvatarPatch({ scale: Number(event.target.value) })
              }
            />
          }
        />
        <InspectorFieldRow
          key="actor_avatar_offset_x"
          className="record-editor-field record-editor-field-number"
          label={<span>Avatar offset X</span>}
          control={
            <input
              type="number"
              step={1}
              value={String(offsetX)}
              onChange={(event) =>
                setActorAvatarPatch({ offsetX: Number(event.target.value) })
              }
            />
          }
        />
        <InspectorFieldRow
          key="actor_avatar_offset_y"
          className="record-editor-field record-editor-field-number"
          label={<span>Avatar offset Y</span>}
          control={
            <input
              type="number"
              step={1}
              value={String(offsetY)}
              onChange={(event) =>
                setActorAvatarPatch({ offsetY: Number(event.target.value) })
              }
            />
          }
        />
      </>
    );
  }

  function renderActorMetadataFields() {
    return (
      <div className="flat-json-field-group">
        {renderActorColorField()}
        {renderActorAvatarFields()}
      </div>
    );
  }

  function renderProductionSettingsField(field: AppFieldDefinition) {
    const root = parsedObject(drafts[field.column] ?? "{}");
    const mediaRoot = typeof root.mediaRoot === "string" ? root.mediaRoot : "";
    async function chooseDirectory() {
      const [directory] = await (mockupsNative()?.pickDirectory?.() ?? Promise.resolve([]));
      if (directory) {
        setJsonDraft(field.column, {
          ...root,
          mediaRoot: directory,
        });
      }
    }
    return (
      <div key={field.column} className="flat-json-field-group">
        <InspectorFieldRow
          className="record-editor-field flat-json-row"
          label={<span>Media root</span>}
          control={
            <div className="media-file-control">
              <DeferredTextInput
                value={mediaRoot}
                onCommit={(nextValue) =>
                  setJsonDraft(field.column, {
                    ...root,
                    mediaRoot: nextValue,
                  })
                }
              />
              <button
                type="button"
                className="record-editor-compact-button"
                disabled={!mockupsNative()?.pickDirectory}
                onClick={() => {
                  void chooseDirectory();
                }}
              >
                Browse…
              </button>
            </div>
          }
        />
      </div>
    );
  }

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

  function renderPresetCodec() {
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

    return (
      <InspectorFieldRow
        key="render_preset_codec"
        className="record-editor-field record-editor-field-string"
        label={<span>{format === "image" ? "Image type" : "Codec"}</span>}
        control={
          <select
            value={current}
            onChange={(event) => updateCodec(event.target.value)}
          >
            {options.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        }
      />
    );
  }

  function renderRenderPresetField(field: AppFieldDefinition) {
    if (
      [
        "production_id",
        "width",
        "height",
        "fps",
        "color_json",
        "quality_json",
      ].includes(field.column)
    ) {
      return null;
    }
    if (field.column === "codec_json") {
      return renderPresetCodec();
    }
    if (field.column === "format") {
      const format = drafts.format === "image" ? "image" : "mov";
      return (
        <InspectorFieldRow
          key={field.column}
          className="record-editor-field record-editor-field-string"
          label={<span>Format</span>}
          control={
            <select
              value={format}
              onChange={(event) => {
                const nextFormat = event.target.value;
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
              }}
            >
              <option value="mov">MOV</option>
              <option value="image">Image</option>
            </select>
          }
        />
      );
    }
    if (field.column === "export_json") {
      const exportRoot = parsedObject(drafts.export_json ?? "{}");
      const ffmpegArgs =
        typeof exportRoot.ffmpegArgs === "string"
          ? exportRoot.ffmpegArgs
          : "";
      return (
        <InspectorFieldRow
          key={field.column}
          className="record-editor-field record-editor-field-string"
          label={<span>FFmpeg args</span>}
          control={
            <textarea
              data-testid="field-ffmpeg_args"
              value={ffmpegArgs}
              onChange={(event) =>
                setJsonDraft("export_json", {
                  ...exportRoot,
                  ffmpegArgs: event.target.value,
                })
              }
              rows={2}
            />
          }
        />
      );
    }
    return renderField(field);
  }

  function renderGenericField(field: AppFieldDefinition) {
    if (table.id === "productions" && field.column === "settings_json") {
      return renderProductionSettingsField(field);
    }
    if (table.id === "render_presets") {
      return renderRenderPresetField(field);
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
      return renderActorMetadataFields();
    }
    if (
      table.id === "shots" &&
      [
        "production_id",
        "sort_order",
        "render_preset_id",
      ].includes(field.column)
    ) {
      return null;
    }
    if (table.id === "shots" && field.column === "fps") {
      return renderShotFpsField(field);
    }
    if (table.id === "shots" && field.column === "duration_frames") {
      return renderShotDurationField(field);
    }
    if (table.id === "shots" && field.column === "owner_actor_id") {
      return (
        <>
          {renderField(field)}
          {renderShotOwnerDeviceField()}
        </>
      );
    }
    if (table.id === "shots" && field.column === "version") {
      return (
        <>
          {renderField(field)}
          <InspectorFieldRow
            key="render_name"
            className="record-editor-field record-editor-field-string is-readonly"
            label={<span>Render name</span>}
            control={<input disabled value={renderShotName()} />}
          />
        </>
      );
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

  function normalizeChromeBackground(value: unknown, dark = false) {
    if (value === "transparent" || value === undefined || value === null) {
      return dark ? "rgba(0,0,0,0)" : "rgba(255,255,255,0)";
    }
    return value;
  }

  function themeChromeDefaults(
    groupKey: "statusBar" | "navigationBar",
    dark = false,
  ): Record<string, JsonValue> {
    const family = String(drafts.family ?? record?.family ?? "ios").toLowerCase();
    const isAndroid = family === "android";
    const foreground = dark ? "#FFFFFF" : "#000000";
    if (groupKey === "statusBar") {
      return {
        type: isAndroid ? "android-default" : "ios-default",
        foreground,
        background: normalizeChromeBackground(undefined, dark) as string,
        iconScale: 1,
      };
    }
    return {
      type: isAndroid ? "android-gesture" : "ios-home-indicator",
      foreground,
      background: normalizeChromeBackground(undefined, dark) as string,
      iconScale: 1,
    };
  }

  function normalizeThemeChromeGroup(
    groupKey: "statusBar" | "navigationBar",
    value: unknown,
    dark = false,
  ) {
    const root = isJsonObject(value as JsonValue)
      ? (value as Record<string, JsonValue>)
      : {};
    return {
      ...themeChromeDefaults(groupKey, dark),
      ...root,
      background: normalizeChromeBackground(root.background, dark) as JsonValue,
    };
  }

  function normalizedThemeTokenRoot(root: Record<string, unknown>) {
    const modes = isJsonObject(root.modes as JsonValue)
      ? (root.modes as Record<string, JsonValue>)
      : {};
    const lightMode = isJsonObject(modes.light)
      ? (modes.light as Record<string, JsonValue>)
      : {};
    const darkMode = isJsonObject(modes.dark)
      ? (modes.dark as Record<string, JsonValue>)
      : {};
    const legacyNotifications = isJsonObject(root.notifications as JsonValue)
      ? (root.notifications as Record<string, JsonValue>)
      : {};
    const visibleNotifications = {
      ...legacyNotifications,
    };
    delete visibleNotifications.background;
    delete visibleNotifications.titleColor;
    delete visibleNotifications.bodyColor;
    const lightNotifications = isJsonObject(lightMode.notifications)
      ? (lightMode.notifications as Record<string, JsonValue>)
      : {};
    const darkNotifications = isJsonObject(darkMode.notifications)
      ? (darkMode.notifications as Record<string, JsonValue>)
      : {};
    return {
      ...root,
      notifications: visibleNotifications,
      statusBar: normalizeThemeChromeGroup("statusBar", root.statusBar),
      navigationBar: normalizeThemeChromeGroup("navigationBar", root.navigationBar),
      modes: {
        ...modes,
        light: {
          ...lightMode,
          statusBar: normalizeThemeChromeGroup("statusBar", lightMode.statusBar),
          navigationBar: normalizeThemeChromeGroup(
            "navigationBar",
            lightMode.navigationBar,
          ),
          notifications: {
            background:
              lightNotifications.background ??
              legacyNotifications.background ??
              "rgba(245,245,247,0.92)",
            titleColor:
              lightNotifications.titleColor ??
              legacyNotifications.titleColor ??
              "#000000",
            bodyColor:
              lightNotifications.bodyColor ??
              legacyNotifications.bodyColor ??
              "#3A3A3C",
          },
        },
        dark: {
          ...darkMode,
          statusBar: normalizeThemeChromeGroup("statusBar", darkMode.statusBar, true),
          navigationBar: normalizeThemeChromeGroup(
            "navigationBar",
            darkMode.navigationBar,
            true,
          ),
          notifications: {
            background:
              darkNotifications.background ??
              legacyNotifications.background ??
              "rgba(44,44,46,0.92)",
            titleColor:
              darkNotifications.titleColor ??
              legacyNotifications.titleColor ??
              "#FFFFFF",
            bodyColor:
              darkNotifications.bodyColor ??
              legacyNotifications.bodyColor ??
              "#D1D1D6",
          },
        },
      },
    } as Record<string, JsonValue>;
  }

  function renderThemeChromeGroup(
    tokenRoot: Record<string, JsonValue>,
    groupKey: "statusBar" | "navigationBar",
  ) {
    const group = isJsonObject(tokenRoot[groupKey])
      ? (tokenRoot[groupKey] as Record<string, JsonValue>)
      : themeChromeDefaults(groupKey);
    const typeOptions =
      groupKey === "statusBar"
        ? ["dummy-status-bar", "ios-default", "android-default"]
        : [
            "dummy-navigation-bar",
            "ios-home-indicator",
            "android-gesture",
            "android-3-button",
          ];
    function updateChrome(path: JsonPath, nextValue: JsonValue) {
      setJsonDraft(
        "tokens_json",
        setAtPath(tokenRoot as JsonValue, [groupKey, ...path], nextValue),
      );
    }

    return (
      <div className="theme-chrome-editor">
        <InspectorFieldRow
          className="record-editor-field"
          label={<span>Type</span>}
          control={
            <select
              value={String(group.type ?? typeOptions[0])}
              onChange={(event) => updateChrome(["type"], event.target.value)}
            >
              {typeOptions.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          }
        />
        <InspectorFieldRow
          className="record-editor-field"
          label={<span>Icon scale</span>}
          control={
            <input
              type="number"
              step="0.05"
              value={Number(group.iconScale ?? 1)}
              onChange={(event) =>
                updateChrome(["iconScale"], Number(event.target.value))
              }
            />
          }
        />
        <InspectorFieldRow
          className="record-editor-field"
          label={<span>Background</span>}
          control={
            <ColorValueEditor
              value={String(group.background ?? "rgba(255,255,255,0)")}
              alpha
              onChange={(nextValue) => updateChrome(["background"], nextValue)}
            />
          }
        />
      </div>
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

    function appInitials() {
      const source = String(drafts.name ?? record?.name ?? "");
      const words = source
        .split(/\s+/)
        .map((word) => word.trim())
        .filter(Boolean);
      if (words.length === 0) return "?";
      return words
        .slice(0, 2)
        .map((word) => word[0]?.toUpperCase() ?? "")
        .join("");
    }

    function appIcon() {
      const icon = appMetadataRoot.icon;
      return icon && typeof icon === "object" && !Array.isArray(icon)
        ? (icon as Record<string, unknown>)
        : {};
    }

    function setAppIconPatch(patch: Record<string, unknown>) {
      const icon = appIcon();
      setJsonDraft("metadata_json", {
        ...appMetadataRoot,
        icon: {
          baseSize: 640,
          scale: 1,
          offsetX: 0,
          offsetY: 0,
          ...icon,
          ...patch,
        },
      });
    }

    function renderAppIconFields() {
      const icon = appIcon();
      const filePath = typeof icon.filePath === "string" ? icon.filePath : "";
      const scale = typeof icon.scale === "number" ? icon.scale : 1;
      const offsetX = typeof icon.offsetX === "number" ? icon.offsetX : 0;
      const offsetY = typeof icon.offsetY === "number" ? icon.offsetY : 0;
      const mediaRoot = productionMediaRoot();

      async function chooseAppIconFile() {
        const [selectedPath] = await (mockupsNative()?.pickFile?.() ?? Promise.resolve([]));
        if (selectedPath) {
          setAppIconPatch({
            filePath: relativePathFromRoot(selectedPath, mediaRoot),
          });
        }
      }

      return (
        <>
          <InspectorFieldRow
            key="app_icon_file"
            className="record-editor-field record-editor-field-string"
            label={<span>App icon image</span>}
            control={
              <div className="media-file-control actor-avatar-file-control">
                <DeferredTextInput
                  value={filePath}
                  onCommit={(nextValue) => setAppIconPatch({ filePath: nextValue })}
                />
                <button
                  type="button"
                  className="record-editor-compact-button"
                  disabled={!mockupsNative()?.pickFile}
                  onClick={() => {
                    void chooseAppIconFile();
                  }}
                >
                  Browse…
                </button>
              </div>
            }
          />
          <div className="actor-avatar-frame app-icon-frame" aria-label="App icon crop frame">
            <ActorAvatarPreview
              filePath={filePath}
              mediaRoot={mediaRoot}
              scale={scale}
              offsetX={offsetX}
              offsetY={offsetY}
              useInitials={false}
              backgroundColor="#f2f4f7"
              textColor="#475467"
              initials={appInitials()}
            />
            <small>Base icon frame: 640×640</small>
          </div>
          <InspectorFieldRow
            key="app_icon_scale"
            className="record-editor-field record-editor-field-number"
            label={<span>Icon scale</span>}
            control={
              <input
                type="number"
                step={0.01}
                min={0.01}
                value={String(scale)}
                onChange={(event) =>
                  setAppIconPatch({ scale: Number(event.target.value) })
                }
              />
            }
          />
          <InspectorFieldRow
            key="app_icon_offset_x"
            className="record-editor-field record-editor-field-number"
            label={<span>Icon offset X</span>}
            control={
              <input
                type="number"
                step={1}
                value={String(offsetX)}
                onChange={(event) =>
                  setAppIconPatch({ offsetX: Number(event.target.value) })
                }
              />
            }
          />
          <InspectorFieldRow
            key="app_icon_offset_y"
            className="record-editor-field record-editor-field-number"
            label={<span>Icon offset Y</span>}
            control={
              <input
                type="number"
                step={1}
                value={String(offsetY)}
                onChange={(event) =>
                  setAppIconPatch({ offsetY: Number(event.target.value) })
                }
              />
            }
          />
        </>
      );
    }

    function appWallpaper() {
      const wallpaper = appTokenRoot.wallpaper;
      return wallpaper && typeof wallpaper === "object" && !Array.isArray(wallpaper)
        ? (wallpaper as Record<string, unknown>)
        : {};
    }

    function appWallpaperModeColor(mode: "light" | "dark") {
      const modes = appTokenRoot.modes;
      const modeRoot =
        modes && typeof modes === "object" && !Array.isArray(modes)
          ? (modes as Record<string, unknown>)[mode]
          : undefined;
      const modeObject =
        modeRoot && typeof modeRoot === "object" && !Array.isArray(modeRoot)
          ? (modeRoot as Record<string, unknown>)
          : {};
      const wallpaper =
        modeObject.wallpaper &&
        typeof modeObject.wallpaper === "object" &&
        !Array.isArray(modeObject.wallpaper)
          ? (modeObject.wallpaper as Record<string, unknown>)
          : {};
      const color = wallpaper.color;
      return typeof color === "string" && color.trim()
        ? color
        : mode === "light"
          ? "#f7f7f7"
          : "#000000";
    }

    function updateAppWallpaper(path: JsonPath, nextValue: JsonValue) {
      updateAppTokenRoot(
        setAtPath(appTokenRoot as JsonValue, ["wallpaper", ...path], nextValue),
      );
    }

    function updateAppWallpaperModeColor(mode: "light" | "dark", nextColor: string) {
      updateAppTokenRoot(
        setAtPath(
          appTokenRoot as JsonValue,
          ["modes", mode, "wallpaper", "color"],
          nextColor,
        ),
      );
    }

    function renderAppWallpaperEditor() {
      const wallpaper = appWallpaper();
      const kind = String(wallpaper.kind ?? "solid");
      const opacity =
        typeof wallpaper.opacity === "number" && Number.isFinite(wallpaper.opacity)
          ? wallpaper.opacity
          : 1;
      const image =
        wallpaper.image && typeof wallpaper.image === "object" && !Array.isArray(wallpaper.image)
          ? (wallpaper.image as Record<string, unknown>)
          : {};
      const filePath = typeof image.filePath === "string" ? image.filePath : "";
      const mediaRoot = productionMediaRoot();

      async function chooseWallpaperFile() {
        const [selectedPath] = await (mockupsNative()?.pickFile?.() ?? Promise.resolve([]));
        if (selectedPath) {
          updateAppWallpaper(
            ["image"],
            {
              filePath: relativePathFromRoot(selectedPath, mediaRoot),
              fit: "cover",
              position: "center",
            },
          );
        }
      }

      return (
        <div className="record-editor-field-stack record-editor-single-column wallpaper-editor">
          <InspectorFieldRow
            className="record-editor-field record-editor-field-string"
            label={<span>Kind</span>}
            control={
              <select
                value={kind === "image" ? "image" : "solid"}
                onChange={(event) =>
                  updateAppWallpaper(["kind"], event.target.value)
                }
              >
                <option value="solid">solid</option>
                <option value="image">image</option>
              </select>
            }
          />
          <InspectorFieldRow
            className="record-editor-field record-editor-field-number"
            label={<span>Opacity</span>}
            control={
              <input
                type="number"
                min={0}
                max={1}
                step={0.01}
                value={String(opacity)}
                onChange={(event) =>
                  updateAppWallpaper(["opacity"], Number(event.target.value))
                }
              />
            }
          />
          {kind === "image" ? (
            <>
              <InspectorFieldRow
                className="record-editor-field record-editor-field-string"
                label={<span>Image</span>}
                control={
                  <div className="media-file-control actor-avatar-file-control">
                    <DeferredTextInput
                      value={filePath}
                      onCommit={(nextValue) =>
                        updateAppWallpaper(["image"], {
                          filePath: nextValue,
                          fit: "cover",
                          position: "center",
                        })
                      }
                    />
                    <button
                      type="button"
                      className="record-editor-compact-button"
                      disabled={!mockupsNative()?.pickFile}
                      onClick={() => {
                        void chooseWallpaperFile();
                      }}
                    >
                      Browse…
                    </button>
                  </div>
                }
              />
              <div className="wallpaper-preview-frame">
                <MediaCoverPreview
                  filePath={filePath}
                  mediaRoot={mediaRoot}
                  fallbackLabel="No wallpaper image"
                />
                <small>Fit: cover · Position: center</small>
              </div>
            </>
          ) : (
            <div className="wallpaper-color-grid">
              <div className="wallpaper-color-heading" />
              <div className="wallpaper-color-heading">Light</div>
              <div className="wallpaper-color-heading">Dark</div>
              <span>Color</span>
              {(["light", "dark"] as const).map((mode) => (
                <div key={mode} className="actor-color-control compact-color-control">
                  <input
                    aria-label={`Wallpaper ${mode} color`}
                    type="color"
                    value={appWallpaperModeColor(mode)}
                    onChange={(event) =>
                      updateAppWallpaperModeColor(mode, event.target.value)
                    }
                  />
                  <input
                    aria-label={`Wallpaper ${mode} hex`}
                    value={appWallpaperModeColor(mode)}
                    onChange={(event) =>
                      updateAppWallpaperModeColor(mode, event.target.value)
                    }
                  />
                </div>
              ))}
            </div>
          )}
        </div>
      );
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
            {renderAppIconFields()}
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
                    renderAppWallpaperEditor()
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
    const themeTokenRoot = normalizedThemeTokenRoot(
      parsedObject(drafts.tokens_json ?? "{}"),
    );
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
                  {renderThemeChromeGroup(themeTokenRoot, group)}
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

    function updateBehaviorValue(path: JsonPath, nextValue: JsonValue) {
      const root = parsedObject(drafts.behavior_json ?? "{}");
      setDrafts({
        ...drafts,
        behavior_json: stringifyJson(setAtPath(root as JsonValue, path, nextValue)),
      });
    }

    function renderModuleBehaviorFields() {
      const root = parsedObject(drafts.behavior_json ?? "{}");
      return (
        <>
          {[
            ["Show header", "showHeader", true],
            ["Show status bar", "showStatusBar", true],
            ["Show keyboard", "showKeyboard", false],
          ].map(([label, key, fallback]) => (
            <InspectorFieldRow
              key={String(key)}
              className="record-editor-field record-editor-field-boolean"
              label={<span>{String(label)}</span>}
              control={
                <input
                  type="checkbox"
                  checked={Boolean(root[String(key)] ?? fallback)}
                  onChange={(event) =>
                    updateBehaviorValue([String(key)], event.target.checked)
                  }
                />
              }
            />
          ))}
          <InspectorFieldRow
            key="initialScroll"
            className="record-editor-field record-editor-field-string"
            label={<span>Initial scroll</span>}
            control={
              <select
                value={String(root.initialScroll ?? "bottom")}
                onChange={(event) =>
                  updateBehaviorValue(["initialScroll"], event.target.value)
                }
              >
                <option value="top">Top</option>
                <option value="bottom">Bottom</option>
                <option value="preserve">Preserve</option>
              </select>
            }
          />
        </>
      );
    }

    function contentGroupHasWarning(group: string) {
      const contentRoot = parsedObject(drafts.content_json ?? "{}");
      const participants = contentRoot.participants;
      if (group === "header") {
        const headerValue = contentRoot.header as JsonValue;
        if (!isJsonObject(headerValue) || !Array.isArray(participants)) {
          return false;
        }
        const header = headerValue;
        const participant = (participants as JsonValue[])
          .filter(isJsonObject)
          .find((item) => item.id === header.avatarParticipantId);
        const inheritedName = participant
          ? String(
              participant.displayName ??
                records.actors?.find((item) => item.id === participant.actorId)
                  ?.display_name ??
                "",
            )
          : "";
        return Boolean(inheritedName) && String(header.title ?? "") !== inheritedName;
      }
      if (group !== "participants") return false;
      if (!Array.isArray(participants)) return false;
      return participants.some((participant) => {
        if (!isJsonObject(participant)) return false;
        const actor = records.actors?.find(
          (item) => item.id === participant.actorId,
        );
        const inheritedName = String(actor?.display_name ?? "");
        return (
          Boolean(inheritedName) &&
          String(participant.displayName ?? "") !== inheritedName
        );
      });
    }

    return (
      <ModuleInstanceEditor
        table={table}
        record={record}
        activeTab={screenTab}
        activeContentTab={activeContentTab}
        contentFieldExists={Boolean(contentField)}
        behaviorFieldExists={Boolean(behaviorField)}
        contentGroups={safeContentGroups}
        contentGroupHasWarning={contentGroupHasWarning}
        renderContentGroup={(group) =>
          contentField
            ? renderContentGroupEditor(contentField, group, "content_json")
            : null
        }
        renderBehaviorFields={renderModuleBehaviorFields}
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
        renderTransitionFields={renderScreenTransitionField}
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
      showGeneralWarning={table.id === "shots" && shotHasFpsOverride()}
    />
  );
}
