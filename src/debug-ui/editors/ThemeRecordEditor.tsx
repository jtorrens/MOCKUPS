import type { ReactNode } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppTableDefinition,
} from "../api/client.js";
import { THEME_TOKEN_BINDINGS } from "../../domain/fields/themeFields.js";
import { EditorSubsectionAccordion } from "../editor-ui/EditorSubsectionAccordion.js";
import { friendlyGroupLabel } from "../components/json-editor/labels.js";
import { ModeColorEditor } from "../components/json-editor/ModeColorEditor.js";
import { jsonUiHintsFromFieldBindings } from "../components/json-editor/fieldDefinitionHints.js";
import { tokenOverrideHasNonDefaultFields } from "../components/json-editor/TokenOverrideEditor.js";
import { createPaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import {
  stringifyJson,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";
import {
  NeutralTintGroupEditor,
  normalizedThemeTokenRoot,
  ThemeChromeGroupEditor,
  ThemeCursorGroupEditor,
  ThemeFontsGroupEditor,
  ThemeSurfaceReliefGroupEditor,
} from "./ThemeFields.js";
import { createProductionFontCatalog } from "../components/json-editor/productionFonts.js";
import { ThemeEditor } from "./ThemeEditor.js";
import {
  editorValueForThemeTokenGroup,
  nextThemeTokenGroupValue,
  tokenEditorGroups,
} from "./recordTokenUtils.js";
import { parsedJsonValue, parsedObject } from "./recordJsonUtils.js";
import type { RawJsonFieldOverride } from "./RecordFieldRenderer.js";
import type { ThemeEditorTab } from "./editorTabs.js";

interface ThemeRecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  records: Record<string, AppRecord[]>;
  fieldsByColumn: Map<string, AppFieldDefinition>;
  drafts: Record<string, string>;
  activeTab: ThemeEditorTab;
  activeTokenGroup: string;
  renderFields: (columns: string[]) => ReactNode;
  renderField: (
    field: AppFieldDefinition,
    rawOverride?: RawJsonFieldOverride,
  ) => ReactNode;
  setActiveTab: (tab: ThemeEditorTab) => void;
  setActiveTokenGroup: (group: string) => void;
  setJsonDraft: (column: string, value: JsonValue) => void;
}

export function ThemeRecordEditor({
  table,
  record,
  records,
  fieldsByColumn,
  drafts,
  activeTab,
  activeTokenGroup,
  renderFields,
  renderField,
  setActiveTab,
  setActiveTokenGroup,
  setJsonDraft,
}: ThemeRecordEditorProps) {
  const tokensField = fieldsByColumn.get("tokens_json");
  const family = String(drafts.family ?? record?.family ?? "ios");
  const paletteCatalog = createPaletteColorCatalog(
    records,
    typeof record.production_id === "string" ? record.production_id : undefined,
  );
  const productionFontCatalog = createProductionFontCatalog(records);
  const themeTokenHints = jsonUiHintsFromFieldBindings(THEME_TOKEN_BINDINGS);
  const themeTokenRoot = normalizedThemeTokenRoot({
    root: parsedObject(drafts.tokens_json ?? "{}"),
    family,
  });
  const themeTokenGroups = tokenEditorGroups(themeTokenRoot);
  const resolvedActiveTokenGroup =
    activeTokenGroup && themeTokenGroups.includes(activeTokenGroup)
      ? activeTokenGroup
      : "";

  function updateThemeTokenGroupValue(groupKey: string, nextRawText: string) {
    const parsedValue = parsedJsonValue(nextRawText, {});
    const nextRoot = {
      ...themeTokenRoot,
      [groupKey]: nextThemeTokenGroupValue({
        themeTokenRoot,
        groupKey,
        parsedValue,
      }),
    };
    setJsonDraft("tokens_json", nextRoot);
  }

  function themeTokenGroupWarning(group: string) {
    return tokenOverrideHasNonDefaultFields({
      rootValue:
        group === "neutralTint"
          ? (themeTokenRoot as JsonValue)
          : (editorValueForThemeTokenGroup(themeTokenRoot, group) as JsonValue),
      inheritedRoot: {},
      hints: themeTokenHints,
      groupContext: group,
    });
  }

  return (
    <ThemeEditor
      table={table}
      record={record}
      activeTab={activeTab}
      tokensFieldExists={Boolean(tokensField)}
      renderGeneral={() =>
        renderFields([
          "id",
          "name",
          "family",
          "icon_theme_id",
          "status_bar_id",
          "navigation_bar_id",
          "version",
        ])
      }
      renderTokens={() =>
        tokensField ? (
          <>
            {themeTokenGroups
              .filter(
                (group) =>
                  group !== "statusBar" &&
                  group !== "navigationBar" &&
                  group !== "keyboard" &&
                  group !== "cursor" &&
                  group !== "fonts" &&
                  group !== "neutralTint" &&
                  group !== "surfaceRelief",
              )
              .sort((left, right) =>
                friendlyGroupLabel(left).localeCompare(friendlyGroupLabel(right)),
              )
              .map((group) => (
                <EditorSubsectionAccordion
                  key={group}
                  group={group}
                  activeGroup={resolvedActiveTokenGroup}
                  warning={themeTokenGroupWarning(group)}
                  onToggle={setActiveTokenGroup}
                >
                  <div className="record-editor-field-stack record-editor-single-column theme-token-group-editor">
                    {renderField(tokensField, {
                      hideLabel: true,
                      rawText: stringifyJson(
                        editorValueForThemeTokenGroup(
                          themeTokenRoot,
                          group,
                        ) as JsonValue,
                      ),
                      groupContext: group,
                      onRawTextChange: (nextRawText) =>
                        updateThemeTokenGroupValue(group, nextRawText),
                    })}
                  </div>
                </EditorSubsectionAccordion>
              ))}
            {(["navigationBar"] as const).map((group) => (
              <EditorSubsectionAccordion
                key={group}
                group={group}
                activeGroup={resolvedActiveTokenGroup}
                warning={themeTokenGroupWarning(group)}
                onToggle={setActiveTokenGroup}
              >
                <ThemeChromeGroupEditor
                  tokenRoot={themeTokenRoot}
                  groupKey={group}
                  family={family}
                  onTokenRootChange={(nextRoot) =>
                    setJsonDraft("tokens_json", nextRoot)
                  }
                />
              </EditorSubsectionAccordion>
            ))}
            <EditorSubsectionAccordion
              key="fonts"
              group="fonts"
              activeGroup={resolvedActiveTokenGroup}
              warning={themeTokenGroupWarning("fonts")}
              onToggle={setActiveTokenGroup}
            >
              <ThemeFontsGroupEditor
                tokenRoot={themeTokenRoot}
                productionFontCatalog={productionFontCatalog}
                onTokenRootChange={(nextRoot) =>
                  setJsonDraft("tokens_json", nextRoot)
                }
              />
            </EditorSubsectionAccordion>
            <EditorSubsectionAccordion
              key="neutralTint"
              group="neutralTint"
              activeGroup={resolvedActiveTokenGroup}
              warning={themeTokenGroupWarning("neutralTint")}
              onToggle={setActiveTokenGroup}
            >
              <NeutralTintGroupEditor
                tokenRoot={themeTokenRoot}
                onTokenRootChange={(nextRoot) =>
                  setJsonDraft("tokens_json", nextRoot)
                }
              />
            </EditorSubsectionAccordion>
            <EditorSubsectionAccordion
              key="cursor"
              group="cursor"
              activeGroup={resolvedActiveTokenGroup}
              warning={themeTokenGroupWarning("cursor")}
              onToggle={setActiveTokenGroup}
            >
              <ThemeCursorGroupEditor
                tokenRoot={themeTokenRoot}
                onTokenRootChange={(nextRoot) =>
                  setJsonDraft("tokens_json", nextRoot)
                }
              />
            </EditorSubsectionAccordion>
            <EditorSubsectionAccordion
              key="surfaceRelief"
              group="surfaceRelief"
              activeGroup={resolvedActiveTokenGroup}
              warning={themeTokenGroupWarning("surfaceRelief")}
              onToggle={setActiveTokenGroup}
            >
              <ThemeSurfaceReliefGroupEditor
                tokenRoot={themeTokenRoot}
                onTokenRootChange={(nextRoot) =>
                  setJsonDraft("tokens_json", nextRoot)
                }
              />
            </EditorSubsectionAccordion>
          </>
        ) : null
      }
      renderColors={() =>
        tokensField ? (
          <ModeColorEditor
            rootValue={themeTokenRoot as JsonValue}
            paletteCatalog={paletteCatalog}
            onRootChange={(nextValue) => setJsonDraft("tokens_json", nextValue)}
          />
        ) : null
      }
      setActiveTab={setActiveTab}
    />
  );
}
