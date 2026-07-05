import { useState, type ReactNode } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppTableDefinition,
} from "../api/client.js";
import {
  CHAT_BUBBLE_TOKEN_BINDINGS,
  CHAT_HEADER_TOKEN_BINDINGS,
  CHAT_LAYOUT_MESSAGE_TOKEN_BINDINGS,
  CHAT_TYPOGRAPHY_TOKEN_BINDINGS,
} from "../../domain/fields/chatFields.js";
import type { JsonFieldBinding } from "../../domain/value-system/index.js";
import { EditorSubsectionAccordion } from "../editor-ui/EditorSubsectionAccordion.js";
import {
  DICTIONARY_FIELD_CLASS,
  DictionaryFieldControl,
} from "../editor-ui/DictionaryFieldControl.js";
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
import {
  TokenOverrideEditor,
  tokenOverrideHasNonDefaultFields,
} from "../components/json-editor/TokenOverrideEditor.js";
import { jsonUiHintsFromFieldBindings } from "../components/json-editor/fieldDefinitionHints.js";
import { createPaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import type { ProductionFontCatalog } from "../components/json-editor/productionFonts.js";
import type { JsonValue } from "../components/json-editor/jsonEditorUtils.js";
import type { FieldDefinition } from "../../domain/value-system/index.js";
import { ModuleThemeConfigEditor } from "./ModuleThemeConfigEditor.js";
import { ModuleFunctionalConfigFields } from "./ModuleFunctionalConfigFields.js";
import { parsedObject } from "./recordJsonUtils.js";
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
  productionFontCatalog?: ProductionFontCatalog;
  renderFields: (columns: string[]) => ReactNode;
  renderFlatJsonObjectEditor: (column: string, omitKeys?: string[]) => ReactNode;
  setActiveTab: (tab: ModuleThemeTab) => void;
  setActiveDesignGroup: (group: string) => void;
  setJsonDraft: (column: string, value: JsonValue) => void;
}

