import type { ReactNode } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppTableDefinition,
} from "../api/client.js";
import { EditorSubsectionAccordion } from "../editor-ui/EditorSubsectionAccordion.js";
import { ModeColorEditor } from "../components/json-editor/ModeColorEditor.js";
import {
  stringifyJson,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";
import {
  normalizedThemeTokenRoot,
  ThemeChromeGroupEditor,
} from "./ThemeFields.js";
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
          "version",
        ])
      }
      renderTokens={() =>
        tokensField ? (
          <>
            {themeTokenGroups
              .filter(
                (group) => group !== "statusBar" && group !== "navigationBar",
              )
              .map((group) => (
                <EditorSubsectionAccordion
                  key={group}
                  group={group}
                  activeGroup={resolvedActiveTokenGroup}
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
      setActiveTab={setActiveTab}
    />
  );
}
