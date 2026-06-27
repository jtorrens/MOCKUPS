import { useState, type ReactNode } from "react";
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
import {
  InspectorFieldRow,
} from "../components/inspector/InspectorFieldRow.js";
import {
  ComponentOverrideModal,
  type ComponentOverrideField,
} from "../components/ComponentOverrideModal.js";
import { createPaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import type { JsonValue } from "../components/json-editor/jsonEditorUtils.js";
import { ModuleThemeConfigEditor } from "./ModuleThemeConfigEditor.js";
import { ModuleFunctionalConfigFields } from "./ModuleFunctionalConfigFields.js";
import { parsedObject } from "./recordJsonUtils.js";
import type { RawJsonFieldOverride } from "./RecordFieldRenderer.js";
import type { ModuleThemeTab } from "./editorTabs.js";
import {
  normalizeCoreChatModuleTokensForEditor,
  stripModuleSystemOwnedTokens,
} from "./recordTokenUtils.js";

interface ModuleThemeConfigRecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  records: Record<string, AppRecord[]>;
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

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function componentTokens(record: AppRecord | undefined) {
  return isPlainObject(record?.tokens_json)
    ? (record.tokens_json as Record<string, unknown>)
    : {};
}

const labelOverrideFields: ComponentOverrideField[] = [
  {
    key: "sizingMode",
    label: "Sizing mode",
    kind: "select",
    options: [
      { value: "content", label: "Text + padding" },
      { value: "fixed", label: "Fixed box" },
    ],
  },
  { key: "width", label: "Width", kind: "number" },
  { key: "height", label: "Height", kind: "number" },
  { key: "paddingX", label: "Padding X", kind: "number" },
  { key: "paddingY", label: "Padding Y", kind: "number" },
  { key: "cornerRadius", label: "Corner radius", kind: "number" },
  { key: "borderWidth", label: "Border width", kind: "number" },
  { key: "borderColorToken", label: "Border theme color", kind: "text" },
  { key: "backgroundVisible", label: "Background visible", kind: "boolean" },
  { key: "backgroundColorToken", label: "Background theme color", kind: "text" },
  { key: "textColorToken", label: "Text theme color", kind: "text" },
  { key: "fontSize", label: "Text size", kind: "number" },
  { key: "fontWeight", label: "Text weight", kind: "text" },
  { key: "shadowEnabled", label: "Shadow", kind: "boolean" },
  { key: "shadowToken", label: "Shadow token", kind: "text" },
  { key: "surfaceReliefEnabled", label: "Surface relief", kind: "boolean" },
];

function differsFromInherited(
  drafts: Record<string, string>,
  inheritedFields: Record<string, unknown>,
  column: string,
) {
  const inherited = inheritedFields[column];
  if (!inherited) return hasObjectContent(drafts[column]);
  return explicitLocalDiffers(parsedObject(drafts[column] ?? "{}"), inherited);
}

function moduleEditorTokenRoot(
  record: AppRecord,
  value: unknown,
): Record<string, JsonValue> {
  const stripped = stripModuleSystemOwnedTokens(value);
  return record.module_id === "core.chat"
    ? normalizeCoreChatModuleTokensForEditor(stripped)
    : stripped;
}

function stringifyJson(value: unknown): string {
  return JSON.stringify(value, null, 2);
}

export function ModuleThemeConfigRecordEditor({
  table,
  record,
  records,
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
  const [componentOverrideModal, setComponentOverrideModal] = useState<
    string | null
  >(null);
  const tokensField = fieldsByColumn.get("tokens_json");
  const themeRecord = (records.themes ?? []).find(
    (theme) => theme.id === record.theme_id,
  );
  const paletteCatalog = createPaletteColorCatalog(
    records,
    typeof themeRecord?.production_id === "string"
      ? themeRecord.production_id
      : undefined,
  );
  const tokenRoot = moduleEditorTokenRoot(
    record,
    parsedObject(drafts.tokens_json ?? "{}"),
  );
  const inheritedTokenRoot =
    inheritedFields.tokens_json &&
    typeof inheritedFields.tokens_json === "object" &&
    !Array.isArray(inheritedFields.tokens_json)
      ? moduleEditorTokenRoot(record, inheritedFields.tokens_json)
      : undefined;
  const designGroups = Object.keys(tokenRoot).filter(
    (group) =>
      group !== "modes" &&
      group !== "textInputBar" &&
      group !== "keyboard" &&
      group !== "avatars" &&
      group !== "componentOverrides",
  );
  const selectableDesignGroups = [...designGroups, "controls"];
  const controlsWarning =
    explicitLocalDiffers(tokenRoot.textInputBar, inheritedTokenRoot?.textInputBar) ||
    explicitLocalDiffers(tokenRoot.keyboard, inheritedTokenRoot?.keyboard);
  const resolvedActiveDesignGroup =
    activeDesignGroup && selectableDesignGroups.includes(activeDesignGroup)
      ? activeDesignGroup
      : "";
  const avatarComponent = (records.component_classes ?? []).find(
    (component) =>
      component.production_id === themeRecord?.production_id &&
      component.component_type === "avatar",
  );
  const textInputBarComponent = (records.component_classes ?? []).find(
    (component) =>
      component.production_id === themeRecord?.production_id &&
      component.component_type === "text_input_bar",
  );
  const buttonIconComponent = (records.component_classes ?? []).find(
    (component) =>
      component.production_id === themeRecord?.production_id &&
      component.component_type === "button_icon",
  );
  const audioMessageComponent = (records.component_classes ?? []).find(
    (component) =>
      component.production_id === themeRecord?.production_id &&
      component.component_type === "audio_message",
  );
  const videoMessageComponent = (records.component_classes ?? []).find(
    (component) =>
      component.production_id === themeRecord?.production_id &&
      component.component_type === "video_message",
  );
  const keyboardComponent = (records.component_classes ?? []).find(
    (component) =>
      component.production_id === themeRecord?.production_id &&
      component.component_type === "keyboard",
  );
  const labelComponent = (records.component_classes ?? []).find(
    (component) =>
      component.production_id === themeRecord?.production_id &&
      component.component_type === "label",
  );
  const labelBaseTokens = componentTokens(labelComponent);
  const componentOverrides = isPlainObject(tokenRoot.componentOverrides)
    ? (tokenRoot.componentOverrides as Record<string, unknown>)
    : {};
  const labelOverrides = isPlainObject(componentOverrides.label)
    ? (componentOverrides.label as Record<string, unknown>)
    : {};
  const hasLabelOverrides = Object.keys(labelOverrides).length > 0;

  function setLabelOverrides(nextOverrides: Record<string, unknown>) {
    const nextComponentOverrides = {
      ...componentOverrides,
      ...(Object.keys(nextOverrides).length ? { label: nextOverrides } : {}),
    };
    if (!Object.keys(nextOverrides).length) {
      delete nextComponentOverrides.label;
    }
    const nextRoot: Record<string, unknown> = {
      ...tokenRoot,
      componentOverrides: nextComponentOverrides,
    };
    if (!Object.keys(nextComponentOverrides).length) {
      delete nextRoot.componentOverrides;
    }
    setJsonDraft("tokens_json", nextRoot as JsonValue);
  }

  function setLabelOverrideValue(key: string, value: unknown) {
    setLabelOverrides({
      ...labelOverrides,
      [key]: value,
    });
  }

  function restoreLabelOverride(key: string) {
    const nextOverrides = { ...labelOverrides };
    delete nextOverrides[key];
    setLabelOverrides(nextOverrides);
  }

  return (
    <>
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
                  {record.module_id === "core.chat" && group === "header" ? (
                    <InspectorFieldRow
                      label="Avatar component"
                      control={
                        <span style={{ display: "inline-flex", gap: 10, alignItems: "center" }}>
                          <input
                            className="json-value-control"
                            disabled
                            value={String(avatarComponent?.name ?? "Default avatar")}
                            readOnly
                          />
                          <button
                            type="button"
                            className="inspector-restore-button"
                            title="Component overrides will be edited here"
                            aria-label="Edit avatar component overrides"
                            disabled
                          >
                            ✎
                          </button>
                        </span>
                      }
                    />
                  ) : null}
                  {record.module_id === "core.chat" && group === "chatBubbles" ? (
                    <>
                      <InspectorFieldRow
                        label="Message label component"
                        control={
                          <span style={{ display: "inline-flex", gap: 10, alignItems: "center" }}>
                            <input
                              className="json-value-control"
                              disabled
                              value={String(labelComponent?.name ?? "Default label")}
                              readOnly
                            />
                            <button
                              type="button"
                              className="inspector-restore-button"
                              title={
                                hasLabelOverrides
                                  ? "Edit message label component overrides"
                                  : "Add message label component overrides"
                              }
                              aria-label="Edit message label component overrides"
                              onClick={() => setComponentOverrideModal("label")}
                              style={{
                                color: hasLabelOverrides
                                  ? "var(--editor-warning-color, #b45309)"
                                  : undefined,
                                borderColor: hasLabelOverrides
                                  ? "var(--editor-warning-border-color, #e4ad68)"
                                  : undefined,
                                background: hasLabelOverrides
                                  ? "var(--editor-warning-background, #fff7ed)"
                                  : undefined,
                              }}
                            >
                              ✎
                            </button>
                          </span>
                        }
                      />
                    </>
                  ) : null}
                  {renderField(tokensField, {
                    rawText: stringifyJson(tokenRoot[group] ?? {}),
                    hideLabel: true,
                    groupContext: group,
                    inheritedValue: inheritedTokenRoot?.[group] as
                      | Record<string, unknown>
                      | undefined,
                    onRawTextChange: (nextRawText) =>
                      setJsonDraft("tokens_json", {
                        ...tokenRoot,
                        [group]: parsedObject(nextRawText),
                      } as JsonValue),
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
                  {record.module_id === "core.chat" ? (
                    <>
                      <InspectorFieldRow
                        label="Text input component"
                        control={
                          <span style={{ display: "inline-flex", gap: 10, alignItems: "center" }}>
                            <input
                              className="json-value-control"
                              disabled
                              value={String(
                                textInputBarComponent?.name ??
                                  "Default text input bar",
                              )}
                              readOnly
                            />
                            <button
                              type="button"
                              className="inspector-restore-button"
                              title="Component overrides will be edited here"
                              aria-label="Edit text input component overrides"
                              disabled
                            >
                              ✎
                            </button>
                          </span>
                        }
                      />
                      <InspectorFieldRow
                        label="Icon button component"
                        control={
                          <span style={{ display: "inline-flex", gap: 10, alignItems: "center" }}>
                            <input
                              className="json-value-control"
                              disabled
                              value={String(
                                buttonIconComponent?.name ??
                                  "Default icon button",
                              )}
                              readOnly
                            />
                            <button
                              type="button"
                              className="inspector-restore-button"
                              title="Component overrides will be edited here"
                              aria-label="Edit icon button component overrides"
                              disabled
                            >
                              ✎
                            </button>
                          </span>
                        }
                      />
                      <InspectorFieldRow
                        label="Audio message component"
                        control={
                          <span style={{ display: "inline-flex", gap: 10, alignItems: "center" }}>
                            <input
                              className="json-value-control"
                              disabled
                              value={String(
                                audioMessageComponent?.name ??
                                  "Default audio message",
                              )}
                              readOnly
                            />
                            <button
                              type="button"
                              className="inspector-restore-button"
                              title="Component overrides will be edited here"
                              aria-label="Edit audio message component overrides"
                              disabled
                            >
                              ✎
                            </button>
                          </span>
                        }
                      />
                      <InspectorFieldRow
                        label="Video message component"
                        control={
                          <span style={{ display: "inline-flex", gap: 10, alignItems: "center" }}>
                            <input
                              className="json-value-control"
                              disabled
                              value={String(
                                videoMessageComponent?.name ??
                                  "Default video message",
                              )}
                              readOnly
                            />
                            <button
                              type="button"
                              className="inspector-restore-button"
                              title="Component overrides will be edited here"
                              aria-label="Edit video message component overrides"
                              disabled
                            >
                              ✎
                            </button>
                          </span>
                        }
                      />
                      <InspectorFieldRow
                        label="Keyboard component"
                        control={
                          <span style={{ display: "inline-flex", gap: 10, alignItems: "center" }}>
                            <input
                              className="json-value-control"
                              disabled
                              value={String(
                                keyboardComponent?.name ?? "Default keyboard",
                              )}
                              readOnly
                            />
                            <button
                              type="button"
                              className="inspector-restore-button"
                              title="Component overrides will be edited here"
                              aria-label="Edit keyboard component overrides"
                              disabled
                            >
                              ✎
                            </button>
                          </span>
                        }
                      />
                    </>
                  ) : null}
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
            paletteCatalog={paletteCatalog}
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
    {componentOverrideModal === "label" ? (
      <ComponentOverrideModal
        title="Message label"
        fields={labelOverrideFields}
        baseTokens={labelBaseTokens}
        overrides={labelOverrides}
        onClose={() => setComponentOverrideModal(null)}
        onSetOverride={setLabelOverrideValue}
        onRestoreOverride={restoreLabelOverride}
      />
    ) : null}
    </>
  );
}
