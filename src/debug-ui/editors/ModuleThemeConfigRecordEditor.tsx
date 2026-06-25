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
import type { JsonValue } from "../components/json-editor/jsonEditorUtils.js";
import { ModuleThemeConfigEditor } from "./ModuleThemeConfigEditor.js";
import { ModuleFunctionalConfigFields } from "./ModuleFunctionalConfigFields.js";
import { parsedObject } from "./recordJsonUtils.js";
import type { RawJsonFieldOverride } from "./RecordFieldRenderer.js";
import type { ModuleThemeTab } from "./editorTabs.js";
import { stripModuleSystemOwnedTokens } from "./recordTokenUtils.js";

interface ModuleThemeConfigRecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  fieldsByColumn: Map<string, AppFieldDefinition>;
  drafts: Record<string, string>;
  inheritedFields: Record<string, unknown>;
  activeTab: ModuleThemeTab;
  activeDesignGroup: string;
  renderFields: (columns: string[]) => ReactNode;
  renderField: (
    field: AppFieldDefinition,
    rawOverride?: RawJsonFieldOverride,
  ) => ReactNode;
  renderFlatJsonObjectEditor: (column: string, omitKeys?: string[]) => ReactNode;
  rawForJsonGroupValue: (column: string, groupKey: string) => string;
  updateJsonGroupValue: (
    column: string,
    groupKey: string,
    nextRawText: string,
  ) => void;
  setActiveTab: (tab: ModuleThemeTab) => void;
  setActiveDesignGroup: (group: string) => void;
  setJsonDraft: (column: string, value: JsonValue) => void;
}

function hasObjectContent(value: string | undefined) {
  const parsed = parsedObject(value ?? "{}");
  return Object.keys(parsed).length > 0;
}

function explicitLocalDiffers(local: unknown, inherited: unknown): boolean {
  if (local && typeof local === "object" && !Array.isArray(local)) {
    return Object.entries(local as Record<string, unknown>).some(([key, value]) =>
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

function differsFromInherited(
  drafts: Record<string, string>,
  inheritedFields: Record<string, unknown>,
  column: string,
) {
  const inherited = inheritedFields[column];
  if (!inherited) return hasObjectContent(drafts[column]);
  return explicitLocalDiffers(parsedObject(drafts[column] ?? "{}"), inherited);
}

export function ModuleThemeConfigRecordEditor({
  table,
  record,
  fieldsByColumn,
  drafts,
  inheritedFields,
  activeTab,
  activeDesignGroup,
  renderFields,
  renderField,
  renderFlatJsonObjectEditor,
  rawForJsonGroupValue,
  updateJsonGroupValue,
  setActiveTab,
  setActiveDesignGroup,
  setJsonDraft,
}: ModuleThemeConfigRecordEditorProps) {
  const tokensField = fieldsByColumn.get("tokens_json");
  const tokenRoot = stripModuleSystemOwnedTokens(
    parsedObject(drafts.tokens_json ?? "{}"),
  );
  const inheritedTokenRoot =
    inheritedFields.tokens_json &&
    typeof inheritedFields.tokens_json === "object" &&
    !Array.isArray(inheritedFields.tokens_json)
      ? stripModuleSystemOwnedTokens(
          inheritedFields.tokens_json as Record<string, unknown>,
        )
      : undefined;
  const designGroups = Object.keys(tokenRoot).filter(
    (group) =>
      group !== "modes" && group !== "textInputBar" && group !== "keyboard",
  );
  const selectableDesignGroups = [...designGroups, "controls"];
  const controlsWarning =
    explicitLocalDiffers(tokenRoot.textInputBar, inheritedTokenRoot?.textInputBar) ||
    explicitLocalDiffers(tokenRoot.keyboard, inheritedTokenRoot?.keyboard);
  const resolvedActiveDesignGroup =
    activeDesignGroup && selectableDesignGroups.includes(activeDesignGroup)
      ? activeDesignGroup
      : "";

  return (
    <ModuleThemeConfigEditor
      table={table}
      record={record}
      activeTab={activeTab}
      designFieldExists={Boolean(tokensField)}
      colorsFieldExists={Boolean(tokensField)}
      designWarning={differsFromInherited(drafts, inheritedFields, "tokens_json")}
      colorsWarning={hasModeColorOverrides(
        tokenRoot as JsonValue,
        inheritedTokenRoot as JsonValue | undefined,
      )}
      renderDesign={() =>
        tokensField
          ? (
            <>
              {designGroups.map((group) => (
              <EditorSubsectionAccordion
                key={group}
                group={group}
                activeGroup={resolvedActiveDesignGroup}
                warning={explicitLocalDiffers(
                  tokenRoot[group],
                  inheritedTokenRoot?.[group],
                )}
                onToggle={setActiveDesignGroup}
              >
                <div className="record-editor-field-stack record-editor-single-column">
                  {renderField(tokensField, {
                    rawText: rawForJsonGroupValue("tokens_json", group),
                    hideLabel: true,
                    groupContext: group,
                    inheritedValue: inheritedTokenRoot?.[group] as
                      | Record<string, unknown>
                      | undefined,
                    onRawTextChange: (nextRawText) =>
                      updateJsonGroupValue("tokens_json", group, nextRawText),
                  })}
                </div>
              </EditorSubsectionAccordion>
              ))}
              <EditorSubsectionAccordion
                key="controls"
                group="controls"
                activeGroup={resolvedActiveDesignGroup}
                warning={controlsWarning}
                onToggle={setActiveDesignGroup}
              >
                <div className="record-editor-field-stack record-editor-single-column">
                  <ModuleFunctionalConfigFields
                    rawValue={JSON.stringify(tokenRoot, null, 2)}
                    onRawChange={(nextRaw) =>
                      setJsonDraft(
                        "tokens_json",
                        parsedObject(nextRaw) as JsonValue,
                      )
                    }
                  />
                </div>
              </EditorSubsectionAccordion>
            </>
          )
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
      setActiveTab={setActiveTab}
    />
  );
}
