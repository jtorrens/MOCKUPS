import type { ReactNode } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppTableDefinition,
} from "../api/client.js";
import {
  APP_CONFIG_BINDINGS,
  APP_METADATA_BINDINGS,
} from "../../domain/fields/appFields.js";
import { THEME_TOKEN_BINDINGS } from "../../domain/fields/themeFields.js";
import { EditorSubsectionAccordion } from "../editor-ui/EditorSubsectionAccordion.js";
import {
  hasModeColorOverrides,
  ModeColorEditor,
} from "../components/json-editor/ModeColorEditor.js";
import {
  TokenOverrideEditor,
  tokenOverrideHasNonDefaultFields,
} from "../components/json-editor/TokenOverrideEditor.js";
import { jsonUiHintsFromFieldBindings } from "../components/json-editor/fieldDefinitionHints.js";
import { createPaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import type { ProductionFontCatalog } from "../components/json-editor/productionFonts.js";
import {
  isJsonObject,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";
import { friendlyGroupLabel } from "../components/json-editor/labels.js";
import { AppEditor } from "./AppEditor.js";
import { NeutralTintGroupEditor } from "./ThemeFields.js";
import {
  AppIconFields,
  AppWallpaperEditor,
} from "./AppMediaFields.js";
import {
  editorValueForTokenGroup,
  inheritedValueForTokenGroup,
  mergeTokenGroupWithInternalFields,
  stripAppStatusAndNavigationTokens,
  tokenEditorGroups,
} from "./recordTokenUtils.js";
import { parsedObject } from "./recordJsonUtils.js";
import type { AppEditorTab } from "./editorTabs.js";

interface AppNativeBridge {
  pickFile?: () => Promise<string[]>;
}

const APP_EDITABLE_TOKEN_GROUPS = [
  "fonts",
  "surfaceRelief",
] as const;

const APP_SPECIAL_TOKEN_GROUPS = new Set<string>([
  "icon",
  "neutralTint",
  "wallpaper",
]);

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
  productionFontCatalog?: ProductionFontCatalog;
  nativeBridge: AppNativeBridge | undefined;
  renderFields: (columns: string[]) => ReactNode;
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
  productionFontCatalog,
  nativeBridge,
  renderFields,
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
  const appNoteHints = jsonUiHintsFromFieldBindings(
    APP_METADATA_BINDINGS.filter((binding) => binding.outputPath[0] === "note"),
  );
  const appIconHints = jsonUiHintsFromFieldBindings(
    APP_METADATA_BINDINGS.filter((binding) => binding.outputPath[0] === "icon"),
  );
  const appWallpaperHints = jsonUiHintsFromFieldBindings(APP_CONFIG_BINDINGS);
  const appTokenHints = jsonUiHintsFromFieldBindings(THEME_TOKEN_BINDINGS);
  const appTokenGroups = tokenEditorGroups(appEditorTokenRoot, inheritedAppRoot);
  const hintedAppTokenGroups = new Set(
    Object.values(appTokenHints)
      .map((hint) => {
        const path = hint.storagePath ?? [];
        return typeof path[0] === "string" ? path[0] : undefined;
      })
      .filter((group): group is string => Boolean(group)),
  );
  const appTokenSections = Array.from(
    new Set([...appTokenGroups, ...APP_EDITABLE_TOKEN_GROUPS, "icon"]),
  )
    .filter(
      (group) =>
        APP_SPECIAL_TOKEN_GROUPS.has(group) || hintedAppTokenGroups.has(group),
    )
    .sort((left, right) =>
      friendlyGroupLabel(left).localeCompare(friendlyGroupLabel(right)),
    );
  const resolvedActiveTokenGroup =
    activeTokenGroup && appTokenSections.includes(activeTokenGroup)
      ? activeTokenGroup
      : "";

  function updateAppTokenRoot(nextValue: JsonValue) {
    const cleanNextValue = stripAppStatusAndNavigationTokens(nextValue);
    const nextConfig = Object.hasOwn(appConfigRoot, "tokens_json")
      ? { ...appConfigRoot, tokens_json: cleanNextValue }
      : cleanNextValue;
    setJsonDraft("config_json", nextConfig);
  }

  function appTokenSectionWarning(group: string) {
    if (group === "icon") {
      return tokenOverrideHasNonDefaultFields({
        rootValue: appMetadataRoot as JsonValue,
        inheritedRoot: {},
        hints: appIconHints,
        groupContext: "icon",
      });
    }
    if (group === "wallpaper") {
      return tokenOverrideHasNonDefaultFields({
        rootValue: appTokenRoot as JsonValue,
        inheritedRoot: {},
        hints: appWallpaperHints,
        groupContext: "wallpaper",
      });
    }
    return tokenOverrideHasNonDefaultFields({
      rootValue:
        group === "neutralTint"
          ? (appEditorTokenRoot as JsonValue)
          : (editorValueForTokenGroup(appEditorTokenRoot, group) as JsonValue),
      inheritedRoot:
        group === "neutralTint"
          ? (inheritedAppRoot as JsonValue)
          : ((inheritedValueForTokenGroup(inheritedAppRoot, group) ??
              {}) as JsonValue),
      hints: appTokenHints,
      groupContext: group,
    });
  }

  return (
    <AppEditor
      table={table}
      record={record}
      activeTab={activeTab}
      tokensFieldExists={Boolean(configField)}
      notesFieldExists={Boolean(metadataField)}
      colorsFieldExists={false}
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
          {renderFields(["name"])}
        </>
      )}
      renderTokens={() =>
        configField
          ? appTokenSections.map((group) => (
              <EditorSubsectionAccordion
                key={group}
                group={group}
                activeGroup={resolvedActiveTokenGroup}
                warning={appTokenSectionWarning(group)}
                onToggle={setActiveTokenGroup}
              >
                {group === "icon" ? (
                  <AppIconFields
                    metadataRoot={appMetadataRoot}
                    mediaRoot={mediaRoot}
                    nativeBridge={nativeBridge}
                    onMetadataRootChange={(nextRoot) =>
                      setJsonDraft("metadata_json", nextRoot)
                    }
                  />
                ) : group === "wallpaper" ? (
                  <AppWallpaperEditor
                    appTokenRoot={appTokenRoot}
                    mediaRoot={mediaRoot}
                    nativeBridge={nativeBridge}
                    paletteCatalog={paletteCatalog}
                    onTokenRootChange={updateAppTokenRoot}
                  />
                ) : group === "neutralTint" ? (
                  <NeutralTintGroupEditor
                    tokenRoot={appEditorTokenRoot as Record<string, JsonValue>}
                    onTokenRootChange={updateAppTokenRoot}
                  />
                ) : (
                  <div className="record-editor-field-stack record-editor-single-column theme-token-group-editor">
                    <TokenOverrideEditor
                      rootValue={
                        editorValueForTokenGroup(
                          appEditorTokenRoot,
                          group,
                        ) as JsonValue
                      }
                      inheritedRoot={
                        (inheritedValueForTokenGroup(inheritedAppRoot, group) ??
                          {}) as JsonValue
                      }
                      hints={appTokenHints}
                      groupContext={group}
                      inlineSingleGroup
                      productionFontCatalog={productionFontCatalog}
                      paletteCatalog={paletteCatalog}
                      mediaRoot={mediaRoot}
                      nativeBridge={nativeBridge}
                      onRootChange={(nextVisibleValue) => {
                        updateAppTokenRoot({
                          ...appEditorTokenRoot,
                          [group]: mergeTokenGroupWithInternalFields(
                            appEditorTokenRoot[group],
                            nextVisibleValue,
                          ),
                        } as JsonValue);
                      }}
                    />
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
        metadataField ? (
          <TokenOverrideEditor
            rootValue={appMetadataRoot as JsonValue}
            inheritedRoot={{}}
            hints={appNoteHints}
            inlineSingleGroup
            onRootChange={(nextRoot) => setJsonDraft("metadata_json", nextRoot)}
          />
        ) : null
      }
      setActiveTab={setActiveTab}
    />
  );
}
