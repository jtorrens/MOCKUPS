import type { ReactNode } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppTableDefinition,
} from "../api/client.js";
import { EditorSubsectionAccordion } from "../editor-ui/EditorSubsectionAccordion.js";
import {
  hasModeColorOverrides,
  ModeColorEditor,
} from "../components/json-editor/ModeColorEditor.js";
import { createPaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import {
  isJsonObject,
  stringifyJson,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";
import { AppEditor } from "./AppEditor.js";
import { NeutralTintGroupEditor } from "./ThemeFields.js";
import {
  AppIconFields,
  AppWallpaperEditor,
} from "./AppMediaFields.js";
import { ActorAvatarPreview, MediaCoverPreview } from "./MediaPreviews.js";
import {
  editorValueForTokenGroup,
  inheritedValueForTokenGroup,
  mergeTokenGroupWithInternalFields,
  stripAppStatusAndNavigationTokens,
  tokenEditorGroups,
} from "./recordTokenUtils.js";
import { parsedObject } from "./recordJsonUtils.js";
import type { RawJsonFieldOverride } from "./RecordFieldRenderer.js";
import type { AppEditorTab } from "./editorTabs.js";

interface AppNativeBridge {
  pickFile?: () => Promise<string[]>;
}

interface AppRecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  records: Record<string, AppRecord[]>;
  fieldsByColumn: Map<string, AppFieldDefinition>;
  drafts: Record<string, string>;
  inheritedFields: Record<string, unknown>;
  activeTab: AppEditorTab;
  activeTokenGroup: string;
  mediaRoot: string;
  nativeBridge: AppNativeBridge | undefined;
  renderFields: (columns: string[]) => ReactNode;
  renderField: (
    field: AppFieldDefinition,
    rawOverride?: RawJsonFieldOverride,
  ) => ReactNode;
  renderFlatJsonObjectEditor: (column: string, omitKeys?: string[]) => ReactNode;
  setActiveTab: (tab: AppEditorTab) => void;
  setActiveTokenGroup: (group: string) => void;
  setJsonDraft: (column: string, value: JsonValue) => void;
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
    return Object.entries(local as Record<string, unknown>).some(([key, value]) =>
      explicitLocalOverridesInherited(
        value,
        (inherited as Record<string, unknown>)[key],
      ),
    );
  }
  if (Array.isArray(local)) {
    return (
      Array.isArray(inherited) &&
      JSON.stringify(local) !== JSON.stringify(inherited)
    );
  }
  return JSON.stringify(local) !== JSON.stringify(inherited);
}

export function AppRecordEditor({
  table,
  record,
  records,
  fieldsByColumn,
  drafts,
  inheritedFields,
  activeTab,
  activeTokenGroup,
  mediaRoot,
  nativeBridge,
  renderFields,
  renderField,
  renderFlatJsonObjectEditor,
  setActiveTab,
      setActiveTokenGroup,
      setJsonDraft,
}: AppRecordEditorProps) {
  const configField = fieldsByColumn.get("config_json");
  const metadataField = fieldsByColumn.get("metadata_json");
  const appConfigRoot = parsedObject(drafts.config_json ?? "{}");
  const appMetadataRoot = parsedObject(drafts.metadata_json ?? "{}");
  const paletteCatalog = createPaletteColorCatalog(
    records,
    typeof record.production_id === "string" ? record.production_id : undefined,
  );
  const appTokenRoot = isJsonObject(appConfigRoot.tokens_json as JsonValue)
    ? (appConfigRoot.tokens_json as Record<string, unknown>)
    : appConfigRoot;
  const appEditorTokenRoot = stripAppStatusAndNavigationTokens(appTokenRoot);
  const inheritedAppRoot = stripAppStatusAndNavigationTokens(
    inheritedFields.config_json,
  );
  const appTokenGroups = tokenEditorGroups(appEditorTokenRoot, inheritedAppRoot);
  const resolvedActiveTokenGroup =
    activeTokenGroup && appTokenGroups.includes(activeTokenGroup)
      ? activeTokenGroup
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
      activeTab={activeTab}
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
          {renderFields(["id", "production_id", "name", "bundle_key", "app_type"])}
          <AppIconFields
            record={record}
            drafts={drafts}
            metadataRoot={appMetadataRoot}
            mediaRoot={mediaRoot}
            nativeBridge={nativeBridge}
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
              <EditorSubsectionAccordion
                key={group}
                group={group}
                activeGroup={resolvedActiveTokenGroup}
                onToggle={setActiveTokenGroup}
              >
                {group === "wallpaper" ? (
                  <AppWallpaperEditor
                    appTokenRoot={appTokenRoot}
                    mediaRoot={mediaRoot}
                    nativeBridge={nativeBridge}
                    paletteCatalog={paletteCatalog}
                    MediaCoverPreview={MediaCoverPreview}
                    onTokenRootChange={updateAppTokenRoot}
                  />
                ) : group === "neutralTint" ? (
                  <NeutralTintGroupEditor
                    tokenRoot={appEditorTokenRoot as Record<string, JsonValue>}
                    onTokenRootChange={updateAppTokenRoot}
                  />
                ) : (
                  <div className="record-editor-field-stack record-editor-single-column theme-token-group-editor">
                    {renderField(configField, {
                      hideLabel: true,
                      rawText: stringifyJson(
                        editorValueForTokenGroup(
                          appEditorTokenRoot,
                          group,
                        ) as JsonValue,
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
              </EditorSubsectionAccordion>
            ))
          : null
      }
      renderColors={() => (
        <ModeColorEditor
          rootValue={appEditorTokenRoot as JsonValue}
          inheritedRoot={inheritedAppRoot as JsonValue | undefined}
          hiddenGroups={["wallpaper"]}
          paletteCatalog={paletteCatalog}
          onRootChange={updateAppTokenRoot}
        />
      )}
      renderNotes={() =>
        metadataField ? renderFlatJsonObjectEditor("metadata_json") : null
      }
      setActiveTab={setActiveTab}
    />
  );
}