function withGroupPrefix(
  group: string,
  bindings: readonly JsonFieldBinding[],
): JsonFieldBinding[] {
  return bindings.map((binding) => ({
    ...binding,
    outputPath: [group, ...binding.outputPath],
  }));
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

const THEME_COLOR_TOKEN_OPTIONS = [
  "background",
  "surface",
  "card",
  "label",
  "text",
  "textPrimary",
  "textSecondary",
  "icon",
  "button",
  "field",
  "checkbox",
  "radio",
  "switch",
  "tab",
  "menuItem",
  "badge",
  "toast",
  "divider",
  "icons.primary",
  "icons.secondary",
  "icons.accent",
  "borders.primary",
  "borders.secondary",
  "borders.alternate",
  "theme.cursor.color",
].map((token) => ({ value: token, label: token }));

function overrideField(
  key: string,
  kind: FieldDefinition["kind"],
  label: string,
  defaultValue: unknown,
  ui: FieldDefinition["ui"] = {},
): ComponentOverrideField {
  const field: FieldDefinition = {
    id: `componentOverride.label.${key}`,
    kind,
    defaultValue,
    ui: {
      label,
      ...ui,
    },
  };
  return {
    key,
    field,
    selectOptions:
      kind === "enum"
        ? {
            options: (ui.options ?? []).map((option) => ({
              value: option,
              label: option,
            })),
          }
        : kind === "themeColorToken"
          ? { options: THEME_COLOR_TOKEN_OPTIONS }
          : undefined,
  };
}

const labelOverrideFields: ComponentOverrideField[] = [
  overrideField("sizingMode", "enum", "Sizing mode", "content", {
    options: ["content", "fixed"],
  }),
  overrideField("width", "decimal", "Width", 120, { min: 0, step: 1 }),
  overrideField("height", "decimal", "Height", 28, { min: 0, step: 1 }),
  overrideField("paddingX", "decimal", "Padding X", 8, { min: 0, step: 1 }),
  overrideField("paddingY", "decimal", "Padding Y", 4, { min: 0, step: 1 }),
  overrideField("cornerRadius", "decimal", "Corner radius", 10, { min: 0, step: 1 }),
  overrideField("borderWidth", "decimal", "Border width", 0, { min: 0, step: 1 }),
  overrideField("borderColorToken", "themeColorToken", "Border theme color", "borders.primary"),
  overrideField("backgroundVisible", "boolean", "Background visible", true),
  overrideField("backgroundColorToken", "themeColorToken", "Background theme color", "background"),
  overrideField("textColorToken", "themeColorToken", "Text theme color", "textPrimary"),
  overrideField("fontFamily", "fontFamily", "Font family", ""),
  overrideField("fontSize", "decimal", "Text size", 12, { min: 0, step: 1 }),
  overrideField("fontWeight", "fontWeight", "Text weight", 400),
  overrideField("fontStyle", "fontStyle", "Font style", "normal"),
  overrideField("shadowEnabled", "boolean", "Shadow", false),
  overrideField("shadowToken", "text", "Shadow token", "system"),
  overrideField("surfaceReliefEnabled", "boolean", "Surface relief", false),
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

const MODULE_DESIGN_GROUPS = new Set([
  "layout",
  "header",
  "messages",
  "typography",
  "chatBubbles",
]);

const moduleDesignHints = jsonUiHintsFromFieldBindings([
  ...withGroupPrefix("header", CHAT_HEADER_TOKEN_BINDINGS),
  ...CHAT_LAYOUT_MESSAGE_TOKEN_BINDINGS,
  ...CHAT_TYPOGRAPHY_TOKEN_BINDINGS,
  ...CHAT_BUBBLE_TOKEN_BINDINGS,
]);

export function ModuleThemeConfigRecordEditor({
  table,
  record,
  records,
  fieldsByColumn,
  drafts,
  inheritedFields,
  activeTab,
  activeDesignGroup,
  productionFontCatalog,
  renderFields,
  renderFlatJsonObjectEditor,
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
  const designGroups = Array.from(
    new Set([
      ...Object.keys(inheritedTokenRoot ?? {}),
      ...Object.keys(tokenRoot),
    ]),
  ).filter(
    (group) =>
      MODULE_DESIGN_GROUPS.has(group) &&
      group !== "modes" &&
      group !== "fonts" &&
      group !== "textInputBar" &&
      group !== "keyboard" &&
      group !== "avatars" &&
      group !== "componentOverrides",
  ).sort((left, right) => left.localeCompare(right));
  const selectableDesignGroups = [...designGroups, "controls"];
  const controlsWarning =
    explicitLocalDiffers(tokenRoot.textInputBar, inheritedTokenRoot?.textInputBar) ||
    explicitLocalDiffers(tokenRoot.keyboard, inheritedTokenRoot?.keyboard);
  const resolvedActiveDesignGroup =
    activeDesignGroup && selectableDesignGroups.includes(activeDesignGroup)
      ? activeDesignGroup
      : "";
  const hiddenModeColorGroups: string[] = [];
  const hiddenModeColorRolePaths: string[] = [];
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
  const labelComponentName = String(labelComponent?.name ?? "Default label");
  const labelComponentOverrideField: FieldDefinition = {
    id: "componentOverride.label",
    kind: "componentOverride",
    defaultValue: {},
    ui: { label: `. ${labelComponentName}` },
  };
  const messageLabelComponentRow =
    record.module_id === "core.chat" ? (
      <InspectorFieldRow
        className={`record-editor-field ${DICTIONARY_FIELD_CLASS}`}
        state={hasLabelOverrides ? "override" : "default"}
        label={<span>{labelComponentOverrideField.ui?.label}</span>}
        control={
          <DictionaryFieldControl
            field={labelComponentOverrideField}
            value={labelOverrides}
            componentOverride={{
              componentName: labelComponentName,
              hasOverrides: hasLabelOverrides,
              onEdit: () => setComponentOverrideModal("label"),
            }}
            onChange={() => {
              // componentOverride edits are opened through its dictionary control.
            }}
          />
        }
      />
    ) : null;

  function tokenGroupValue(group: string, root?: Record<string, JsonValue>) {
    const value = root?.[group];
    return isPlainObject(value) ? (value as JsonValue) : ({} as JsonValue);
  }

  function setTokenGroupValue(group: string, nextValue: JsonValue) {
    setJsonDraft("tokens_json", {
      ...tokenRoot,
      [group]: nextValue,
    } as JsonValue);
  }

  function designGroupWarning(group: string) {
    return tokenOverrideHasNonDefaultFields({
      rootValue: tokenGroupValue(group, tokenRoot as Record<string, JsonValue>),
      inheritedRoot: tokenGroupValue(
        group,
        inheritedTokenRoot as Record<string, JsonValue> | undefined,
      ),
      hints: moduleDesignHints,
      groupContext: group,
    });
  }

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
        hiddenModeColorGroups,
        hiddenModeColorRolePaths,
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
                warning={designGroupWarning(group)}
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
                  <TokenOverrideEditor
                    rootValue={tokenGroupValue(
                      group,
                      tokenRoot as Record<string, JsonValue>,
                    )}
                    inheritedRoot={tokenGroupValue(
                      group,
                      inheritedTokenRoot as Record<string, JsonValue> | undefined,
                    )}
                    hints={moduleDesignHints}
                    groupContext={group}
                    groupHeaderExtras={
                      group === "chatBubbles" && messageLabelComponentRow
                        ? { label: messageLabelComponentRow }
                        : undefined
                    }
                    inlineSingleGroup={group !== "chatBubbles" && group !== "typography"}
                    productionFontCatalog={productionFontCatalog}
                    paletteCatalog={paletteCatalog}
                    onRootChange={(nextValue) => setTokenGroupValue(group, nextValue)}
                  />
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
                    sessionKey={`module_theme_configs:${record.id}:functional`}
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
            hiddenGroups={hiddenModeColorGroups}
            hiddenRolePaths={hiddenModeColorRolePaths}
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
        componentName={String(labelComponent?.name ?? "Default label")}
        fields={labelOverrideFields}
        baseTokens={labelBaseTokens}
        overrides={labelOverrides}
        paletteCatalog={paletteCatalog}
        productionFontCatalog={productionFontCatalog}
        onCancel={() => setComponentOverrideModal(null)}
        onApply={(nextOverrides) => {
          setLabelOverrides(nextOverrides);
          setComponentOverrideModal(null);
        }}
      />
    ) : null}
    </>
  );
}
